using System.Globalization;

namespace PetStatsOverlay;

/// <summary>First-party UI strings. English is the default independently of Windows language.</summary>
public static class L
{
    public static bool IsChinese { get; private set; }
    public static string Language => IsChinese ? "zh" : "en";
    public static void SetLanguage(string? language)
    {
        IsChinese = string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(IsChinese ? "zh-CN" : "en-US");
    }

    public static string Pick(string english, string chinese) => IsChinese ? chinese : english;
    public static string Text(string chinese) => IsChinese ? chinese : English.GetValueOrDefault(chinese, chinese);
    public static string Format(FormattableString chinese) => string.Format(CultureInfo.InvariantCulture, Text(chinese.Format), chinese.GetArguments());

    public static string Error(Exception exception)
    {
        // Preserve our own translated validation messages. System exception text
        // follows Windows rather than the selected app language, so give it a
        // localized explanation instead of mixing languages inside the dialog.
        foreach (var pair in English)
        {
            var template = IsChinese ? pair.Key : pair.Value;
            if (template == exception.Message) return exception.Message;
            var prefix = template.Split('{')[0];
            if (prefix.Length >= 8 && exception.Message.StartsWith(prefix, StringComparison.Ordinal))
                return exception.Message;
            if (template.Contains('{'))
            {
                var suffix = template[(template.LastIndexOf('}') + 1)..];
                if (prefix.Length + suffix.Length >= 3 && exception.Message.StartsWith(prefix, StringComparison.Ordinal)
                    && exception.Message.EndsWith(suffix, StringComparison.Ordinal)) return exception.Message;
            }
        }
        return exception switch
        {
            UnauthorizedAccessException => Pick("Access denied. Check that this folder is writable and try again.", "访问被拒绝。请确认文件夹可写后重试。"),
            FileNotFoundException => Pick("A required file was not found. Check the selected file or reinstall the complete package.", "找不到所需文件。请检查所选文件，或重新解压完整安装包。"),
            DirectoryNotFoundException => Pick("The folder was not found. Check the selected path.", "找不到文件夹。请检查所选路径。"),
            System.Text.Json.JsonException => Pick("The file contains invalid JSON. Check its format or restore a backup.", "文件中的 JSON 格式无效。请检查格式或恢复备份。"),
            IOException => Pick("The file could not be read or written. Check permissions and close any app using the file.", "无法读写文件。请检查权限，并关闭正在使用该文件的应用。"),
            _ => Pick($"The operation could not be completed ({exception.GetType().Name}). Check the selected files and try again.", $"操作未能完成（{exception.GetType().Name}）。请检查所选文件后重试。")
        };
    }

    public static string Retranslate(string text)
    {
        if (English.ContainsKey(text)) return Text(text);
        foreach (var pair in English)
            if (string.Equals(pair.Value, text, StringComparison.Ordinal)) return IsChinese ? pair.Key : pair.Value;
        return text;
    }

    public static void RefreshControls(Control parent)
    {
        parent.Text = Retranslate(parent.Text);
        if (parent is Button button && button.Parent is FlowLayoutPanel)
            button.Width = Math.Max(button.Width, TextRenderer.MeasureText(button.Text, button.Font).Width + 24);
        foreach (Control child in parent.Controls) RefreshControls(child);
        if (parent is DataGridView grid)
            foreach (DataGridViewColumn column in grid.Columns) column.HeaderText = Retranslate(column.HeaderText);
    }

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["Bongo Cat Mver 自定义工具"] = "Nikki Bongo Cat",
        ["打开统计面板"] = "Open statistics",
        ["自定义桌宠"] = "Customize pet",
        ["重启桌宠"] = "Restart pet",
        ["退出统计浮窗"] = "Exit companion",
        ["⌨️ 打字次数"] = "⌨️ Keystrokes",
        ["🖱️ 点击次数"] = "🖱️ Clicks",
        ["💬 对话次数"] = "💬 Requests",
        ["📦 令牌消耗"] = "📦 Tokens",
        ["令牌"] = "Tokens",
        ["💜 花费"] = "💜 Est. cost",
        ["次"] = "calls",
        ["高级详情"] = "Settings",
        ["今日陪伴"] = "Today Together",
        ["打字"] = "Typing",
        ["点击"] = "Clicks",
        ["花费"] = "Est. cost",
        ["对话"] = "Requests",
        ["自定义"] = "Customize",
        ["自定义桌宠打开失败"] = "Could not open customization",
        ["切换皮肤"] = "Switch skin",
        ["切换皮肤失败"] = "Could not switch skin",
        ["常驻入口"] = "Always show button",
        ["浮窗显示指标"] = "Button display",
        ["重置今日"] = "Reset today's input counts",
        ["解锁桌宠"] = "Unlock pet",
        ["锁定桌宠"] = "Lock pet",
        ["打开设置"] = "Open settings file",
        ["打开数据文件夹"] = "Open data folder",
        ["🌸 今日陪伴 ✨"] = "🌸 Today Together ✨",
        ["已锁定"] = "Locked",
        ["可交互"] = "Interactive",
        ["📊 模型使用占比"] = "📊 Model usage by tokens",
        ["'更新于' HH:mm"] = "'Updated' HH:mm",
        ["未知模型"] = "Unknown model",
        ["打字 {0:N0}"] = "Typing {0:N0}",
        ["点击 {0:N0}"] = "Clicks {0:N0}",
        ["对话 {0:N0}"] = "Requests {0:N0}",
        ["触发键与图片"] = "Keys & images",
        ["键盘/手部动画触发项"] = "Keyboard & hand animations",
        ["表情、Live2D 与预览"] = "Expressions, Live2D & preview",
        ["动画"] = "Animation",
        ["触发键"] = "Hotkey",
        ["键盘图"] = "Keyboard image",
        ["手部图"] = "Hand image",
        ["表情"] = "Expression",
        ["表情图"] = "Expression image",
        ["添加表情"] = "Add expression",
        ["删除表情"] = "Delete expression",
        ["选择表情图"] = "Choose image",
        ["预览"] = "Preview",
        ["Live2D 表情触发键"] = "Live2D expression hotkeys",
        ["标签"] = "Label",
        ["说明"] = "Description",
        ["添加 Live2D"] = "Add Live2D",
        ["删除 Live2D"] = "Delete Live2D",
        ["预览标签"] = "Preview label",
        ["当前预览"] = "Current preview",
        ["添加动画"] = "Add animation",
        ["删除动画"] = "Delete animation",
        ["选择键盘图"] = "Keyboard image",
        ["选择手部图"] = "Hand image",
        ["启用 Live2D"] = "Enable Live2D",
        ["显示完整路径"] = "Show full paths",
        ["保存并重启"] = "Save & restart",
        ["保存设置"] = "Save settings",
        ["重新读取"] = "Reload",
        ["基础素材"] = "Assets",
        ["快速换肤（自动记住上次选择）"] = "Quick skins (last choice saved)",
        ["粉色暖暖（默认）"] = "Pink Nikki (default)",
        ["紫色暖暖"] = "Purple Nikki",
        ["Live2D 模型"] = "Live2D models",
        ["刷新 Live2D 模型列表"] = "Refresh Live2D models",
        ["删除选中 Live2D 模型"] = "Delete selected Live2D model",
        ["应用选中模型并重启"] = "Apply model & restart",
        ["打开 Live2D 模型库"] = "Open Live2D library",
        ["基础 PNG 素材"] = "PNG assets",
        ["替换桌宠主体 cat.png"] = "Replace body · cat.png",
        ["替换手臂 arm.png"] = "Replace arm · arm.png",
        ["替换鼠标 mouse.png"] = "Replace mouse · mouse.png",
        ["替换鼠标背景 mousebg.png"] = "Replace mouse background · mousebg.png",
        ["替换鼠标左键 mouse_left.png"] = "Replace left click · mouse_left.png",
        ["替换鼠标右键 mouse_right.png"] = "Replace right click · mouse_right.png",
        ["替换鼠标侧键 mouse_side.png"] = "Replace side click · mouse_side.png",
        ["键盘样式素材"] = "Keyboard assets",
        ["替换键盘整体 tablet.png"] = "Replace keyboard · tablet.png",
        ["替换键盘背景 tabletbg.png"] = "Replace background · tabletbg.png",
        ["替换键盘左键 tablet_left.png"] = "Replace left key · tablet_left.png",
        ["替换键盘右键 tablet_right.png"] = "Replace right key · tablet_right.png",
        ["替换抬起状态 up.png"] = "Replace raised pose · up.png",
        ["导入 Live2D 模型文件夹（校验）"] = "Import Live2D model folder",
        ["导入 Live2D 整包 zip"] = "Import Live2D ZIP package",
        ["导出选中 Live2D 整包"] = "Export selected Live2D package",
        ["打开当前素材目录"] = "Open current assets folder",
        ["打开备份目录"] = "Open backups folder",
        ["替换当前 Live2D 贴图"] = "Replace current Live2D texture",
        ["基础素材预览"] = "Asset preview",
        ["添加动画/表情请在“触发键与图片”页直接添加行。保存会自动把图片复制成对应序号并写入 config.json。"] = "Add animation and expression rows in Keys & images. Saving copies each image to its numbered slot and updates the pet settings.",
        ["一键恢复默认桌宠"] = "Restore default pet",
        ["已刷新 Live2D 模型列表。"] = "Live2D model list refreshed.",
        ["请选择一个 Live2D 模型。"] = "Select a Live2D model.",
        ["确定要从模型库删除“{0}”吗？"] = "Delete “{0}” from the model library?",
        ["删除 Live2D 模型"] = "Delete Live2D model",
        ["已删除 Live2D 模型：{0}。"] = "Deleted Live2D model: {0}.",
        ["当前标准桌宠（实际显示）"] = "Current pet (as displayed)",
        ["当前 Live2D 贴图"] = "Current Live2D texture",
        ["主体 cat.png"] = "Body · cat.png",
        ["手臂 arm.png"] = "Arm · arm.png",
        ["抬起 up.png"] = "Raised pose · up.png",
        ["鼠标 mouse.png"] = "Mouse · mouse.png",
        ["鼠标背景 mousebg.png"] = "Mouse background · mousebg.png",
        ["鼠标左键 mouse_left.png"] = "Left click · mouse_left.png",
        ["鼠标右键 mouse_right.png"] = "Right click · mouse_right.png",
        ["鼠标侧键 mouse_side.png"] = "Side click · mouse_side.png",
        ["键盘整体 tablet.png"] = "Keyboard · tablet.png",
        ["键盘背景 tabletbg.png"] = "Keyboard background · tabletbg.png",
        ["键盘左键 tablet_left.png"] = "Left key · tablet_left.png",
        ["键盘右键 tablet_right.png"] = "Right key · tablet_right.png",
        ["当前模型：{0}"] = "Current model: {0}",
        ["动画 {0}"] = "Animation {0}",
        ["表情 {0}"] = "Expression {0}",
        ["Live2D 表情"] = "Live2D expression",
        ["已读取当前配置。"] = "Current settings loaded.",
        ["已保存图片和触发键到 config.json。重启桌宠后生效。"] = "Images and hotkeys saved. Restart the pet to apply them.",
        ["已保存并重启桌宠。"] = "Saved and restarted the pet.",
        ["{0} 缺少对应图片。"] = "{0} is missing its image.",
        ["找不到图片文件。"] = "Image file not found.",
        ["选择 PNG 图片"] = "Choose a PNG image",
        ["PNG 图片|*.png"] = "PNG images|*.png",
        ["已选择图片，保存后会复制到桌宠素材目录。"] = "Image selected. Saving will copy it to the pet's assets folder.",
        ["未绑定触发键"] = "No hotkey assigned",
        ["触发键：{0}"] = "Hotkey: {0}",
        ["没有表情参数摘要"] = "No expression parameter summary",
        ["已替换：{0}。重启桌宠后生效。"] = "Replaced {0}. Restart the pet to apply it.",
        ["已应用 Live2D 模型：{0}。"] = "Applied Live2D model: {0}.",
        ["找不到对应的内置皮肤资源。"] = "Built-in skin assets not found.",
        ["选择 Live2D 模型根目录，目录里应有一个 .model3.json"] = "Select the Live2D model folder containing one .model3.json file",
        ["导入前会自动备份当前 cat_model。确认继续吗？"] = "The current model will be backed up before import. Continue?",
        ["导入 Live2D"] = "Import Live2D",
        ["Live2D 模型已导入，重启桌宠后生效。"] = "Live2D model imported. Restart the pet to apply it.",
        ["选择 Live2D 整包 zip"] = "Choose a Live2D ZIP package",
        ["Live2D 整包|*.zip"] = "Live2D packages|*.zip",
        ["已导入并选中 Live2D 整包：{0}。"] = "Imported and selected Live2D package: {0}.",
        ["导出 Live2D 整包"] = "Export Live2D package",
        ["已导出 Live2D 整包：{0}"] = "Exported Live2D package: {0}",
        ["将恢复默认标准桌宠并重启。当前模型会先自动备份。继续吗？"] = "Restore the default pet and restart? Your current model will be backed up first.",
        ["恢复默认桌宠"] = "Restore default pet",
        ["已恢复默认标准桌宠并重启。"] = "Default pet restored and restarted.",
        ["触发键录入：选中此格后直接按键盘按键或组合键，例如 Ctrl+1、Shift+A、Space。"] = "Select a hotkey cell and press a key or combination, such as Ctrl+1, Shift+A or Space.",
        ["已录入触发键：{0}"] = "Hotkey captured: {0}",
        ["粉色暖暖"] = "Pink Nikki",
        ["未知的内置皮肤。"] = "Unknown built-in skin.",
        ["找不到当前 Live2D 贴图。"] = "Current Live2D texture not found.",
        ["找不到 BongoCatMver.exe。"] = "BongoCatMver.exe not found.",
        ["检测到另一个 BongoCatMver 实例，但无法关闭。请先从托盘退出旧桌宠后重试。"] = "Another BongoCatMver instance could not be closed. Exit the old pet from its tray icon and try again.",
        ["找不到要导入的图片。"] = "Image to import not found.",
        ["目前只支持导入 PNG 图片。"] = "Only PNG images are supported.",
        ["找不到默认桌宠模型备份。"] = "Default pet model backup not found.",
        ["请选择一个要删除的 Live2D 模型。"] = "Select a Live2D model to delete.",
        ["当前正在使用的 Live2D 模型不能删除，请先切换到其他模型。"] = "The active Live2D model cannot be deleted. Switch to another model first.",
        ["找不到要删除的 Live2D 模型文件夹。"] = "Live2D model folder to delete not found.",
        ["模型目录不在 Live2D 模型库中。"] = "The model folder is outside the Live2D library.",
        ["找不到 Live2D 整包。"] = "Live2D package not found.",
        ["请选择一个要导出的 Live2D 模型。"] = "Select a Live2D model to export.",
        ["导出文件不能放在模型目录里面。"] = "The export file cannot be saved inside the model folder.",
        ["找不到 config.json。"] = "config.json not found.",
        ["config.json 不是有效对象。"] = "config.json is not a valid JSON object.",
        ["未找到模型"] = "Model not found",
        ["找不到 Live2D 模型文件夹。"] = "Live2D model folder not found.",
        ["这个文件夹里没有找到有效的 Live2D 模型。"] = "No valid Live2D model found in this folder.",
        ["整包里没有找到有效的 Live2D 模型。"] = "No valid Live2D model found in the package.",
        ["找不到表情文件"] = "Expression file not found",
        ["默认表情"] = "Default expression",
        ["请选择只包含一个 .model3.json 的 Live2D 模型根目录。"] = "Select a Live2D model folder containing exactly one .model3.json file.",
        ["model3.json 不是有效 JSON。"] = "model3.json is not valid JSON.",
        ["model3.json 缺少 FileReferences。"] = "model3.json is missing FileReferences.",
        ["Live2D 模型缺少有效的 Moc 文件。"] = "The Live2D model is missing a valid Moc file.",
        ["Live2D 模型缺少贴图列表。"] = "The Live2D model is missing its texture list.",
        ["Live2D 贴图不存在：{0}"] = "Live2D texture not found: {0}",
        ["{0}（当前）"] = "{0} (active)",
        ["启动参数：\n\n"] = "Launch options:\n\n",
        ["--launch-pet   启动本地桌宠和统计浮窗\n"] = "--launch-pet   Start the local pet and companion\n",
        ["--steam        请求 Steam 启动 Bongo Cat 后退出\n"] = "--steam        Ask Steam to start Bongo Cat, then exit\n",
        ["--steam-launcher  已由 Steam 启动，只启动一只本地桌宠\n"] = "--steam-launcher  Start one local pet from Steam\n",
        ["--help         显示此帮助"] = "--help         Show this help",
        ["Steam Bongo Cat 启动失败：\n\n{0}"] = "Could not start Steam Bongo Cat:\n\n{0}",
        ["桌宠启动失败：\n\n{0}"] = "Could not start the pet:\n\n{0}",
    };
}
