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
object CodexTokens(long input, long output) => new { input_tokens = input, cached_input_tokens = 0, cache_write_input_tokens = 0, output_tokens = output, reasoning_output_tokens = 0, total_tokens = input + output };
string CodexRequest(string time, string id, long input, long output, long cumulativeInput, long cumulativeOutput) => JsonSerializer.Serialize(new
{
    timestamp = time, type = "token_usage_record", payload = new { thread_id = "codex-session", session_id = "codex-session", response_id = id,
        usage = CodexTokens(input, output), turn_token_usage = CodexTokens(cumulativeInput, cumulativeOutput), thread_token_usage = CodexTokens(cumulativeInput, cumulativeOutput) }
});
string CodexSnapshot(string time, long input, long output, long cumulativeInput, long cumulativeOutput) => JsonSerializer.Serialize(new
{
    timestamp = time, type = "event_msg", payload = new { type = "token_count", info = new { last_token_usage = CodexTokens(input, output), total_token_usage = CodexTokens(cumulativeInput, cumulativeOutput) } }
});
string CodexSession() => """{"type":"session_meta","payload":{"id":"codex-session"}}""";
string CodexCompaction(string time, string id, long input, long output, long cumulativeInput, long cumulativeOutput)
{
    using var request = JsonDocument.Parse(CodexRequest(time, id, input, output, cumulativeInput, cumulativeOutput));
    return JsonSerializer.Serialize(new { timestamp = time, type = "compacted", payload = new { compaction_response_id = id, latest_token_usage_record = request.RootElement.GetProperty("payload") } });
}
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
Test("Codex request records never count turn/thread cumulative totals", () =>
{
    var usage = Read(CodexRequest(today, "request-one", 100, 10, 1000000, 200000), CodexRequest(today, "request-two", 200, 20, 1000200, 200020));
    Equal(330L, usage.TotalTokens); Equal(2, usage.RecordCount);
});
Test("Codex primary requests and UI snapshots count once", () =>
{
    var usage = Read(CodexSession(), CodexRequest(today, "request-one", 100, 10, 100, 10), CodexSnapshot(today, 100, 10, 100, 10),
        CodexRequest(today, "request-two", 200, 20, 300, 30), CodexSnapshot(today, 200, 20, 300, 30), CodexSnapshot(today, 200, 20, 300, 30));
    Equal(330L, usage.TotalTokens); Equal(2, usage.RecordCount);
});
Test("Codex compaction offsets do not defeat request/snapshot pairing", () =>
{
    var usage = Read(CodexSession(), CodexRequest(today, "request-one", 100, 10, 1100, 210), CodexSnapshot(today, 100, 10, 100, 10),
        CodexRequest(today, "request-two", 200, 20, 1300, 230), CodexSnapshot(today, 200, 20, 300, 30), CodexSnapshot(today, 200, 20, 300, 30));
    Equal(330L, usage.TotalTokens); Equal(2, usage.RecordCount);
});
Test("Codex compaction metadata replays the same request without cumulative billing", () =>
{
    var usage = Read(CodexRequest(today, "compaction", 100, 10, 1000000, 200000), CodexCompaction(today, "compaction", 100, 10, 1000000, 200000));
    Equal(110L, usage.TotalTokens); Equal(1, usage.RecordCount);
});
Test("Undated embedded usage never inherits today's compaction timestamp", () =>
{
    var usage = Read(CodexCompaction(today, "no-original-date", 100, 10, 1000000, 200000));
    Equal(0L, usage.TotalTokens); Equal(0, usage.RecordCount);
});
Test("Codex replay envelope cannot move an old request across midnight", () =>
{
    var usage = Read(CodexCompaction(today, "old-request", 100, 10, 1000000, 200000), CodexRequest(yesterday, "old-request", 100, 10, 1000000, 200000));
    Equal(0L, usage.TotalTokens); Equal(0, usage.RecordCount);
});
Test("Unmatched snapshot consumes pending pair without swallowing later requests", () =>
{
    var usage = Read(CodexSession(), CodexRequest(today, "request-one", 100, 10, 100, 10), CodexSnapshot(today, 200, 20, 300, 30), CodexSnapshot(today, 100, 10, 400, 40));
    Equal(440L, usage.TotalTokens); Equal(3, usage.RecordCount);
});
Test("Codex mixed legacy and new requests retain actual old-format calls", () =>
{
    var usage = Read(CodexSession(), CodexSnapshot(today, 100, 10, 100, 10), CodexRequest(today, "request-two", 200, 20, 300, 30), CodexSnapshot(today, 200, 20, 300, 30));
    Equal(330L, usage.TotalTokens); Equal(2, usage.RecordCount);
});
Test("Codex delayed midnight snapshots keep the request's actual date", () =>
{
    var justBeforeMidnight = DateTime.Today.AddMilliseconds(-100).ToString("O");
    var justAfterMidnight = DateTime.Today.AddMilliseconds(100).ToString("O");
    var usage = Read(CodexSession(), CodexRequest(justBeforeMidnight, "old-request", 100, 10, 100, 10), CodexSnapshot(justAfterMidnight, 100, 10, 100, 10),
        CodexRequest(justAfterMidnight, "new-request", 200, 20, 300, 30), CodexSnapshot(justAfterMidnight, 200, 20, 300, 30));
    Equal(220L, usage.TotalTokens); Equal(1, usage.RecordCount);
});
Test("Codex reverse-order and repeated export records deduplicate", () =>
{
    var usage = Read(CodexSession(), CodexSnapshot(today, 100, 10, 100, 10), CodexRequest(today, "request-one", 100, 10, 100, 10), CodexRequest(today, "request-one", 100, 10, 100, 10));
    Equal(110L, usage.TotalTokens); Equal(1, usage.RecordCount);
});
Test("Codex equal-size separate requests remain separate", () =>
{
    var usage = Read(CodexSession(), CodexRequest(today, "request-one", 100, 10, 100, 10), CodexSnapshot(today, 100, 10, 100, 10),
        CodexRequest(today, "request-two", 100, 10, 200, 20), CodexSnapshot(today, 100, 10, 200, 20));
    Equal(220L, usage.TotalTokens); Equal(2, usage.RecordCount);
});
Test("Codex first cumulative-only event establishes a baseline", () =>
{
    var usage = Read(At("""{"timestamp":"@TIME@","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"input_tokens":1000000,"output_tokens":200000,"total_tokens":1200000}}}}"""),
        At("""{"timestamp":"@TIME@","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"input_tokens":1000100,"output_tokens":200010,"total_tokens":1200110}}}}"""));
    Equal(110L, usage.TotalTokens); Equal(1, usage.RecordCount);
});
Test("Codex cumulative-only resets are fresh baselines", () =>
{
    string Event(long input) => JsonSerializer.Serialize(new { timestamp = today, type = "event_msg", payload = new { type = "token_count", info = new { total_token_usage = CodexTokens(input, 0) } } });
    var usage = Read(Event(1000000), Event(100), Event(110));
    Equal(10L, usage.TotalTokens); Equal(1, usage.RecordCount);
});
Test("Codex history/tool envelopes cannot introduce embedded model counters", () =>
{
    var usage = Read(CodexSession(), At("""{"timestamp":"@TIME@","type":"world_state","payload":{"state":{"model":"yesterday-model","usage":{"input_tokens":999999}}}}"""),
        At("""{"timestamp":"@TIME@","type":"response_item","payload":{"type":"function_call_output","output":{"model":"yesterday-model","usage":{"input_tokens":999999}}}}"""),
        At("""{"timestamp":"@TIME@","type":"future-history-envelope","payload":{"model":"yesterday-model","usage":{"input_tokens":999999}}}"""),
        CodexRequest(today, "actual", 100, 10, 100, 10));
    Equal(110L, usage.TotalTokens); Equal(1, usage.RecordCount);
});
Test("SDK camelCase model summaries supported", () =>
{
    var usage = Read(At("""{"timestamp":"@TIME@","session_id":"sdk","modelUsage":{"claude-opus-5":{"inputTokens":10,"outputTokens":20,"cacheReadInputTokens":30,"cacheCreationInputTokens":40,"costUSD":0.25}}}"""));
    Equal(100L, usage.TotalTokens); Equal(0.25M, usage.EstimatedCost);
});
Test("SDK independent result UUIDs in one session are separate queries", () =>
{
    var usage = Read(
        At("""{"timestamp":"@TIME@","type":"result","uuid":"result-one","session_id":"sdk","modelUsage":{"claude-haiku-4-5-20251001":{"inputTokens":10915,"outputTokens":572}}}"""),
        At("""{"timestamp":"@TIME@","type":"result","uuid":"result-two","session_id":"sdk","modelUsage":{"claude-haiku-4-5-20251001":{"inputTokens":15626,"outputTokens":489}}}"""),
        At("""{"timestamp":"@TIME@","type":"result","uuid":"result-three","session_id":"sdk","modelUsage":{"claude-haiku-4-5-20251001":{"inputTokens":34348,"outputTokens":1132}}}"""),
        At("""{"timestamp":"@TIME@","type":"result","uuid":"result-three","session_id":"sdk","modelUsage":{"claude-haiku-4-5-20251001":{"inputTokens":34348,"outputTokens":1132}}}"""));
    Equal(63082L, usage.TotalTokens); Equal(3, usage.RecordCount);
});
Test("SDK result dates filter independently within one reused session", () =>
{
    var usage = Read(
        At("""{"timestamp":"@TIME@","type":"result","uuid":"yesterday-result","session_id":"sdk","modelUsage":{"old-model":{"inputTokens":999999,"outputTokens":999}}}""", yesterday),
        At("""{"timestamp":"@TIME@","type":"result","uuid":"today-result","session_id":"sdk","modelUsage":{"today-model":{"inputTokens":100,"outputTokens":10}}}"""));
    Equal(110L, usage.TotalTokens); Equal(1, usage.RecordCount); Equal("today-model", usage.Providers.Single().Records.Single().Model);
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
Test("One reader follows repeated appends and completes a partially written line", () =>
{
    var folder = Path.Combine(work, "live-appends"); Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, "live.jsonl");
    var settings = new PetStatsSettings { PricingCatalogUrl = "", AutoDiscoverLogRoots = false,
        LogRoots = [new() { Name = "fixture", Path = folder }] };
    var reader = new TokenLogReader(settings, Path.Combine(folder, "cache"));
    File.WriteAllText(path, CodexSession() + "\n");
    for (var i = 1; i <= 8; i++)
    {
        var request = CodexRequest(today, "live-" + i, 10, 5, i * 10, i * 5);
        File.AppendAllText(path, request[..^2]);
        Equal((i - 1) * 15L, reader.GetUsage(date).TotalTokens);
        File.AppendAllText(path, request[^2..] + "\n");
        var usage = reader.GetUsage(date);
        Equal(i * 15L, usage.TotalTokens); Equal(i, usage.RecordCount);
        Equal(i * 15L, reader.GetUsage(date).TotalTokens);
    }
});
Test("Growing line log remains counted beyond MaxLogFileMb", () =>
{
    var folder = Path.Combine(work, "large-log"); Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, "live.jsonl");
    var settings = new PetStatsSettings { MaxLogFileMb = 1, PricingCatalogUrl = "", AutoDiscoverLogRoots = false,
        LogRoots = [new() { Name = "fixture", Path = folder }] };
    var reader = new TokenLogReader(settings, Path.Combine(folder, "cache"));
    File.WriteAllText(path, CodexSession() + "\n" + CodexRequest(today, "before-limit", 10, 5, 10, 5) + "\n");
    Equal(15L, reader.GetUsage(date).TotalTokens);
    using (var writer = File.AppendText(path))
        for (var i = 0; i < 1200; i++) writer.WriteLine("{\"content\":\"" + new string('x', 1024) + "\"}");
    File.AppendAllText(path, CodexRequest(today, "after-limit", 20, 5, 30, 10) + "\n");
    Equal(40L, reader.GetUsage(date).TotalTokens); Equal(2, reader.GetUsage(date).RecordCount);
});
Test("Cached files cannot contaminate each other after a duplicate is removed", () =>
{
    var folder = Path.Combine(work, "duplicate-cache"); Directory.CreateDirectory(folder);
    var a = Path.Combine(folder, "a.jsonl"); var b = Path.Combine(folder, "b.jsonl");
    File.WriteAllText(a, At("""{"timestamp":"@TIME@","id":"shared","model":"gpt-4o","usage":{"input_tokens":10,"output_tokens":1}}"""));
    File.WriteAllText(b, At("""{"timestamp":"@TIME@","id":"shared","model":"gpt-4o","usage":{"input_tokens":10,"output_tokens":90}}"""));
    var settings = new PetStatsSettings { PricingCatalogUrl = "", AutoDiscoverLogRoots = false,
        LogRoots = [new() { Name = "fixture", Path = folder }] };
    var reader = new TokenLogReader(settings, Path.Combine(folder, "cache"));
    Equal(100L, reader.GetUsage(date).TotalTokens);
    File.Delete(b);
    Equal(11L, reader.GetUsage(date).TotalTokens);
    // Removing a log root must also remove its cached counters.
    settings.LogRoots[0].Enabled = false;
    Equal(0L, reader.GetUsage(date).TotalTokens);
});
Test("JSON rewrite retries automatically and catches equal-length updates", () =>
{
    var folder = Path.Combine(work, "rewrite"); Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, "live.json");
    var first = At("""{"timestamp":"@TIME@","id":"a","model":"gpt-4o","usage":{"input_tokens":10,"output_tokens":10}}""");
    File.WriteAllText(path, first);
    var settings = new PetStatsSettings { PricingCatalogUrl = "", AutoDiscoverLogRoots = false,
        LogRoots = [new() { Name = "fixture", Path = folder, ScanJsonFiles = true }] };
    var reader = new TokenLogReader(settings, Path.Combine(folder, "cache"));
    Equal(20L, reader.GetUsage(date).TotalTokens);
    var originalTime = File.GetLastWriteTimeUtc(path);
    File.WriteAllText(path, first.Replace(":10", ":20"));
    File.SetLastWriteTimeUtc(path, originalTime);
    Equal(40L, reader.GetUsage(date).TotalTokens);
    File.WriteAllText(path, "{\"usage\":");
    Equal(40L, reader.GetUsage(date).TotalTokens);
    File.WriteAllText(path, first);
    Equal(20L, reader.GetUsage(date).TotalTokens);
});
Test("Cached reader handles truncation, replacement, new files and midnight", () =>
{
    var folder = Path.Combine(work, "rotation"); Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, "live.jsonl");
    File.WriteAllText(path, CodexSession() + "\n" + CodexRequest(yesterday, "old", 10, 5, 10, 5) + "\n"
        + CodexRequest(today, "today", 20, 5, 30, 10) + "\n");
    var settings = new PetStatsSettings { PricingCatalogUrl = "", AutoDiscoverLogRoots = false,
        LogRoots = [new() { Name = "fixture", Path = folder }] };
    var reader = new TokenLogReader(settings, Path.Combine(folder, "cache"));
    Equal(15L, reader.GetUsage(date.AddDays(-1)).TotalTokens);
    Equal(25L, reader.GetUsage(date).TotalTokens);
    File.WriteAllText(path, CodexRequest(today, "truncated", 5, 1, 5, 1));
    Equal(6L, reader.GetUsage(date).TotalTokens);
    File.WriteAllText(Path.Combine(folder, "new.jsonl"), CodexRequest(today, "new", 5, 2, 5, 2));
    Equal(13L, reader.GetUsage(date).TotalTokens);
    File.Delete(path);
    File.WriteAllText(path, CodexRequest(today, "replacement", 50, 1, 50, 1));
    Equal(58L, reader.GetUsage(date).TotalTokens);
});
Test("Cancelled refresh can be followed by a successful refresh on the same reader", () =>
{
    var folder = Path.Combine(work, "cancel"); Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "live.jsonl"), CodexRequest(today, "one", 10, 5, 10, 5));
    var settings = new PetStatsSettings { PricingCatalogUrl = "", AutoDiscoverLogRoots = false,
        LogRoots = [new() { Name = "fixture", Path = folder }] };
    var reader = new TokenLogReader(settings, Path.Combine(folder, "cache"));
    using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    var cancelled = false;
    try { reader.GetUsage(date, cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
    Equal(true, cancelled); Equal(15L, reader.GetUsage(date).TotalTokens);
});
Test("Live file snapshots stop at opening EOF and observe cancellation", () =>
{
    using var source = new MemoryStream(); source.Write(new byte[] { 1, 2, 3, 4 }); source.Position = 0;
    using var cancellation = new CancellationTokenSource();
    var type = typeof(TokenLogReader).Assembly.GetType("PetStatsOverlay.UsageSnapshotStream")!;
    using var snapshot = (Stream)Activator.CreateInstance(type, source, cancellation.Token)!;
    source.Position = 4; source.Write(new byte[] { 5, 6, 7, 8 }); source.Position = 0;
    var buffer = new byte[16];
    Equal(4, snapshot.Read(buffer)); Equal(0, snapshot.Read(buffer));
    cancellation.Cancel();
    var cancelled = false;
    try { _ = snapshot.Read(buffer); } catch (OperationCanceledException) { cancelled = true; }
    Equal(true, cancelled);
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
