using System.Reflection;
using System.Text.RegularExpressions;
using PetStatsOverlay;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var temporary = Path.Combine(Path.GetTempPath(), "NikkiUiSmoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        var store = new StatsStore(temporary, Path.Combine(temporary, "settings.json"));
        store.Settings.PricingCatalogUrl = "";
        store.AddTyping(5208);
        store.AddMouseClicks(1342);
        using var form = new Form1(store, startTimers: false);
        form.Opacity = 0;
        form.Show();
        var preview = CreateUsage(8);
        Invoke(form, "UpdateView", preview);
        var percentages = GetField<List<(string Name, int Percent)>>(form, "providerRowsForDisplay");
        Assert(percentages.Sum(row => row.Percent) == 100, "Rounded model shares must total 100%.");
        Invoke(form, "SetExpanded", true);

        foreach (var language in new[] { "en", "zh", "en" })
        {
            Invoke(form, "SetLanguage", language);
            Assert(store.Settings.CompanionUi.Language == language, "Language was not persisted.");
            foreach (var scale in new[] { 0.65D, 1D, 1.75D })
            {
                Invoke(form, "SetPanelScale", scale);
                Assert(Math.Abs(store.Settings.CompanionUi.PanelScale - scale) < 0.001, "Panel scale was not saved.");
                Assert(form.Height <= Screen.FromPoint(form.Location).WorkingArea.Height, "Panel extends below the screen.");
                AssertTopLevelBounds(form);
                if (language == "en") AssertEnglish(form);
            }
        }

        Invoke(form, "SetPanelScale", 1D);
        var writePreviews = args.Contains("--write-previews", StringComparer.OrdinalIgnoreCase);
        var images = writePreviews ? Path.Combine(root, "docs", "images") : Path.Combine(temporary, "previews");
        Directory.CreateDirectory(images);
        foreach (var language in new[] { "en", "zh" })
        {
            Invoke(form, "SetLanguage", language);
            SavePreview(form, Path.Combine(images, $"dashboard-{language}.png"));
        }
        Invoke(form, "UpdateView", CreateUsage(35));
        Invoke(form, "SetPanelScale", 1.75D);
        Assert(form.Height <= Screen.FromPoint(form.Location).WorkingArea.Height, "Many-model panel extends below screen.");
        var list = GetField<Panel>(form, "modelRowsPanel");
        Assert(list.AutoScrollMinSize.Height > list.Height, "Many models should scroll.");
        Assert(list.Controls.Count == 70, "Some model rows were omitted.");
        AssertTopLevelBounds(form);
        list.AutoScrollPosition = new Point(0, 240);
        Invoke(form, "SetLanguage", "en");
        Assert(list.AutoScrollPosition.Y < 0 && list.Controls[0].Top < 0, "Language switch broke scrolled model rows.");
        Invoke(form, "SetLanguage", "zh");
        var unpriced = new DailyUsage();
        unpriced.Add(new UsageRecord { Model = "future-model", Provider = "custom", InputTokens = 700, IsPriced = false });
        unpriced.RecalculateTotals();
        Invoke(form, "UpdateView", unpriced);
        Assert(GetField<Label>(form, "costValueLabel").Text == "—", "Missing pricing must not appear as free usage.");
        Assert(GetField<Label>(form, "pricingStatusLabel").Visible, "Missing pricing notice is hidden.");
        store.Settings.CompanionUi.ButtonMetric = "cost";
        Invoke(form, "SetExpanded", false);
        Assert(GetField<Label>(form, "titleLabel").Text == "—", "Collapsed cost must not show unpriced usage as free.");
        Assert(!GetField<Label>(form, "statusLabel").Visible, "Startup interaction label is still visible.");
        Assert(form.Controls.Cast<Control>().Count(control => control.Visible) <= 1, "Collapsed button contains extra controls.");

        var persisted = new StatsStore(temporary, Path.Combine(temporary, "settings.json"));
        Assert(persisted.Settings.CompanionUi.Language == "zh", "Language did not survive reload.");
        Assert(persisted.Settings.CompanionUi.PanelScale == 1.75D, "Scale did not survive reload.");
        CheckCustomization(temporary);
        form.Close();
        // This harness creates only this unique, checked temporary directory.
        if (Path.GetDirectoryName(temporary) == Path.TrimEndingDirectorySeparator(Path.GetTempPath()))
            Directory.Delete(temporary, recursive: true);
        Console.WriteLine("UI smoke passed: English/Chinese, persistent scale, compact label, 35-model scrolling, unpriced costs, preserved customization drafts." + (writePreviews ? " Previews written to docs/images." : ""));
    }

    private static void CheckCustomization(string temporary)
    {
        var petRoot = Path.Combine(temporary, "fixture-pet");
        var model = Path.Combine(petRoot, "img", "standard", "live2d_models", "current_nuannuan");
        Directory.CreateDirectory(model);
        File.WriteAllText(Path.Combine(petRoot, "config.json"), """
            {"standard":{"l2d":true,"live2d_model":"current_nuannuan","keyboard":[[65]],"hand":[[65]],"face":[[66]],"l2d_expression":[[67]]}}
            """);
        File.WriteAllText(Path.Combine(model, "cat.model3.json"), """
            {"Version":3,"FileReferences":{"Moc":"fixture.moc3","Textures":["texture.png"]}}
            """);
        File.WriteAllBytes(Path.Combine(model, "fixture.moc3"), [0]);
        using (var texture = new Bitmap(16, 16)) texture.Save(Path.Combine(model, "texture.png"));
        var runtime = Path.Combine(petRoot, "img", "standard", "cat_model");
        Directory.CreateDirectory(runtime);
        foreach (var file in Directory.GetFiles(model)) File.Copy(file, Path.Combine(runtime, Path.GetFileName(file)));
        L.SetLanguage("en");
        using var customize = new CustomizationForm(new BongoCatConfigEditor(petRoot));
        customize.Opacity = 0;
        customize.Show();
        AssertEnglish(customize);
        var grid = (DataGridView)typeof(CustomizationForm).GetField("animationGrid", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(customize)!;
        grid.Rows[0].Cells[1].Value = "Ctrl+F9";
        L.SetLanguage("zh");
        customize.ApplyLanguage();
        Assert(customize.Text == "自定义桌宠", "Customization title did not switch to Chinese.");
        L.SetLanguage("en");
        customize.ApplyLanguage();
        AssertEnglish(customize);
        Assert(Convert.ToString(grid.Rows[0].Cells[1].Value) == "Ctrl+F9", "Language switching discarded unsaved bindings.");
        customize.Close();
    }

    private static DailyUsage CreateUsage(int count)
    {
        var names = new[] { "claude-opus-5", "kimi-k2.5", "glm-5", "deepseek-v3.2", "gemini-3.1-pro", "grok-4.1", "gpt-5.4", "qwen3.5-plus" };
        var providers = new[] { "anthropic", "moonshot", "zai", "deepseek", "gemini", "xai", "openai", "qwen" };
        var weights = new[] { 94000, 40000, 28000, 18000, 14000, 11000, 8000, 6000 };
        var usage = new DailyUsage { Date = DateOnly.FromDateTime(DateTime.Now) };
        for (var i = 0; i < count; i++)
            usage.Add(new UsageRecord
            {
                Model = names[i % names.Length] + (i >= names.Length ? $"-{i}" : ""),
                Provider = providers[i % providers.Length],
                InputTokens = weights[i % weights.Length],
                OutputTokens = 4700,
                EstimatedCost = 0.09M + (count - i) * 0.02M,
                IsPriced = true
            });
        usage.RecalculateTotals();
        return usage;
    }

    private static void SavePreview(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void AssertEnglish(Control control)
    {
        if (control is not TextBox && Regex.IsMatch(control.Text, "[\\p{IsCJKUnifiedIdeographs}]"))
            throw new InvalidOperationException($"Untranslated UI: {control.Text}");
        foreach (Control child in control.Controls) AssertEnglish(child);
        if (control is DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns)
                Assert(!Regex.IsMatch(column.HeaderText, "[\\p{IsCJKUnifiedIdeographs}]"), $"Untranslated column: {column.HeaderText}");
            foreach (DataGridViewRow row in grid.Rows)
                foreach (DataGridViewCell cell in row.Cells)
                    Assert(!Regex.IsMatch(Convert.ToString(cell.Value) ?? "", "[\\p{IsCJKUnifiedIdeographs}]"), $"Untranslated cell: {cell.Value}");
        }
        if (control is ComboBox combo)
            foreach (var item in combo.Items)
                Assert(!Regex.IsMatch(item.ToString() ?? "", "[\\p{IsCJKUnifiedIdeographs}]"), $"Untranslated option: {item}");
    }

    private static void AssertTopLevelBounds(Form form)
    {
        foreach (Control control in form.Controls)
            if (control.Visible)
                Assert(form.ClientRectangle.Contains(control.Bounds), $"Control outside panel: {control.GetType().Name}: {control.Text}");
    }

    private static void Invoke(Form1 form, string name, params object[] args)
        => typeof(Form1).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, args);
    private static T GetField<T>(Form1 form, string name)
        => (T)typeof(Form1).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
