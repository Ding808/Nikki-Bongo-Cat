# Model usage and pricing

The application reads usage that AI clients have written locally. It does not intercept network traffic, read API keys, or send conversation contents to a server. It retains unknown model identifiers in the token totals and model breakdown.

## Bundled catalogue

- Source: [LiteLLM model registry](https://github.com/BerriAI/litellm/blob/main/model_prices_and_context_window.json), retrieved **2026-09-11**.
- Bundled file: `PetStatsOverlay/Data/model-prices.litellm.json`.
- SHA-256: `86ee577ca0e1b0643295c57be6e27e8bc2178f3ee30c7ee27d3aac4b9e85bc08`.
- This snapshot has **2,959 chat/completion/responses price entries across 90 provider IDs**. Entries include aliases, dated versions, regional deployments, and hosted variants; these are not 2,959 distinct base models.
- License: [Berri AI MIT license](../PetStatsOverlay/Data/LiteLLM-LICENSE.txt).
- The saved online catalogue takes precedence over the bundled copy. Updates are checked on a background usage refresh, at the configured interval (24 hours by default). A failed update leaves the bundled/saved catalogue usable. A constructor never waits for the network.

The registry covers OpenAI, Anthropic, Google Gemini, DeepSeek, Moonshot/Kimi, Z.ai/GLM, Qwen, xAI/Grok, MiniMax, Mistral, Cohere, Perplexity, and many hosted services including OpenRouter, Groq, Together, Fireworks, Azure, Vertex AI and Bedrock. A matching priced entry does not imply that the corresponding desktop client exposes readable usage logs.

Claude Opus 5's bundled standard rates were checked against the [official Claude pricing page](https://platform.claude.com/docs/en/about-claude/pricing) on the retrieval date: input $5, output $25, cache read $0.50, five-minute cache write $6.25 and one-hour cache write $10 per million tokens. The display name `Opus 5 Max` uses the Opus 5 model rate: effort and subscription plan names do not create a separate model price. Logged fast-mode and US-inference modifiers are applied where present in the catalogue.

## What the numbers mean

**Estimated USD is API-equivalent token cost, not a subscription invoice.** Max, Pro and other prepaid subscriptions do not incur an additional charge equal to this estimate. Explicit USD costs in a usage record take precedence, including a recorded zero. Other currencies are not silently relabelled as USD. Region, batch, special service tiers, negotiated discounts, tool calls, images, audio, video, storage, taxes and other non-token charges can differ from the displayed token estimate.

The parser keeps uncached input, cached input, cache writes and output separate. One-hour Claude cache writes use their own rate. OpenAI reasoning is a subset of output; Gemini thoughts are included as billable output. Compatible prompt-cache counters are not charged twice. Supported catalogue long-context token tiers are applied using the request's total input context.

A missing model or missing required rate is marked **unpriced**. Its tokens still count toward the total and model share; any known cost components remain included in the partial estimate. Total-only logs without an input/output split cannot be priced using directional rates. No model-family price is guessed for a future or private model ID.

Provider aliases and provider-prefixed IDs are normalized while preserving hosted-service routes. Catalog IDs match exactly; dated version suffixes may use the same model's undated entry. Custom `Prices` entries override catalogue entries and can use an explicit `*` wildcard.

## Readable sources

- Claude Code JSONL and Agent SDK results, including nested assistant messages and camelCase per-model summaries.
- Claude Desktop's local `claude-conversation-store` IndexedDB records, including the Microsoft Store/MSIX package location. Only structured assistant usage metadata is retained. The database is read without modifying it; message content is discarded. Format changes, cloud-only or evicted history may limit recoverable records.
- Codex session token events, including cumulative updates, cache tokens, reasoning breakdowns and session-scoped deduplication.
- OpenAI-compatible JSON/JSONL/NDJSON exports, native Gemini `usageMetadata`, and supported JSON session records from other clients.
- Discovered folders include Gemini CLI, Kimi CLI, Qwen Code, OpenCode JSON storage, Continue, Cursor/Windsurf extension storage, and Cline/Roo/Kilo task folders. Newer clients using other databases still require a compatible export.

Repeated request/message snapshots update a single record. Overlapping roots do not double-scan JSON files. SDK result summaries are suppressed when the same session and model already provide individual messages. The local calendar date of each usage timestamp determines the day. Undated records are omitted because file modification time does not establish when tokens were consumed. Oversized or inaccessible files and partial JSON lines are skipped.

## Custom imports and rates

Add a folder under `ExtraLogRoots`, or an enabled `LogRoots` item. `ExtraLogRoots` accepts JSON, JSONL and NDJSON. A minimal JSONL entry is:

```json
{"timestamp":"2026-09-11T12:00:00-04:00","id":"unique-request-id","provider":"kimi","model":"kimi-k2.5","usage":{"prompt_tokens":1000,"completion_tokens":200}}
```

For a private model or negotiated rate, add a `Prices` item (USD per million tokens):

```json
{
  "Pattern": "private-model-*",
  "Provider": "private-provider",
  "InputPerMillion": 1.0,
  "OutputPerMillion": 4.0,
  "CacheReadPerMillion": 0.1,
  "CacheWrite5mPerMillion": 1.25,
  "CacheWrite1hPerMillion": 2.0
}
```

The `Provider` field can be empty for a universal custom override. Specify zero only when that component is free. `TotalPerMillion` is available for a source that prices an undifferentiated token total. Clear `PricingCatalogUrl` to disable network catalogue updates when constructing settings directly; the normal saved-settings loader supplies its default URL when empty.

## Regression checks

Run `dotnet run --project Tests/UsageTests/UsageTests.csproj`. Tests use synthetic local fixtures and the bundled catalogue. The optional `-- --local` check reads the current user's packaged Claude Desktop metadata and verifies that today's Opus 5 usage is counted and priced; it requires matching local history and is not part of CI. Desktop storage-format fixtures are separately covered by `Tests/DesktopTests`.
