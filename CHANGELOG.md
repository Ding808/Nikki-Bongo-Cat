# Changelog

## v1.1.4 — 2026-09-14

### English

- Refresh tokens, estimated cost, and model shares every five seconds, including while the dashboard is hidden or collapsed; show the completed refresh time with seconds.
- Keep counting long JSONL/NDJSON conversations after their files exceed the whole-document size limit. Reuse unchanged line-log results and model price matches to reduce repeated scanning.
- Read a finite snapshot of actively growing logs, cancel overlong scans cooperatively, and retry partial JSON writes without discarding the last readable same-day result.
- Prevent generated diagnostics from being counted as new usage when a custom log root includes the companion's data folder.
- Add regression coverage for repeated live appends, partial writes, size-limit crossings, rotation, midnight, cancellation, and automatic hidden-panel refreshes.

### 简体中文

- 每五秒更新 Token、预估费用和模型占比，面板隐藏或收起时也继续；更新时间精确到秒。
- JSONL／NDJSON 长会话超过整份文档的大小限制后仍能统计；复用未变化的逐行日志结果和模型价格匹配，减少重复扫描。
- 为持续写入的日志读取有限快照；扫描耗时过长时协作式取消并重试；整份 JSON 写到一半时保留最近一次成功读取的当日结果。
- 自定义日志目录包含统计程序的数据目录时，避免把程序自己的诊断汇总再次计入用量。
- 增加连续追加、半写入、超过大小限制、日志轮换、跨日、取消读取，以及隐藏面板自动刷新的回归测试。

## v1.1.3 — 2026-09-11

### English

- Kept the pet above ordinary application and browser windows in both locked and unlocked modes. Dragging and resizing no longer remove its always-on-top state.
- Preserved focus in the application you switch to, transparent-area click-through, and the original window state when the companion exits.
- Added Windows regression checks for startup, lock/unlock, interaction, and switching foreground windows.

### 简体中文

- 修复解锁时被其他软件或浏览器遮住的问题；锁定、解锁、拖动和缩放时均保持桌宠置顶。
- 切换应用时保留该应用的输入焦点，维持透明区域穿透；退出统计程序时恢复桌宠原先的窗口状态。
- 增加启动、锁定／解锁、桌宠交互和切换前台窗口的 Windows 回归检查。

## v1.1.2 — 2026-09-11

### English

- Restored right-button pet resizing after idle click-through: presses on the visible, unlocked pet are now routed to the native renderer so it receives the input and focus needed for dragging and resizing.
- Kept transparent-area click-through, lock controls, and mouse-movement processing independent of graphics capture and window-control work.
- Added native input regression coverage that holds the mouse button, moves the pointer, and checks the renderer's actual position and size.
- Clarified resizing in both READMEs: drag right/down to enlarge, left/up to shrink, and release to finish.

### 简体中文

- 恢复右键缩放：空闲穿透状态下，点击已解锁的可见桌宠本体时，将按键正确交给原生渲染器，使其获得输入和焦点，正常执行左键拖动与右键缩放。
- 保留透明空白区域穿透和锁定功能；鼠标移动仍不等待图像采样或窗口控制操作。
- 增加真实鼠标按住、移动后的原生回归检查，核对桌宠实际位置和大小变化。
- 中英文 README 补充缩放方向：向右／向下放大，向左／向上缩小，松开结束。

## v1.1.1 — 2026-09-11

### English

- Fixed system-wide pointer slowdown by moving the mouse hook off the dashboard thread and sampling the pet's alpha mask in the background. Mouse movement no longer reads graphics pixels or changes window styles.
- Fixed inflated Codex usage and cost when modern request records and legacy token events coexist. Session and turn totals are no longer mistaken for individual requests, and matching records are counted once.
- Excluded undated compaction/history copies from daily usage, and kept distinct Claude Agent SDK result UUIDs as distinct queries when counting model summaries.
- Kept daily totals tied to request timestamps in the computer's local time. Yesterday's dashboard data is cleared at midnight, including when a scan finishes after the date changes.
- Skipped Claude Desktop query summaries that clearly cross local midnight and cannot be split reliably by day. Individually timestamped messages remain counted; unsplittable summary usage is omitted.
- Preserved pending input counts during midnight rollover and reset. The update time now reflects a completed usage scan, rather than a keystroke or display change.
- Small nonzero model shares show `<1%` instead of zero, with token counts and more precise percentages on hover.
- Rewrote both READMEs with a six-step Steam quick start, the exact finished ZIP to download, automatic launch-option copying, and troubleshooting for source downloads and moved folders.

### 简体中文

- 修复启动后系统鼠标变慢的问题：鼠标钩子与统计界面分离，后台采样桌宠透明度；鼠标移动不再同步读取图像或切换窗口样式。
- 修复 Codex 新旧用量记录并存时令牌与估算花费异常放大：不再把会话／回合累计量当作单次请求，并对相同调用去重。
- 排除没有原始请求时间的压缩／历史副本；Claude Agent SDK 的不同结果 UUID 按独立查询统计模型摘要。
- 按电脑本地时间和请求发生时间统计今日用量；跨午夜清除昨日面板数据，过期扫描结果不会重新显示为今日数据。
- 跳过明确跨本地午夜、无法可靠按日拆分的 Claude 桌面端查询总摘要；仍统计有时间戳的逐条消息，不把无法拆分的摘要全量计入结束日。
- 跨日和重置时保留尚未保存的昨日输入次数；“更新于”仅表示用量扫描完成时间，不再随打字或界面变化刷新。
- 较小的非零模型占比显示为 `<1%`；悬停可查看令牌数和更精确的比例。
- 重写中英文 README 的六步 Steam 快速入门，明确成品 ZIP 下载、自动复制启动参数，并补充误下源码和移动目录的处理方法。

## v1.1.0 — 2026-09-11

### English

- Added Claude Desktop usage reading from standalone and Microsoft Store conversation caches, including Opus 5 model metadata. Repeated cached snapshots and overlapping log roots are deduplicated.
- Expanded generic usage parsing across OpenAI-, Anthropic-, and Gemini-style logs and current provider/model aliases. Known families include Kimi, GLM, DeepSeek, Gemini, Grok, Qwen, MiniMax, Mistral, and many gateway-hosted models.
- Bundled an offline pricing snapshot with 2,959 chat/completion/responses entries across 90 provider identifiers, with automatic online refresh and configurable overrides.
- Preserved tokens and model shares for unknown-price models, and added a visible notice when cost totals are incomplete. Explicit logged USD costs take priority over estimates.
- Added persistent English/Chinese language selection throughout the companion dashboard, menus, tooltips, dialogs, customization editor, and launcher messages. English is the default.
- Added independent 65–175% statistics-panel sizing, presets, a custom percentage, and scrolling for long model lists.
- Removed the startup “Interactive” text.
- Fixed the transparent rectangular pet area intercepting input: drag and right-button resizing must begin on a visible body pixel; lock mode passes input through the pet.
- Launch the bundled renderer at the companion's privilege level so Windows permits click-through and lock controls, without changing the renderer binary.
- Added usage/cache, language/layout, and real Windows hit-testing regression checks, plus illustrated English and Chinese documentation and clean release packaging.

Costs remain API-equivalent estimates in USD, not subscription invoices. Logs or cached conversations that a client does not expose locally cannot be counted.

### 简体中文

- 新增 Claude 桌面端用量读取，覆盖独立安装版与微软商店版的会话缓存，并识别 Opus 5 模型信息；对重复缓存快照和重叠日志目录去重。
- 扩展 OpenAI、Anthropic、Gemini 常见日志格式，以及当前模型／服务商别名处理，覆盖 Kimi、GLM、DeepSeek、Gemini、Grok、Qwen、MiniMax、Mistral 和多种网关托管模型。
- 内置来自 90 个服务商标识的 2,959 条聊天／补全／Responses 价格记录，支持在线更新和自定义单价。
- 缺少价格时仍保留令牌总量和模型占比，并明确提示花费统计不完整；日志中明确记录的美元花费优先于估算。
- 新增可记忆的中英语言切换，覆盖统计面板、菜单、提示、对话框、自定义界面和启动提示；默认英文。
- 统计面板支持独立设置为 65%–175%、预设和自定义百分比；较长的模型列表支持滚动。
- 移除启动时的“可交互”字样。
- 修复桌宠透明矩形区域拦截鼠标的问题：左键拖动、右键缩放只能从可见本体开始；锁定后整个桌宠允许穿透。
- 使用与统计程序相同的权限启动内置渲染器，让 Windows 正常执行穿透和锁定控制，不修改渲染器二进制文件。
- 增加用量／缓存、语言／布局和真实 Windows 命中测试，补齐带预览图的中英文说明及干净的发布打包流程。

花费仍为按 API 单价折算的美元估算值，并非订阅账单；客户端没有在本机公开的日志或会话缓存无法被统计。
