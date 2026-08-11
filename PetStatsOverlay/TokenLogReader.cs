using System.Net.Http;
using System.Text.Json;

namespace PetStatsOverlay;

public sealed class TokenLogReader
{
    private readonly PetStatsSettings settings;
    private readonly string dataDirectory;
    private readonly ModelPriceCatalog priceCatalog;

    public TokenLogReader(PetStatsSettings settings, string dataDirectory)
    {
        this.settings = settings;
        this.dataDirectory = dataDirectory;
        priceCatalog = new ModelPriceCatalog(settings, dataDirectory);
    }

    public DailyUsage GetTodayUsage()
    {
        var start = DateTime.Today;
        var usage = new DailyUsage { Date = DateOnly.FromDateTime(DateTime.Now) };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetLogRoots())
        {
            ReadRoot(root, start, usage, seen);
        }

        usage.RecalculateTotals();
        WriteUsageDiagnostics(usage);
        return usage;
    }

    private IEnumerable<LogRootSetting> GetLogRoots()
    {
        var roots = new List<LogRootSetting>();

        if (settings.LogRoots.Count > 0)
        {
            roots.AddRange(settings.LogRoots.Where(root => root.Enabled));
        }
        else
        {
            roots.Add(new LogRootSetting { Name = "codex", ProviderHint = "openai", Path = settings.CodexLogRoot });
            roots.Add(new LogRootSetting { Name = "claude", ProviderHint = "anthropic", Path = settings.ClaudeLogRoot });
        }

        if (settings.AutoDiscoverLogRoots)
        {
            roots.AddRange(AiLogRootDiscovery.DiscoverExisting());
        }

        roots.AddRange(settings.ExtraLogRoots.Select((path, index) => new LogRootSetting
        {
            Name = $"extra-{index + 1}",
            Path = path
        }));

        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root.Path))
            .GroupBy(root => NormalizePath(root.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(root => root.ScanJsonFiles).First());
    }

    private void ReadRoot(LogRootSetting root, DateTime start, DailyUsage usage, HashSet<string> seen)
    {
        var path = ExpandPath(root.Path);
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in EnumerateCandidateFiles(path, root, start))
        {
            if (file.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
            {
                ReadJsonl(file, root, start, usage, seen);
            }
            else if (ShouldScanJsonFiles(root))
            {
                ReadJson(file, root, start, usage, seen);
            }
        }
    }

    private IEnumerable<string> EnumerateCandidateFiles(string root, LogRootSetting logRoot, DateTime start)
    {
        EnumerationOptions options = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true
        };

        var patterns = ShouldScanJsonFiles(logRoot) ? new[] { "*.jsonl", "*.json" } : new[] { "*.jsonl" };
        foreach (var pattern in patterns)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, pattern, options);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                FileInfo info;
                try
                {
                    info = new FileInfo(file);
                }
                catch
                {
                    continue;
                }

                if (info.LastWriteTime >= start && info.Length <= Math.Max(1, settings.MaxLogFileMb) * 1024L * 1024L)
                {
                    yield return file;
                }
            }
        }
    }

    private void ReadJsonl(string path, LogRootSetting root, DateTime start, DailyUsage usage, HashSet<string> seen)
    {
        var lineNumber = 0;
        var modelHint = "";
        foreach (var line in SafeReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                modelHint = FirstNonEmpty(ReadModelHint(doc.RootElement), modelHint);

                var timestamp = ReadTimestamp(doc.RootElement);
                if (timestamp is not null && timestamp.Value < start)
                {
                    continue;
                }

                ExtractRecords(doc.RootElement, root, path, lineNumber.ToString(), start, usage, seen, timestamp, modelHint);
            }
            catch
            {
                // Skip partial append-only log lines.
            }
        }
    }

    private void ReadJson(string path, LogRootSetting root, DateTime start, DailyUsage usage, HashSet<string> seen)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(stream);
            var timestamp = ReadTimestamp(doc.RootElement);
            if (timestamp is not null && timestamp.Value < start)
            {
                return;
            }

            ExtractRecords(doc.RootElement, root, path, "json", start, usage, seen, timestamp, ReadModelHint(doc.RootElement));
        }
        catch
        {
            // Ignore config files and logs being written.
        }
    }

    private void ExtractRecords(
        JsonElement element,
        LogRootSetting root,
        string path,
        string offset,
        DateTime start,
        DailyUsage usage,
        HashSet<string> seen,
        DateTime? inheritedTimestamp,
        string modelHint)
    {
        foreach (var candidate in UsageRecordExtractor.FindUsageRecords(element, root.ProviderHint, modelHint))
        {
            if (candidate.Timestamp is not null && candidate.Timestamp.Value < start)
            {
                continue;
            }

            if (candidate.TotalTokens == 0 && candidate.ExplicitCost <= 0)
            {
                continue;
            }

            candidate.Provider = NormalizeProvider(candidate.Provider, candidate.Model, root.ProviderHint, root.Name);
            candidate.Source = root.Name;
            var stableId = candidate.StableId ?? $"{path}:{offset}:{candidate.Provider}:{candidate.Model}:{candidate.TotalTokens}:{candidate.ExplicitCost}";
            if (!seen.Add(stableId))
            {
                continue;
            }

            candidate.EstimatedCost = candidate.ExplicitCost > 0
                ? candidate.ExplicitCost
                : priceCatalog.Estimate(candidate);
            usage.Add(candidate);
        }
    }

    private void WriteUsageDiagnostics(DailyUsage usage)
    {
        try
        {
            var path = Path.Combine(dataDirectory, "last-usage-debug.json");
            var payload = new
            {
                generatedAt = DateTime.Now,
                usage.Date,
                usage.TotalTokens,
                usage.EstimatedCost,
                usage.RecordCount,
                providers = usage.Providers.Select(provider => new
                {
                    provider.Provider,
                    provider.TotalTokens,
                    provider.EstimatedCost,
                    provider.InputTokens,
                    provider.CacheReadTokens,
                    provider.CacheCreationTokens,
                    provider.OutputTokens,
                    provider.ReasoningTokens,
                    recordCount = provider.Records.Count
                }),
                models = usage.Providers
                    .SelectMany(provider => provider.Records)
                    .GroupBy(record => string.IsNullOrWhiteSpace(record.Model) ? record.Provider : record.Model, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new
                    {
                        model = group.Key,
                        provider = group.Select(record => record.Provider).FirstOrDefault(provider => !string.IsNullOrWhiteSpace(provider)) ?? "",
                        totalTokens = group.Sum(record => record.TotalTokens),
                        estimatedCost = group.Sum(record => record.EstimatedCost),
                        inputTokens = group.Sum(record => record.InputTokens),
                        cacheReadTokens = group.Sum(record => record.CacheReadTokens),
                        cacheCreationTokens = group.Sum(record => record.CacheCreationTokens),
                        outputTokens = group.Sum(record => record.OutputTokens),
                        reasoningTokens = group.Sum(record => record.ReasoningTokens),
                        recordCount = group.Count()
                    })
                    .OrderByDescending(model => model.totalTokens),
                topRecords = usage.Providers
                    .SelectMany(provider => provider.Records.Select(record => new
                    {
                        record.Provider,
                        record.Model,
                        record.Source,
                        record.Timestamp,
                        record.TotalTokens,
                        record.InputTokens,
                        record.CacheReadTokens,
                        record.CacheCreationTokens,
                        record.OutputTokens,
                        record.ReasoningTokens,
                        record.EstimatedCost
                    }))
                    .OrderByDescending(record => record.TotalTokens)
                    .Take(25)
            };

            File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Diagnostics are helpful, not required for the overlay.
        }
    }

    private static IEnumerable<string> SafeReadLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line is not null)
            {
                yield return line;
            }
        }
    }

    private static DateTime? ReadTimestamp(JsonElement root)
    {
        foreach (var name in new[] { "timestamp", "created_at", "createdAt", "time", "datetime", "date" })
        {
            if (JsonHelpers.TryGetProperty(root, name, out var value))
            {
                return JsonHelpers.ReadTimestamp(value);
            }
        }

        return null;
    }

    private static string ReadModelHint(JsonElement root)
    {
        foreach (var path in new[]
        {
            new[] { "payload", "model" },
            new[] { "payload", "collaboration_mode", "settings", "model" },
            new[] { "model" }
        })
        {
            if (JsonHelpers.TryGetPath(root, path, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? "";
            }
        }

        return "";
    }

    private static string NormalizeProvider(string provider, string model, string hint, string source)
    {
        var raw = FirstNonEmpty(provider, hint, source, InferProvider(model));
        var lower = raw.ToLowerInvariant();

        if (lower.Contains("anthropic") || lower.Contains("claude"))
        {
            return "anthropic";
        }

        if (lower.Contains("openai") || lower.Contains("codex") || lower.Contains("gpt") || lower.Contains("oai"))
        {
            return "openai";
        }

        if (lower.Contains("google") || lower.Contains("gemini") || lower.Contains("vertex"))
        {
            return "google";
        }

        if (lower.Contains("deepseek"))
        {
            return "deepseek";
        }

        if (lower.Contains("qwen") || lower.Contains("dashscope") || lower.Contains("aliyun") || lower.Contains("tongyi"))
        {
            return "qwen";
        }

        if (lower.Contains("doubao") || lower.Contains("volc") || lower.Contains("bytedance"))
        {
            return "doubao";
        }

        if (lower.Contains("kimi") || lower.Contains("moonshot"))
        {
            return "kimi";
        }

        if (lower.Contains("zhipu") || lower.Contains("glm"))
        {
            return "zhipu";
        }

        if (lower.Contains("minimax"))
        {
            return "minimax";
        }

        if (lower.Contains("grok") || lower.Contains("xai"))
        {
            return "xai";
        }

        return string.IsNullOrWhiteSpace(raw) ? "unknown" : raw;
    }

    private static string InferProvider(string model)
    {
        var lower = model.ToLowerInvariant();
        if (lower.Contains("claude")) return "anthropic";
        if (lower.Contains("gpt") || lower.StartsWith("o", StringComparison.OrdinalIgnoreCase)) return "openai";
        if (lower.Contains("gemini")) return "google";
        if (lower.Contains("deepseek")) return "deepseek";
        if (lower.Contains("qwen")) return "qwen";
        if (lower.Contains("doubao")) return "doubao";
        if (lower.Contains("kimi") || lower.Contains("moonshot")) return "kimi";
        if (lower.Contains("glm")) return "zhipu";
        if (lower.Contains("minimax")) return "minimax";
        if (lower.Contains("grok")) return "xai";
        return "";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(ExpandPath(path));
        }
        catch
        {
            return path;
        }
    }

    private static string ExpandPath(string path)
    {
        return AiLogRootDiscovery.ExpandPath(path);
    }

    private bool ShouldScanJsonFiles(LogRootSetting root)
    {
        return settings.ScanJsonFiles || root.ScanJsonFiles;
    }
}

public static class UsageRecordExtractor
{
    public static IEnumerable<UsageRecord> FindUsageRecords(JsonElement root, string providerHint, string modelHint)
    {
        if (IsCodexTokenCount(root))
        {
            if (TryCreateCodexTokenCount(root, providerHint, modelHint, out var record))
            {
                yield return record;
            }

            yield break;
        }

        foreach (var record in Walk(root, root, providerHint, modelHint, 0))
        {
            yield return record;
        }
    }

    private static IEnumerable<UsageRecord> Walk(JsonElement element, JsonElement context, string providerHint, string modelHint, int depth)
    {
        if (depth > 10)
        {
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryCreateRecord(element, context, providerHint, modelHint, out var record))
            {
                yield return record;
            }

            foreach (var property in element.EnumerateObject())
            {
                var nextContext = IsLikelyMessageOrResponse(property.Name) ? property.Value : context;
                foreach (var child in Walk(property.Value, nextContext, providerHint, modelHint, depth + 1))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in Walk(item, context, providerHint, modelHint, depth + 1))
                {
                    yield return child;
                }
            }
        }
    }

    private static bool TryCreateRecord(JsonElement element, JsonElement context, string providerHint, string modelHint, out UsageRecord record)
    {
        record = new UsageRecord();

        if (JsonHelpers.TryGetProperty(element, "usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            record = FromUsageObject(usage, context, providerHint, modelHint);
            return record.HasAnyUsage;
        }

        if (JsonHelpers.TryGetProperty(element, "usageMetadata", out var usageMetadata) && usageMetadata.ValueKind == JsonValueKind.Object)
        {
            record = FromGeminiUsage(usageMetadata, context, providerHint, modelHint);
            return record.HasAnyUsage;
        }

        if (LooksLikeUsageObject(element))
        {
            record = FromUsageObject(element, context, providerHint, modelHint);
            return record.HasAnyUsage;
        }

        return false;
    }

    private static UsageRecord FromUsageObject(JsonElement usage, JsonElement context, string providerHint, string modelHint)
    {
        var input = ReadAnyLong(usage, "input_tokens", "prompt_tokens", "promptTokens", "promptTokenCount", "inputTokens", "tokens_in");
        var output = ReadAnyLong(usage, "output_tokens", "completion_tokens", "completionTokens", "candidatesTokenCount", "outputTokens", "tokens_out");
        var total = ReadAnyLong(usage, "total_tokens", "totalTokens", "totalTokenCount");
        var cacheRead = ReadAnyLong(usage, "cache_read_input_tokens", "cached_input_tokens", "cached_tokens", "prompt_cache_hit_tokens", "cacheReadInputTokens");
        var cacheCreate = ReadAnyLong(usage, "cache_creation_input_tokens", "prompt_cache_miss_tokens", "cacheCreationInputTokens");
        var reasoning = ReadAnyLong(usage, "reasoning_tokens", "thoughtsTokenCount", "reasoningTokens");

        if (JsonHelpers.TryGetProperty(usage, "input_tokens_details", out var inputDetails))
        {
            cacheRead += ReadAnyLong(inputDetails, "cached_tokens", "cache_read_input_tokens");
        }

        if (JsonHelpers.TryGetProperty(usage, "output_tokens_details", out var outputDetails))
        {
            reasoning += ReadAnyLong(outputDetails, "reasoning_tokens");
        }

        if (JsonHelpers.TryGetProperty(usage, "cache_creation", out var cacheCreation))
        {
            var nestedCacheCreate = ReadAnyLong(cacheCreation, "ephemeral_1h_input_tokens", "ephemeral_5m_input_tokens");
            if (nestedCacheCreate > 0)
            {
                cacheCreate = nestedCacheCreate;
            }
        }

        if (total > 0 && input == 0 && output == 0 && cacheRead == 0 && cacheCreate == 0)
        {
            input = total;
        }

        if (IsOpenAiLike(context, providerHint))
        {
            input = Math.Max(0, input - cacheRead);
        }

        return BuildRecord(context, usage, providerHint, modelHint, input, output, cacheRead, cacheCreate, reasoning, total);
    }

    private static UsageRecord FromGeminiUsage(JsonElement usage, JsonElement context, string providerHint, string modelHint)
    {
        var input = ReadAnyLong(usage, "promptTokenCount");
        var output = ReadAnyLong(usage, "candidatesTokenCount");
        var total = ReadAnyLong(usage, "totalTokenCount");
        var cacheRead = ReadAnyLong(usage, "cachedContentTokenCount");
        var reasoning = ReadAnyLong(usage, "thoughtsTokenCount");
        return BuildRecord(context, usage, providerHint, modelHint, input, output, cacheRead, 0, reasoning, total);
    }

    private static UsageRecord BuildRecord(
        JsonElement context,
        JsonElement usage,
        string providerHint,
        string modelHint,
        long input,
        long output,
        long cacheRead,
        long cacheCreate,
        long reasoning,
        long total)
    {
        return new UsageRecord
        {
            Provider = ReadProvider(context, providerHint),
            Model = FirstNonEmpty(ReadModel(context, usage), modelHint),
            Timestamp = ReadTimestamp(context),
            StableId = ReadStableId(context),
            InputTokens = input,
            OutputTokens = output,
            CacheReadTokens = cacheRead,
            CacheCreationTokens = cacheCreate,
            ReasoningTokens = reasoning,
            TotalTokensOverride = total,
            ExplicitCost = ReadCost(context, usage)
        };
    }

    private static bool IsCodexTokenCount(JsonElement root)
    {
        return JsonHelpers.TryGetPath(root, new[] { "payload", "type" }, out var type)
            && type.ValueKind == JsonValueKind.String
            && string.Equals(type.GetString(), "token_count", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateCodexTokenCount(JsonElement root, string providerHint, string modelHint, out UsageRecord record)
    {
        record = new UsageRecord();
        if (!JsonHelpers.TryGetPath(root, new[] { "payload", "info", "last_token_usage" }, out var usage)
            || usage.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var input = ReadAnyLong(usage, "input_tokens");
        var cacheRead = ReadAnyLong(usage, "cached_input_tokens");
        var output = ReadAnyLong(usage, "output_tokens");
        var reasoning = ReadAnyLong(usage, "reasoning_output_tokens", "reasoning_tokens");
        var total = ReadAnyLong(usage, "total_tokens");
        var cumulativeTotal = 0L;
        if (JsonHelpers.TryGetPath(root, new[] { "payload", "info", "total_token_usage" }, out var cumulative)
            && cumulative.ValueKind == JsonValueKind.Object)
        {
            cumulativeTotal = ReadAnyLong(cumulative, "total_tokens");
        }

        var timestamp = ReadTimestamp(root);

        record = new UsageRecord
        {
            Provider = string.IsNullOrWhiteSpace(providerHint) ? "openai" : providerHint,
            Model = FirstNonEmpty(modelHint, ReadModel(root, usage), "codex"),
            Timestamp = timestamp,
            StableId = $"codex-token-count:{cumulativeTotal}:{input}:{cacheRead}:{output}:{reasoning}:{total}",
            InputTokens = Math.Max(0, input - cacheRead),
            CacheReadTokens = cacheRead,
            OutputTokens = output,
            ReasoningTokens = reasoning,
            TotalTokensOverride = total
        };

        return record.HasAnyUsage;
    }

    private static bool LooksLikeUsageObject(JsonElement element)
    {
        return HasAny(element, "input_tokens", "prompt_tokens", "output_tokens", "completion_tokens", "total_tokens", "promptTokenCount", "totalTokenCount");
    }

    private static bool HasAny(JsonElement element, params string[] names)
    {
        return names.Any(name => JsonHelpers.TryGetProperty(element, name, out _));
    }

    private static long ReadAnyLong(JsonElement element, params string[] names)
    {
        return names.Sum(name => JsonHelpers.ReadLong(element, name));
    }

    private static string ReadProvider(JsonElement context, string providerHint)
    {
        foreach (var path in new[]
        {
            new[] { "provider" },
            new[] { "model_provider" },
            new[] { "litellm_provider" },
            new[] { "payload", "model_provider" },
            new[] { "message", "model_provider" }
        })
        {
            if (JsonHelpers.TryGetPath(context, path, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? providerHint;
            }
        }

        return providerHint;
    }

    private static bool IsOpenAiLike(JsonElement context, string providerHint)
    {
        var provider = ReadProvider(context, providerHint);
        if (provider.Contains("openai", StringComparison.OrdinalIgnoreCase)
            || provider.Contains("codex", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var model = ReadModel(context, context);
        return model.Contains("gpt", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("o", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadModel(JsonElement context, JsonElement usage)
    {
        foreach (var source in new[] { context, usage })
        {
            foreach (var path in new[]
            {
                new[] { "model" },
                new[] { "model_name" },
                new[] { "modelName" },
                new[] { "engine" },
                new[] { "message", "model" },
                new[] { "payload", "model" },
                new[] { "payload", "message", "model" },
                new[] { "response", "model" }
            })
            {
                if (JsonHelpers.TryGetPath(source, path, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? "";
                }
            }
        }

        return "";
    }

    private static DateTime? ReadTimestamp(JsonElement context)
    {
        foreach (var path in new[]
        {
            new[] { "timestamp" },
            new[] { "created_at" },
            new[] { "createdAt" },
            new[] { "time" },
            new[] { "payload", "timestamp" },
            new[] { "message", "created_at" }
        })
        {
            if (JsonHelpers.TryGetPath(context, path, out var value))
            {
                var timestamp = JsonHelpers.ReadTimestamp(value);
                if (timestamp is not null)
                {
                    return timestamp;
                }
            }
        }

        return null;
    }

    private static string? ReadStableId(JsonElement context)
    {
        foreach (var path in new[]
        {
            new[] { "id" },
            new[] { "uuid" },
            new[] { "request_id" },
            new[] { "response_id" },
            new[] { "message", "id" },
            new[] { "payload", "message", "id" },
            new[] { "payload", "response", "id" }
        })
        {
            if (JsonHelpers.TryGetPath(context, path, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static decimal ReadCost(JsonElement context, JsonElement usage)
    {
        foreach (var source in new[] { usage, context })
        {
            foreach (var name in new[] { "cost", "total_cost", "response_cost", "usage_cost", "estimated_cost" })
            {
                var cost = JsonHelpers.ReadDecimal(source, name);
                if (cost > 0)
                {
                    return cost;
                }
            }
        }

        return 0;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }

    private static bool IsLikelyMessageOrResponse(string name)
    {
        return name.Contains("message", StringComparison.OrdinalIgnoreCase)
            || name.Contains("payload", StringComparison.OrdinalIgnoreCase)
            || name.Contains("response", StringComparison.OrdinalIgnoreCase)
            || name.Contains("request", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ModelPriceCatalog
{
    private readonly List<ModelPrice> prices = new();

    public ModelPriceCatalog(PetStatsSettings settings, string dataDirectory)
    {
        prices.AddRange(settings.Prices);
        prices.AddRange(LoadRemoteCatalog(settings, dataDirectory));
    }

    public decimal Estimate(UsageRecord record)
    {
        var price = FindPrice(record);
        if (price is null)
        {
            return 0;
        }

        var total = Cost(record.InputTokens, price.InputPerMillion);
        total += Cost(record.OutputTokens, price.OutputPerMillion);
        total += Cost(record.CacheReadTokens, price.CacheReadPerMillion);
        total += Cost(record.CacheCreationTokens, price.CacheWrite5mPerMillion > 0 ? price.CacheWrite5mPerMillion : price.InputPerMillion);

        if (total == 0 && price.TotalPerMillion > 0)
        {
            total = Cost(record.TotalTokens, price.TotalPerMillion);
        }

        return total;
    }

    private ModelPrice? FindPrice(UsageRecord record)
    {
        var model = record.Model.ToLowerInvariant();
        var provider = record.Provider.ToLowerInvariant();
        return prices
            .Where(price => !string.IsNullOrWhiteSpace(price.Pattern))
            .Where(price => string.IsNullOrWhiteSpace(price.Provider) || provider.Contains(price.Provider.ToLowerInvariant()))
            .OrderByDescending(price => price.Pattern.Length)
            .FirstOrDefault(price => model.Contains(price.Pattern.ToLowerInvariant()));
    }

    private static IEnumerable<ModelPrice> LoadRemoteCatalog(PetStatsSettings settings, string dataDirectory)
    {
        var cachePath = Path.Combine(dataDirectory, "model-prices.litellm.json");
        RefreshCacheIfNeeded(settings, cachePath);

        if (!File.Exists(cachePath))
        {
            yield break;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllText(cachePath));
        }
        catch
        {
            yield break;
        }

        using (doc)
        {
            foreach (var model in doc.RootElement.EnumerateObject())
            {
                if (model.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var inputCost = JsonHelpers.ReadDecimal(model.Value, "input_cost_per_token");
                var outputCost = JsonHelpers.ReadDecimal(model.Value, "output_cost_per_token");
                var cacheRead = JsonHelpers.ReadDecimal(model.Value, "cache_read_input_token_cost");
                var cacheCreate = JsonHelpers.ReadDecimal(model.Value, "cache_creation_input_token_cost");
                var provider = JsonHelpers.ReadString(model.Value, "litellm_provider");

                if (inputCost == 0 && outputCost == 0 && cacheRead == 0 && cacheCreate == 0)
                {
                    continue;
                }

                yield return new ModelPrice
                {
                    Pattern = model.Name,
                    Provider = provider,
                    InputPerMillion = inputCost * 1_000_000M,
                    OutputPerMillion = outputCost * 1_000_000M,
                    CacheReadPerMillion = cacheRead * 1_000_000M,
                    CacheWrite5mPerMillion = cacheCreate * 1_000_000M
                };
            }
        }
    }

    private static void RefreshCacheIfNeeded(PetStatsSettings settings, string cachePath)
    {
        if (string.IsNullOrWhiteSpace(settings.PricingCatalogUrl))
        {
            return;
        }

        try
        {
            var info = new FileInfo(cachePath);
            if (info.Exists && DateTime.Now - info.LastWriteTime < TimeSpan.FromHours(Math.Max(1, settings.RefreshPricingHours)))
            {
                return;
            }

            using HttpClient client = new()
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            using var response = client.GetAsync(settings.PricingCatalogUrl).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var _ = JsonDocument.Parse(body);
            File.WriteAllText(cachePath, body);
        }
        catch
        {
            // Pricing is best-effort; built-in prices and explicit logged costs still work.
        }
    }

    private static decimal Cost(long tokens, decimal perMillion)
    {
        return tokens <= 0 || perMillion <= 0 ? 0M : tokens / 1_000_000M * perMillion;
    }
}

public sealed class DailyUsage
{
    public DateOnly Date { get; set; }
    public List<ProviderUsage> Providers { get; } = new();
    public long TotalTokens { get; private set; }
    public decimal EstimatedCost { get; private set; }
    public int RecordCount => Providers.Sum(provider => provider.Records.Count);

    public void Add(UsageRecord record)
    {
        var provider = Providers.FirstOrDefault(item => string.Equals(item.Provider, record.Provider, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            provider = new ProviderUsage { Provider = record.Provider };
            Providers.Add(provider);
        }

        provider.Add(record);
    }

    public void RecalculateTotals()
    {
        foreach (var provider in Providers)
        {
            provider.RecalculateTotals();
        }

        TotalTokens = Providers.Sum(provider => provider.TotalTokens);
        EstimatedCost = Providers.Sum(provider => provider.EstimatedCost);
        Providers.Sort((left, right) => right.EstimatedCost.CompareTo(left.EstimatedCost));
    }
}

public sealed class ProviderUsage
{
    public string Provider { get; set; } = "";
    public List<UsageRecord> Records { get; } = new();
    public long InputTokens { get; private set; }
    public long CacheCreationTokens { get; private set; }
    public long CacheReadTokens { get; private set; }
    public long OutputTokens { get; private set; }
    public long ReasoningTokens { get; private set; }
    public long TotalTokens { get; private set; }
    public decimal EstimatedCost { get; private set; }

    public void Add(UsageRecord record)
    {
        Records.Add(record);
    }

    public void RecalculateTotals()
    {
        InputTokens = Records.Sum(record => record.InputTokens);
        CacheCreationTokens = Records.Sum(record => record.CacheCreationTokens);
        CacheReadTokens = Records.Sum(record => record.CacheReadTokens);
        OutputTokens = Records.Sum(record => record.OutputTokens);
        ReasoningTokens = Records.Sum(record => record.ReasoningTokens);
        TotalTokens = Records.Sum(record => record.TotalTokens);
        EstimatedCost = Records.Sum(record => record.EstimatedCost);
    }
}

public sealed class UsageRecord
{
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string Source { get; set; } = "";
    public string? StableId { get; set; }
    public DateTime? Timestamp { get; set; }
    public long InputTokens { get; set; }
    public long CacheCreationTokens { get; set; }
    public long CacheReadTokens { get; set; }
    public long OutputTokens { get; set; }
    public long ReasoningTokens { get; set; }
    public long TotalTokensOverride { get; set; }
    public decimal ExplicitCost { get; set; }
    public decimal EstimatedCost { get; set; }
    public bool HasAnyUsage => InputTokens + CacheCreationTokens + CacheReadTokens + OutputTokens + ReasoningTokens + TotalTokensOverride > 0;
    public long TotalTokens => TotalTokensOverride > 0
        ? TotalTokensOverride
        : InputTokens + CacheCreationTokens + CacheReadTokens + OutputTokens + ReasoningTokens;
}

public static class JsonHelpers
{
    public static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    public static bool TryGetPath(JsonElement root, IEnumerable<string> names, out JsonElement value)
    {
        value = root;
        foreach (var name in names)
        {
            if (!TryGetProperty(value, name, out value))
            {
                return false;
            }
        }

        return true;
    }

    public static string ReadString(JsonElement element, string name)
    {
        return TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    public static long ReadLong(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var doubleValue))
        {
            return (long)doubleValue;
        }

        return value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number)
            ? number
            : 0;
    }

    public static decimal ReadDecimal(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out number)
            ? number
            : 0;
    }

    public static DateTime? ReadTimestamp(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var raw = value.GetString();
            if (DateTimeOffset.TryParse(raw, out var dto))
            {
                return dto.LocalDateTime;
            }

            if (long.TryParse(raw, out var numeric))
            {
                return FromUnixLike(numeric);
            }
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return FromUnixLike(number);
        }

        return null;
    }

    private static DateTime FromUnixLike(long value)
    {
        return value > 100_000_000_000
            ? DateTimeOffset.FromUnixTimeMilliseconds(value).LocalDateTime
            : DateTimeOffset.FromUnixTimeSeconds(value).LocalDateTime;
    }
}
