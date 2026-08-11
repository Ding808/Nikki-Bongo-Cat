using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace PetStatsOverlay;

public sealed partial class Form1 : Form
{
    private static readonly Size CollapsedBaseSize = new(160, 46);

    private const int ExpandedBaseWidth = 420;
    private const int ModelSectionTitleY = 216;
    private const int ModelRowTopY = 242;
    private const int ModelRowSpacing = 28;
    private const int FooterGapWithoutModelRows = 18;
    private const int FooterGapAfterModelRows = 56;
    private const int FooterBottomPadding = 38;
    private const int BasePositionGap = 8;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WsExNoActivate = 0x08000000;

    private readonly StatsStore store;
    private readonly TokenLogReader tokenLogReader;
    private readonly KeyboardCounter inputCounter;
    private readonly PetWindowController petController = new();
    private readonly BongoCatConfigEditor bongoConfig = new();
    private readonly bool launchPetThroughSteam;
    private readonly System.Windows.Forms.Timer refreshTimer;
    private readonly System.Windows.Forms.Timer inputFlushTimer;
    private readonly System.Windows.Forms.Timer saveTimer;
    private readonly System.Windows.Forms.Timer followTimer;

    private readonly Label titleLabel = new();
    private readonly Label subtitleLabel = new();
    private readonly Label statusLabel = new();
    private readonly Label closeLabel = new();
    private readonly Label callsTitleLabel = new();
    private readonly Label tokenTitleLabel = new();
    private readonly Label costTitleLabel = new();
    private readonly Label typingTitleLabel = new();
    private readonly Label mouseTitleLabel = new();
    private readonly Label callsValueLabel = new();
    private readonly Label callsUnitLabel = new();
    private readonly Label tokenValueLabel = new();
    private readonly Label costValueLabel = new();
    private readonly Label typingValueLabel = new();
    private readonly Label mouseValueLabel = new();
    private readonly Label providerTitleLabel = new();
    private readonly List<Label> providerLineLabels = new();
    private readonly List<Label> providerPercentLabels = new();
    private readonly Label updatedLabel = new();
    private readonly Button advancedButton = new();
    private readonly ComboBox buttonMetricCombo = new();
    private readonly CheckBox alwaysVisibleCheckBox = new();
    private readonly Button customizePetButton = new();
    private readonly Button restartPetButton = new();
    private readonly NotifyIcon trayIcon = new();
    private CustomizationForm? customizationForm;

    private List<(string Name, int Percent)> providerRowsForDisplay = new();

    private DailyUsage cachedUsage = new();
    private long pendingTypingCount;
    private long pendingMouseClickCount;
    private bool expanded;
    private bool hasUnsavedInput;
    private bool usageRefreshRunning;
    private bool hasSeenPetWindow;
    private bool entryVisible;
    private bool overlayClickThrough;
    private double currentUiScale = 1D;
    private DateTime suppressPetMissingCloseUntil = DateTime.MinValue;

    public Form1(bool launchPetThroughSteam = false)
    {
        InitializeComponent();
        this.launchPetThroughSteam = launchPetThroughSteam;

        store = new StatsStore();
        tokenLogReader = new TokenLogReader(store.Settings, store.DataDirectory);
        inputCounter = new KeyboardCounter();
        inputCounter.TextKeyPressed += (_, _) => Interlocked.Increment(ref pendingTypingCount);
        inputCounter.MouseClicked += (_, _) => Interlocked.Increment(ref pendingMouseClickCount);
        inputCounter.LockRequested += (_, _) => SetPetLock(true);
        inputCounter.UnlockRequested += (_, _) => SetPetLock(false);

        refreshTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        refreshTimer.Tick += (_, _) => BeginUsageRefresh();

        inputFlushTimer = new System.Windows.Forms.Timer { Interval = 250 };
        inputFlushTimer.Tick += (_, _) => FlushInputToMemory();

        saveTimer = new System.Windows.Forms.Timer { Interval = 5_000 };
        saveTimer.Tick += (_, _) => SaveInputIfNeeded();

        followTimer = new System.Windows.Forms.Timer { Interval = 16 };
        followTimer.Tick += (_, _) =>
        {
            FollowPet();
            petController.ApplyLockState();
        };

        ConfigureWindow();
        BuildUi();
        SetExpanded(false);
        HideStatsWindow();
        UpdateView(cachedUsage);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExNoActivate;
            return parameters;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        inputCounter.Start();
        refreshTimer.Start();
        inputFlushTimer.Start();
        saveTimer.Start();
        followTimer.Start();
        BeginUsageRefresh();
        FollowPet();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        refreshTimer.Stop();
        inputFlushTimer.Stop();
        saveTimer.Stop();
        followTimer.Stop();
        FlushInputToMemory();
        SaveInputIfNeeded(force: true);
        inputCounter.Dispose();
        customizationForm?.Close();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        base.OnFormClosing(e);
    }

    private void ConfigureWindow()
    {
        Text = "Today Companion";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(255, 247, 250);
        ForeColor = Color.FromArgb(126, 70, 92);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        DoubleBuffered = true;
        Click += ExpandFromClick;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Reset today", null, (_, _) =>
        {
            Interlocked.Exchange(ref pendingTypingCount, 0);
            Interlocked.Exchange(ref pendingMouseClickCount, 0);
            store.ResetToday();
            hasUnsavedInput = false;
            UpdateView(cachedUsage);
        });
        menu.Items.Add("Lock pet (Num -)", null, (_, _) => SetPetLock(true));
        menu.Items.Add("Unlock pet (Num +)", null, (_, _) => SetPetLock(false));
        menu.Items.Add("Customize pet", null, (_, _) => OpenCustomizationForm());
        menu.Items.Add("Open settings", null, (_, _) => Process.Start(new ProcessStartInfo
        {
            FileName = store.SettingsPath,
            UseShellExecute = true
        }));
        menu.Items.Add("Open data folder", null, (_, _) => Process.Start(new ProcessStartInfo
        {
            FileName = store.DataDirectory,
            UseShellExecute = true
        }));
        menu.Items.Add("Exit", null, (_, _) => Close());
        ContextMenuStrip = menu;
        ConfigureTrayIcon();
    }

    private void ConfigureTrayIcon()
    {
        var petPath = Path.Combine(bongoConfig.RootDirectory, "BongoCatMver.exe");
        trayIcon.Icon = File.Exists(petPath) ? Icon.ExtractAssociatedIcon(petPath) ?? SystemIcons.Application : SystemIcons.Application;
        trayIcon.Text = "Bongo Cat Mver 自定义工具";
        trayIcon.Visible = true;
        trayIcon.DoubleClick += (_, _) =>
        {
            SetExpanded(true);
            ShowStatsWindow();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开统计面板", null, (_, _) =>
        {
            SetExpanded(true);
            ShowStatsWindow();
        });
        menu.Items.Add("自定义桌宠", null, (_, _) => OpenCustomizationForm());
        menu.Items.Add("重启桌宠", null, (_, _) => RestartPetAndShowOverlay());
        menu.Items.Add("退出统计浮窗", null, (_, _) => Close());
        trayIcon.ContextMenuStrip = menu;
    }

    private void BuildUi()
    {
        Controls.Clear();

        foreach (var label in new[]
        {
            titleLabel, subtitleLabel, statusLabel, callsTitleLabel, tokenTitleLabel,
            costTitleLabel, typingTitleLabel, mouseTitleLabel, callsValueLabel,
            callsUnitLabel, tokenValueLabel, costValueLabel, typingValueLabel,
            mouseValueLabel, providerTitleLabel, updatedLabel
        })
        {
            label.AutoSize = false;
            label.BackColor = Color.Transparent;
            label.Click += ExpandFromClick;
        }

        titleLabel.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold, GraphicsUnit.Point);
        titleLabel.ForeColor = Color.FromArgb(233, 90, 140);
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        subtitleLabel.ForeColor = Color.FromArgb(154, 102, 124);

        statusLabel.ForeColor = Color.FromArgb(224, 110, 145);
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;

        closeLabel.Text = "\u00d7";
        closeLabel.AutoSize = false;
        closeLabel.TextAlign = ContentAlignment.MiddleCenter;
        closeLabel.ForeColor = Color.FromArgb(236, 150, 175);
        closeLabel.BackColor = Color.Transparent;
        closeLabel.Cursor = Cursors.Hand;
        closeLabel.Click += (_, _) => SetExpanded(false);

        // Column titles inside the white stats card.
        foreach (var label in new[] { callsTitleLabel, tokenTitleLabel, costTitleLabel, typingTitleLabel, mouseTitleLabel })
        {
            label.Font = new Font(Font.FontFamily, 9F, FontStyle.Regular, GraphicsUnit.Point);
            label.ForeColor = Color.FromArgb(150, 120, 135);
            label.TextAlign = ContentAlignment.MiddleCenter;
        }
        typingTitleLabel.Text = "\u2328\ufe0f \u6253\u5b57\u6b21\u6570";
        mouseTitleLabel.Text = "\ud83d\uddb1\ufe0f \u70b9\u51fb\u6b21\u6570";
        callsTitleLabel.Text = "\ud83d\udcac \u5bf9\u8bdd\u6b21\u6570";
        tokenTitleLabel.Text = "\ud83d\udce6 Token \u6d88\u8017";
        costTitleLabel.Text = "\ud83d\udc9c \u82b1\u8d39";

        foreach (var label in new[] { callsValueLabel, tokenValueLabel, costValueLabel, typingValueLabel, mouseValueLabel })
        {
            label.Font = new Font(Font.FontFamily, 15F, FontStyle.Bold, GraphicsUnit.Point);
            label.ForeColor = Color.FromArgb(244, 101, 145);
            label.TextAlign = ContentAlignment.MiddleCenter;
        }

        callsUnitLabel.Text = "\u6b21";
        callsUnitLabel.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        callsUnitLabel.ForeColor = Color.FromArgb(150, 120, 135);
        callsUnitLabel.TextAlign = ContentAlignment.BottomLeft;

        providerTitleLabel.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold, GraphicsUnit.Point);
        providerTitleLabel.ForeColor = Color.FromArgb(233, 90, 140);
        providerTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        updatedLabel.ForeColor = Color.FromArgb(178, 126, 148);
        updatedLabel.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        updatedLabel.TextAlign = ContentAlignment.MiddleLeft;

        // The "\u9ad8\u7ea7\u8be6\u60c5" pill collects every secondary control/action.
        advancedButton.Text = "\u9ad8\u7ea7\u8be6\u60c5";
        advancedButton.FlatStyle = FlatStyle.Flat;
        advancedButton.BackColor = Color.White;
        advancedButton.ForeColor = Color.FromArgb(224, 110, 145);
        advancedButton.FlatAppearance.BorderColor = Color.FromArgb(255, 190, 210);
        advancedButton.FlatAppearance.BorderSize = 1;
        advancedButton.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        advancedButton.Cursor = Cursors.Hand;
        advancedButton.Click += (_, _) => ShowAdvancedMenu();

        // Secondary controls are kept alive (state still bound) but not shown on the panel;
        // they are surfaced through the advanced menu instead.
        buttonMetricCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        buttonMetricCombo.Items.AddRange(
        [
            new MetricOption("companion", "\u4eca\u65e5\u966a\u4f34"),
            new MetricOption("tokens", "Token"),
            new MetricOption("typing", "\u6253\u5b57"),
            new MetricOption("mouse", "\u70b9\u51fb"),
            new MetricOption("cost", "\u82b1\u8d39"),
            new MetricOption("calls", "\u5bf9\u8bdd")
        ]);
        buttonMetricCombo.SelectedIndexChanged += (_, _) =>
        {
            if (buttonMetricCombo.SelectedItem is MetricOption option)
            {
                store.Settings.CompanionUi.ButtonMetric = option.Key;
                store.SaveSettings();
                UpdateView(cachedUsage);
            }
        };

        alwaysVisibleCheckBox.CheckedChanged += (_, _) =>
        {
            store.Settings.CompanionUi.ButtonAlwaysVisible = alwaysVisibleCheckBox.Checked;
            store.SaveSettings();
            if (alwaysVisibleCheckBox.Checked)
            {
                ShowStatsWindow();
            }
        };

        SelectMetricOption(store.Settings.CompanionUi.ButtonMetric);
        alwaysVisibleCheckBox.Checked = store.Settings.CompanionUi.ButtonAlwaysVisible;
        ConfigurePetControls();

        Controls.AddRange(
        [
            titleLabel,
            statusLabel,
            closeLabel,
            typingTitleLabel,
            mouseTitleLabel,
            typingValueLabel,
            mouseValueLabel,
            callsTitleLabel,
            tokenTitleLabel,
            costTitleLabel,
            callsValueLabel,
            callsUnitLabel,
            tokenValueLabel,
            costValueLabel,
            providerTitleLabel,
            updatedLabel,
            advancedButton
        ]);
    }

    private void EnsureProviderRowLabels(int rowCount)
    {
        while (providerLineLabels.Count < rowCount)
        {
            var lineLabel = CreateProviderLineLabel();
            var percentLabel = CreateProviderPercentLabel();
            providerLineLabels.Add(lineLabel);
            providerPercentLabels.Add(percentLabel);
            Controls.Add(lineLabel);
            Controls.Add(percentLabel);
        }
    }

    private Label CreateProviderLineLabel()
    {
        var label = new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = ScaledFont(9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(136, 82, 105),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Visible = false
        };
        label.Click += ExpandFromClick;
        return label;
    }

    private Label CreateProviderPercentLabel()
    {
        var label = new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = ScaledFont(9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(224, 110, 145),
            TextAlign = ContentAlignment.MiddleRight,
            Visible = false
        };
        label.Click += ExpandFromClick;
        return label;
    }

    private void SetExpanded(bool value)
    {
        expanded = value;
        SetFixedSizeAndLayout();
        Invalidate();
        FollowPet();
        UpdateView(cachedUsage);
    }

    private void LayoutUi()
    {
        ApplyScaledFonts();

        if (!expanded)
        {
            SetBoundsScaled(titleLabel, 16, 11, 128, 24);
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            foreach (var control in Controls.Cast<Control>())
            {
                control.Visible = false;
            }
            titleLabel.Visible = true;
            return;
        }

        titleLabel.TextAlign = ContentAlignment.MiddleLeft;

        // Header: 🌸 今日陪伴 ✨ ........ ×
        SetBoundsScaled(titleLabel, 18, 14, 300, 28);
        SetBoundsScaled(closeLabel, 386, 14, 22, 22);
        statusLabel.Visible = false;
        subtitleLabel.Visible = false;

        // White stats card (drawn in OnPaint at 16,50,388,152) — two rows.
        // Top row: 2 columns (16..210 | 210..404). Bottom row: 3 columns (16..145 | 145..274 | 274..404).
        SetBoundsScaled(typingTitleLabel, 16, 56, 194, 20);
        SetBoundsScaled(mouseTitleLabel, 210, 56, 194, 20);
        SetBoundsScaled(typingValueLabel, 16, 80, 194, 38);
        SetBoundsScaled(mouseValueLabel, 210, 80, 194, 38);

        SetBoundsScaled(callsTitleLabel, 16, 134, 129, 20);
        SetBoundsScaled(tokenTitleLabel, 145, 134, 129, 20);
        SetBoundsScaled(costTitleLabel, 274, 134, 130, 20);

        SetBoundsScaled(callsValueLabel, 24, 158, 74, 38);
        SetBoundsScaled(callsUnitLabel, 100, 170, 24, 24);
        SetBoundsScaled(tokenValueLabel, 145, 158, 129, 38);
        SetBoundsScaled(costValueLabel, 274, 158, 130, 38);

        // Model usage section.
        var modelRowCount = providerRowsForDisplay.Count;
        providerTitleLabel.Visible = modelRowCount > 0;
        if (modelRowCount > 0)
        {
            SetBoundsScaled(providerTitleLabel, 18, ModelSectionTitleY, 220, 20);
        }

        for (var index = 0; index < providerLineLabels.Count; index++)
        {
            var isVisible = index < modelRowCount;
            providerLineLabels[index].Visible = isVisible;
            providerPercentLabels[index].Visible = isVisible;
            if (!isVisible)
            {
                continue;
            }

            var rowY = GetModelRowY(index);
            SetBoundsScaled(providerLineLabels[index], 20, rowY, 168, 18);
            SetBoundsScaled(providerPercentLabels[index], 356, rowY - 2, 48, 18);
        }

        // Footer.
        var footerY = GetFooterY();
        SetBoundsScaled(updatedLabel, 20, footerY, 190, 24);
        SetBoundsScaled(advancedButton, 300, footerY - 4, 104, 30);
        ApplyPillRegion(advancedButton);

        typingTitleLabel.Visible = true;
        mouseTitleLabel.Visible = true;
        typingValueLabel.Visible = true;
        mouseValueLabel.Visible = true;
        callsTitleLabel.Visible = true;
        tokenTitleLabel.Visible = true;
        costTitleLabel.Visible = true;
        callsValueLabel.Visible = true;
        callsUnitLabel.Visible = true;
        tokenValueLabel.Visible = true;
        costValueLabel.Visible = true;
        titleLabel.Visible = true;
        closeLabel.Visible = true;
        updatedLabel.Visible = true;
        advancedButton.Visible = true;
    }

    private void UpdateScaledSizeAndLayout(Rectangle? petRect)
    {
        SetFixedSizeAndLayout();
    }

    private void SetFixedSizeAndLayout()
    {
        const double fixedScale = 1D;
        var baseSize = expanded ? GetExpandedBaseSize() : CollapsedBaseSize;
        var nextSize = baseSize;

        if (ClientSize != nextSize || Math.Abs(currentUiScale - fixedScale) > 0.01D)
        {
            currentUiScale = fixedScale;
            ClientSize = nextSize;
            LayoutUi();
        }
        else
        {
            currentUiScale = fixedScale;
        }

        ApplyWindowRegion();
    }

    private Size GetExpandedBaseSize()
    {
        return new Size(ExpandedBaseWidth, GetFooterY() + FooterBottomPadding);
    }

    private int GetFooterY()
    {
        return providerRowsForDisplay.Count == 0
            ? 202 + FooterGapWithoutModelRows
            : ModelRowTopY + providerRowsForDisplay.Count * ModelRowSpacing + FooterGapAfterModelRows;
    }

    private static int GetModelRowY(int index)
    {
        return ModelRowTopY + index * ModelRowSpacing;
    }

    private Size lastRegionSize = Size.Empty;

    /// <summary>
    /// Clips the rectangular window to a rounded shape so the four corners are
    /// physically cut away — without this the painted rounded card still sits
    /// inside a rectangular window and the corners show through.
    /// </summary>
    private void ApplyWindowRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        var size = new Size(Width, Height);
        if (size == lastRegionSize && Region is not null)
        {
            return;
        }

        lastRegionSize = size;
        var radius = Scale(22) * 2;
        var path = new GraphicsPath();
        // +1 keeps the painted 2px border from being clipped on the right/bottom edge.
        path.AddArc(0, 0, radius, radius, 180, 90);
        path.AddArc(Width - radius, 0, radius, radius, 270, 90);
        path.AddArc(Width - radius, Height - radius, radius, radius, 0, 90);
        path.AddArc(0, Height - radius, radius, radius, 90, 90);
        path.CloseFigure();

        var oldRegion = Region;
        Region = new Region(path);
        path.Dispose();
        oldRegion?.Dispose();
    }

    private void ApplyScaledFonts()
    {
        titleLabel.Font = ScaledFont(expanded ? 13F : 10.5F, FontStyle.Bold);
        statusLabel.Font = ScaledFont(8.5F, FontStyle.Regular);
        closeLabel.Font = ScaledFont(13F, FontStyle.Regular);
        providerTitleLabel.Font = ScaledFont(9.5F, FontStyle.Bold);
        updatedLabel.Font = ScaledFont(8.5F, FontStyle.Regular);
        advancedButton.Font = ScaledFont(8.5F, FontStyle.Regular);

        foreach (var label in new[] { callsTitleLabel, tokenTitleLabel, costTitleLabel, typingTitleLabel, mouseTitleLabel })
        {
            label.Font = ScaledFont(9F, FontStyle.Regular);
        }

        foreach (var label in new[] { callsValueLabel, tokenValueLabel, costValueLabel, typingValueLabel, mouseValueLabel })
        {
            label.Font = ScaledFont(16F, FontStyle.Bold);
        }

        callsUnitLabel.Font = ScaledFont(8.5F, FontStyle.Regular);

        foreach (var label in providerLineLabels)
        {
            label.Font = ScaledFont(9F, FontStyle.Regular);
        }

        foreach (var label in providerPercentLabels)
        {
            label.Font = ScaledFont(9F, FontStyle.Bold);
        }
    }

    private Font ScaledFont(float size, FontStyle style)
    {
        return new Font(Font.FontFamily, Math.Max(6F, size * (float)currentUiScale), style, GraphicsUnit.Point);
    }

    private void SetBoundsScaled(Control control, int x, int y, int width, int height)
    {
        control.SetBounds(Scale(x), Scale(y), Scale(width), Scale(height));
    }

    private void ConfigurePetControls()
    {
        ConfigureSoftButton(customizePetButton, "自定义");
        ConfigureSoftButton(restartPetButton, "\u91cd\u542f\u684c\u5ba0");

        customizePetButton.Click += (_, _) => OpenCustomizationForm();
        restartPetButton.Click += (_, _) => RestartPetAndShowOverlay();
    }

    private void OpenCustomizationForm()
    {
        try
        {
            if (customizationForm is null || customizationForm.IsDisposed)
            {
                customizationForm = new CustomizationForm(bongoConfig);
                customizationForm.RestartAndShowRequested += (_, _) => RestartPetAndShowOverlay();
                customizationForm.FormClosed += (_, _) => customizationForm = null;
            }

            ShowCustomizationFormInFront();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "自定义桌宠打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowCustomizationFormInFront()
    {
        if (customizationForm is null || customizationForm.IsDisposed)
        {
            return;
        }

        if (customizationForm.WindowState == FormWindowState.Minimized)
        {
            customizationForm.WindowState = FormWindowState.Normal;
        }

        if (!customizationForm.Visible)
        {
            customizationForm.Show();
        }

        customizationForm.ShowInTaskbar = true;
        customizationForm.TopMost = true;
        customizationForm.BringToFront();
        customizationForm.Activate();
        customizationForm.TopMost = false;
    }

    private void RestartPetAndShowOverlay()
    {
        suppressPetMissingCloseUntil = DateTime.UtcNow.AddSeconds(8);
        SetExpanded(true);
        ShowStatsWindow();
        bongoConfig.RestartPet(launchPetThroughSteam);

        var attempts = 0;
        var retryTimer = new System.Windows.Forms.Timer { Interval = 500 };
        retryTimer.Tick += (_, _) =>
        {
            attempts++;
            FollowPet();
            SetExpanded(true);
            ShowStatsWindow();

            if (petController.GetPetRect() is not null || attempts >= 16)
            {
                retryTimer.Stop();
                retryTimer.Dispose();
            }
        };
        retryTimer.Start();
    }

    private void ConfigureSoftButton(Button button, string text)
    {
        button.Text = text;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Color.FromArgb(255, 232, 241);
        button.ForeColor = Color.FromArgb(224, 110, 145);
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
    }

    private void ShowAdvancedMenu()
    {
        var menu = new ContextMenuStrip
        {
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            BackColor = Color.FromArgb(255, 247, 250),
            ForeColor = Color.FromArgb(126, 70, 92),
            ShowImageMargin = true,
            Renderer = new ToolStripProfessionalRenderer(new PinkColorTable()) { RoundedEdges = true },
            Padding = new Padding(4)
        };

        // Always-visible toggle.
        var alwaysItem = new ToolStripMenuItem("常驻入口")
        {
            CheckOnClick = true,
            Checked = store.Settings.CompanionUi.ButtonAlwaysVisible
        };
        alwaysItem.CheckedChanged += (_, _) =>
        {
            store.Settings.CompanionUi.ButtonAlwaysVisible = alwaysItem.Checked;
            alwaysVisibleCheckBox.Checked = alwaysItem.Checked;
            store.SaveSettings();
            if (alwaysItem.Checked)
            {
                ShowStatsWindow();
            }
        };
        menu.Items.Add(alwaysItem);

        // Collapsed-pill display metric.
        var metricItem = new ToolStripMenuItem("浮窗显示指标");
        foreach (var item in buttonMetricCombo.Items)
        {
            if (item is not MetricOption option)
            {
                continue;
            }

            var child = new ToolStripMenuItem(option.Label)
            {
                Checked = string.Equals(option.Key, store.Settings.CompanionUi.ButtonMetric, StringComparison.OrdinalIgnoreCase)
            };
            child.Click += (_, _) =>
            {
                store.Settings.CompanionUi.ButtonMetric = option.Key;
                store.SaveSettings();
                SelectMetricOption(option.Key);
                UpdateView(cachedUsage);
            };
            metricItem.DropDownItems.Add(child);
        }
        menu.Items.Add(metricItem);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("自定义桌宠", null, (_, _) => OpenCustomizationForm());
        menu.Items.Add("重启桌宠", null, (_, _) => RestartPetAndShowOverlay());
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("重置今日", null, (_, _) =>
        {
            Interlocked.Exchange(ref pendingTypingCount, 0);
            Interlocked.Exchange(ref pendingMouseClickCount, 0);
            store.ResetToday();
            hasUnsavedInput = false;
            UpdateView(cachedUsage);
        });
        menu.Items.Add(petController.IsLocked ? "解锁桌宠" : "锁定桌宠", null, (_, _) => SetPetLock(!petController.IsLocked));
        menu.Items.Add("打开设置", null, (_, _) => Process.Start(new ProcessStartInfo
        {
            FileName = store.SettingsPath,
            UseShellExecute = true
        }));
        menu.Items.Add("打开数据文件夹", null, (_, _) => Process.Start(new ProcessStartInfo
        {
            FileName = store.DataDirectory,
            UseShellExecute = true
        }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出统计浮窗", null, (_, _) => Close());

        // Pop up above the pill so it does not cover the panel.
        menu.Show(advancedButton, new Point(0, -menu.GetPreferredSize(Size.Empty).Height));
    }

    /// <summary>Soft pink palette so the advanced menu matches the companion card.</summary>
    private sealed class PinkColorTable : ProfessionalColorTable
    {
        public PinkColorTable()
        {
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground => Color.FromArgb(255, 247, 250);
        public override Color MenuBorder => Color.FromArgb(255, 177, 202);
        public override Color MenuItemBorder => Color.FromArgb(255, 150, 182);

        public override Color MenuItemSelected => Color.FromArgb(255, 224, 235);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(255, 226, 237);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(255, 214, 228);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(255, 219, 231);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(255, 208, 223);

        public override Color ImageMarginGradientBegin => Color.FromArgb(255, 240, 245);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(255, 236, 242);
        public override Color ImageMarginGradientEnd => Color.FromArgb(255, 232, 240);

        public override Color CheckBackground => Color.FromArgb(255, 200, 218);
        public override Color CheckSelectedBackground => Color.FromArgb(255, 182, 205);
        public override Color CheckPressedBackground => Color.FromArgb(255, 182, 205);

        public override Color SeparatorDark => Color.FromArgb(255, 205, 220);
        public override Color SeparatorLight => Color.FromArgb(255, 248, 251);
    }

    private void UpdateView(DailyUsage usage)
    {
        var providerRows = GetProviderRows(usage).ToList();
        var providerRowCountChanged = providerRows.Count != providerRowsForDisplay.Count;
        providerRowsForDisplay = providerRows;
        EnsureProviderRowLabels(providerRowsForDisplay.Count);
        if (expanded && providerRowCountChanged)
        {
            SetFixedSizeAndLayout();
        }

        titleLabel.Text = expanded ? "\ud83c\udf38 \u4eca\u65e5\u966a\u4f34 \u2728" : GetButtonText(usage);
        statusLabel.Text = petController.IsLocked ? "\u5df2\u9501\u5b9a" : "\u53ef\u4ea4\u4e92";
        callsValueLabel.Text = $"{usage.RecordCount:N0}";
        tokenValueLabel.Text = FormatTokens(usage.TotalTokens);
        costValueLabel.Text = FormatMoney(usage.EstimatedCost);
        typingValueLabel.Text = $"{store.Today.TypingCount:N0}";
        mouseValueLabel.Text = $"{store.Today.MouseClickCount:N0}";
        providerTitleLabel.Text = "\ud83d\udcca \u6a21\u578b\u4f7f\u7528\u5360\u6bd4";
        for (var index = 0; index < providerRowsForDisplay.Count; index++)
        {
            providerLineLabels[index].Text = providerRowsForDisplay[index].Name;
            providerPercentLabels[index].Text = $"{providerRowsForDisplay[index].Percent}%";
        }
        updatedLabel.Text = DateTime.Now.ToString("'\u66f4\u65b0\u4e8e' HH:mm", CultureInfo.InvariantCulture);
        if (alwaysVisibleCheckBox.Checked != store.Settings.CompanionUi.ButtonAlwaysVisible)
        {
            alwaysVisibleCheckBox.Checked = store.Settings.CompanionUi.ButtonAlwaysVisible;
        }

        if (expanded)
        {
            Invalidate();
        }
    }

    private void FlushInputToMemory()
    {
        var typing = Interlocked.Exchange(ref pendingTypingCount, 0);
        var clicks = Interlocked.Exchange(ref pendingMouseClickCount, 0);
        if (typing <= 0 && clicks <= 0)
        {
            return;
        }

        store.AddTyping(typing);
        store.AddMouseClicks(clicks);
        hasUnsavedInput = true;
        UpdateView(cachedUsage);
    }

    private void SaveInputIfNeeded(bool force = false)
    {
        if (!force && !hasUnsavedInput)
        {
            return;
        }

        store.Save();
        hasUnsavedInput = false;
    }

    private void BeginUsageRefresh()
    {
        if (usageRefreshRunning)
        {
            return;
        }

        usageRefreshRunning = true;
        Task.Run(() =>
        {
            try
            {
                return tokenLogReader.GetTodayUsage();
            }
            catch
            {
                return cachedUsage;
            }
        }).ContinueWith(task =>
        {
            usageRefreshRunning = false;
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            BeginInvoke(() =>
            {
                cachedUsage = task.Result;
                UpdateView(cachedUsage);
            });
        }, TaskScheduler.Default);
    }

    private void SetPetLock(bool locked)
    {
        if (locked)
        {
            petController.Lock();
        }
        else
        {
            petController.Unlock();
        }

        SetOverlayClickThrough(false);
        UpdateView(cachedUsage);
    }

    private void FollowPet()
    {
        var petRect = petController.GetPetRect();
        if (petRect is null)
        {
            if (hasSeenPetWindow && DateTime.UtcNow <= suppressPetMissingCloseUntil)
            {
                ShowStatsWindow();
                return;
            }

            if (hasSeenPetWindow)
            {
                Close();
                return;
            }

            var fallbackArea = Screen.FromPoint(Location).WorkingArea;
            UpdateScaledSizeAndLayout(null);
            var fallback = new Point(fallbackArea.Right - Width - 26, fallbackArea.Bottom - Height - 180);
            MoveIfNeeded(ClampToWorkingArea(fallback, fallbackArea));
            if (!expanded)
            {
                HideStatsWindow();
            }
            return;
        }

        hasSeenPetWindow = true;
        UpdateScaledSizeAndLayout(petRect.Value);

        var area = Screen.FromRectangle(petRect.Value).WorkingArea;
        MoveIfNeeded(FindFollowLocation(petRect.Value, area));
        UpdateEntryVisibility(petRect.Value);
    }

    private Point FindFollowLocation(Rectangle petRect, Rectangle area)
    {
        var gap = Scale(BasePositionGap);
        var centeredX = petRect.Left + petRect.Width / 2 - Width / 2;
        var target = expanded
            ? new Point(centeredX, petRect.Bottom + gap)
            : new Point(centeredX, petRect.Top - Height - gap);

        return ClampToWorkingArea(target, area);
    }

    private void UpdateEntryVisibility(Rectangle petRect)
    {
        if (expanded)
        {
            ShowStatsWindow();
            return;
        }

        if (overlayClickThrough)
        {
            HideStatsWindow();
            return;
        }

        var hoverArea = Rectangle.Inflate(petRect, 70, 70);
        var cursor = Cursor.Position;
        var shouldShow = store.Settings.CompanionUi.ButtonAlwaysVisible
            || hoverArea.Contains(cursor)
            || (Visible && Bounds.Contains(cursor));
        if (shouldShow)
        {
            ShowStatsWindow();
        }
        else
        {
            HideStatsWindow();
        }
    }

    private void ShowStatsWindow()
    {
        if (entryVisible && Visible)
        {
            return;
        }

        entryVisible = true;
        Opacity = 1D;
        Show();
    }

    private void HideStatsWindow()
    {
        if (!entryVisible && !Visible)
        {
            return;
        }

        entryVisible = false;
        Hide();
    }

    private Point ClampToWorkingArea(Point point, Rectangle area)
    {
        return new Point(
            ClampCoordinate(point.X, area.Left + 4, area.Right - Width - 4),
            ClampCoordinate(point.Y, area.Top + 4, area.Bottom - Height - 4));
    }

    private static int ClampCoordinate(int value, int min, int max)
    {
        return max < min ? min : Math.Clamp(value, min, max);
    }

    private Point GetLeastOverlappingLocation(IEnumerable<Point> candidates, Rectangle area, Rectangle petSafeRect)
    {
        return candidates
            .Select(candidate =>
            {
                var location = ClampToWorkingArea(candidate, area);
                var bounds = new Rectangle(location, Size);
                return (Location: location, Overlap: GetIntersectionArea(bounds, petSafeRect));
            })
            .OrderBy(candidate => candidate.Overlap)
            .ThenBy(candidate => DistanceSquared(candidate.Location, Location))
            .First()
            .Location;
    }

    private void MoveIfNeeded(Point nextLocation)
    {
        if (Location != nextLocation)
        {
            Location = nextLocation;
        }
    }

    private void ExpandFromClick(object? sender, EventArgs e)
    {
        if (!expanded)
        {
            SetExpanded(true);
            ShowStatsWindow();
        }
    }

    private static IEnumerable<(string Name, int Percent)> GetProviderRows(DailyUsage usage)
    {
        var total = Math.Max(1, usage.TotalTokens);
        var rows = usage.Providers
            .SelectMany(provider => provider.Records)
            .Where(record => record.TotalTokens > 0 || record.EstimatedCost > 0)
            .GroupBy(ModelBreakdownKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Name = group.Key,
                TotalTokens = group.Sum(record => record.TotalTokens),
                EstimatedCost = group.Sum(record => record.EstimatedCost)
            })
            .OrderByDescending(model => model.TotalTokens)
            .ThenByDescending(model => model.EstimatedCost)
            .Select(model => (
                Name: model.Name,
                Percent: (int)Math.Clamp(model.TotalTokens * 100 / total, 0, 100)))
            .ToList();

        return rows;
    }

    private static string ModelBreakdownKey(UsageRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.Model))
        {
            return record.Model;
        }

        if (!string.IsNullOrWhiteSpace(record.Provider))
        {
            return $"{record.Provider} unknown";
        }

        return "\u672a\u77e5\u6a21\u578b";
    }

    private string GetButtonText(DailyUsage usage)
    {
        return store.Settings.CompanionUi.ButtonMetric switch
        {
            "tokens" => $"{FormatTokens(usage.TotalTokens)} Token",
            "typing" => $"\u6253\u5b57 {store.Today.TypingCount:N0}",
            "mouse" => $"\u70b9\u51fb {store.Today.MouseClickCount:N0}",
            "cost" => FormatMoney(usage.EstimatedCost),
            "calls" => $"\u5bf9\u8bdd {usage.RecordCount:N0}",
            _ => "\u4eca\u65e5\u966a\u4f34"
        };
    }

    private void SelectMetricOption(string key)
    {
        foreach (var item in buttonMetricCombo.Items)
        {
            if (item is MetricOption option && string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                buttonMetricCombo.SelectedItem = item;
                return;
            }
        }

        if (buttonMetricCombo.Items.Count > 0)
        {
            buttonMetricCombo.SelectedIndex = 0;
        }
    }

    private static string FormatTokens(long tokens)
    {
        if (tokens >= 1_000_000)
        {
            return $"{tokens / 1_000_000D:0.##}M";
        }

        if (tokens >= 1_000)
        {
            return $"{tokens / 1_000D:0.#}K";
        }

        return tokens.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatMoney(decimal value)
    {
        return value < 0.01M && value > 0M
            ? "$<0.01"
            : value.ToString("$0.00", CultureInfo.InvariantCulture);
    }

    private int Scale(int value)
    {
        return Math.Max(1, (int)Math.Round(value * currentUiScale));
    }

    private Size ScaleSize(Size size, double scale)
    {
        return new Size(
            Math.Max(1, (int)Math.Round(size.Width * scale)),
            Math.Max(1, (int)Math.Round(size.Height * scale)));
    }

    private Rectangle ScaleRect(int x, int y, int width, int height)
    {
        return new Rectangle(Scale(x), Scale(y), Scale(width), Scale(height));
    }

    private static int GetIntersectionArea(Rectangle left, Rectangle right)
    {
        var intersection = Rectangle.Intersect(left, right);
        return intersection.IsEmpty ? 0 : intersection.Width * intersection.Height;
    }

    private static long DistanceSquared(Point left, Point right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return (long)dx * dx + (long)dy * dy;
    }

    private void SetOverlayClickThrough(bool value)
    {
        if (overlayClickThrough == value || !IsHandleCreated)
        {
            overlayClickThrough = value;
            return;
        }

        overlayClickThrough = value;
        var style = GetWindowLongPtr(Handle, GwlExStyle);
        var nextStyle = value
            ? style | WsExLayered | WsExTransparent
            : style & ~WsExTransparent;
        SetWindowLongPtr(Handle, GwlExStyle, nextStyle);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (overlayClickThrough)
        {
            var style = GetWindowLongPtr(Handle, GwlExStyle);
            SetWindowLongPtr(Handle, GwlExStyle, style | WsExLayered | WsExTransparent);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect(rect, Scale(expanded ? 22 : 22));
        using var fill = new LinearGradientBrush(rect, Color.FromArgb(255, 253, 254), Color.FromArgb(255, 235, 243), 90F);
        using var border = new Pen(Color.FromArgb(255, 177, 202), 2F);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        if (!expanded)
        {
            return;
        }

        // Single white stats card: top row (2 cells) + bottom row (3 cells).
        var card = ScaleRect(16, 50, 388, 152);
        DrawSoftPanel(e.Graphics, card, currentUiScale);
        using (var divider = new Pen(Color.FromArgb(60, 255, 190, 210), 1F))
        {
            // Horizontal divider between the two rows.
            var rowSplitY = Scale(126);
            e.Graphics.DrawLine(divider, card.Left + Scale(16), rowSplitY, card.Right - Scale(16), rowSplitY);

            // Top row: single vertical divider (2 columns).
            var topTop = card.Top + Scale(16);
            var topBottom = rowSplitY - Scale(8);
            e.Graphics.DrawLine(divider, Scale(210), topTop, Scale(210), topBottom);

            // Bottom row: two vertical dividers (3 columns).
            var botTop = rowSplitY + Scale(8);
            var botBottom = card.Bottom - Scale(16);
            e.Graphics.DrawLine(divider, Scale(145), botTop, Scale(145), botBottom);
            e.Graphics.DrawLine(divider, Scale(274), botTop, Scale(274), botBottom);
        }

        for (var index = 0; index < providerRowsForDisplay.Count; index++)
        {
            DrawUsageBar(e.Graphics, ScaleRect(196, GetModelRowY(index) + 4, 154, 10), providerRowsForDisplay[index].Percent);
        }
    }

    private static void DrawUsageBar(Graphics graphics, Rectangle rect, int percent)
    {
        // Radius must be half the height so the ends are clean semicircles;
        // using the full height distorts the arcs into vertical lines at each end.
        var radius = Math.Max(1, rect.Height / 2);
        using (var track = RoundedRect(rect, radius))
        using (var trackBrush = new SolidBrush(Color.FromArgb(255, 224, 233)))
        {
            graphics.FillPath(trackBrush, track);
        }

        var clamped = Math.Clamp(percent, 0, 100);
        if (clamped <= 0)
        {
            return;
        }

        var fillWidth = Math.Max(rect.Height, (int)Math.Round(rect.Width * clamped / 100D));
        var fillRect = new Rectangle(rect.X, rect.Y, fillWidth, rect.Height);
        using var fillPath = RoundedRect(fillRect, radius);
        // Inflate the gradient rectangle by 1px on each side so GDI+'s edge artifact
        // (a thin vertical line of the wrong colour at the start/end) falls outside the bar.
        var gradientRect = new Rectangle(rect.X - 1, rect.Y, rect.Width + 2, rect.Height);
        using var fillBrush = new LinearGradientBrush(
            gradientRect,
            Color.FromArgb(255, 150, 182),
            Color.FromArgb(243, 92, 140),
            0F)
        {
            WrapMode = WrapMode.TileFlipX
        };
        graphics.FillPath(fillBrush, fillPath);
    }

    private static void DrawSoftPanel(Graphics graphics, Rectangle rect, double scale)
    {
        using var path = RoundedRect(rect, Math.Max(2, (int)Math.Round(16 * scale)));
        using var fill = new SolidBrush(Color.FromArgb(252, 255, 255, 255));
        using var pen = new Pen(Color.FromArgb(120, 255, 205, 220), 1F);
        graphics.FillPath(fill, path);
        graphics.DrawPath(pen, path);
    }

    private static void ApplyPillRegion(Control control)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        var bounds = new Rectangle(0, 0, control.Width, control.Height);
        var path = RoundedRect(bounds, control.Height / 2);
        control.Region?.Dispose();
        control.Region = new Region(path);
        path.Dispose();
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    private sealed record MetricOption(string Key, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }
}
