# Nikki Bongo Cat

一个以暖暖 Live2D 模型为核心的 Windows 桌宠。项目在 Bongo Cat Mver 运行时之上增加了统计浮窗、模型管理和 Steam Bongo Cat 游玩时长模式。

## 下载

普通用户请从 [GitHub Releases](https://github.com/Ding808/Nikki-Bongo-Cat/releases/latest) 下载 `Nikki-Bongo-Cat-win-x64.zip`，完整解压后再使用。不要下载仓库页面中“Code → Download ZIP”生成的源码包：源码包按照 Git 规则不包含编译后的 `PetStatsOverlay.exe`。

成品包已经包含所有运行组件，用户无需安装 .NET、SDK 或 Visual Studio，也不需要执行任何编译操作。

## 功能

- 根据键盘、鼠标输入播放桌宠动作与表情
- 显示当日 AI 调用次数、Token、估算费用、键盘次数和鼠标点击次数
- 自动读取 Codex、Claude Code 等本地日志，并支持额外日志目录
- 在托盘中打开统计面板、自定义桌宠、重启桌宠或退出浮窗
- “今日陪伴”入口会跟随桌宠移动，并在桌宠靠近屏幕边缘时自动切换到合适方向
- 从托盘或自定义界面一键切换粉色/紫色暖暖，首次使用默认粉色，并自动记住上次选择
- 导入、切换、导出 Live2D 模型和配置动作按键
- 锁定桌宠后保持置顶并允许鼠标穿透
- 通过 Steam 模式启动官方 Bongo Cat，让 Steam 正常累计 App 3419430 的游玩时长

## 系统要求

- Windows 10/11 x64
- 普通模式不需要 Steam
- Steam 模式需要安装 Steam，并在库中安装免费的 [Bongo Cat](https://store.steampowered.com/app/3419430/Bongo_Cat/)
- 从源码构建需要 .NET 9 SDK

## 快速开始

请完整解压成品包，不要单独移动 EXE、DLL、`img` 或 `Resources`。

### 普通模式

双击 `StartPetWithStats.cmd`。它会启动本地桌宠和统计浮窗。

成品包中的统计程序已经自包含，不需要安装任何开发环境。不要把其中的 EXE 单独移动出去；程序还需要同一套目录中的设置、模型和桌宠运行文件。

### Steam 游玩时长模式

推荐先双击 `CopySteamLaunchOption.cmd`。它会读取当前解压位置，自动生成正确的绝对路径并复制到剪贴板。然后在 Steam 库中右键 Bongo Cat，打开“属性 → 通用 → 启动选项”，删除原有内容并直接粘贴；不需要照抄作者电脑上的盘符或目录。

如果需要手动填写，格式如下：

```text
"<你的解压目录>\Nikki-Bongo-Cat\PetStatsOverlay\PetStatsOverlay.exe" --steam-launcher %command%
```

启动选项看起来较长是正常的，不会影响 Steam 启动或游玩时长统计。请保留 EXE 路径外侧的英文双引号；路径中含空格或中文也可以正常使用。

请以电脑上实际存在的文件为准。不要为了缩短启动选项而单独移动 `PetStatsOverlay.exe`，否则它可能找不到 `config.json`、桌宠程序或模型资源。

设置一次后，直接在 Steam 中点击“开始游戏”即可。Steam 启动的是统计程序，统计程序只启动一只本地暖暖桌宠；统计程序保持运行期间，Steam 会把时长记录到 Bongo Cat。关闭桌宠后统计程序会一起退出，Steam 结束计时。

完成 Steam 启动选项配置后，也可以双击 `StartPetWithStats.Steam.cmd`。它只负责请求 Steam 启动 Bongo Cat，后续仍由上面的 `--steam-launcher` 接管。

## 启动参数

| 参数 | 作用 |
| --- | --- |
| `--launch-pet` | 启动本地桌宠后运行统计浮窗 |
| `--steam-launcher` | 供 Steam 启动选项使用，只启动一只本地桌宠和统计浮窗 |
| `--steam` | 请求 Steam 启动 App 3419430 后退出 |
| `--help` | 显示参数帮助 |

旧参数 `--attach-steam-bongo-cat` 仍可使用，它与 `--steam-launcher` 等效。

## 使用说明

- 双击桌宠旁边的粉色入口可展开统计面板。
- 右键桌宠或使用系统托盘菜单可以打开设置。
- 在托盘菜单选择“切换皮肤 → 粉色暖暖/紫色暖暖”即可换肤并重启；选择会写入 `config.json`，下次启动自动恢复。
- 小键盘 `-`：锁定桌宠并开启鼠标穿透。
- 小键盘 `+`：解除锁定。
- 在“自定义桌宠”中可以更换图片、动作按键和 Live2D 模型。
- 修改模型后使用“重启桌宠”应用配置。

统计数据默认保存在：

```text
%LOCALAPPDATA%\PetStatsOverlay\state.json
```

统计设置保存在 `PetStatsOverlay.exe` 同目录的 `pet-stats-settings.json`。AI 用量来自本机日志；模型价格目录默认从 LiteLLM 的公开价格表更新。

## Live2D 模型

模型库位于：

```text
img\standard\live2d_models
```

每个模型使用独立文件夹，文件夹根目录应包含且只包含一个 `*.model3.json`，并具备它引用的 Moc、贴图等资源。程序切换模型时会把所选模型同步到运行目录 `img\standard\cat_model`。

`petstats-live2d-profile.json` 保存模型名称、动作键、表情键等配置；`petstats-assets` 保存该模型配套的桌宠图片和音效。

## 开发者：从源码构建

在仓库根目录执行：

```powershell
dotnet restore .\PetStatsOverlay\PetStatsOverlay.csproj
dotnet build .\PetStatsOverlay\PetStatsOverlay.csproj -c Release -r win-x64
```

生成可独立运行的 Windows x64 版本：

```powershell
dotnet publish .\PetStatsOverlay\PetStatsOverlay.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true
```

发布结果位于：

```text
PetStatsOverlay\bin\Release\net9.0-windows\win-x64\publish
```

保留发布目录在仓库内部的默认位置即可，两个启动脚本会自动找到它。

生成可直接发给普通用户的完整压缩包：

```powershell
.\BuildRelease.ps1
```

成品位于 `artifacts\Nikki-Bongo-Cat-win-x64.zip`，其中已经包含自包含的统计程序、桌宠运行文件、模型、启动脚本和 README，不要求用户安装 .NET。推送形如 `v1.0.0` 的 Git 标签时，GitHub Actions 也会自动构建并把同名 ZIP 附加到 GitHub Release；手动运行工作流则会生成可下载的 Actions artifact。

## 项目结构

```text
Nikki-Bongo-Cat/
├─ PetStatsOverlay/            # 统计浮窗与桌宠管理器源码
├─ img/                        # Bongo Cat 图片、音效和 Live2D 模型
├─ Resources/                  # Bongo Cat 运行资源
├─ BongoCatMver.exe            # 桌宠运行时
├─ BongoCatUI.exe              # 原生配置界面
├─ BuildRelease.ps1            # 生成完整 Windows 发布压缩包
├─ CopySteamLaunchOption.cmd   # 按实际解压路径复制 Steam 启动选项
├─ config.json                 # 桌宠配置
├─ StartPetWithStats.cmd       # 普通入口，也接受 --steam
└─ StartPetWithStats.Steam.cmd # Steam 时长模式快捷入口
```
