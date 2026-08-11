using System.Text.Json;
using System.Text.Json.Serialization;

namespace PetStatsOverlay;

public sealed class StatsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string DataDirectory { get; }
    public string StatePath { get; }
    public string SettingsPath { get; }
    public PetStatsSettings Settings { get; }
    public DailyState Today { get; private set; }

    public StatsStore()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PetStatsOverlay");
        Directory.CreateDirectory(DataDirectory);

        StatePath = Path.Combine(DataDirectory, "state.json");
        SettingsPath = Path.Combine(AppContext.BaseDirectory, "pet-stats-settings.json");

        Settings = LoadSettings();
        Today = LoadState().GetToday();
    }

    public void IncrementTyping()
    {
        AddTyping(1);
    }

    public void AddTyping(long count)
    {
        if (count <= 0)
        {
            return;
        }

        EnsureToday();
        Today.TypingCount += count;
    }

    public void AddMouseClicks(long count)
    {
        if (count <= 0)
        {
            return;
        }

        EnsureToday();
        Today.MouseClickCount += count;
    }

    public void ResetToday()
    {
        Today = new DailyState
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            TypingCount = 0,
            MouseClickCount = 0
        };
        Save();
    }

    public void Save()
    {
        EnsureToday();
        var state = LoadState();
        state.Days[Today.Date.ToString("yyyy-MM-dd")] = Today;
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, JsonOptions));
    }

    public void SaveSettings()
    {
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, JsonOptions));
    }

    private void EnsureToday()
    {
        var current = DateOnly.FromDateTime(DateTime.Now);
        if (Today.Date != current)
        {
            Today = new DailyState
            {
                Date = current
            };
        }
    }

    private PetStatsSettings LoadSettings()
    {
        if (!File.Exists(SettingsPath))
        {
            var defaults = PetStatsSettings.CreateDefault();
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(defaults, JsonOptions));
            return defaults;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<PetStatsSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                ?? PetStatsSettings.CreateDefault();
            var merged = MergeDefaults(loaded);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(merged, JsonOptions));
            return merged;
        }
        catch
        {
            return PetStatsSettings.CreateDefault();
        }
    }

    private PetStatsSettings MergeDefaults(PetStatsSettings loaded)
    {
        var defaults = PetStatsSettings.CreateDefault();
        loaded.LogRoots ??= [];
        loaded.ExtraLogRoots ??= [];
        loaded.Prices ??= [];

        if (string.IsNullOrWhiteSpace(loaded.CodexLogRoot))
        {
            loaded.CodexLogRoot = defaults.CodexLogRoot;
        }
        else
        {
            loaded.CodexLogRoot = AiLogRootDiscovery.ToPortableKnownPath(loaded.CodexLogRoot);
        }

        if (string.IsNullOrWhiteSpace(loaded.ClaudeLogRoot))
        {
            loaded.ClaudeLogRoot = defaults.ClaudeLogRoot;
        }
        else
        {
            loaded.ClaudeLogRoot = AiLogRootDiscovery.ToPortableKnownPath(loaded.ClaudeLogRoot);
        }

        if (loaded.LogRoots.Count == 0)
        {
            loaded.LogRoots = defaults.LogRoots;
        }
        else
        {
            loaded.LogRoots = loaded.LogRoots
                .Select(AiLogRootDiscovery.NormalizeKnownRoot)
                .ToList();
            AddMissingLogRoots(loaded.LogRoots, defaults.LogRoots);
        }

        loaded.ExtraLogRoots = loaded.ExtraLogRoots
            .Select(AiLogRootDiscovery.ToPortableKnownPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(loaded.PricingCatalogUrl))
        {
            loaded.PricingCatalogUrl = defaults.PricingCatalogUrl;
        }

        loaded.CompanionUi ??= defaults.CompanionUi;
        if (string.IsNullOrWhiteSpace(loaded.CompanionUi.ButtonMetric))
        {
            loaded.CompanionUi.ButtonMetric = defaults.CompanionUi.ButtonMetric;
        }

        if (loaded.RefreshPricingHours <= 0)
        {
            loaded.RefreshPricingHours = defaults.RefreshPricingHours;
        }

        if (loaded.MaxLogFileMb <= 0)
        {
            loaded.MaxLogFileMb = defaults.MaxLogFileMb;
        }

        loaded.Prices = loaded.Prices
            .Where(price => !string.IsNullOrWhiteSpace(price.Pattern))
            .ToList();

        return loaded;
    }

    private static void AddMissingLogRoots(List<LogRootSetting> target, IEnumerable<LogRootSetting> defaults)
    {
        foreach (var root in defaults)
        {
            if (!target.Any(existing => AiLogRootDiscovery.PathsReferToSameLocation(existing.Path, root.Path)))
            {
                target.Add(root);
            }
        }
    }

    private PetStatsState LoadState()
    {
        if (!File.Exists(StatePath))
        {
            return new PetStatsState();
        }

        try
        {
            return JsonSerializer.Deserialize<PetStatsState>(File.ReadAllText(StatePath), JsonOptions)
                ?? new PetStatsState();
        }
        catch
        {
            return new PetStatsState();
        }
    }
}

public sealed class PetStatsState
{
    public Dictionary<string, DailyState> Days { get; set; } = new();

    public DailyState GetToday()
    {
        var key = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
        if (!Days.TryGetValue(key, out var today))
        {
            today = new DailyState
            {
                Date = DateOnly.FromDateTime(DateTime.Now)
            };
            Days[key] = today;
        }

        return today;
    }
}

public sealed class DailyState
{
    public DateOnly Date { get; set; }
    public long TypingCount { get; set; }
    public long MouseClickCount { get; set; }
}

public sealed class PetStatsSettings
{
    public string CodexLogRoot { get; set; } = "";
    public string ClaudeLogRoot { get; set; } = "";
    public List<LogRootSetting> LogRoots { get; set; } = new();
    public List<string> ExtraLogRoots { get; set; } = new();
    public bool AutoDiscoverLogRoots { get; set; } = true;
    public string PricingCatalogUrl { get; set; } = "";
    public int RefreshPricingHours { get; set; } = 24;
    public int MaxLogFileMb { get; set; } = 128;
    public bool ScanJsonFiles { get; set; } = false;
    public CompanionUiSettings CompanionUi { get; set; } = new();
    public List<ModelPrice> Prices { get; set; } = new();

    public static PetStatsSettings CreateDefault()
    {
        return new PetStatsSettings
        {
            CodexLogRoot = @"%USERPROFILE%\.codex\sessions",
            ClaudeLogRoot = @"%USERPROFILE%\.claude\projects",
            AutoDiscoverLogRoots = true,
            PricingCatalogUrl = "https://raw.githubusercontent.com/BerriAI/litellm/main/model_prices_and_context_window.json",
            RefreshPricingHours = 24,
            ScanJsonFiles = false,
            CompanionUi = new CompanionUiSettings(),
            LogRoots = AiLogRootDiscovery.GetDefaultRoots().ToList(),
            Prices = []
        };
    }
}

public sealed class CompanionUiSettings
{
    public bool ButtonAlwaysVisible { get; set; }
    public string ButtonMetric { get; set; } = "companion";
    public bool ButtonManualPosition { get; set; }
    public UiPoint? ButtonLocation { get; set; }
    public bool PanelManualPosition { get; set; }
    public UiPoint? PanelLocation { get; set; }
}

public sealed class UiPoint
{
    public int X { get; set; }
    public int Y { get; set; }

    public Point ToPoint()
    {
        return new Point(X, Y);
    }

    public static UiPoint FromPoint(Point point)
    {
        return new UiPoint { X = point.X, Y = point.Y };
    }
}

public sealed class LogRootSetting
{
    public string Name { get; set; } = "";
    public string ProviderHint { get; set; } = "";
    public string Path { get; set; } = "";
    public bool ScanJsonFiles { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class ModelPrice
{
    public string Pattern { get; set; } = "";
    public string Provider { get; set; } = "";
    public decimal InputPerMillion { get; set; }
    public decimal CacheWrite5mPerMillion { get; set; }
    public decimal CacheWrite1hPerMillion { get; set; }
    public decimal CacheReadPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
    public decimal TotalPerMillion { get; set; }
}
