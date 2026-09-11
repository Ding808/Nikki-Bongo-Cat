using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    public DailyUsage GetTodayUsage() => GetUsage(DateOnly.FromDateTime(DateTime.Now));
    public DailyUsage GetUsage(DateOnly date)
    {
        // The UI runs usage reads on its worker; network refresh never blocks construction.
        priceCatalog.RefreshIfNeeded();
        var usage = new DailyUsage { Date = date };
        var records = new Dictionary<string, UsageRecord>(StringComparer.Ordinal);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in GetLogRoots())
        {
            foreach (var file in EnumerateCandidateFiles(root))
            {
                if (!files.Add(Path.GetFullPath(file))) continue;
                ReadFile(file, root, date, records);
            }
            if (root.Name.Contains("claude", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var desktop in ClaudeDesktopUsageReader.Read(AiLogRootDiscovery.ExpandPath(root.Path), settings.MaxLogFileMb))
                    Collect(desktop.Payload, root, new LogFileState { SessionKey = desktop.Source }, desktop.Source, date, records);
            }
        }
        // SDK result summaries repeat the per-message counters from the same transcript.
        var detailed = records.Values.Where(r => !r.IsSessionSummary).Select(r => (r.SessionKey, r.Provider, r.Model)).ToHashSet();
        foreach (var record in records.Values)
        {
            if (record.IsSessionSummary && detailed.Contains((record.SessionKey, record.Provider, record.Model))) continue;
            record.EstimatedCost = priceCatalog.Estimate(record);
            usage.Add(record);
        }
        usage.RecalculateTotals();
        WriteUsageDiagnostics(usage);
        return usage;
    }
    private IEnumerable<LogRootSetting> GetLogRoots()
    {
        var roots = settings.LogRoots.Count > 0 ? settings.LogRoots.Where(root => root.Enabled).ToList()
            : new List<LogRootSetting>
            {
                new() { Name = "codex", ProviderHint = "openai", Path = settings.CodexLogRoot },
                new() { Name = "claude", ProviderHint = "anthropic", Path = settings.ClaudeLogRoot }
            };
        if (settings.AutoDiscoverLogRoots)
        {
            var disabled = settings.LogRoots.Where(r => !r.Enabled).Select(r => AiLogRootDiscovery.ExpandPath(r.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            roots.AddRange(AiLogRootDiscovery.DiscoverExisting().Where(r => !disabled.Contains(AiLogRootDiscovery.ExpandPath(r.Path))));
        }
        roots.AddRange(settings.ExtraLogRoots.Select((path, i) => new LogRootSetting { Name = $"extra-{i + 1}", Path = path, ScanJsonFiles = true }));
        return roots.Where(r => !string.IsNullOrWhiteSpace(r.Path)).GroupBy(r => AiLogRootDiscovery.ExpandPath(r.Path), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(r => r.ScanJsonFiles).First());
    }
    private IEnumerable<string> EnumerateCandidateFiles(LogRootSetting root)
    {
        var result = new List<string>();
        var path = AiLogRootDiscovery.ExpandPath(root.Path);
        if (!Directory.Exists(path)) return result;
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", options))
            {
                var extension = Path.GetExtension(file);
                if (!extension.Equals(".jsonl", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".ndjson", StringComparison.OrdinalIgnoreCase)
                    && (!(settings.ScanJsonFiles || root.ScanJsonFiles) || !extension.Equals(".json", StringComparison.OrdinalIgnoreCase))) continue;
                try { if (new FileInfo(file).Length <= Math.Max(1, settings.MaxLogFileMb) * 1024L * 1024L) result.Add(file); }
                catch (IOException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        return result;
    }
    private void ReadFile(string path, LogRootSetting root, DateOnly date, Dictionary<string, UsageRecord> records)
    {
        var state = new LogFileState { SessionKey = path };
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                using var document = JsonDocument.Parse(stream);
                Process(document.RootElement, "json");
            }
            else
            {
                using var reader = new StreamReader(stream);
                var lineNumber = 0;
                while (reader.ReadLine() is { } line)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try { using var document = JsonDocument.Parse(line); Process(document.RootElement, lineNumber.ToString(CultureInfo.InvariantCulture)); }
                    catch (JsonException) { /* A writer may still be appending this line. */ }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        void Process(JsonElement element, string offset) => Collect(element, root, state, path + ":" + offset, date, records);
    }
    private static void Collect(JsonElement element, LogRootSetting root, LogFileState state, string location, DateOnly date, Dictionary<string, UsageRecord> records)
    {
        state.Observe(element);
        var index = 0;
        foreach (var candidate in UsageRecordExtractor.FindUsageRecords(element, root.ProviderHint, state.ModelHint, state))
        {
            index++;
            // Modification time cannot establish the date of a request.
            if (!candidate.HasAnyUsage || candidate.Timestamp is null || DateOnly.FromDateTime(candidate.Timestamp.Value) != date) continue;
            candidate.Provider = ModelIdentity.ResolveProvider(candidate.Provider, candidate.Model, root.ProviderHint);
            candidate.Source = root.Name;
            candidate.SessionKey = state.SessionKey;
            var key = candidate.StableId is { Length: > 0 } id ? $"{candidate.Provider}:{id}" : $"{location}:{index}";
            if (records.TryGetValue(key, out var previous)) previous.MergeSnapshot(candidate);
            else records[key] = candidate;
        }
    }
    private void WriteUsageDiagnostics(DailyUsage usage)
    {
        try
        {
            Directory.CreateDirectory(dataDirectory);
            // Persist only aggregate counters and model identifiers, never log bodies.
            var payload = new
            {
                generatedAt = DateTime.Now, usage.Date, usage.TotalTokens, usage.EstimatedCost, usage.RecordCount, usage.UnpricedRecordCount,
                models = usage.Providers.SelectMany(p => p.Records).GroupBy(r => new { r.Provider, r.Model }).Select(g => new
                {
                    g.Key.Provider, g.Key.Model, totalTokens = g.Sum(r => r.TotalTokens), estimatedCost = g.Sum(r => r.EstimatedCost),
                    unpricedRecords = g.Count(r => !r.IsPriced), recordCount = g.Count()
                }).OrderByDescending(m => m.totalTokens)
            };
            File.WriteAllText(Path.Combine(dataDirectory, "last-usage-debug.json"), JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
public sealed class LogFileState
{
    public string SessionKey { get; set; } = "standalone";
    public string ModelHint { get; private set; } = "";
    public string? StreamMessageId { get; set; }
    public JsonElement PreviousCumulative { get; set; }
    public void Observe(JsonElement root)
    {
        var model = UsageRecordExtractor.ReadModel(root);
        if (model.Length > 0 && model != "<synthetic>") ModelHint = model;
        if (JsonHelpers.ReadString(root, "type") == "session_meta" && JsonHelpers.TryGetPath(root, ["payload", "id"], out var id)) SessionKey = id.GetString() ?? SessionKey;
        var sessionId = JsonHelpers.FirstString(root, "sessionId", "session_id");
        if (sessionId.Length > 0) SessionKey = sessionId;
    }
}
public static class UsageRecordExtractor
{
    private static readonly string[] UsageNames = ["usage", "usageMetadata", "usage_metadata", "tokenUsage", "token_usage", "usage_metrics"];
    public static IEnumerable<UsageRecord> FindUsageRecords(JsonElement root, string providerHint, string modelHint, LogFileState? state = null)
    {
        state ??= new LogFileState();
        if (JsonHelpers.TryGetPath(root, ["payload", "type"], out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "token_count")
        {
            var record = CodexRecord(root, providerHint, modelHint, state);
            if (record is not null) yield return record;
            yield break;
        }
        var context = new RecordContext("", modelHint, null, null, 0, false, "", "", "");
        foreach (var record in Walk(root, context, providerHint, state, 0)) yield return record;
    }
    private static IEnumerable<UsageRecord> Walk(JsonElement element, RecordContext inherited, string providerHint, LogFileState state, int depth)
    {
        if (depth > 24) yield break;
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                foreach (var record in Walk(item, inherited with { Id = null, Cost = 0, HasCost = false }, providerHint, state, depth + 1)) yield return record;
            yield break;
        }
        if (element.ValueKind != JsonValueKind.Object) yield break;
        var model = JsonHelpers.FirstString(element, "model", "model_name", "modelName", "modelVersion", "modelID", "engine");
        var provider = JsonHelpers.FirstString(element, "provider", "model_provider", "litellm_provider", "providerID");
        var id = JsonHelpers.FirstString(element, "id", "request_id", "requestId", "response_id", "responseId", "uuid");
        var loggedCost = ReadCost(element, inherited.Currency);
        var context = inherited with
        {
            Model = model.Length > 0 ? model : inherited.Model, Provider = provider.Length > 0 ? provider : inherited.Provider,
            Timestamp = ReadTimestamp(element) ?? inherited.Timestamp, Id = id.Length > 0 ? id : inherited.Id,
            Cost = loggedCost ?? inherited.Cost, HasCost = loggedCost.HasValue || inherited.HasCost,
            Currency = First(JsonHelpers.FirstString(element, "currency", "cost_currency"), inherited.Currency),
            Speed = First(JsonHelpers.ReadString(element, "speed"), inherited.Speed), InferenceGeo = First(JsonHelpers.ReadString(element, "inference_geo"), inherited.InferenceGeo)
        };
        var eventType = JsonHelpers.ReadString(element, "type");
        if (eventType == "message_start" && JsonHelpers.TryGetProperty(element, "message", out var startMessage)) state.StreamMessageId = JsonHelpers.ReadString(startMessage, "id");
        if (eventType == "message_delta" && context.Id is null) context = context with { Id = state.StreamMessageId };
        if (JsonHelpers.TryGetProperty(element, "modelUsage", out var models) && models.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in models.EnumerateObject())
            {
                var record = FromUsage(entry.Value, context with { Model = entry.Name, HasCost = false, Cost = 0 });
                var cost = ReadCost(entry.Value, context.Currency);
                if (cost is not null) { record.ExplicitCost = cost.Value; record.HasExplicitCost = true; }
                record.StableId = $"summary:{state.SessionKey}:{entry.Name}";
                record.IsSessionSummary = true;
                if (record.HasAnyUsage) yield return record;
            }
            yield break;
        }
        var parsed = false;
        foreach (var name in UsageNames)
        {
            if (!JsonHelpers.TryGetProperty(element, name, out var usage) || usage.ValueKind != JsonValueKind.Object) continue;
            var record = FromUsage(usage, context);
            if (record.HasAnyUsage) { parsed = true; yield return record; }
            break;
        }
        // OpenCode exports use tokens: {input,output,reasoning,cache:{read,write}}.
        if (!parsed && JsonHelpers.TryGetProperty(element, "tokens", out var tokens) && tokens.ValueKind == JsonValueKind.Object)
        {
            var record = FromUsage(tokens, context, true);
            if (record.HasAnyUsage) { parsed = true; yield return record; }
        }
        if (!parsed && LooksLikeUsage(element))
        {
            var record = FromUsage(element, context);
            if (record.HasAnyUsage) { parsed = true; yield return record; }
        }
        foreach (var property in element.EnumerateObject())
        {
            if (UsageNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase) || property.Name is "tokens" or "modelUsage"
                or "content" or "text" or "prompt" or "system" or "tools" or "arguments" or "input_tokens_details"
                or "output_tokens_details" or "prompt_tokens_details" or "completion_tokens_details" or "cache_creation") continue;
            if (parsed && LooksLikeUsage(element)) continue;
            foreach (var record in Walk(property.Value, context, providerHint, state, depth + 1)) yield return record;
        }
    }
    private static UsageRecord FromUsage(JsonElement usage, RecordContext context, bool simple = false)
    {
        var input = JsonHelpers.FirstLong(usage, "input_tokens", "prompt_tokens", "promptTokens", "promptTokenCount", "inputTokens", "tokens_in");
        var output = JsonHelpers.FirstLong(usage, "output_tokens", "completion_tokens", "completionTokens", "candidatesTokenCount", "outputTokens", "tokens_out");
        var total = JsonHelpers.FirstLong(usage, "total_tokens", "totalTokens", "totalTokenCount");
        var cacheRead = JsonHelpers.FirstLong(usage, "cache_read_input_tokens", "cached_input_tokens", "cached_tokens", "prompt_cache_hit_tokens", "cacheReadInputTokens", "cachedContentTokenCount");
        var cacheCreate = JsonHelpers.FirstLong(usage, "cache_creation_input_tokens", "cacheCreationInputTokens");
        var reasoning = JsonHelpers.FirstLong(usage, "reasoning_tokens", "reasoning_output_tokens", "thoughtsTokenCount", "reasoningTokens");
        foreach (var name in new[] { "input_tokens_details", "prompt_tokens_details" })
            if (JsonHelpers.TryGetProperty(usage, name, out var details)) cacheRead = Math.Max(cacheRead, JsonHelpers.FirstLong(details, "cached_tokens", "cache_read_input_tokens"));
        foreach (var name in new[] { "output_tokens_details", "completion_tokens_details" })
            if (JsonHelpers.TryGetProperty(usage, name, out var details)) reasoning = Math.Max(reasoning, JsonHelpers.ReadLong(details, "reasoning_tokens"));
        var cache1h = 0L;
        if (JsonHelpers.TryGetProperty(usage, "cache_creation", out var creation))
        {
            cache1h = JsonHelpers.ReadLong(creation, "ephemeral_1h_input_tokens");
            cacheCreate = Math.Max(cacheCreate, cache1h + JsonHelpers.ReadLong(creation, "ephemeral_5m_input_tokens"));
        }
        if (simple)
        {
            input = JsonHelpers.ReadLong(usage, "input"); output = JsonHelpers.ReadLong(usage, "output"); reasoning = JsonHelpers.ReadLong(usage, "reasoning");
            if (JsonHelpers.TryGetProperty(usage, "cache", out var cache)) { cacheRead = JsonHelpers.ReadLong(cache, "read"); cacheCreate = JsonHelpers.ReadLong(cache, "write"); }
            // Gemini CLI TokensSummary (chatRecordingTypes.ts), distinct from OpenCode.
            if (JsonHelpers.HasAny(usage, "cached", "thoughts", "tool"))
            {
                cacheRead = JsonHelpers.ReadLong(usage, "cached");
                input = Math.Max(0, input - cacheRead);
                reasoning = JsonHelpers.ReadLong(usage, "thoughts");
                output += reasoning;
                total = JsonHelpers.ReadLong(usage, "total");
            }
        }
        // Anthropic input excludes caches; compatible prompt counters and Gemini include them.
        // DeepSeek cache misses are uncached input, not Anthropic cache creation.
        var nativeAnthropic = JsonHelpers.HasAny(usage, "cache_read_input_tokens", "cache_creation_input_tokens", "cacheReadInputTokens", "cacheCreationInputTokens")
            && !JsonHelpers.HasAny(usage, "prompt_tokens", "promptTokenCount", "prompt_tokens_details");
        if (!nativeAnthropic && !simple) input = Math.Max(0, input - cacheRead);
        // Gemini thoughts are additional output; OpenAI reasoning is already included in output.
        if (JsonHelpers.HasAny(usage, "thoughtsTokenCount")) output += reasoning;
        if (total == 0) total = input + output + cacheRead + cacheCreate;
        var cost = ReadCost(usage, context.Currency);
        return new UsageRecord
        {
            Provider = context.Provider, Model = First(JsonHelpers.FirstString(usage, "model", "model_name", "modelName"), context.Model),
            Timestamp = ReadTimestamp(usage) ?? context.Timestamp, StableId = context.Id, InputTokens = input, OutputTokens = output,
            CacheReadTokens = cacheRead, CacheCreationTokens = cacheCreate, CacheCreation1hTokens = cache1h, ReasoningTokens = reasoning,
            TotalTokensOverride = total, ExplicitCost = cost ?? context.Cost, HasExplicitCost = cost.HasValue || context.HasCost,
            Speed = First(JsonHelpers.ReadString(usage, "speed"), context.Speed), InferenceGeo = First(JsonHelpers.ReadString(usage, "inference_geo"), context.InferenceGeo)
        };
    }
    private static UsageRecord? CodexRecord(JsonElement root, string providerHint, string modelHint, LogFileState state)
    {
        JsonHelpers.TryGetPath(root, ["payload", "info", "last_token_usage"], out var last);
        JsonHelpers.TryGetPath(root, ["payload", "info", "total_token_usage"], out var cumulative);
        var selected = last.ValueKind == JsonValueKind.Object ? last : cumulative;
        if (selected.ValueKind != JsonValueKind.Object) return null;
        var context = new RecordContext(providerHint, First(modelHint, ReadModel(root), "codex"), ReadTimestamp(root), null, 0, false, "", "", "");
        var record = FromUsage(selected, context);
        if (cumulative.ValueKind == JsonValueKind.Object)
        {
            var previous = state.PreviousCumulative;
            if (previous.ValueKind == JsonValueKind.Object && JsonHelpers.ReadLong(cumulative, "total_tokens") >= JsonHelpers.ReadLong(previous, "total_tokens"))
            {
                long Delta(string field) => Math.Max(0, JsonHelpers.ReadLong(cumulative, field) - JsonHelpers.ReadLong(previous, field));
                record.CacheReadTokens = Delta("cached_input_tokens"); record.InputTokens = Math.Max(0, Delta("input_tokens") - record.CacheReadTokens);
                record.OutputTokens = Delta("output_tokens"); record.ReasoningTokens = Math.Max(Delta("reasoning_output_tokens"), Delta("reasoning_tokens"));
                record.TotalTokensOverride = Delta("total_tokens");
            }
            state.PreviousCumulative = cumulative.Clone();
            record.StableId = $"codex:{state.SessionKey}:" + string.Join(':', new[] { "input_tokens", "cached_input_tokens", "output_tokens", "reasoning_output_tokens", "reasoning_tokens", "total_tokens" }.Select(f => JsonHelpers.ReadLong(cumulative, f)));
        }
        return record;
    }
    internal static string ReadModel(JsonElement root)
    {
        foreach (var path in new string[][] { ["model"], ["model_name"], ["modelName"], ["modelVersion"], ["message", "model"], ["payload", "model"], ["payload", "collaboration_mode", "settings", "model"], ["response", "model"] })
            if (JsonHelpers.TryGetPath(root, path, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString() ?? "";
        return "";
    }
    internal static DateTime? ReadTimestamp(JsonElement context)
    {
        foreach (var name in new[] { "timestamp", "created_at", "createdAt", "created", "time", "datetime", "date", "startTime", "ts" })
        {
            if (!JsonHelpers.TryGetProperty(context, name, out var value)) continue;
            var timestamp = JsonHelpers.ReadTimestamp(value);
            if (timestamp is not null) return timestamp;
            if (value.ValueKind == JsonValueKind.Object && JsonHelpers.TryGetProperty(value, "created", out var created)) return JsonHelpers.ReadTimestamp(created);
        }
        return null;
    }
    private static decimal? ReadCost(JsonElement element, string inheritedCurrency = "")
    {
        var currency = First(JsonHelpers.FirstString(element, "currency", "cost_currency"), inheritedCurrency);
        foreach (var name in new[] { "costUSD", "cost_usd", "total_cost_usd", "cost", "total_cost", "response_cost", "usage_cost", "estimated_cost" })
        {
            if (currency.Length > 0 && !currency.Equals("USD", StringComparison.OrdinalIgnoreCase) && !name.Contains("usd", StringComparison.OrdinalIgnoreCase)) continue;
            if (JsonHelpers.TryGetProperty(element, name, out var value) && JsonHelpers.TryDecimal(value, out var cost) && cost >= 0) return cost;
        }
        return null;
    }
    private static bool LooksLikeUsage(JsonElement e) => JsonHelpers.HasAny(e, "input_tokens", "prompt_tokens", "output_tokens", "completion_tokens", "total_tokens", "promptTokenCount", "totalTokenCount", "inputTokens", "outputTokens", "totalTokens");
    private static string First(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
    private sealed record RecordContext(string Provider, string Model, DateTime? Timestamp, string? Id, decimal Cost, bool HasCost, string Speed, string InferenceGeo, string Currency);
}
public static class ModelIdentity
{
    public static string NormalizeProvider(string provider)
    {
        var value = provider.Trim().ToLowerInvariant().Replace('-', '_');
        return value switch
        {
            "claude" or "anthropic" or "claude_code" => "anthropic",
            "codex" or "oai" or "openai" => "openai",
            "gemini" or "google" => "google",
            "vertex_ai" or "vertex" => "vertex_ai",
            "moonshot" or "moonshotai" or "moonshot_ai" or "kimi" or "kimi_coding" => "kimi",
            "z_ai" or "zai" or "z.ai" or "zhipu" or "zhipuai" or "glm" => "zhipu",
            "dashscope" or "aliyun" or "alibaba" or "tongyi" or "qwen" => "qwen",
            "volcengine" or "volcengine_cn" or "volc" or "doubao" or "bytedance" => "doubao",
            "x_ai" or "xai" or "grok" => "xai", "minimax" or "minimax_chat" => "minimax",
            "mistralai" or "mistral" => "mistral", "together_ai" or "together" => "together_ai",
            "cohere_chat" or "cohere" => "cohere",
            "fireworks" or "fireworks_ai" => "fireworks_ai", "amazon_bedrock" or "bedrock" => "bedrock",
            _ => value
        };
    }
    public static string ResolveProvider(string provider, string model, string hint = "")
    {
        var explicitProvider = NormalizeProvider(provider);
        var prefix = model.Contains('/') ? NormalizeProvider(model.Split('/')[0]) : "";
        if (explicitProvider.Length > 0 && explicitProvider != "openai") return explicitProvider;
        if (RoutingProviders.Contains(prefix)) return prefix;
        var inferred = InferProvider(model);
        if (inferred.Length > 0) return inferred;
        return explicitProvider.Length > 0 ? explicitProvider : string.IsNullOrWhiteSpace(hint) ? "unknown" : NormalizeProvider(hint);
    }
    private static readonly HashSet<string> RoutingProviders = ["openrouter", "bedrock", "azure", "azure_ai", "vertex_ai", "groq", "together_ai", "fireworks_ai", "deepinfra", "cerebras", "perplexity", "ollama", "nvidia_nim", "novita", "siliconflow", "sambanova"];
    public static string InferProvider(string model)
    {
        var m = model.ToLowerInvariant();
        if (m.Contains("claude") || Regex.IsMatch(m, @"\b(opus|sonnet|haiku|fable|mythos)[- ]\d")) return "anthropic";
        if (m.Contains("gpt") || m.Contains("codex") || Regex.IsMatch(m, @"(^|/)(o[1-9])([-/]|$)")) return "openai";
        if (m.Contains("gemini") || m.Contains("gemma")) return "google";
        if (m.Contains("deepseek")) return "deepseek";
        if (m.Contains("qwen") || m.Contains("qwq")) return "qwen";
        if (m.Contains("kimi") || m.Contains("moonshot")) return "kimi";
        if (m.Contains("glm")) return "zhipu";
        if (m.Contains("grok")) return "xai";
        if (m.Contains("minimax")) return "minimax";
        if (m.Contains("doubao")) return "doubao";
        if (m.Contains("mistral") || m.Contains("codestral") || m.Contains("magistral") || m.Contains("devstral") || m.Contains("ministral") || m.Contains("pixtral")) return "mistral";
        if (m.Contains("command-r") || m.Contains("command-a")) return "cohere";
        if (m.Contains("sonar")) return "perplexity";
        if (m.Contains("llama")) return "meta";
        if (m.Contains("jamba")) return "ai21";
        if (m.Contains("ernie")) return "baidu";
        if (m.Contains("hunyuan")) return "tencent";
        return "";
    }
    public static string NormalizeModel(string model)
    {
        var normalized = model.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[\s_]+", "-");
        // Effort/plan labels are UI metadata, not a different model's API rate.
        normalized = Regex.Replace(normalized, @"[- ](?:max|xhigh|high|medium|low)$", "");
        normalized = Regex.Replace(normalized, @"(?<=\d)\.(?=\d)", "-");
        if (Regex.IsMatch(normalized, @"^(opus|sonnet|haiku|fable|mythos)-\d")) normalized = "claude-" + normalized;
        return normalized;
    }
}
public sealed class ModelPriceCatalog
{
    private readonly List<PriceEntry> prices = [];
    private readonly PetStatsSettings settings;
    private readonly string cachePath;
    private DateTime nextRefreshCheck;
    public ModelPriceCatalog(PetStatsSettings settings, string dataDirectory)
    {
        this.settings = settings;
        cachePath = Path.Combine(dataDirectory, "model-prices.litellm.json");
        prices.AddRange(settings.Prices.Where(p => !string.IsNullOrWhiteSpace(p.Pattern)).Select(p => new PriceEntry(p, default, true)));
        Directory.CreateDirectory(dataDirectory);
        LoadAvailableCatalogs();
    }
    private void LoadAvailableCatalogs()
    {
        prices.RemoveAll(p => !p.Custom);
        try { if (File.Exists(cachePath)) LoadCatalog(File.ReadAllText(cachePath)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        using var bundled = Assembly.GetExecutingAssembly().GetManifestResourceStream("PetStatsOverlay.Data.model-prices.litellm.json");
        if (bundled is not null) { using var reader = new StreamReader(bundled); LoadCatalog(reader.ReadToEnd()); }
    }
    public void RefreshIfNeeded()
    {
        if (DateTime.UtcNow < nextRefreshCheck || string.IsNullOrWhiteSpace(settings.PricingCatalogUrl)) return;
        // Retry failed network updates in thirty minutes, not on every UI refresh.
        nextRefreshCheck = DateTime.UtcNow.AddMinutes(30);
        var before = File.Exists(cachePath) ? File.GetLastWriteTimeUtc(cachePath) : DateTime.MinValue;
        RefreshCacheIfNeeded(settings, cachePath);
        var after = File.Exists(cachePath) ? File.GetLastWriteTimeUtc(cachePath) : DateTime.MinValue;
        if (after != before) LoadAvailableCatalogs();
        if (after > DateTime.UtcNow.AddHours(-Math.Max(1, settings.RefreshPricingHours)))
            nextRefreshCheck = after.AddHours(Math.Max(1, settings.RefreshPricingHours));
    }
    public decimal Estimate(UsageRecord record)
    {
        if (record.HasExplicitCost || record.ExplicitCost > 0) { record.IsPriced = true; return record.ExplicitCost; }
        var match = FindPrice(record);
        record.IsPriced = match is not null;
        if (match is null) return 0;
        var price = match.Price;
        var contextTokens = record.InputTokens + record.CacheReadTokens + record.CacheCreationTokens;
        var input = Rate(match, "input_cost_per_token", price.InputPerMillion, contextTokens);
        var output = Rate(match, "output_cost_per_token", price.OutputPerMillion, contextTokens);
        var cacheRead = Rate(match, "cache_read_input_token_cost", price.CacheReadPerMillion, contextTokens);
        var write5m = price.CacheWrite5mPerMillion;
        var write1h = price.CacheWrite1hPerMillion;
        // Missing cache rates are unknown, never silently interpreted as free.
        if (!match.Custom && ((record.InputTokens > 0 && !HasRate(match.Metadata, "input_cost_per_token"))
            || (record.OutputTokens > 0 && !HasRate(match.Metadata, "output_cost_per_token"))
            || (record.CacheReadTokens > 0 && !HasRate(match.Metadata, "cache_read_input_token_cost"))
            || (record.CacheCreationTokens > record.CacheCreation1hTokens && !HasRate(match.Metadata, "cache_creation_input_token_cost"))
            || (record.CacheCreation1hTokens > 0 && !HasRate(match.Metadata, "cache_creation_input_token_cost_above_1hr", "cache_creation_input_token_cost_1hr")))) record.IsPriced = false;
        var total = Cost(record.InputTokens, input) + Cost(record.OutputTokens, output) + Cost(record.CacheReadTokens, cacheRead)
            + Cost(Math.Max(0, record.CacheCreationTokens - record.CacheCreation1hTokens), write5m)
            + Cost(record.CacheCreation1hTokens, write1h);
        if (price.TotalPerMillion > 0 && total == 0) total = Cost(record.TotalTokens, price.TotalPerMillion);
        if (record.TotalTokens > 0 && record.InputTokens + record.OutputTokens + record.CacheReadTokens + record.CacheCreationTokens == 0 && price.TotalPerMillion == 0) record.IsPriced = false;
        if (ModelIdentity.NormalizeProvider(record.Provider) == "anthropic" && match.Metadata.ValueKind == JsonValueKind.Object
            && JsonHelpers.TryGetProperty(match.Metadata, "provider_specific_entry", out var modifiers))
        {
            var fast = JsonHelpers.ReadDecimal(modifiers, "fast"); var geo = JsonHelpers.ReadDecimal(modifiers, "us");
            if (record.Speed == "fast" && fast > 0) total *= fast;
            if (record.InferenceGeo == "us" && geo > 0) total *= geo;
        }
        return total;
    }
    private PriceEntry? FindPrice(UsageRecord record)
    {
        var model = ModelIdentity.NormalizeModel(record.Model);
        if (model.Length == 0) return null;
        var provider = ModelIdentity.NormalizeProvider(record.Provider);
        var candidates = prices.Where(p => string.IsNullOrEmpty(p.Price.Provider) || ModelIdentity.NormalizeProvider(p.Price.Provider) == provider).ToList();
        var custom = candidates.Where(p => p.Custom).OrderByDescending(p => p.Price.Pattern.Length).FirstOrDefault(p => MatchModel(model, p.Price.Pattern, provider, true));
        if (custom is not null) return custom;
        return candidates.FirstOrDefault(p => MatchModel(model, p.Price.Pattern, provider, false))
            ?? candidates.FirstOrDefault(p => MatchModel(RemoveDate(model), p.Price.Pattern, provider, false));
    }
    private static bool MatchModel(string model, string pattern, string provider, bool custom)
    {
        var normalized = ModelIdentity.NormalizeModel(pattern);
        static string StripProvider(string value, string provider)
        {
            var slash = value.IndexOf('/');
            return slash >= 0 && ModelIdentity.NormalizeProvider(value[..slash]) == provider ? value[(slash + 1)..] : value;
        }
        var left = StripProvider(model, provider); var right = StripProvider(normalized, provider);
        if (left == right) return true;
        // Explicit wildcards allow private/deployment aliases without guessing catalog prices.
        return custom && pattern.Contains('*') && Regex.IsMatch(left, "^" + Regex.Escape(right).Replace("\\*", ".*") + "$");
    }
    private static string RemoveDate(string model) => Regex.Replace(model, @"-(?:20\d{6}|20\d{2}-\d{2}-\d{2})$", "");
    private void LoadCatalog(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
            foreach (var model in doc.RootElement.EnumerateObject())
            {
                var data = model.Value;
                if (data.ValueKind != JsonValueKind.Object || !JsonHelpers.HasAny(data, "input_cost_per_token", "output_cost_per_token")) continue;
                var mode = JsonHelpers.ReadString(data, "mode");
                if (mode.Length > 0 && mode is not ("chat" or "completion" or "responses")) continue;
                prices.Add(new PriceEntry(new ModelPrice
                {
                    Pattern = model.Name, Provider = JsonHelpers.ReadString(data, "litellm_provider"),
                    InputPerMillion = JsonHelpers.ReadDecimal(data, "input_cost_per_token") * 1_000_000,
                    OutputPerMillion = JsonHelpers.ReadDecimal(data, "output_cost_per_token") * 1_000_000,
                    CacheReadPerMillion = JsonHelpers.ReadDecimal(data, "cache_read_input_token_cost") * 1_000_000,
                    CacheWrite5mPerMillion = JsonHelpers.ReadDecimal(data, "cache_creation_input_token_cost") * 1_000_000,
                    CacheWrite1hPerMillion = JsonHelpers.FirstDecimal(data, "cache_creation_input_token_cost_above_1hr", "cache_creation_input_token_cost_1hr") * 1_000_000
                }, data.Clone(), false));
            }
        }
        catch (JsonException) { }
    }
    private static decimal Rate(PriceEntry entry, string field, decimal fallback, long contextTokens)
    {
        if (entry.Metadata.ValueKind != JsonValueKind.Object) return fallback;
        var result = fallback; var highest = 0L;
        foreach (var property in entry.Metadata.EnumerateObject())
        {
            var match = Regex.Match(property.Name, "^" + Regex.Escape(field) + @"_above_(\d+)(k)?_tokens$");
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out var threshold)) continue;
            if (match.Groups[2].Success) threshold *= 1000;
            if (contextTokens > threshold && threshold > highest && JsonHelpers.TryDecimal(property.Value, out var rate)) { highest = threshold; result = rate * 1_000_000; }
        }
        return result;
    }
    private static void RefreshCacheIfNeeded(PetStatsSettings settings, string cachePath)
    {
        if (string.IsNullOrWhiteSpace(settings.PricingCatalogUrl)) return;
        try
        {
            if (File.Exists(cachePath) && DateTime.Now - File.GetLastWriteTime(cachePath) < TimeSpan.FromHours(Math.Max(1, settings.RefreshPricingHours))) return;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var body = client.GetStringAsync(settings.PricingCatalogUrl).GetAwaiter().GetResult();
            if (body.Length > 16 * 1024 * 1024) return;
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.EnumerateObject().Any(p => JsonHelpers.HasAny(p.Value, "input_cost_per_token", "output_cost_per_token"))) return;
            var temp = cachePath + ".tmp"; File.WriteAllText(temp, body); File.Move(temp, cachePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException or JsonException) { }
    }
    private static decimal Cost(long tokens, decimal rate) => tokens <= 0 || rate <= 0 ? 0 : tokens / 1_000_000M * rate;
    private static bool HasRate(JsonElement metadata, params string[] fields) => fields.Any(f => JsonHelpers.TryGetProperty(metadata, f, out var value) && JsonHelpers.TryDecimal(value, out var rate) && rate >= 0);
    private sealed record PriceEntry(ModelPrice Price, JsonElement Metadata, bool Custom);
}
public sealed class DailyUsage
{
    public DateOnly Date { get; set; }
    public List<ProviderUsage> Providers { get; } = [];
    public long TotalTokens { get; private set; }
    public decimal EstimatedCost { get; private set; }
    public int RecordCount => Providers.Sum(p => p.Records.Count);
    public int UnpricedRecordCount => Providers.Sum(p => p.UnpricedRecordCount);
    public void Add(UsageRecord record)
    {
        var provider = Providers.FirstOrDefault(p => p.Provider.Equals(record.Provider, StringComparison.OrdinalIgnoreCase));
        if (provider is null) { provider = new ProviderUsage { Provider = record.Provider }; Providers.Add(provider); }
        provider.Records.Add(record);
    }
    public void RecalculateTotals()
    {
        foreach (var provider in Providers) provider.RecalculateTotals();
        TotalTokens = Providers.Sum(p => p.TotalTokens); EstimatedCost = Providers.Sum(p => p.EstimatedCost);
        Providers.Sort((a, b) => b.TotalTokens.CompareTo(a.TotalTokens));
    }
}
public sealed class ProviderUsage
{
    public string Provider { get; set; } = "";
    public List<UsageRecord> Records { get; } = [];
    public long InputTokens { get; private set; }
    public long CacheCreationTokens { get; private set; }
    public long CacheReadTokens { get; private set; }
    public long OutputTokens { get; private set; }
    public long ReasoningTokens { get; private set; }
    public long TotalTokens { get; private set; }
    public decimal EstimatedCost { get; private set; }
    public int UnpricedRecordCount => Records.Count(r => !r.IsPriced);
    public void Add(UsageRecord record) => Records.Add(record);
    public void RecalculateTotals()
    {
        InputTokens = Records.Sum(r => r.InputTokens); CacheCreationTokens = Records.Sum(r => r.CacheCreationTokens);
        CacheReadTokens = Records.Sum(r => r.CacheReadTokens); OutputTokens = Records.Sum(r => r.OutputTokens);
        ReasoningTokens = Records.Sum(r => r.ReasoningTokens); TotalTokens = Records.Sum(r => r.TotalTokens); EstimatedCost = Records.Sum(r => r.EstimatedCost);
    }
}
public sealed class UsageRecord
{
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string Source { get; set; } = "";
    public string? StableId { get; set; }
    public string SessionKey { get; set; } = "";
    public bool IsSessionSummary { get; set; }
    public DateTime? Timestamp { get; set; }
    public long InputTokens { get; set; }
    public long CacheCreationTokens { get; set; }
    public long CacheCreation1hTokens { get; set; }
    public long CacheReadTokens { get; set; }
    public long OutputTokens { get; set; }
    public long ReasoningTokens { get; set; }
    public long TotalTokensOverride { get; set; }
    public decimal ExplicitCost { get; set; }
    public bool HasExplicitCost { get; set; }
    public bool IsPriced { get; set; }
    public decimal EstimatedCost { get; set; }
    public string Speed { get; set; } = "";
    public string InferenceGeo { get; set; } = "";
    public bool HasAnyUsage => TotalTokens > 0 || ExplicitCost > 0;
    // Reasoning is an output breakdown, already included in OutputTokens.
    public long TotalTokens => TotalTokensOverride > 0 ? TotalTokensOverride : InputTokens + CacheCreationTokens + CacheReadTokens + OutputTokens;
    public void MergeSnapshot(UsageRecord other)
    {
        InputTokens = Math.Max(InputTokens, other.InputTokens); OutputTokens = Math.Max(OutputTokens, other.OutputTokens);
        CacheReadTokens = Math.Max(CacheReadTokens, other.CacheReadTokens); CacheCreationTokens = Math.Max(CacheCreationTokens, other.CacheCreationTokens);
        CacheCreation1hTokens = Math.Max(CacheCreation1hTokens, other.CacheCreation1hTokens); ReasoningTokens = Math.Max(ReasoningTokens, other.ReasoningTokens);
        TotalTokensOverride = Math.Max(Math.Max(TotalTokensOverride, other.TotalTokensOverride), InputTokens + OutputTokens + CacheReadTokens + CacheCreationTokens);
        if (other.HasExplicitCost) { ExplicitCost = other.ExplicitCost; HasExplicitCost = true; }
        if (string.IsNullOrWhiteSpace(Model)) Model = other.Model;
        if (other.Speed.Length > 0) Speed = other.Speed;
        if (other.InferenceGeo.Length > 0) InferenceGeo = other.InferenceGeo;
    }
}
public static class JsonHelpers
{
    public static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        value = default; return false;
    }
    public static bool TryGetPath(JsonElement root, IEnumerable<string> names, out JsonElement value)
    {
        value = root;
        foreach (var name in names) if (!TryGetProperty(value, name, out value)) return false;
        return true;
    }
    public static bool HasAny(JsonElement element, params string[] names) => names.Any(n => TryGetProperty(element, n, out _));
    public static string ReadString(JsonElement element, string name) => TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    public static string FirstString(JsonElement element, params string[] names) => names.Select(n => ReadString(element, n)).FirstOrDefault(v => v.Length > 0) ?? "";
    public static long ReadLong(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || !TryDecimal(value, out var number) || number < 0 || number > long.MaxValue) return 0;
        return (long)number;
    }
    public static long FirstLong(JsonElement element, params string[] names)
    {
        foreach (var name in names) if (TryGetProperty(element, name, out _)) return ReadLong(element, name);
        return 0;
    }
    public static decimal ReadDecimal(JsonElement element, string name) => TryGetProperty(element, name, out var value) && TryDecimal(value, out var result) ? result : 0;
    public static decimal FirstDecimal(JsonElement element, params string[] names)
    {
        foreach (var name in names) if (TryGetProperty(element, name, out var value) && TryDecimal(value, out var result)) return result;
        return 0;
    }
    public static bool TryDecimal(JsonElement value, out decimal result)
    {
        if (value.ValueKind == JsonValueKind.Number) return value.TryGetDecimal(out result);
        if (value.ValueKind == JsonValueKind.String) return decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        result = 0; return false;
    }
    public static DateTime? ReadTimestamp(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var timestamp)) return timestamp.LocalDateTime;
        if (!TryDecimal(value, out var numeric) || numeric > long.MaxValue || numeric < long.MinValue) return null;
        try { return numeric > 100_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds((long)numeric).LocalDateTime : DateTimeOffset.FromUnixTimeSeconds((long)numeric).LocalDateTime; }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
