namespace PetStatsOverlay;

public static class AiLogRootDiscovery
{
    private static readonly LogRootSetting[] KnownRoots =
    [
        new() { Name = "codex", ProviderHint = "openai", Path = @"%USERPROFILE%\.codex\sessions" },
        new() { Name = "codex-app", ProviderHint = "openai", Path = @"%APPDATA%\Codex", ScanJsonFiles = true },
        new() { Name = "codex-app-local", ProviderHint = "openai", Path = @"%LOCALAPPDATA%\Codex", ScanJsonFiles = true },
        new() { Name = "claude", ProviderHint = "anthropic", Path = @"%USERPROFILE%\.claude\projects" },
        new() { Name = "claude-history", ProviderHint = "anthropic", Path = @"%USERPROFILE%\.claude" },
        new() { Name = "claude-app", ProviderHint = "anthropic", Path = @"%APPDATA%\Claude", ScanJsonFiles = true },
        new() { Name = "claude-app-local", ProviderHint = "anthropic", Path = @"%LOCALAPPDATA%\Claude", ScanJsonFiles = true },
        new() { Name = "cursor-global", Path = @"%APPDATA%\Cursor\User\globalStorage", ScanJsonFiles = true },
        new() { Name = "cursor-workspace", Path = @"%APPDATA%\Cursor\User\workspaceStorage", ScanJsonFiles = true },
        new() { Name = "windsurf-global", Path = @"%APPDATA%\Windsurf\User\globalStorage", ScanJsonFiles = true },
        new() { Name = "windsurf-workspace", Path = @"%APPDATA%\Windsurf\User\workspaceStorage", ScanJsonFiles = true },
        new() { Name = "continue", Path = @"%USERPROFILE%\.continue", ScanJsonFiles = true },
        new() { Name = "gemini-cli", ProviderHint = "google", Path = @"%USERPROFILE%\.gemini\tmp", ScanJsonFiles = true },
        new() { Name = "kimi-cli", ProviderHint = "kimi", Path = @"%USERPROFILE%\.kimi\sessions", ScanJsonFiles = true },
        new() { Name = "qwen-code", ProviderHint = "qwen", Path = @"%USERPROFILE%\.qwen\projects", ScanJsonFiles = true },
        new() { Name = "opencode", Path = @"%USERPROFILE%\.local\share\opencode\storage", ScanJsonFiles = true },
        new() { Name = "cline", Path = @"%APPDATA%\Code\User\globalStorage\saoudrizwan.claude-dev\tasks", ScanJsonFiles = true },
        new() { Name = "roo-code", Path = @"%APPDATA%\Code\User\globalStorage\rooveterinaryinc.roo-cline\tasks", ScanJsonFiles = true },
        new() { Name = "kilo-code", Path = @"%APPDATA%\Code\User\globalStorage\kilocode.kilo-code\tasks", ScanJsonFiles = true }
    ];

    private static readonly PortableSuffix[] PortableSuffixes =
    [
        new(@"/.codex/sessions", @"%USERPROFILE%\.codex\sessions"),
        new(@"/.claude/projects", @"%USERPROFILE%\.claude\projects"),
        new(@"/.claude", @"%USERPROFILE%\.claude"),
        new(@"/appdata/roaming/codex", @"%APPDATA%\Codex"),
        new(@"/appdata/local/codex", @"%LOCALAPPDATA%\Codex"),
        new(@"/appdata/roaming/claude", @"%APPDATA%\Claude"),
        new(@"/appdata/local/claude", @"%LOCALAPPDATA%\Claude"),
        new(@"/appdata/roaming/cursor/user/globalstorage", @"%APPDATA%\Cursor\User\globalStorage"),
        new(@"/appdata/roaming/cursor/user/workspacestorage", @"%APPDATA%\Cursor\User\workspaceStorage"),
        new(@"/appdata/roaming/windsurf/user/globalstorage", @"%APPDATA%\Windsurf\User\globalStorage"),
        new(@"/appdata/roaming/windsurf/user/workspacestorage", @"%APPDATA%\Windsurf\User\workspaceStorage"),
        new(@"/.continue", @"%USERPROFILE%\.continue")
    ];

    public static IReadOnlyList<LogRootSetting> GetDefaultRoots()
    {
        return KnownRoots.Select(Clone).ToArray();
    }

    public static IEnumerable<LogRootSetting> DiscoverExisting()
    {
        foreach (var root in GetDefaultRoots().Concat(DiscoverPackagedClaudeRoots()))
        {
            var exists = false;
            try
            {
                exists = Directory.Exists(ExpandPath(root.Path));
            }
            catch
            {
                // Auto-discovery is best-effort; bad environment paths should not break stats.
            }

            if (exists)
            {
                yield return root;
            }
        }
    }

    // Store/MSIX installations redirect roaming/local app data into the package cache.
    // Discover the publisher suffix instead of hard-coding one user's package identity.
    public static IEnumerable<LogRootSetting> DiscoverPackagedClaudeRoots(string? packagesDirectory = null)
    {
        var packages = packagesDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
        var roots = new List<LogRootSetting>();
        if (!Directory.Exists(packages)) return roots;
        try
        {
            foreach (var package in Directory.EnumerateDirectories(packages, "*Claude*", new EnumerationOptions { IgnoreInaccessible = true }))
                foreach (var suffix in new[] { @"LocalCache\Roaming\Claude", @"LocalCache\Local\Claude", @"LocalState\Claude" })
                {
                    var path = Path.Combine(package, suffix);
                    if (Directory.Exists(path)) roots.Add(new LogRootSetting { Name = "claude-desktop-store", ProviderHint = "anthropic", Path = path, ScanJsonFiles = true });
                }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return roots;
    }

    public static LogRootSetting NormalizeKnownRoot(LogRootSetting root)
    {
        var path = ToPortableKnownPath(root.Path);
        var known = KnownRoots.FirstOrDefault(candidate => PathsReferToSameLocation(candidate.Path, path));
        return new LogRootSetting
        {
            Name = FirstNonEmpty(root.Name, known?.Name ?? ""),
            ProviderHint = FirstNonEmpty(root.ProviderHint, known?.ProviderHint ?? ""),
            Path = path,
            ScanJsonFiles = root.ScanJsonFiles || known?.ScanJsonFiles == true,
            Enabled = root.Enabled
        };
    }

    public static string ToPortableKnownPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        foreach (var root in KnownRoots)
        {
            if (PathsReferToSameLocation(path, root.Path))
            {
                return root.Path;
            }
        }

        var normalized = NormalizeForSuffix(ExpandPath(path));
        foreach (var suffix in PortableSuffixes)
        {
            if (normalized.EndsWith(suffix.Suffix, StringComparison.OrdinalIgnoreCase))
            {
                return suffix.PortablePath;
            }
        }

        return path;
    }

    public static bool PathsReferToSameLocation(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
    }

    public static string ExpandPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var expanded = Environment.ExpandEnvironmentVariables(path);
        if (expanded == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (expanded.StartsWith(@"~\", StringComparison.Ordinal) || expanded.StartsWith("~/", StringComparison.Ordinal))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), expanded[2..]);
        }

        return expanded;
    }

    private static LogRootSetting Clone(LogRootSetting root)
    {
        return new LogRootSetting
        {
            Name = root.Name,
            ProviderHint = root.ProviderHint,
            Path = root.Path,
            ScanJsonFiles = root.ScanJsonFiles,
            Enabled = root.Enabled
        };
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(ExpandPath(path))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
        catch
        {
            return path.TrimEnd('\\', '/').Replace('/', '\\');
        }
    }

    private static string NormalizeForSuffix(string path)
    {
        return path.TrimEnd('\\', '/').Replace('\\', '/').ToLowerInvariant();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }

    private sealed record PortableSuffix(string Suffix, string PortablePath);
}
