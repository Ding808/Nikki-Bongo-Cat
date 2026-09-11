# Nikki Bongo Cat v1.1.1

## English

This update fixes pointer slowdown after launch and inflated daily Codex usage. Mouse movement no longer performs graphics readbacks. Usage parsing now distinguishes individual requests from session/turn totals and deduplicates the modern and legacy Codex records for the same call.

Daily statistics use your computer's local date and the request's recorded time. Yesterday's figures are cleared at midnight, and the update label shows the last completed scan. Models recorded in background/helper calls still appear in their actual day's usage. Costs are API-equivalent USD estimates, not subscription invoices.

Claude Desktop query summaries that clearly cross local midnight are skipped when they cannot be reliably split by day. Individually timestamped messages remain counted, so those daily totals may be incomplete.

### Quick start with Steam

1. Install **Bongo Cat** in Steam and stop it if it is running.
2. Download **Nikki-Bongo-Cat-win-x64.zip** below and **extract the whole folder** to a permanent location. The **Source code** downloads do not contain the finished app.
3. In the extracted folder, double-click **CopySteamLaunchOption.cmd**.
4. Open **Steam → Library → Bongo Cat → Properties → General → Launch Options**. Replace the existing text with the copied option, including its quotes and `%command%`.
5. Click **Play** in Steam. Click the small companion button to open the dashboard. Use **Settings → Panel size** to resize it; the **EN** menu offers Chinese.

Both illustrated READMEs now include a fuller beginner's guide and troubleshooting. No .NET installation or compilation is needed. To use the pet without Steam, run **StartPetWithStats.cmd**.

For upgrades, stop the old pet first. Extract this release, retain your settings/models if needed, and repeat the copy-and-paste steps if its folder path changes. Local daily input history stays in your Windows profile. The package excludes personal logs, usage data, settings, and build intermediates. A `.zip.sha256` asset is included for optional integrity checks.

## 简体中文

此版本修复启动后鼠标变慢、Codex 今日用量和估算花费异常放大的问题。鼠标移动不再同步读取图像；统计会区分单次请求与会话／回合累计量，并对同一调用的新旧日志去重。

今日统计以电脑本地日期和请求记录的发生时间为准。跨午夜清除昨日数据，“更新于”显示最近一次完成扫描的时间。应用后台／辅助调用中实际记录的模型仍会计入对应日期。花费为按 API 单价折算的美元估算值，并非订阅账单。

明确跨本地午夜、无法可靠按日拆分的 Claude 桌面端查询总摘要会被跳过；有时间戳的逐条消息仍会统计，因此相关日期的用量可能不完整。

### 通过 Steam 快速开始

1. 在 Steam 安装 **Bongo Cat**；如果正在运行，先停止。
2. 下载下方的 **Nikki-Bongo-Cat-win-x64.zip**，将**整个文件夹解压**到准备长期存放的位置。不要下载 **Source code** 源码包。
3. 在解压后的文件夹内双击 **CopySteamLaunchOption.cmd**，自动复制启动参数。
4. 打开 **Steam → 库 → Bongo Cat → 属性 → 通用 → 启动选项**，用复制的内容完整替换旧参数，保留双引号和 `%command%`。
5. 在 Steam 点击“开始游戏”。点击桌宠旁的小入口打开面板，通过 **EN → Language → Chinese** 切换中文；“高级详情 → 面板大小”可调整大小。

中英文 README 均已补充更详细的新手教程、预览图和常见问题。无需安装 .NET 或编译。不使用 Steam 时，直接运行 **StartPetWithStats.cmd**。

升级前先停止旧版桌宠。解压此版本，按需保留设置和模型；解压路径改变后，务必重新复制并粘贴 Steam 参数。每日输入历史保留在 Windows 用户目录中。发布包不含个人日志、用量数据、设置或构建中间文件；另附 `.zip.sha256` 文件供可选校验。
