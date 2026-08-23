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

        if (options.ShowHelp)
        {
            MessageBox.Show(
                "启动参数：\n\n" +
                "--launch-pet   启动本地桌宠和统计浮窗\n" +
                "--steam        请求 Steam 启动 Bongo Cat 后退出\n" +
                "--steam-launcher  已由 Steam 启动，只启动一只本地桌宠\n" +
                "--help         显示此帮助",
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
                MessageBox.Show(
                    $"Steam Bongo Cat 启动失败：\n\n{ex.Message}",
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
                MessageBox.Show(
                    $"桌宠启动失败：\n\n{ex.Message}",
                    "Nikki Bongo Cat",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
        }

        Application.Run(new Form1());
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
