using System.Globalization;
using System.Text.Json;
using PetStatsOverlay;

var passed = 0;
var failed = 0;
var date = DateOnly.FromDateTime(DateTime.Now);
var today = DateTime.Today.AddHours(12).ToString("O");
var yesterday = DateTime.Today.AddDays(-1).AddHours(12).ToString("O");
var work = Path.Combine(Path.GetTempPath(), "nikki-usage-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work);
var catalog = new ModelPriceCatalog(new PetStatsSettings(), work);
void Test(string name, Action body)
{
    try { body(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception ex) { Console.WriteLine("FAIL " + name + ": " + ex.Message); failed++; }
}
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}"); }
UsageRecord Parse(string json)
{
    using var doc = JsonDocument.Parse(json);
    var rows = UsageRecordExtractor.FindUsageRecords(doc.RootElement, "", "").ToList();
    Equal(1, rows.Count);
    var record = rows[0]; record.Provider = ModelIdentity.ResolveProvider(record.Provider, record.Model);
    return record;
}
DailyUsage Read(params string[] lines)
{
    var folder = Path.Combine(work, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, "test.jsonl"); File.WriteAllLines(path, lines);
    var settings = new PetStatsSettings { AutoDiscoverLogRoots = false, LogRoots = [new() { Name = "fixture", Path = folder, ScanJsonFiles = true }] };
    return new TokenLogReader(settings, Path.Combine(folder, "cache")).GetUsage(date);
}
string At(string json, string? time = null) => json.Replace("@TIME@", time ?? today);
Test("Claude nested context keeps model/date/id and parses once", () =>
{
    var r = Parse(At("""{"uuid":"wrapper","timestamp":"@TIME@","message":{"id":"msg","model":"claude-opus-5","usage":{"input_tokens":10,"output_tokens":20,"cache_read_input_tokens":30,"cache_creation_input_tokens":40,"cache_creation":{"ephemeral_5m_input_tokens":15,"ephemeral_1h_input_tokens":25}}}}"""));
    Equal("claude-opus-5", r.Model); Equal("msg", r.StableId); Equal(date, DateOnly.FromDateTime(r.Timestamp!.Value)); Equal(100L, r.TotalTokens); Equal(25L, r.CacheCreation1hTokens);
    Equal(0.00090875M, catalog.Estimate(r)); Equal(true, r.IsPriced);
});
Test("Opus 5 Max effort label uses verified Opus 5 price", () =>
{
    var r = Parse("""{"model":"Opus 5 Max","usage":{"input_tokens":1000000,"output_tokens":1000000}}""");
    Equal(30M, catalog.Estimate(r)); Equal(true, r.IsPriced);
});
Test("Unknown successor is counted without inheriting another price", () =>
{
    var usage = Read(At("""{"timestamp":"@TIME@","model":"claude-opus-50","usage":{"input_tokens":40,"output_tokens":10}}"""));
    Equal(50L, usage.TotalTokens); Equal(1, usage.UnpricedRecordCount); Equal(0M, usage.EstimatedCost);
});
Test("OpenAI reasoning/cache aliases do not double count", () =>
{
    var r = Parse("""{"model":"gpt-4o","usage":{"prompt_tokens":100,"input_tokens":100,"completion_tokens":30,"prompt_tokens_details":{"cached_tokens":60},"completion_tokens_details":{"reasoning_tokens":10}}}""");
    Equal(40L, r.InputTokens); Equal(60L, r.CacheReadTokens); Equal(30L, r.OutputTokens); Equal(10L, r.ReasoningTokens); Equal(130L, r.TotalTokens);
});
Test("DeepSeek cache misses are normal input", () =>
{
    var r = Parse("""{"model":"deepseek-chat","usage":{"prompt_tokens":100,"completion_tokens":20,"prompt_cache_hit_tokens":70,"prompt_cache_miss_tokens":30}}""");
    Equal(30L, r.InputTokens); Equal(0L, r.CacheCreationTokens); Equal(120L, r.TotalTokens); catalog.Estimate(r); Equal(true, r.IsPriced);
});
Test("Gemini cached input and billable thoughts handled once", () =>
{
    var r = Parse("""{"modelVersion":"gemini-2.5-pro","usageMetadata":{"promptTokenCount":100,"cachedContentTokenCount":60,"candidatesTokenCount":20,"thoughtsTokenCount":10,"totalTokenCount":130}}""");
    Equal(40L, r.InputTokens); Equal(60L, r.CacheReadTokens); Equal(30L, r.OutputTokens); Equal(130L, r.TotalTokens); Equal("google", r.Provider);
});
Test("Gemini CLI TokensSummary cache/thoughts/total schema", () =>
{
    var r = Parse("""{"model":"gemini-2.5-pro","tokens":{"input":100,"output":20,"cached":60,"thoughts":10,"tool":0,"total":130}}""");
    Equal(40L, r.InputTokens); Equal(60L, r.CacheReadTokens); Equal(30L, r.OutputTokens); Equal(10L, r.ReasoningTokens); Equal(130L, r.TotalTokens);
});
Test("Distinct array messages retain their own models and dates", () =>
{
    var usage = Read(At("""{"messages":[{"timestamp":"@TIME@","model":"kimi-k2.5","usage":{"input_tokens":10,"output_tokens":20}},{"timestamp":"@TIME@","model":"glm-5","usage":{"input_tokens":30,"output_tokens":40}}]}"""));
    Equal(2, usage.RecordCount); Equal(2, usage.Providers.Count); Equal(100L, usage.TotalTokens);
});
Test("Live Claude partial snapshots merge into final message once", () =>
{
    var usage = Read(At("""{"timestamp":"@TIME@","uuid":"first","message":{"id":"shared","model":"claude-opus-5","usage":{"input_tokens":10,"output_tokens":1}}}"""), At("""{"timestamp":"@TIME@","uuid":"second","message":{"id":"shared","model":"claude-opus-5","usage":{"input_tokens":10,"output_tokens":50}}}"""));
    Equal(1, usage.RecordCount); Equal(60L, usage.TotalTokens);
});
Test("Anthropic stream start and message delta merge", () =>
{
    var usage = Read(At("""{"type":"message_start","timestamp":"@TIME@","message":{"id":"stream","model":"claude-opus-5","usage":{"input_tokens":100,"output_tokens":0}}}"""), At("""{"type":"message_delta","timestamp":"@TIME@","usage":{"output_tokens":20}}"""));
    Equal(1, usage.RecordCount); Equal(120L, usage.TotalTokens);
});
Test("Yesterday and undated counters never leak into today", () =>
{
    var usage = Read(At("""{"timestamp":"@TIME@","model":"gpt-4o","usage":{"input_tokens":100}}""", yesterday), """{"model":"gpt-4o","usage":{"input_tokens":50}}""", At("""{"timestamp":"@TIME@","model":"gpt-4o","usage":{"input_tokens":10}}"""));
    Equal(10L, usage.TotalTokens); Equal(1, usage.RecordCount);
});
Test("Malformed append and out-of-range timestamp isolated", () =>
{
    var usage = Read("{partial", """{"timestamp":999999999999999999,"usage":{"input_tokens":100}}""", At("""{"timestamp":"@TIME@","model":"gpt-4o","usage":{"input_tokens":10}}"""));
    Equal(10L, usage.TotalTokens);
});
Test("Cumulative Codex repeated events and multiple sessions", () =>
{
    var folder = Path.Combine(work, "codex"); Directory.CreateDirectory(folder);
    var token = At("""{"timestamp":"@TIME@","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":100,"cached_input_tokens":20,"output_tokens":10,"total_tokens":110},"total_token_usage":{"input_tokens":100,"cached_input_tokens":20,"output_tokens":10,"total_tokens":110}}}}""");
    foreach (var session in new[] { "one", "two" }) File.WriteAllLines(Path.Combine(folder, session + ".jsonl"), ["{\"type\":\"session_meta\",\"payload\":{\"id\":\"" + session + "\"}}", "{\"type\":\"turn_context\",\"payload\":{\"model\":\"gpt-4o\"}}", token, token]);
    var settings = new PetStatsSettings { AutoDiscoverLogRoots = false, LogRoots = [new() { Name = "codex", Path = folder }] };
    var usage = new TokenLogReader(settings, Path.Combine(folder, "cache")).GetUsage(date);
    Equal(220L, usage.TotalTokens); Equal(2, usage.RecordCount);
});
Test("Codex delta uses prior day's baseline", () =>
{
    var before = At("""{"timestamp":"@TIME@","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":100,"output_tokens":10,"total_tokens":110},"total_token_usage":{"input_tokens":100,"output_tokens":10,"total_tokens":110}}}}""", yesterday);
    var now = At("""{"timestamp":"@TIME@","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":100,"output_tokens":10,"total_tokens":110},"total_token_usage":{"input_tokens":150,"output_tokens":30,"total_tokens":180}}}}""");
    Equal(70L, Read(before, now, now).TotalTokens);
});
Test("SDK camelCase model summaries supported", () =>
{
    var usage = Read(At("""{"timestamp":"@TIME@","session_id":"sdk","modelUsage":{"claude-opus-5":{"inputTokens":10,"outputTokens":20,"cacheReadInputTokens":30,"cacheCreationInputTokens":40,"costUSD":0.25}}}"""));
    Equal(100L, usage.TotalTokens); Equal(0.25M, usage.EstimatedCost);
});
Test("SDK summary does not repeat assistant records", () =>
{
    var usage = Read(At("""{"timestamp":"@TIME@","session_id":"sdk","message":{"id":"msg","model":"claude-opus-5","usage":{"input_tokens":10,"output_tokens":20}}}"""), At("""{"timestamp":"@TIME@","session_id":"sdk","modelUsage":{"claude-opus-5":{"inputTokens":10,"outputTokens":20,"costUSD":0.25}}}"""));
    Equal(30L, usage.TotalTokens); Equal(1, usage.RecordCount);
});
Test("Zero logged USD cost is authoritative and decimal strings are invariant", () =>
{
    var old = CultureInfo.CurrentCulture;
    try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); var r = Parse("""{"model":"unknown","cost":"0.00","usage":{"input_tokens":100}}"""); Equal(0M, catalog.Estimate(r)); Equal(true, r.IsPriced); }
    finally { CultureInfo.CurrentCulture = old; }
});
Test("Session total costs are not inherited by every array message", () =>
{
    var usage = Read(At("""{"timestamp":"@TIME@","total_cost":100,"messages":[{"id":"a","model":"unknown","usage":{"input_tokens":10}},{"id":"b","model":"unknown","usage":{"input_tokens":20}}]}"""));
    Equal(30L, usage.TotalTokens); Equal(0M, usage.EstimatedCost); Equal(2, usage.UnpricedRecordCount);
});
Test("Nested non-USD costs are never silently counted as USD", () =>
{
    var r = Parse("""{"model":"unknown","currency":"CNY","usage":{"input_tokens":10,"cost":20}}""");
    Equal(0M, catalog.Estimate(r)); Equal(false, r.IsPriced);
});
Test("Model families and provider routes have offline prices", () =>
{
    foreach (var model in new[] { "claude-opus-5", "kimi-k2.5", "glm-5", "deepseek-chat", "gemini-2.5-pro", "grok-4", "qwen-plus", "MiniMax-M2.5", "mistral-large-latest", "command-r-plus", "sonar", "openrouter/anthropic/claude-opus-5", "groq/llama-3.3-70b-versatile" })
    {
        var r = new UsageRecord { Model = model, Provider = ModelIdentity.ResolveProvider("", model), InputTokens = 1000, OutputTokens = 100 };
        var cost = catalog.Estimate(r); if (!r.IsPriced || cost <= 0) throw new Exception(model + " missing price (provider " + r.Provider + ")");
    }
});
Test("Dated model alias matches, provider-specific routes stay distinct", () =>
{
    var r = new UsageRecord { Model = "anthropic/claude-opus-5-20260724", Provider = "anthropic", InputTokens = 1000000 }; Equal(5M, catalog.Estimate(r));
    r = new UsageRecord { Model = "claude-opus-5", Provider = "private-host", InputTokens = 1000 }; Equal(0M, catalog.Estimate(r)); Equal(false, r.IsPriced);
});
Test("User wildcard prices override catalog", () =>
{
    var custom = new ModelPriceCatalog(new PetStatsSettings { Prices = [new() { Pattern = "claude-opus-5", InputPerMillion = 1 }, new() { Pattern = "private-*", InputPerMillion = 2 }] }, work);
    Equal(1M, custom.Estimate(new() { Model = "claude-opus-5", Provider = "anthropic", InputTokens = 1000000 }));
    Equal(2M, custom.Estimate(new() { Model = "private-tuned", Provider = "custom", InputTokens = 1000000 }));
});
Test("Missing directional/cache rates stay incomplete without fabricated charges", () =>
{
    var folder = Path.Combine(work, "partial-prices"); Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "model-prices.litellm.json"), """{"partial-input":{"litellm_provider":"private","input_cost_per_token":null,"output_cost_per_token":0.000004},"partial-output":{"litellm_provider":"private","input_cost_per_token":0.000002},"partial-cache":{"litellm_provider":"private","input_cost_per_token":0.000002,"output_cost_per_token":0.000004,"cache_creation_input_token_cost":0.000003},"free":{"litellm_provider":"private","input_cost_per_token":0,"output_cost_per_token":0}}""");
    var partial = new ModelPriceCatalog(new PetStatsSettings(), folder);
    var r = new UsageRecord { Model = "partial-input", Provider = "private", InputTokens = 1000000, OutputTokens = 1000000 };
    Equal(4M, partial.Estimate(r)); Equal(false, r.IsPriced);
    r = new UsageRecord { Model = "partial-output", Provider = "private", InputTokens = 1000000, OutputTokens = 1000000 };
    Equal(2M, partial.Estimate(r)); Equal(false, r.IsPriced);
    r = new UsageRecord { Model = "partial-cache", Provider = "private", CacheCreationTokens = 1000000, CacheCreation1hTokens = 1000000 };
    Equal(0M, partial.Estimate(r)); Equal(false, r.IsPriced);
    r = new UsageRecord { Model = "free", Provider = "private", InputTokens = 1000000, OutputTokens = 1000000 };
    Equal(0M, partial.Estimate(r)); Equal(true, r.IsPriced);
});
Test("Total-only token records stay unpriced without an invented split", () =>
{
    var r = Parse("""{"model":"gpt-4o","usage":{"total_tokens":1000}}"""); Equal(1000L, r.TotalTokens); Equal(0M, catalog.Estimate(r)); Equal(false, r.IsPriced);
});
Test("Store package discovery does not hard-code suffix", () =>
{
    var packages = Path.Combine(work, "Packages"); var app = Path.Combine(packages, "Claude_testpublisher", "LocalCache", "Roaming", "Claude"); Directory.CreateDirectory(app);
    Equal(app, AiLogRootDiscovery.DiscoverPackagedClaudeRoots(packages).Single().Path);
});
if (args.Contains("--local"))
{
    Test("Actual local Claude Desktop today is counted and priced", () =>
    {
        var roots = AiLogRootDiscovery.DiscoverPackagedClaudeRoots().ToList();
        if (roots.Count == 0) throw new Exception("No packaged Claude installed");
        var settings = new PetStatsSettings { AutoDiscoverLogRoots = false, LogRoots = roots };
        var usage = new TokenLogReader(settings, Path.Combine(work, "local-cache")).GetUsage(date);
        var opus = usage.Providers.SelectMany(p => p.Records).Where(r => r.Model == "claude-opus-5").ToList();
        if (opus.Count == 0 || opus.Sum(r => r.TotalTokens) <= 0 || opus.Any(r => !r.IsPriced)) throw new Exception("No priced Opus 5 usage today");
        Console.WriteLine($"Local metadata only: {opus.Count} unique Opus 5 messages, {opus.Sum(r => r.TotalTokens)} tokens, ${opus.Sum(r => r.EstimatedCost):F4} API-equivalent");
    });
}
Console.WriteLine($"{passed} passed, {failed} failed");
// Only remove the exact test directory created above, never an input transcript directory.
Directory.Delete(work, true);
return failed == 0 ? 0 : 1;
