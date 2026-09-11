# Nikki Bongo Cat v1.1.3

## English

This update fixes the unlocked pet falling behind other application and browser windows. Always-on-top behavior is now independent of locking: the pet stays visible when you switch applications, drag it, or resize it. Switching applications keeps input focus in the application you selected.

**To resize:** unlock the pet, hold the right mouse button on its visible body, and drag right/down to enlarge or left/up to shrink. Release to finish. Left-button dragging moves the pet.

Transparent areas remain available to other applications, and locking still enables click-through across the whole pet. The previous right-button resize and pointer-performance fixes are included.

### Quick start with Steam

1. Install **Bongo Cat** in Steam and stop it if it is running.
2. Download **Nikki-Bongo-Cat-win-x64.zip** below and **extract the whole folder** to a permanent location. The **Source code** downloads do not contain the finished app.
3. In the extracted folder, double-click **CopySteamLaunchOption.cmd**.
4. Open **Steam → Library → Bongo Cat → Properties → General → Launch Options**. Replace the existing text with the copied option, including its quotes and `%command%`.
5. Click **Play** in Steam. Click the small companion button to open the dashboard. Use **Settings → Panel size** to resize it; the **EN** menu offers Chinese.

Both illustrated READMEs now include a fuller beginner's guide and troubleshooting. No .NET installation or compilation is needed. To use the pet without Steam, run **StartPetWithStats.cmd**.

For upgrades, stop the old pet first. Extract this release, retain your settings/models if needed, and repeat the copy-and-paste steps if its folder path changes. Local daily input history stays in your Windows profile. The package excludes personal logs, usage data, settings, and build intermediates. A `.zip.sha256` asset is included for optional integrity checks.

## 简体中文

此版本修复解锁桌宠后被其他软件或浏览器遮住的问题。置顶行为与锁定状态分开：切换应用、拖动或缩放时，桌宠都会保持在普通窗口上方。切换软件时，输入焦点仍留在你选择的软件中。

**缩放方法：**先解锁桌宠，在可见本体上按住右键，向右／向下拖动放大，向左／向上拖动缩小，松开结束。按住左键拖动可以移动桌宠。

透明空白区域仍可操作其他应用；锁定后整个桌宠允许鼠标穿透。此版本包含之前的右键缩放和鼠标卡顿修复。

### 通过 Steam 快速开始

1. 在 Steam 安装 **Bongo Cat**；如果正在运行，先停止。
2. 下载下方的 **Nikki-Bongo-Cat-win-x64.zip**，将**整个文件夹解压**到准备长期存放的位置。不要下载 **Source code** 源码包。
3. 在解压后的文件夹内双击 **CopySteamLaunchOption.cmd**，自动复制启动参数。
4. 打开 **Steam → 库 → Bongo Cat → 属性 → 通用 → 启动选项**，用复制的内容完整替换旧参数，保留双引号和 `%command%`。
5. 在 Steam 点击“开始游戏”。点击桌宠旁的小入口打开面板，通过 **EN → Language → Chinese** 切换中文；“高级详情 → 面板大小”可调整大小。

中英文 README 均已补充更详细的新手教程、预览图和常见问题。无需安装 .NET 或编译。不使用 Steam 时，直接运行 **StartPetWithStats.cmd**。

升级前先停止旧版桌宠。解压此版本，按需保留设置和模型；解压路径改变后，务必重新复制并粘贴 Steam 参数。每日输入历史保留在 Windows 用户目录中。发布包不含个人日志、用量数据、设置或构建中间文件；另附 `.zip.sha256` 文件供可选校验。
