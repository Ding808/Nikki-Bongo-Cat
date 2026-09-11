# Nikki Bongo Cat v1.1.0

## English

Download **Nikki-Bongo-Cat-win-x64.zip**, extract the whole folder, and run **StartPetWithStats.cmd**. No .NET installation or compilation is needed. The automatically generated source archives are for developers. The companion `.zip.sha256` asset lets you verify the download checksum.

This release fixes missing Claude Desktop usage, including Opus 5 metadata from locally cached conversations, and broadens model recognition and pricing. Kimi, GLM, DeepSeek, Gemini, Grok, Qwen, MiniMax, Mistral, Claude, OpenAI, and many providers/gateways are covered by the supported log formats and price catalogue. The bundled offline snapshot contains 2,959 chat/completion/responses entries across 90 provider identifiers, including aliases and deployments.

- English is now the default. Switch to Chinese in the panel header or **Settings → Language**; menus and customization screens switch immediately and remember your choice.
- Resize the statistics panel independently under **Settings → Panel size**, from 65% to 175% with presets or a custom percentage.
- Scroll through all observed models. Unknown-price models remain in token totals and usage shares; incomplete cost totals are visibly marked.
- Drag with the left button or resize with the right button on the visible pet body. Transparent surroundings pass clicks through to your desktop applications.
- The startup “Interactive” label has been removed.
- Includes English/Chinese illustrated guides, regression checks, and a portable package without local logs, settings, backups, or build intermediates.

**Cost figures are API-equivalent USD estimates, not Claude Max or other subscription invoices.** Only usage metadata present in readable local logs/caches can be counted. Claude Desktop can evict or omit cached conversations; open a missing conversation locally and allow the next refresh. Your prompts and usage records are not uploaded.

For an upgrade, extract into a new folder and copy your settings and custom models/configuration if needed. Daily input history stays in your Windows profile. Read **README.md** or **README.zh-CN.md** in the package for controls, Steam setup, custom log folders, prices, and troubleshooting.

## 简体中文

请下载 **Nikki-Bongo-Cat-win-x64.zip**，完整解压后双击 **StartPetWithStats.cmd**。无需安装 .NET，也无需编译；GitHub 自动生成的源码压缩包面向开发者。另附 `.zip.sha256` 文件，可用于核对下载校验值。

本次修复 Claude 桌面端本地缓存用量缺失的问题，包括 Opus 5 模型信息，并扩展模型识别和价格统计。支持的日志格式和价格目录覆盖 Kimi、GLM、DeepSeek、Gemini、Grok、Qwen、MiniMax、Mistral、Claude、OpenAI 及多种厂商／网关。离线快照内置来自 90 个服务商标识的 2,959 条聊天／补全／Responses 记录，包括别名和部署。

- 首次启动默认英文。可在面板右上角或“高级详情 → 语言”切换中文，菜单与自定义界面即时切换并保存选择。
- “高级详情 → 面板大小”可以独立调整统计面板，支持 65%–175% 预设及自定义百分比。
- 模型列表可以滚动查看全部已记录模型。缺价模型仍计入令牌总量和占比，花费不完整时明确提示。
- 仅从可见桌宠本体开始的左键拖动和右键缩放会操作桌宠，周围透明区域可以正常操作其他应用。
- 移除启动时的“可交互”字样。
- 补齐带图中英文文档、回归检查和完整便携成品包，排除本地日志、个人设置、备份及构建中间文件。

**花费是按 API 单价折算的美元估算值，不是 Claude Max 等订阅账单。** 只能统计本机日志／缓存实际包含的可读取用量。Claude 桌面端可能清理或未缓存部分会话；可以在本机打开缺失会话后等待下一次刷新。提示词和用量记录不会被上传。

升级时建议解压到新文件夹，再复制需要保留的设置、自定义模型和配置；每日输入历史保留在 Windows 用户目录。操作方法、Steam 配置、日志目录、自定义价格和排错说明请阅读包内 **README.zh-CN.md** 或 **README.md**。
