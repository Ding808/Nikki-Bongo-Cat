# Changelog

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
