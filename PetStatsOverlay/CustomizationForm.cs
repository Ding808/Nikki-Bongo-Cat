using System.Diagnostics;

namespace PetStatsOverlay;

public sealed class CustomizationForm : Form
{
    private readonly BongoCatConfigEditor editor;
    private readonly HotkeyDataGridView animationGrid = new();
    private readonly HotkeyDataGridView faceGrid = new();
    private readonly HotkeyDataGridView live2DGrid = new();
    private readonly CheckBox live2DEnabledCheckBox = new();
    private readonly CheckBox showFullPathCheckBox = new();
    private readonly PictureBox bindingPreviewBox = new();
    private readonly PictureBox assetPreviewBox = new();
    private readonly ComboBox assetPreviewCombo = new();
    private readonly ComboBox live2DModelCombo = new();
    private readonly Label modelLabel = new();
    private readonly Label statusLabel = new();
    private List<Live2DExpressionInfo> live2DExpressions = [];
    private Live2DExpressionInfo? currentLive2DPreviewExpression;
    private DataGridView? hotkeyEditingGrid;

    public event EventHandler? RestartAndShowRequested;

    public CustomizationForm(BongoCatConfigEditor editor)
    {
        this.editor = editor;

        Text = L.Text("自定义桌宠");
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1120, 760);
        MinimumSize = new Size(940, 640);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Color.FromArgb(255, 247, 250);
        ForeColor = Color.FromArgb(126, 70, 92);

        BuildUi();
        LoadSnapshot();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        bindingPreviewBox.Image?.Dispose();
        assetPreviewBox.Image?.Dispose();
        base.OnFormClosed(e);
    }

    public void ReloadFromDisk()
    {
        LoadSnapshot();
    }

    public void ApplyLanguage()
    {
        // Translate in place so switching languages never discards unsaved bindings.
        L.RefreshControls(this);
        RenumberRows(animationGrid, L.Text("动画"));
        RenumberRows(faceGrid, L.Text("表情"));
        foreach (DataGridViewRow row in live2DGrid.Rows)
            row.Cells[2].Value = L.Retranslate(Convert.ToString(row.Cells[2].Value) ?? "");
        var snapshot = editor.LoadCustomization();
        modelLabel.Text = L.Format($"当前模型：{snapshot.ModelName}");
        var selectedPreview = assetPreviewCombo.SelectedIndex;
        for (var i = 0; i < assetPreviewCombo.Items.Count; i++)
        {
            if (assetPreviewCombo.Items[i] is AssetPreviewTarget item)
                assetPreviewCombo.Items[i] = item with { Label = L.Retranslate(item.Label) };
        }
        assetPreviewCombo.SelectedIndex = selectedPreview;
        var selectedModel = (live2DModelCombo.SelectedItem as Live2DModelInfo)?.Id;
        RefreshLive2DModelCombo();
        if (selectedModel is not null)
        {
            var sameModel = live2DModelCombo.Items.OfType<Live2DModelInfo>().FirstOrDefault(model => model.Id == selectedModel);
            if (sameModel is not null) live2DModelCombo.SelectedItem = sameModel;
        }
        live2DExpressions = editor.LoadLive2DExpressions();
        foreach (DataGridViewRow row in live2DGrid.Rows)
            if (row.Index < live2DExpressions.Count) row.Tag = live2DExpressions[row.Index];
        if (currentLive2DPreviewExpression is not null && live2DGrid.CurrentRow is not null)
            LoadLive2DLabelPreview(live2DGrid.CurrentRow);
        SetStatus(L.Pick("Language updated. Unsaved changes are kept.", "语言已切换，尚未保存的修改已保留。"));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var grid = GetActiveHotkeyGrid();
        if (grid is not null && TrySetHotkey(grid, keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BuildUi()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 5) };
        tabs.TabPages.Add(BuildBindingsTab());
        tabs.TabPages.Add(BuildAssetsTab());
        Controls.Add(tabs);

        statusLabel.Dock = DockStyle.Bottom;
        statusLabel.Height = 30;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.Padding = new Padding(12, 0, 0, 0);
        statusLabel.ForeColor = Color.FromArgb(178, 126, 148);
        Controls.Add(statusLabel);
    }

    private TabPage BuildBindingsTab()
    {
        var page = new TabPage(L.Text("触发键与图片")) { BackColor = BackColor, AutoScroll = true };
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(14),
            BackColor = BackColor,
            AutoScroll = true
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

        main.Controls.Add(MakeTitle(L.Text("键盘/手部动画触发项")), 0, 0);
        main.Controls.Add(MakeTitle(L.Text("表情、Live2D 与预览")), 1, 0);

        ConfigureGrid(animationGrid);
        animationGrid.Columns.Add(MakeReadOnlyColumn("slot", L.Text("动画"), 56));
        animationGrid.Columns.Add(MakeTextColumn("key", L.Text("触发键"), 82));
        animationGrid.Columns.Add(MakeReadOnlyColumn("keyboard", L.Text("键盘图"), 150));
        animationGrid.Columns.Add(MakeReadOnlyColumn("hand", L.Text("手部图"), 150));
        ConfigureHotkeyCapture(animationGrid);
        animationGrid.CellDoubleClick += (_, _) => PreviewSelectedImage(animationGrid);
        main.Controls.Add(animationGrid, 0, 1);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 7,
            BackColor = BackColor
        };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 28F));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 24F));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));

        ConfigureGrid(faceGrid);
        faceGrid.Columns.Add(MakeReadOnlyColumn("slot", L.Text("表情"), 56));
        faceGrid.Columns.Add(MakeTextColumn("key", L.Text("触发键"), 82));
        faceGrid.Columns.Add(MakeReadOnlyColumn("face", L.Text("表情图"), 150));
        ConfigureHotkeyCapture(faceGrid);
        faceGrid.CellDoubleClick += (_, _) => PreviewSelectedImage(faceGrid);
        right.Controls.Add(faceGrid, 0, 0);
        right.Controls.Add(MakeGridButtonBar(
            (L.Text("添加表情"), (_, _) => AddFaceRow()),
            (L.Text("删除表情"), (_, _) => DeleteSelectedRow(faceGrid)),
            (L.Text("选择表情图"), (_, _) => SelectRowImage(faceGrid, BongoAssetKind.Face, 2)),
            (L.Text("预览"), (_, _) => PreviewSelectedImage(faceGrid))), 0, 1);

        right.Controls.Add(MakeTitle(L.Text("Live2D 表情触发键")), 0, 2);
        ConfigureGrid(live2DGrid);
        live2DGrid.Columns.Add(MakeReadOnlyColumn("slot", L.Text("标签"), 56));
        live2DGrid.Columns.Add(MakeTextColumn("key", L.Text("触发键"), 100));
        live2DGrid.Columns.Add(MakeReadOnlyColumn("note", L.Text("说明"), 120));
        ConfigureHotkeyCapture(live2DGrid);
        live2DGrid.CellDoubleClick += (_, _) => PreviewSelectedImage(live2DGrid);
        right.Controls.Add(live2DGrid, 0, 3);
        right.Controls.Add(MakeGridButtonBar(
            (L.Text("添加 Live2D"), (_, _) => AddLive2DRow()),
            (L.Text("删除 Live2D"), (_, _) => DeleteSelectedRow(live2DGrid)),
            (L.Text("预览标签"), (_, _) => PreviewSelectedImage(live2DGrid))), 0, 4);

        right.Controls.Add(MakeTitle(L.Text("当前预览")), 0, 5);
        ConfigurePreviewBox(bindingPreviewBox);
        right.Controls.Add(bindingPreviewBox, 0, 6);
        main.Controls.Add(right, 1, 1);

        main.Controls.Add(MakeGridButtonBar(
            (L.Text("添加动画"), (_, _) => AddAnimationRow()),
            (L.Text("删除动画"), (_, _) => DeleteSelectedRow(animationGrid)),
            (L.Text("选择键盘图"), (_, _) => SelectRowImage(animationGrid, BongoAssetKind.Keyboard, 2)),
            (L.Text("选择手部图"), (_, _) => SelectRowImage(animationGrid, BongoAssetKind.Hand, 3)),
            (L.Text("预览"), (_, _) => PreviewSelectedImage(animationGrid))), 0, 2);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            BackColor = BackColor
        };
        live2DEnabledCheckBox.Text = L.Text("启用 Live2D");
        live2DEnabledCheckBox.AutoSize = true;
        live2DEnabledCheckBox.ForeColor = Color.FromArgb(136, 82, 105);
        showFullPathCheckBox.Text = L.Text("显示完整路径");
        showFullPathCheckBox.AutoSize = true;
        showFullPathCheckBox.ForeColor = Color.FromArgb(136, 82, 105);
        showFullPathCheckBox.CheckedChanged += (_, _) => RefreshPathDisplays();
        modelLabel.Width = 260;
        modelLabel.Height = 26;
        modelLabel.TextAlign = ContentAlignment.MiddleLeft;
        modelLabel.ForeColor = Color.FromArgb(136, 82, 105);
        options.Controls.Add(live2DEnabledCheckBox);
        options.Controls.Add(showFullPathCheckBox);
        options.Controls.Add(modelLabel);
        main.Controls.Add(options, 1, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            WrapContents = true,
            AutoScroll = true
        };
        buttonPanel.Controls.Add(MakeButton(L.Text("保存并重启"), SaveBindingsAndRestart, 112));
        buttonPanel.Controls.Add(MakeButton(L.Text("保存设置"), SaveBindings, 112));
        buttonPanel.Controls.Add(MakeButton(L.Text("重新读取"), (_, _) => LoadSnapshot(), 92));
        main.Controls.Add(buttonPanel, 0, 3);
        main.SetColumnSpan(buttonPanel, 2);

        page.Controls.Add(main);
        return page;
    }

    private TabPage BuildAssetsTab()
    {
        var page = new TabPage(L.Text("基础素材")) { BackColor = BackColor, AutoScroll = true };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(14),
            BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = BackColor
        };

        left.Controls.Add(MakeTitle(L.Text("快速换肤（自动记住上次选择）")));
        left.Controls.Add(MakeButton(L.Text("粉色暖暖（默认）"), (_, _) => ApplyBuiltInSkin(BongoCatConfigEditor.PinkSkinId), 230));
        left.Controls.Add(MakeButton(L.Text("紫色暖暖"), (_, _) => ApplyBuiltInSkin(BongoCatConfigEditor.PurpleSkinId), 230));
        left.Controls.Add(Spacer(10));
        left.Controls.Add(MakeTitle(L.Text("Live2D 模型")));
        ConfigureLive2DModelCombo();
        left.Controls.Add(live2DModelCombo);
        left.Controls.Add(MakeButton(L.Text("\u5237\u65b0 Live2D \u6a21\u578b\u5217\u8868"), RefreshLive2DModels, 230));
        left.Controls.Add(MakeButton(L.Text("\u5220\u9664\u9009\u4e2d Live2D \u6a21\u578b"), DeleteSelectedLive2DModel, 230));
        left.Controls.Add(MakeButton(L.Text("应用选中模型并重启"), ApplySelectedLive2DModelAndRestart, 230));
        left.Controls.Add(MakeButton(L.Text("打开 Live2D 模型库"), (_, _) => editor.OpenLive2DLibraryFolder(), 230));
        left.Controls.Add(Spacer(10));

        left.Controls.Add(MakeTitle(L.Text("基础 PNG 素材")));
        left.Controls.Add(MakeButton(L.Text("替换桌宠主体 cat.png"), (_, _) => ReplacePng(BongoAssetKind.Cat)));
        left.Controls.Add(MakeButton(L.Text("替换手臂 arm.png"), (_, _) => ReplacePng(BongoAssetKind.Arm)));
        left.Controls.Add(MakeButton(L.Text("替换鼠标 mouse.png"), (_, _) => ReplacePng(BongoAssetKind.Mouse)));
        left.Controls.Add(MakeButton(L.Text("替换鼠标背景 mousebg.png"), (_, _) => ReplacePng(BongoAssetKind.MouseBackground), 210));
        left.Controls.Add(MakeButton(L.Text("替换鼠标左键 mouse_left.png"), (_, _) => ReplacePng(BongoAssetKind.MouseLeft)));
        left.Controls.Add(MakeButton(L.Text("替换鼠标右键 mouse_right.png"), (_, _) => ReplacePng(BongoAssetKind.MouseRight)));
        left.Controls.Add(MakeButton(L.Text("替换鼠标侧键 mouse_side.png"), (_, _) => ReplacePng(BongoAssetKind.MouseSide)));
        left.Controls.Add(Spacer(10));
        left.Controls.Add(MakeTitle(L.Text("键盘样式素材")));
        left.Controls.Add(MakeButton(L.Text("替换键盘整体 tablet.png"), (_, _) => ReplacePng(BongoAssetKind.Tablet), 210));
        left.Controls.Add(MakeButton(L.Text("替换键盘背景 tabletbg.png"), (_, _) => ReplacePng(BongoAssetKind.TabletBackground), 220));
        left.Controls.Add(MakeButton(L.Text("替换键盘左键 tablet_left.png"), (_, _) => ReplacePng(BongoAssetKind.TabletLeft), 220));
        left.Controls.Add(MakeButton(L.Text("替换键盘右键 tablet_right.png"), (_, _) => ReplacePng(BongoAssetKind.TabletRight), 220));
        left.Controls.Add(MakeButton(L.Text("替换抬起状态 up.png"), (_, _) => ReplacePng(BongoAssetKind.Up), 190));
        left.Controls.Add(Spacer(10));
        left.Controls.Add(MakeTitle(L.Text("Live2D 模型")));
        left.Controls.Add(MakeButton(L.Text("导入 Live2D 模型文件夹（校验）"), ImportLive2DModel, 230));
        left.Controls.Add(MakeButton(L.Text("导入 Live2D 整包 zip"), ImportLive2DPackage, 230));
        left.Controls.Add(MakeButton(L.Text("导出选中 Live2D 整包"), ExportSelectedLive2DPackage, 230));
        left.Controls.Add(MakeButton(L.Text("打开当前素材目录"), (_, _) => editor.OpenAssetsFolder()));
        left.Controls.Add(MakeButton(L.Text("打开备份目录"), OpenBackupFolder));
        left.Controls.Add(MakeButton(L.Text("替换当前 Live2D 贴图"), (_, _) => ReplacePng(BongoAssetKind.Live2DTexture), 230));

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            BackColor = BackColor
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        right.Controls.Add(MakeTitle(L.Text("基础素材预览")), 0, 0);
        ConfigureAssetPreviewCombo();
        right.Controls.Add(assetPreviewCombo, 0, 1);
        ConfigurePreviewBox(assetPreviewBox);
        right.Controls.Add(assetPreviewBox, 0, 2);
        right.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = L.Text("添加动画/表情请在“触发键与图片”页直接添加行。保存会自动把图片复制成对应序号并写入 config.json。"),
            ForeColor = Color.FromArgb(136, 82, 105)
        }, 0, 3);

        left.Controls.Add(MakeButton(L.Text("一键恢复默认桌宠"), RestoreDefaultPetAndRestart, 230));
        layout.Controls.Add(left, 0, 0);
        layout.Controls.Add(right, 1, 0);
        page.Controls.Add(layout);
        return page;
    }

    private void RequestRestartAndShow()
    {
        if (RestartAndShowRequested is not null)
        {
            RestartAndShowRequested.Invoke(this, EventArgs.Empty);
        }
        else
        {
            editor.RestartPet();
        }
    }

    private void ConfigureLive2DModelCombo()
    {
        live2DModelCombo.Width = 360;
        live2DModelCombo.Height = 28;
        live2DModelCombo.Margin = new Padding(0, 4, 6, 4);
        live2DModelCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        live2DModelCombo.FlatStyle = FlatStyle.Flat;
        live2DModelCombo.SelectedIndexChanged += (_, _) =>
        {
            if (live2DModelCombo.SelectedItem is Live2DModelInfo model && File.Exists(model.PreviewTexturePath))
            {
                LoadPreview(model.PreviewTexturePath, assetPreviewBox);
            }
        };
    }

    private void RefreshLive2DModelCombo()
    {
        var previousId = (live2DModelCombo.SelectedItem as Live2DModelInfo)?.Id ?? "";
        live2DModelCombo.Items.Clear();

        var models = editor.LoadLive2DModels();
        foreach (var model in models)
        {
            live2DModelCombo.Items.Add(model);
        }

        var selected = models.FirstOrDefault(model => model.IsActive)
            ?? models.FirstOrDefault(model => string.Equals(model.Id, previousId, StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault();
        if (selected is not null)
        {
            live2DModelCombo.SelectedItem = selected;
        }
    }

    private void RefreshLive2DModels(object? sender, EventArgs e)
    {
        RefreshLive2DModelCombo();
        if (live2DModelCombo.SelectedItem is Live2DModelInfo model && File.Exists(model.PreviewTexturePath))
        {
            LoadPreview(model.PreviewTexturePath, assetPreviewBox);
        }

        SetStatus(L.Text("\u5df2\u5237\u65b0 Live2D \u6a21\u578b\u5217\u8868\u3002"));
    }

    private void DeleteSelectedLive2DModel(object? sender, EventArgs e)
    {
        if (live2DModelCombo.SelectedItem is not Live2DModelInfo model)
        {
            SetStatus(L.Text("\u8bf7\u9009\u62e9\u4e00\u4e2a Live2D \u6a21\u578b\u3002"));
            return;
        }

        if (AppDialog.Show(this,
                L.Format($"\u786e\u5b9a\u8981\u4ece\u6a21\u578b\u5e93\u5220\u9664\u201c{model.Name}\u201d\u5417\uff1f"),
                L.Text("\u5220\u9664 Live2D \u6a21\u578b"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning) != DialogResult.OK)
        {
            return;
        }

        try
        {
            editor.DeleteLive2DModel(model.Id);
            RefreshLive2DModelCombo();
            SetStatus(L.Format($"\u5df2\u5220\u9664 Live2D \u6a21\u578b\uff1a{model.Name}\u3002"));
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ConfigureAssetPreviewCombo()
    {
        assetPreviewCombo.Dock = DockStyle.Fill;
        assetPreviewCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        assetPreviewCombo.FlatStyle = FlatStyle.Flat;
        assetPreviewCombo.Items.Clear();
        assetPreviewCombo.Items.AddRange(
        [
            new AssetPreviewTarget(L.Text("当前标准桌宠（实际显示）"), BongoAssetKind.Cat, true),
            new AssetPreviewTarget(L.Text("当前 Live2D 贴图"), BongoAssetKind.Live2DTexture),
            new AssetPreviewTarget(L.Text("主体 cat.png"), BongoAssetKind.Cat),
            new AssetPreviewTarget(L.Text("手臂 arm.png"), BongoAssetKind.Arm),
            new AssetPreviewTarget(L.Text("抬起 up.png"), BongoAssetKind.Up),
            new AssetPreviewTarget(L.Text("鼠标 mouse.png"), BongoAssetKind.Mouse),
            new AssetPreviewTarget(L.Text("鼠标背景 mousebg.png"), BongoAssetKind.MouseBackground),
            new AssetPreviewTarget(L.Text("鼠标左键 mouse_left.png"), BongoAssetKind.MouseLeft),
            new AssetPreviewTarget(L.Text("鼠标右键 mouse_right.png"), BongoAssetKind.MouseRight),
            new AssetPreviewTarget(L.Text("鼠标侧键 mouse_side.png"), BongoAssetKind.MouseSide),
            new AssetPreviewTarget(L.Text("键盘整体 tablet.png"), BongoAssetKind.Tablet),
            new AssetPreviewTarget(L.Text("键盘背景 tabletbg.png"), BongoAssetKind.TabletBackground),
            new AssetPreviewTarget(L.Text("键盘左键 tablet_left.png"), BongoAssetKind.TabletLeft),
            new AssetPreviewTarget(L.Text("键盘右键 tablet_right.png"), BongoAssetKind.TabletRight)
        ]);
        assetPreviewCombo.SelectedIndexChanged += (_, _) =>
        {
            if (assetPreviewCombo.SelectedItem is AssetPreviewTarget target)
            {
                LoadPreview(target.StandardPreview ? editor.GetCurrentStandardPreviewPath() : editor.GetAssetPath(target.Kind), assetPreviewBox);
            }
        };
        assetPreviewCombo.SelectedIndex = 0;
    }

    private void LoadSnapshot()
    {
        var snapshot = editor.LoadCustomization();
        live2DExpressions = editor.LoadLive2DExpressions();
        RefreshLive2DModelCombo();
        live2DEnabledCheckBox.Checked = snapshot.Live2DEnabled;
        modelLabel.Text = L.Format($"当前模型：{snapshot.ModelName}");

        animationGrid.Rows.Clear();
        var animationCount = Math.Max(snapshot.AnimationKeys.Count, Math.Max(CountAssets(BongoAssetKind.Keyboard), CountAssets(BongoAssetKind.Hand)));
        for (var index = 0; index < animationCount; index++)
        {
            var rowIndex = animationGrid.Rows.Add(L.Format($"动画 {index}"), snapshot.AnimationKeys.ElementAtOrDefault(index) ?? "", "", "");
            SetImageCell(animationGrid.Rows[rowIndex], 2, ExistingAssetPath(BongoAssetKind.Keyboard, index));
            SetImageCell(animationGrid.Rows[rowIndex], 3, ExistingAssetPath(BongoAssetKind.Hand, index));
        }

        faceGrid.Rows.Clear();
        var faceCount = Math.Max(snapshot.FaceKeys.Count, CountAssets(BongoAssetKind.Face));
        for (var index = 0; index < faceCount; index++)
        {
            var rowIndex = faceGrid.Rows.Add(L.Format($"表情 {index}"), snapshot.FaceKeys.ElementAtOrDefault(index) ?? "", "");
            SetImageCell(faceGrid.Rows[rowIndex], 2, ExistingAssetPath(BongoAssetKind.Face, index));
        }

        live2DGrid.Rows.Clear();
        for (var index = 0; index < snapshot.Live2DExpressionKeys.Count; index++)
        {
            live2DGrid.Rows.Add($"Live2D {index}", snapshot.Live2DExpressionKeys.ElementAtOrDefault(index) ?? "", L.Text("Live2D 表情"));
        }

        for (var index = 0; index < live2DGrid.Rows.Count; index++)
        {
            var expression = live2DExpressions.ElementAtOrDefault(index);
            if (expression is not null)
            {
                live2DGrid.Rows[index].Cells[2].Value = $"{expression.Name} / {expression.File}";
                live2DGrid.Rows[index].Tag = expression;
            }
        }

        for (var index = live2DGrid.Rows.Count; index < live2DExpressions.Count; index++)
        {
            var expression = live2DExpressions[index];
            var rowIndex = live2DGrid.Rows.Add($"Live2D {index}", "", $"{expression.Name} / {expression.File}");
            live2DGrid.Rows[rowIndex].Tag = expression;
        }

        if (live2DGrid.Rows.Count == 0)
        {
            live2DGrid.Rows.Add("Live2D 0", "", L.Text("Live2D 表情"));
        }

        RenumberRows(animationGrid, L.Text("动画"));
        RenumberRows(faceGrid, L.Text("表情"));
        RenumberRows(live2DGrid, "Live2D");
        SetStatus(L.Text("已读取当前配置。"));
        LoadPreview(editor.GetCurrentStandardPreviewPath(), assetPreviewBox);
        LoadPreview(editor.GetAssetPath(BongoAssetKind.Cat), bindingPreviewBox);
    }

    private void SaveBindings(object? sender, EventArgs e)
    {
        _ = TrySaveBindings();
    }

    private bool TrySaveBindings()
    {
        try
        {
            SaveRowImages();
            var activeModelId = editor.LoadCustomization().Live2DModelId;
            editor.SaveCustomization(new BongoCustomizationSnapshot
            {
                Live2DEnabled = live2DEnabledCheckBox.Checked,
                Live2DModelId = activeModelId,
                AnimationKeys = ReadKeys(animationGrid),
                FaceKeys = ReadKeys(faceGrid),
                Live2DExpressionKeys = ReadKeys(live2DGrid)
            });
            SetStatus(L.Text("已保存图片和触发键到 config.json。重启桌宠后生效。"));
            return true;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return false;
        }
    }

    private void SaveBindingsAndRestart(object? sender, EventArgs e)
    {
        if (!TrySaveBindings())
        {
            return;
        }

        if (RestartAndShowRequested is not null)
        {
            RestartAndShowRequested.Invoke(this, EventArgs.Empty);
        }
        else
        {
            editor.RestartPet();
        }
        SetStatus(L.Text("已保存并重启桌宠。"));
    }

    private void SaveRowImages()
    {
        for (var index = 0; index < animationGrid.Rows.Count; index++)
        {
            SaveCellImage(animationGrid.Rows[index], 2, BongoAssetKind.Keyboard, index, required: HasKey(animationGrid.Rows[index]));
            SaveCellImage(animationGrid.Rows[index], 3, BongoAssetKind.Hand, index, required: HasKey(animationGrid.Rows[index]));
        }

        for (var index = 0; index < faceGrid.Rows.Count; index++)
        {
            SaveCellImage(faceGrid.Rows[index], 2, BongoAssetKind.Face, index, required: HasKey(faceGrid.Rows[index]));
        }
    }

    private void SaveCellImage(DataGridViewRow row, int cellIndex, BongoAssetKind kind, int index, bool required)
    {
        var source = GetImageCellPath(row.Cells[cellIndex]);
        var target = editor.GetAssetPath(kind, index);
        if (string.IsNullOrWhiteSpace(source))
        {
            if (required && !File.Exists(target))
            {
                throw new InvalidOperationException(L.Format($"{row.Cells[0].Value} 缺少对应图片。"));
            }

            return;
        }

        if (!Path.IsPathFullyQualified(source))
        {
            return;
        }

        if (!File.Exists(source))
        {
            throw new FileNotFoundException(L.Text("找不到图片文件。"), source);
        }

        if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            editor.ReplaceAsset(kind, source, index);
            SetImageCell(row, cellIndex, target);
        }
    }

    private void AddAnimationRow()
    {
        var index = animationGrid.Rows.Count;
        animationGrid.Rows.Add(L.Format($"动画 {index}"), "", "", "");
        SelectLastRow(animationGrid);
    }

    private void AddFaceRow()
    {
        var index = faceGrid.Rows.Count;
        faceGrid.Rows.Add(L.Format($"表情 {index}"), "", "");
        SelectLastRow(faceGrid);
    }

    private void AddLive2DRow()
    {
        var index = live2DGrid.Rows.Count;
        live2DGrid.Rows.Add($"Live2D {index}", "", L.Text("Live2D 表情"));
        SelectLastRow(live2DGrid);
    }

    private void DeleteSelectedRow(DataGridView grid)
    {
        if (grid.CurrentRow is null || grid.Rows.Count <= 1)
        {
            return;
        }

        grid.Rows.Remove(grid.CurrentRow);
        RenumberRows(grid, grid == animationGrid ? L.Text("动画") : grid == faceGrid ? L.Text("表情") : "Live2D");
    }

    private void SelectRowImage(DataGridView grid, BongoAssetKind kind, int cellIndex)
    {
        if (grid.CurrentRow is null)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = L.Text("选择 PNG 图片"),
            Filter = L.Text("PNG 图片|*.png"),
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SetImageCell(grid.CurrentRow, cellIndex, dialog.FileName);
        LoadPreview(dialog.FileName, bindingPreviewBox);
        SetStatus(L.Text("已选择图片，保存后会复制到桌宠素材目录。"));
    }

    private void PreviewSelectedImage(DataGridView grid)
    {
        if (grid.CurrentCell is null || grid.CurrentRow is null)
        {
            return;
        }

        var value = GetImageCellPath(grid.CurrentCell);
        if (!string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value) && File.Exists(value))
        {
            LoadPreview(value, bindingPreviewBox);
            return;
        }

        if (grid == animationGrid)
        {
            var index = grid.CurrentRow.Index;
            var kind = grid.CurrentCell.ColumnIndex == 3 ? BongoAssetKind.Hand : BongoAssetKind.Keyboard;
            LoadPreview(editor.GetAssetPath(kind, index), bindingPreviewBox);
        }
        else if (grid == faceGrid)
        {
            LoadPreview(editor.GetAssetPath(BongoAssetKind.Face, grid.CurrentRow.Index), bindingPreviewBox);
        }
        else if (grid == live2DGrid)
        {
            LoadLive2DLabelPreview(grid.CurrentRow);
        }
    }

    private void LoadLive2DLabelPreview(DataGridViewRow row)
    {
        currentLive2DPreviewExpression = row.Tag as Live2DExpressionInfo ?? live2DExpressions.ElementAtOrDefault(row.Index);
        bindingPreviewBox.Image?.Dispose();
        bindingPreviewBox.Image = CreateLive2DLabelPreview(
            Convert.ToString(row.Cells[0].Value) ?? "Live2D",
            Convert.ToString(row.Cells[1].Value) ?? "",
            Convert.ToString(row.Cells[2].Value) ?? L.Text("Live2D 表情"));
    }

    private Bitmap CreateLive2DLabelPreview(string title, string hotkey, string note)
    {
        var bitmap = new Bitmap(420, 220);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.White);

        var card = new Rectangle(22, 34, 376, 144);
        using var path = RoundedRect(card, 18);
        using var fill = new SolidBrush(Color.FromArgb(255, 247, 250));
        using var border = new Pen(Color.FromArgb(255, 177, 202), 2F);
        graphics.FillPath(fill, path);
        graphics.DrawPath(border, path);

        var imageRect = new Rectangle(268, 52, 108, 108);
        if (currentLive2DPreviewExpression is not null && File.Exists(currentLive2DPreviewExpression.TexturePath))
        {
            using var modelImage = Image.FromFile(currentLive2DPreviewExpression.TexturePath);
            graphics.DrawImage(modelImage, imageRect);
        }
        else
        {
            using var placeholderFill = new SolidBrush(Color.FromArgb(255, 232, 241));
            using var placeholderPen = new Pen(Color.FromArgb(255, 177, 202), 1F);
            graphics.FillEllipse(placeholderFill, imageRect);
            graphics.DrawEllipse(placeholderPen, imageRect);
        }

        using var titleFont = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
        using var keyFont = new Font("Microsoft YaHei UI", 14F, FontStyle.Regular, GraphicsUnit.Point);
        using var noteFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        using var titleBrush = new SolidBrush(Color.FromArgb(232, 91, 134));
        using var textBrush = new SolidBrush(Color.FromArgb(126, 70, 92));
        using var mutedBrush = new SolidBrush(Color.FromArgb(178, 126, 148));

        graphics.DrawString(title, titleFont, titleBrush, new RectangleF(46, 54, 210, 40));
        graphics.DrawString(string.IsNullOrWhiteSpace(hotkey) ? L.Text("未绑定触发键") : L.Format($"触发键：{hotkey}"), keyFont, textBrush, new RectangleF(46, 100, 330, 32));
        graphics.DrawString(note, noteFont, mutedBrush, new RectangleF(46, 138, 210, 28));
        graphics.DrawString(currentLive2DPreviewExpression?.ParameterSummary ?? L.Text("没有表情参数摘要"), noteFont, textBrush, new RectangleF(46, 166, 330, 28));
        return bitmap;
    }

    private void ReplacePng(BongoAssetKind kind, int index = 0)
    {
        using var dialog = new OpenFileDialog
        {
            Title = L.Text("选择 PNG 图片"),
            Filter = L.Text("PNG 图片|*.png"),
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            editor.ReplaceAsset(kind, dialog.FileName, index);
            var target = editor.GetAssetPath(kind, index);
            LoadPreview(target, assetPreviewBox);
            SetStatus(L.Format($"已替换：{Path.GetFileName(target)}。重启桌宠后生效。"));
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ApplySelectedLive2DModelAndRestart(object? sender, EventArgs e)
    {
        if (live2DModelCombo.SelectedItem is not Live2DModelInfo model)
        {
            SetStatus(L.Text("请选择一个 Live2D 模型。"));
            return;
        }

        try
        {
            SaveRowImages();
            var activeModelId = editor.LoadCustomization().Live2DModelId;
            editor.SaveCurrentLive2DProfile(new BongoCustomizationSnapshot
            {
                Live2DEnabled = live2DEnabledCheckBox.Checked,
                Live2DModelId = activeModelId,
                AnimationKeys = ReadKeys(animationGrid),
                FaceKeys = ReadKeys(faceGrid),
                Live2DExpressionKeys = ReadKeys(live2DGrid)
            });

            editor.SelectLive2DModel(model.Id);
            LoadSnapshot();
            LoadPreview(editor.GetCurrentStandardPreviewPath(), assetPreviewBox);
            SetStatus(L.Format($"已应用 Live2D 模型：{model.Name}。"));
            RequestRestartAndShow();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ApplyBuiltInSkin(string skinId)
    {
        var skin = live2DModelCombo.Items
            .OfType<Live2DModelInfo>()
            .FirstOrDefault(model => string.Equals(model.Id, skinId, StringComparison.OrdinalIgnoreCase));
        if (skin is null)
        {
            SetStatus(L.Text("找不到对应的内置皮肤资源。"));
            return;
        }

        live2DModelCombo.SelectedItem = skin;
        ApplySelectedLive2DModelAndRestart(this, EventArgs.Empty);
    }

    private void ImportLive2DModel(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = L.Text("选择 Live2D 模型根目录，目录里应有一个 .model3.json")
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (AppDialog.Show(this, L.Text("导入前会自动备份当前 cat_model。确认继续吗？"), L.Text("导入 Live2D"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _ = editor.ImportLive2DModel(dialog.SelectedPath);
            LoadSnapshot();
            SetStatus(L.Text("Live2D 模型已导入，重启桌宠后生效。"));
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ImportLive2DPackage(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = L.Text("选择 Live2D 整包 zip"),
            Filter = L.Text("Live2D 整包|*.zip"),
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var model = editor.ImportLive2DPackage(dialog.FileName);
            LoadSnapshot();
            LoadPreview(editor.GetCurrentStandardPreviewPath(), assetPreviewBox);
            SetStatus(L.Format($"已导入并选中 Live2D 整包：{model.Name}。"));
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ExportSelectedLive2DPackage(object? sender, EventArgs e)
    {
        if (live2DModelCombo.SelectedItem is not Live2DModelInfo model)
        {
            SetStatus(L.Text("请选择一个 Live2D 模型。"));
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = L.Text("导出 Live2D 整包"),
            Filter = L.Text("Live2D 整包|*.zip"),
            FileName = $"{model.Name}.zip",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SaveRowImages();
            var activeModelId = editor.LoadCustomization().Live2DModelId;
            editor.ExportLive2DModelPackage(model.Id, dialog.FileName, new BongoCustomizationSnapshot
            {
                Live2DEnabled = live2DEnabledCheckBox.Checked,
                Live2DModelId = activeModelId,
                AnimationKeys = ReadKeys(animationGrid),
                FaceKeys = ReadKeys(faceGrid),
                Live2DExpressionKeys = ReadKeys(live2DGrid)
            });
            SetStatus(L.Format($"已导出 Live2D 整包：{dialog.FileName}"));
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void RestoreDefaultPetAndRestart(object? sender, EventArgs e)
    {
        if (AppDialog.Show(this, L.Text("将恢复默认标准桌宠并重启。当前模型会先自动备份。继续吗？"), L.Text("恢复默认桌宠"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        try
        {
            editor.RestoreDefaultStandardPet();
            LoadSnapshot();
            LoadPreview(editor.GetCurrentStandardPreviewPath(), assetPreviewBox);
            SetStatus(L.Text("已恢复默认标准桌宠并重启。"));
            RequestRestartAndShow();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void OpenBackupFolder(object? sender, EventArgs e)
    {
        Directory.CreateDirectory(editor.BackupDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = editor.BackupDirectory,
            UseShellExecute = true
        });
    }

    private int CountAssets(BongoAssetKind kind)
    {
        var directory = kind switch
        {
            BongoAssetKind.Keyboard => Path.Combine(editor.StandardAssetDirectory, "keyboard"),
            BongoAssetKind.Hand => Path.Combine(editor.StandardAssetDirectory, "hand"),
            BongoAssetKind.Face => Path.Combine(editor.StandardAssetDirectory, "face"),
            _ => editor.StandardAssetDirectory
        };

        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory.GetFiles(directory, "*.png")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => int.TryParse(name, out var index) ? index + 1 : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private string ExistingAssetPath(BongoAssetKind kind, int index)
    {
        var path = editor.GetAssetPath(kind, index);
        return File.Exists(path) ? path : "";
    }

    private void SetImageCell(DataGridViewRow row, int cellIndex, string path)
    {
        row.Cells[cellIndex].Tag = path;
        row.Cells[cellIndex].Value = FormatPath(path);
    }

    private string GetImageCellPath(DataGridViewCell cell)
    {
        return cell.Tag is string tag && !string.IsNullOrWhiteSpace(tag)
            ? tag
            : Convert.ToString(cell.Value)?.Trim() ?? "";
    }

    private string FormatPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        return showFullPathCheckBox.Checked ? path : Path.GetFileName(path);
    }

    private void RefreshPathDisplays()
    {
        RefreshGridPathDisplays(animationGrid, 2, 3);
        RefreshGridPathDisplays(faceGrid, 2);
    }

    private void RefreshGridPathDisplays(DataGridView grid, params int[] cellIndexes)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            foreach (var cellIndex in cellIndexes)
            {
                var path = GetImageCellPath(row.Cells[cellIndex]);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    row.Cells[cellIndex].Value = FormatPath(path);
                }
            }
        }
    }

    private static bool HasKey(DataGridViewRow row)
    {
        return !string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[1].Value));
    }

    private static List<string> ReadKeys(DataGridView grid)
    {
        return grid.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Select(row => Convert.ToString(row.Cells[1].Value)?.Trim() ?? "")
            .ToList();
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.Dock = DockStyle.Fill;
        grid.EditMode = DataGridViewEditMode.EditProgrammatically;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
    }

    private void ConfigureHotkeyCapture(DataGridView grid)
    {
        if (grid is HotkeyDataGridView hotkeyGrid)
        {
            hotkeyGrid.HotkeyCaptured += keyData => TrySetHotkey(grid, keyData);
        }

        grid.CellEnter += (_, e) =>
        {
            if (e.ColumnIndex == 1)
            {
                SetStatus(L.Text("触发键录入：选中此格后直接按键盘按键或组合键，例如 Ctrl+1、Shift+A、Space。"));
            }
        };
        grid.KeyDown += (_, e) =>
        {
            if (TrySetHotkey(grid, e.KeyData))
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        };
        grid.EditingControlShowing += (_, e) =>
        {
            if (e.Control is not TextBox textBox)
            {
                return;
            }

            hotkeyEditingGrid = grid;
            textBox.KeyDown -= HotkeyEditingTextBoxKeyDown;
            textBox.KeyDown += HotkeyEditingTextBoxKeyDown;
            if (IsHotkeyCell(grid))
            {
                textBox.SelectAll();
            }
        };
    }

    private void HotkeyEditingTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (hotkeyEditingGrid is not null && TrySetHotkey(hotkeyEditingGrid, e.KeyData))
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
        }
    }

    private DataGridView? GetActiveHotkeyGrid()
    {
        foreach (var grid in new[] { animationGrid, faceGrid, live2DGrid })
        {
            if (grid.ContainsFocus && IsHotkeyCell(grid))
            {
                return grid;
            }
        }

        return hotkeyEditingGrid is not null && IsHotkeyCell(hotkeyEditingGrid)
            ? hotkeyEditingGrid
            : null;
    }

    private static bool IsHotkeyCell(DataGridView grid)
    {
        return grid.CurrentCell is not null && grid.CurrentCell.ColumnIndex == 1;
    }

    private bool TrySetHotkey(DataGridView grid, Keys keyData)
    {
        if (!IsHotkeyCell(grid))
        {
            return false;
        }

        var hotkey = FormatHotkey(keyData);
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return false;
        }

        if (grid.CurrentCell is null)
        {
            return false;
        }

        grid.CurrentCell.Value = hotkey;
        grid.EndEdit();
        SetStatus(L.Format($"已录入触发键：{hotkey}"));
        return true;
    }

    private static string FormatHotkey(Keys keyData)
    {
        var keyCode = keyData & Keys.KeyCode;
        if (keyCode == Keys.None)
        {
            return "";
        }

        var parts = new List<string>();
        if ((keyData & Keys.Control) == Keys.Control || IsControlKey(keyCode))
        {
            parts.Add("Ctrl");
        }

        if ((keyData & Keys.Shift) == Keys.Shift || IsShiftKey(keyCode))
        {
            parts.Add("Shift");
        }

        if ((keyData & Keys.Alt) == Keys.Alt || IsAltKey(keyCode))
        {
            parts.Add("Alt");
        }

        var keyName = KeyNameFromKeys(keyCode);
        if (!string.IsNullOrWhiteSpace(keyName))
        {
            parts.Add(keyName);
        }

        return string.Join("+", parts.Distinct());
    }

    private static string KeyNameFromKeys(Keys key)
    {
        if (IsControlKey(key) || IsShiftKey(key) || IsAltKey(key))
        {
            return "";
        }

        if (key is >= Keys.A and <= Keys.Z)
        {
            return key.ToString();
        }

        if (key is >= Keys.D0 and <= Keys.D9)
        {
            return ((int)(key - Keys.D0)).ToString();
        }

        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            return $"Num{(int)(key - Keys.NumPad0)}";
        }

        if (key is >= Keys.F1 and <= Keys.F24)
        {
            return $"F{(int)(key - Keys.F1) + 1}";
        }

        return key switch
        {
            Keys.Space => "Space",
            Keys.Enter => "Enter",
            Keys.Tab => "Tab",
            Keys.Escape => "Esc",
            Keys.Back => "Backspace",
            Keys.Delete => "Delete",
            Keys.Insert => "Insert",
            Keys.Home => "Home",
            Keys.End => "End",
            Keys.PageUp => "PageUp",
            Keys.PageDown => "PageDown",
            Keys.Up => "Up",
            Keys.Down => "Down",
            Keys.Left => "Left",
            Keys.Right => "Right",
            Keys.Add => "Num+",
            Keys.Subtract => "Num-",
            _ => ((int)key).ToString()
        };
    }

    private static bool IsControlKey(Keys key)
    {
        return key is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey;
    }

    private static bool IsShiftKey(Keys key)
    {
        return key is Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey;
    }

    private static bool IsAltKey(Keys key)
    {
        return key is Keys.Menu or Keys.LMenu or Keys.RMenu;
    }

    private static void ConfigurePreviewBox(PictureBox box)
    {
        box.Dock = DockStyle.Fill;
        box.BackColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.SizeMode = PictureBoxSizeMode.Zoom;
    }

    private static DataGridViewTextBoxColumn MakeReadOnlyColumn(string name, string header, int width)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            ReadOnly = true,
            FillWeight = width
        };
    }

    private static DataGridViewTextBoxColumn MakeTextColumn(string name, string header, int width)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            FillWeight = width
        };
    }

    private static Label MakeTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Height = 26,
            Width = 320,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(235, 95, 135),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static FlowLayoutPanel MakeGridButtonBar(params (string Text, EventHandler Handler)[] buttons)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill,
            WrapContents = true,
            AutoScroll = true
        };
        foreach (var (text, handler) in buttons)
        {
            panel.Controls.Add(MakeButton(text, handler, 90));
        }

        return panel;
    }

    private static Button MakeButton(string text, EventHandler onClick, int width = 180)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Width = Math.Max(width, TextRenderer.MeasureText(text, SystemFonts.MessageBoxFont).Width + 24),
            Height = 28,
            Margin = new Padding(0, 4, 6, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(255, 232, 241),
            ForeColor = Color.FromArgb(224, 110, 145),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += onClick;
        return button;
    }

    private static Control Spacer(int height)
    {
        return new Panel { Width = 420, Height = height };
    }

    private static void SelectLastRow(DataGridView grid)
    {
        if (grid.Rows.Count == 0)
        {
            return;
        }

        grid.ClearSelection();
        var row = grid.Rows[^1];
        row.Selected = true;
        grid.CurrentCell = row.Cells[1];
    }

    private static void RenumberRows(DataGridView grid, string prefix)
    {
        for (var index = 0; index < grid.Rows.Count; index++)
        {
            grid.Rows[index].Cells[0].Value = $"{prefix} {index}";
        }
    }

    private static void LoadPreview(string path, PictureBox target)
    {
        target.Image?.Dispose();
        target.Image = null;
        if (!File.Exists(path))
        {
            return;
        }

        using var stream = File.OpenRead(path);
        target.Image = Image.FromStream(stream);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void SetStatus(string text)
    {
        statusLabel.Text = text;
    }

    private void ShowError(Exception ex)
    {
        SetStatus(L.Error(ex));
        AppDialog.Show(this, L.Error(ex), L.Text("自定义桌宠"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    private sealed record AssetPreviewTarget(string Label, BongoAssetKind Kind, bool StandardPreview = false)
    {
        public override string ToString()
        {
            return Label;
        }
    }
}

internal sealed class HotkeyDataGridView : DataGridView
{
    public event Func<Keys, bool>? HotkeyCaptured;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        return TryCaptureHotkey(keyData) || base.ProcessCmdKey(ref msg, keyData);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        return TryCaptureHotkey(keyData) || base.ProcessDialogKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (TryCaptureHotkey(e.KeyData))
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private bool TryCaptureHotkey(Keys keyData)
    {
        if (CurrentCell is null || CurrentCell.ColumnIndex != 1)
        {
            return false;
        }

        return HotkeyCaptured?.Invoke(keyData) == true;
    }
}
