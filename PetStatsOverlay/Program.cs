using System.Diagnostics;

namespace PetStatsOverlay;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        var launchPetThroughSteam = args.Any(arg => string.Equals(arg, "--attach-steam-bongo-cat", StringComparison.OrdinalIgnoreCase));

        if (args.Any(arg => string.Equals(arg, "--steam-launcher", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--launch-pet", StringComparison.OrdinalIgnoreCase)))
        {
            StartBundledPet();
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1(launchPetThroughSteam));
    }

    private static void StartBundledPet()
    {
        var root = FindRootDirectory();
        var petPath = Path.Combine(root, "BongoCatMver.exe");
        if (!File.Exists(petPath) || IsPetAlreadyRunning(petPath))
        {
            return;
        }

        try
        {
            new BongoCatConfigEditor().SyncActiveLive2DModelToRuntime();
            Process.Start(new ProcessStartInfo
            {
                FileName = petPath,
                WorkingDirectory = root,
                UseShellExecute = true
            });
        }
        catch
        {
            // The overlay can still run if the pet was started manually.
        }
    }

    private static bool IsPetAlreadyRunning(string petPath)
    {
        var target = Path.GetFullPath(petPath);
        foreach (var process in Process.GetProcessesByName("BongoCatMver"))
        {
            try
            {
                if (string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? ""), target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }
        }

        return false;
    }

    private static string FindRootDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BongoCatMver.exe"))
                && File.Exists(Path.Combine(directory.FullName, "config.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
