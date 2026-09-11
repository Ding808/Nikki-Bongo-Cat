# Nikki Bongo Cat

[English](README.md) · [简体中文](README.zh-CN.md)

A Windows Live2D desktop companion that reacts to your keyboard and mouse, with a small dashboard for today's AI usage, estimated cost, and input activity. Choose a pink or purple Nikki skin, manage your own models, and keep the statistics panel as small or large as you like.

**[Download the ready-to-use Windows x64 package](https://github.com/Ding808/Nikki-Bongo-Cat/releases/latest)** · [What's new](CHANGELOG.md)

Download **`Nikki-Bongo-Cat-win-x64.zip`**, extract the entire folder, and double-click **`StartPetWithStats.cmd`**. The release includes its runtime; you do not need .NET, Visual Studio, or a build step. GitHub's **Source code** downloads are for developers and do not include the built companion application.

A matching `Nikki-Bongo-Cat-win-x64.zip.sha256` file is attached to each release. To verify a download, compare its value with `Get-FileHash .\Nikki-Bongo-Cat-win-x64.zip -Algorithm SHA256` in PowerShell.

## Preview

![Pink Nikki desktop pet](docs/images/pet-preview.png)

The actual Live2D pet rendered on a transparent background.

![English statistics panel](docs/images/dashboard-en.png)

The real English interface with sample statistics. Figures are illustrative, not a user's account or invoice.

![Chinese statistics panel](docs/images/dashboard-zh.png)

The same interface in Chinese. Language changes apply immediately to the dashboard, menus, tooltips, and customization screens.

## Features

- Animated Live2D pet, keyboard and mouse reactions, and persistent pink/purple skin selection.
- Today's observed AI requests, tokens, estimated cost in USD, keystrokes, and mouse clicks.
- Model usage shares based on tokens, with a scrollable list that keeps every observed model visible.
- Claude Desktop conversation-cache reading, including Microsoft Store and standalone installations; Codex, Claude Code, and compatible JSON/JSONL logs.
- A bundled model price catalogue with online updates and custom price overrides. Models with no known price still count toward tokens and model share.
- English by default, with Chinese available directly inside the panel.
- Independent statistics-panel sizing from 65% to 175%, plus a custom percentage.
- Drag with the left button or resize with the right button **on the visible pet**. Transparent space around it remains available to other applications.
- Lock mode that passes clicks through the entire pet; Steam launch integration is also available.

## Requirements

- Windows 10/11, 64-bit, with a graphics driver that supports the bundled Live2D/OpenGL renderer.
- Steam is optional. Steam integration requires the free [Bongo Cat](https://store.steampowered.com/app/3419430/Bongo_Cat/) in your library.
- A .NET 9 SDK is required only when building from source.

Keep the release's EXE files, DLLs, `img`, and `Resources` together. Extract to a folder your account can write to, since pet settings and model customization are saved alongside the application.

## Everyday controls

| Action | How |
| --- | --- |
| Open the dashboard | Double-click the small companion button; or choose **Open statistics** in the tray menu. |
| Change language | Click **EN** in the panel header (**中** when using Chinese), then choose a language. Also available in **Settings → Language**. |
| Change dashboard size | **Settings → Panel size**: choose a preset or **Custom size…** (65–175%). |
| Move the pet | Hold the left mouse button on a visible part of the unlocked pet and drag. |
| Resize the pet | Hold the right mouse button on a visible part of the unlocked pet and drag. |
| Lock / unlock | Numpad **−** / **+**, or the panel/tray lock controls. |
| Keep the small button visible | **Settings → Always show button**. Otherwise it appears near the pointer/pet. |
| Change the small button's metric | **Settings → Button display**. |
| Change skin | **Settings → Switch skin**, or the tray menu. |
| Manage models and hotkeys | **Settings → Customize pet**; **Ctrl+Shift+S** also opens the bilingual editor while the pet has focus. |
| Apply edited model settings | Choose **Save and restart** in the editor. |
| Reset input counters | **Settings → Reset today's input counts**. AI totals are reconstructed from logs and are not erased by this command. |

Language and panel size are remembered. Large panels are fitted to the monitor's usable area. The desktop pet and its dashboard have separate size controls. The old “Interactive” startup label has been removed.

## AI usage and pricing

### Where the numbers come from

The application reads usage metadata already present on this computer. It does not need your API keys or a connection to your AI account.

| Source | What can be read |
| --- | --- |
| Claude Desktop | Cached conversation usage/model metadata from the local `claude.ai` IndexedDB database, for standalone and Microsoft Store installations. |
| Claude Code | Session/project usage records, normally under `%USERPROFILE%\.claude\projects`. |
| Codex | Session usage logs, normally under `%USERPROFILE%\.codex\sessions`, plus compatible desktop-app logs. |
| Gemini CLI | Saved JSON session records under `%USERPROFILE%\.gemini\tmp`, including cached input and thoughts tokens. |
| Other clients and gateways | Recognized JSON/JSONL/NDJSON usage records in discovered or user-added log folders. Common OpenAI-, Anthropic-, and Gemini-style usage fields are supported. |

Known folders for Claude, Codex, Gemini CLI, Kimi CLI, Qwen Code, OpenCode JSON storage, Cursor, Windsurf, Continue, and Cline/Roo/Kilo task storage are discovered automatically where present. Folder discovery is not a guarantee that every version of that client writes readable token records. A browser conversation, remote account, or client that does not expose usage locally cannot be reconstructed from nothing.

Claude Desktop may evict its cache or omit conversations that have not been opened locally. Only available usage metadata can be included; encrypted or absent records are not estimated by counting prompt text. A new app version may also change its storage format. Records without a usable timestamp cannot be assigned to today and are excluded. Statistics update at startup and approximately once per minute, using the computer's local calendar day.

**Requests** means observed usage records after deduplication, not necessarily the number of chat threads. **Model usage** is the model's share of observed tokens, not its share of money or subscription usage limits. Whole-number percentages may not sum to exactly 100%.

### Model families and prices

The price catalogue covers major providers and gateways, including OpenAI, Anthropic Claude, Google Gemini/Vertex, xAI Grok, Moonshot/Kimi, Z.ai/GLM, DeepSeek, Alibaba Qwen, Mistral, Cohere, Meta Llama through hosting providers, MiniMax, Perplexity, OpenRouter, Groq, Together, Fireworks, Azure, and Amazon Bedrock. Coverage depends on the exact model identifier, route, and pricing fields exposed by the catalogue.

The release carries an offline snapshot dated **2026-09-11**, with **2,959 chat/completion/responses catalogue entries across 90 provider identifiers** (including aliases and deployments, not 2,959 distinct base models), and normally refreshes the public [LiteLLM price catalogue](https://github.com/BerriAI/litellm/blob/main/model_prices_and_context_window.json) every 24 hours. Failed downloads leave the bundled/cached data available. Common aliases, provider prefixes, dated model identifiers, and effort labels are normalized where a supported match exists. See [pricing coverage and provenance](docs/model-pricing.md).

**Cost is an API-equivalent estimate in USD, not a Claude Max, ChatGPT, or other subscription invoice.** Explicit usable costs from a log are preferred; otherwise the app applies supported input, output, and cache rates. Provider billing rules, discounts, taxes, non-token charges, and unexposed usage can differ from these estimates.

An unknown price never removes the model's token usage. The panel marks incomplete pricing with an unpriced-record count; a trailing **+** means the displayed amount is a known subtotal, and **—** means no priced total is available. Add an override for a private gateway or a model whose official identifier/rate is missing.

### Add log folders or custom rates

Open **Settings → Open settings file**, close the companion before editing, and merge the relevant fields into `pet-stats-settings.json`. Restart after saving. Paths support `%USERPROFILE%`, `%APPDATA%`, `%LOCALAPPDATA%`, and `~`.

```json
{
  "ExtraLogRoots": ["D:\\AI-logs"],
  "ScanJsonFiles": true,
  "CompanionUi": {
    "Language": "en",
    "PanelScale": 1.25
  },
  "Prices": [
    {
      "Pattern": "my-private-model",
      "Provider": "my-gateway",
      "InputPerMillion": 1.0,
      "OutputPerMillion": 4.0,
      "CacheReadPerMillion": 0.1,
      "CacheWrite5mPerMillion": 1.25,
      "CacheWrite1hPerMillion": 2.0
    }
  ]
}
```

A `Pattern` can be an exact identifier or a wildcard such as `private-*`; `Provider` is optional. The example prices are **fictional**, for demonstrating settings only. Replace them with your provider's rates per one million tokens. Enable `ScanJsonFiles` when the added source uses `.json` rather than `.jsonl`; add the smallest relevant log folder instead of an entire drive. For per-folder control, use a `LogRoots` entry with `Name`, `Path`, `ProviderHint`, `ScanJsonFiles`, and `Enabled`.

## Privacy, saved data, and updates

| Location | Purpose |
| --- | --- |
| `%LOCALAPPDATA%\PetStatsOverlay\state.json` | Local daily keyboard/mouse counters. |
| `PetStatsOverlay\pet-stats-settings.json` | Language, panel size, log locations, and price overrides. |
| `%LOCALAPPDATA%\PetStatsOverlay` | Companion data and cached pricing. Open it from **Settings → Open data folder**. |
| `config.json` | Renderer settings and selected skin/model. |
| `img\standard\live2d_models` | Model library and per-model profiles/assets. |
| `.petstats_backups` | Local backups made by supported customization operations. |

AI logs and the Claude conversation cache are read locally. The companion does not upload prompts, conversation contents, or usage records; its automatic pricing request downloads public pricing data. Input statistics count key and mouse activity rather than saving typed text.

To update, exit the companion and pet, extract the new release into a new folder, and copy across your `pet-stats-settings.json` and any custom models/configuration you wish to keep. Daily input history remains in your Windows profile. Release packages exclude the author's local settings, logs, caches, backups, build intermediates, and test output.

## Live2D customization

Open **Customize pet** to switch built-in skins, bind animation/expression keys, replace supported images, import a Live2D model folder or ZIP package, and export a model package.

A model folder must contain exactly one `*.model3.json` at its root, along with its referenced Moc and texture resources. The selected library model is synchronized to `img\standard\cat_model` for the renderer. `petstats-live2d-profile.json` keeps its hotkeys and model settings; `petstats-assets` carries its companion images and sounds. Use **Save and restart** or **Apply selected model and restart** to load the changes.

## Steam playtime mode

1. Install Bongo Cat from Steam.
2. Run **`CopySteamLaunchOption.cmd`** from the extracted Nikki Bongo Cat folder.
3. In Steam, right-click **Bongo Cat → Properties → General → Launch Options**. Paste the copied command, replacing old launch options.
4. Start Bongo Cat from Steam, or use **`StartPetWithStats.Steam.cmd`**.

The generated launch option uses your actual extraction path:

```text
"<your extracted folder>\PetStatsOverlay\PetStatsOverlay.exe" --steam-launcher %command%
```

Keep the quotes and `%command%`. Configure it again if you move the folder. In this mode Steam launches the companion and one bundled Nikki pet; the companion's process lifetime is used for playtime. Close the pet/companion when finished.

### Launch arguments

| Argument | Purpose |
| --- | --- |
| `--launch-pet` | Start the bundled pet and companion dashboard. |
| `--steam-launcher` | Entry point used by the Steam launch option. |
| `--steam` | Ask Steam to launch App 3419430, then exit. |
| `--help` | Show launch help. |

The older `--attach-steam-bongo-cat` argument remains an alias of `--steam-launcher`.

## Troubleshooting

| Problem | Check |
| --- | --- |
| “Cannot find PetStatsOverlay.exe” | Download the ready-to-use ZIP from Releases and extract all files. The source archive does not contain that executable. |
| A model has tokens but no cost | Check the unpriced-record notice, the exact logged model identifier, catalogue connectivity, and custom `Prices` overrides. |
| Claude Desktop usage is missing | Open the relevant conversation in Claude Desktop, let its local cache update, and wait for the next statistics refresh. The app cannot recover uncached usage. |
| Another client's usage is missing | Confirm it writes usage counts and timestamps to compatible local logs, then add the relevant folder. Selecting a provider does not enable account-level billing access. |
| Changes to settings are not applied | Close the companion, check that the settings file is valid JSON, save, and restart. |
| Pet is locked or cannot be dragged | Press numpad **+** or choose **Unlock pet**, then drag a visible part of the pet. Use the supplied launcher so pet and companion run at the same privilege level. |
| The dashboard is too large | Choose a smaller **Panel size**. Scroll the model list to see additional models. |
| Steam launches the wrong program | Re-run `CopySteamLaunchOption.cmd` and replace the complete Steam launch option. |

## Build and test from source

Install the .NET 9 SDK, clone the repository, and run from its root:

```powershell
dotnet restore .\PetStatsOverlay\PetStatsOverlay.csproj
dotnet build .\PetStatsOverlay\PetStatsOverlay.csproj -c Release
.\BuildRelease.ps1
```

The last command builds the self-contained application and creates **`artifacts\Nikki-Bongo-Cat-win-x64.zip`** with the renderer, assets, documentation, and launchers. GitHub Actions performs the same packaging for version tags such as `v1.1.0` and attaches the ZIP and its SHA-256 checksum to the release. The verification workflow also runs the automated checks on pushes and pull requests.

Run all regression checks, including language/layout smoke checks:

```powershell
.\Test.ps1 -IncludeUi
```

Or run a focused check:

```powershell
dotnet run --project .\Tests\UsageTests\UsageTests.csproj
dotnet run --project .\Tests\DesktopTests\DesktopTests.csproj
dotnet run --project .\Tests\PetHitTesting\PetHitTesting.csproj
dotnet run --project .\Tests\UiSmoke\UiSmoke.csproj
```

The hit-testing harness also accepts `-- --native <isolated-BongoCatMver-process-id>` to verify actual window-surface alpha and Windows click routing against a separately launched test pet. Run that integration mode only with a disposable test instance: it temporarily changes the pet's window styles and creates a small overlapping test window.

```text
PetStatsOverlay/      Companion UI, log readers, pricing, and pet controller
Tests/                Usage, desktop-cache, hit-testing, and UI checks
img/                  Images, sounds, and Live2D models
Resources/            Bundled renderer resources
docs/                 Preview images and release notes
BuildRelease.ps1      Portable release packaging
```

The pet renderer builds on [MMmmmoko/Bongo-Cat-Mver](https://github.com/MMmmmoko/Bongo-Cat-Mver). Public pricing data is supplied by [LiteLLM](https://github.com/BerriAI/litellm). The model artwork and runtime components retain their respective attribution and licensing.
