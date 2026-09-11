namespace PetStatsOverlay;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var options = LaunchOptions.Parse(args);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var store = new StatsStore();
        L.SetLanguage(store.Settings.CompanionUi.Language);

        if (args.Contains("--copy-steam-option", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var executable = Path.Combine(AppContext.BaseDirectory, "PetStatsOverlay.exe");
                var option = $"\"{executable}\" --steam-launcher %command%";
                Clipboard.SetText(option);
                AppDialog.Show(L.Pick("Steam launch option copied. Paste it into Bongo Cat's Properties → General → Launch Options.\n\n", "已复制 Steam 启动选项。请粘贴到 Bongo Cat 的属性 → 通用 → 启动选项。\n\n") + option,
                    "Nikki Bongo Cat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppDialog.Show(L.Error(ex), "Nikki Bongo Cat", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        if (options.ShowHelp)
        {
            AppDialog.Show(
                L.Text("启动参数：\n\n") +
                L.Text("--launch-pet   启动本地桌宠和统计浮窗\n") +
                L.Text("--steam        请求 Steam 启动 Bongo Cat 后退出\n") +
                L.Text("--steam-launcher  已由 Steam 启动，只启动一只本地桌宠\n") +
                L.Text("--help         显示此帮助") + L.Pick("\n--copy-steam-option   Copy the Steam launch option", "\n--copy-steam-option   复制 Steam 启动选项"),
                "Nikki Bongo Cat",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var editor = new BongoCatConfigEditor();
        if (options.StartSteam)
        {
            try
            {
                editor.StartSteamBongoCat();
            }
            catch (Exception ex)
            {
                AppDialog.Show(
                    L.Format($"Steam Bongo Cat 启动失败：\n\n{L.Error(ex)}"),
                    "Nikki Bongo Cat",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return;
        }

        if (options.LaunchPet)
        {
            try
            {
                editor.StartPet();
            }
            catch (Exception ex)
            {
                AppDialog.Show(
                    L.Format($"桌宠启动失败：\n\n{L.Error(ex)}"),
                    "Nikki Bongo Cat",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
        }

        Application.Run(new Form1(store));
    }

    private sealed record LaunchOptions(bool LaunchPet, bool StartSteam, bool ShowHelp)
    {
        public static LaunchOptions Parse(IEnumerable<string> args)
        {
            var values = args.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var steamHosted = values.Contains("--steam-launcher")
                || values.Contains("--attach-steam-bongo-cat");
            var startSteam = values.Contains("--steam");
            var launchPet = steamHosted || values.Contains("--launch-pet");
            var showHelp = values.Contains("--help") || values.Contains("-h") || values.Contains("/?");
            return new LaunchOptions(launchPet, startSteam, showHelp);
        }
    }
}
