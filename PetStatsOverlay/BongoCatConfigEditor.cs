using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PetStatsOverlay;

public sealed class BongoCatConfigEditor
{
    private readonly string rootDirectory;
    private readonly string configPath;
    private readonly string standardAssetDirectory;
    private readonly string standardModelDirectory;
    private readonly string live2DLibraryDirectory;
    private readonly string backupDirectory;
    private const string SteamBongoCatAppId = "3419430";
    public const string PinkSkinId = "current_nuannuan";
    public const string PurpleSkinId = "nuannuan";
    private const string DefaultLive2DModelId = PinkSkinId;
    private const string ProfileFileName = "petstats-live2d-profile.json";
    private const string ProfileAssetsDirectoryName = "petstats-assets";

    public BongoCatConfigEditor(string? rootDirectory = null)
    {
        this.rootDirectory = rootDirectory ?? FindRootDirectory();
        configPath = Path.Combine(this.rootDirectory, "config.json");
        standardAssetDirectory = Path.Combine(this.rootDirectory, "img", "standard");
        standardModelDirectory = Path.Combine(standardAssetDirectory, "cat_model");
        live2DLibraryDirectory = Path.Combine(standardAssetDirectory, "live2d_models");
        backupDirectory = Path.Combine(this.rootDirectory, ".petstats_backups");
    }

    public string RootDirectory => rootDirectory;
    public string StandardAssetDirectory => standardAssetDirectory;
    public string StandardModelDirectory => standardModelDirectory;
    public string Live2DLibraryDirectory => live2DLibraryDirectory;
    public string BackupDirectory => backupDirectory;

    public IReadOnlyList<BuiltInSkinInfo> LoadBuiltInSkins()
    {
        var activeId = ReadActiveLive2DModelId(GetObject(LoadConfig(), "standard"));
        return new[]
        {
            new BuiltInSkinInfo(PinkSkinId, L.Text("粉色暖暖"), string.Equals(activeId, PinkSkinId, StringComparison.OrdinalIgnoreCase)),
            new BuiltInSkinInfo(PurpleSkinId, L.Text("紫色暖暖"), string.Equals(activeId, PurpleSkinId, StringComparison.OrdinalIgnoreCase))
        }
        .Where(skin => IsValidLive2DModelDirectory(ResolveLive2DModelDirectory(skin.Id)))
        .ToList();
    }

    public void SelectBuiltInSkin(string skinId)
    {
        if (!string.Equals(skinId, PinkSkinId, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(skinId, PurpleSkinId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentOutOfRangeException(nameof(skinId), skinId, L.Text("未知的内置皮肤。"));
        }

        SelectLive2DModel(skinId);
    }

    public string GetCurrentStandardPreviewPath()
    {
        try
        {
            var root = LoadConfig();
            var standard = GetObject(root, "standard");
            if (ReadBool(standard, "l2d"))
            {
                var texturePath = GetLive2DTexturePath();
                if (File.Exists(texturePath))
                {
                    return texturePath;
                }
            }
        }
        catch
        {
            // Fall back to the static sprite preview.
        }

        return GetAssetPath(BongoAssetKind.Cat);
    }

    public string GetCurrentLive2DTexturePath()
    {
        var texturePath = GetLive2DTexturePath();
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            throw new FileNotFoundException(L.Text("找不到当前 Live2D 贴图。"));
        }

        return texturePath;
    }

    public Live2DConfigSnapshot LoadSnapshot()
    {
        var customization = LoadCustomization();
        return new Live2DConfigSnapshot
        {
            Enabled = customization.Live2DEnabled,
            ExpressionKeys = customization.Live2DExpressionKeys,
            MotionKeys = ReadHotkeys(GetObject(LoadConfig(), "standard"), "l2d_motion", 2),
            ModelName = customization.ModelName
        };
    }

    public BongoCustomizationSnapshot LoadCustomization()
    {
        var root = LoadConfig();
        var standard = GetObject(root, "standard");

        return new BongoCustomizationSnapshot
        {
            Live2DEnabled = ReadBool(standard, "l2d"),
            ModelName = ReadModelName(),
            Live2DModelId = ReadActiveLive2DModelId(standard),
            AnimationKeys = ReadHotkeys(standard, "keyboard", Math.Max(1, CountHotkeys(standard, "keyboard"))),
            FaceKeys = ReadHotkeys(standard, "face", Math.Max(1, CountHotkeys(standard, "face"))),
            Live2DExpressionKeys = ReadHotkeys(standard, "l2d_expression", Math.Max(1, CountHotkeys(standard, "l2d_expression"))),
            Live2DMotionKeys = ReadHotkeys(standard, "l2d_motion", Math.Max(1, CountHotkeys(standard, "l2d_motion")))
        };
    }

    public List<Live2DModelInfo> LoadLive2DModels()
    {
        var activeId = EnsureCurrentLive2DModelRegistered();
        var result = new List<Live2DModelInfo>();
        Directory.CreateDirectory(live2DLibraryDirectory);
        if (!Directory.Exists(live2DLibraryDirectory))
        {
            return result;
        }

        foreach (var directory in Directory.GetDirectories(live2DLibraryDirectory, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(Directory.GetCreationTime)
            .ThenBy(Path.GetFileName))
        {
            var info = TryCreateLive2DModelInfo(directory, activeId);
            if (info is not null)
            {
                result.Add(info);
            }
        }

        return result;
    }

    public List<Live2DExpressionInfo> LoadLive2DExpressions()
    {
        var modelPath = Path.Combine(standardModelDirectory, "cat.model3.json");
        if (!File.Exists(modelPath))
        {
            return [];
        }

        var model = JsonNode.Parse(File.ReadAllText(modelPath)) as JsonObject;
        var references = model?["FileReferences"] as JsonObject;
        var expressions = references?["Expressions"] as JsonArray;
        if (expressions is null)
        {
            return [];
        }

        var texturePath = GetLive2DTexturePath(references);
        var result = new List<Live2DExpressionInfo>();
        foreach (var item in expressions)
        {
            if (item is not JsonObject expression)
            {
                continue;
            }

            var name = expression["Name"]?.GetValue<string>() ?? L.Format($"表情 {result.Count + 1}");
            var file = expression["File"]?.GetValue<string>() ?? "";
            var summary = ReadExpressionSummary(Path.Combine(standardModelDirectory, file));
            result.Add(new Live2DExpressionInfo(name, file, summary, texturePath));
        }

        return result;
    }

    public void SaveBindings(bool enabled, IReadOnlyList<string> expressionKeys, IReadOnlyList<string> motionKeys)
    {
        var root = LoadConfig();
        var standard = GetObject(root, "standard");
        standard["l2d"] = enabled;
        standard["l2d_expression"] = ToHotkeyArray(expressionKeys, 3);
        standard["l2d_motion"] = ToHotkeyArray(motionKeys, 2);
        standard["l2d_motion_lockhand"] = ToHotkeyArray(motionKeys, 2);
        SaveConfig(root);
    }

    public void SaveCustomization(BongoCustomizationSnapshot snapshot)
    {
        var root = LoadConfig();
        var standard = GetObject(root, "standard");
        standard["l2d"] = snapshot.Live2DEnabled;
        standard["keyboard"] = ToHotkeyArray(snapshot.AnimationKeys, snapshot.AnimationKeys.Count);
        standard["hand"] = ToHotkeyArray(snapshot.AnimationKeys, snapshot.AnimationKeys.Count);
        standard["face"] = ToHotkeyArray(snapshot.FaceKeys, snapshot.FaceKeys.Count);
        standard["l2d_expression"] = ToHotkeyArray(snapshot.Live2DExpressionKeys, snapshot.Live2DExpressionKeys.Count);
        if (snapshot.Live2DMotionKeys.Count > 0)
        {
            standard["l2d_motion"] = ToHotkeyArray(snapshot.Live2DMotionKeys, snapshot.Live2DMotionKeys.Count);
            standard["l2d_motion_lockhand"] = ToHotkeyArray(snapshot.Live2DMotionKeys, snapshot.Live2DMotionKeys.Count);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Live2DModelId))
        {
            standard["live2d_model"] = snapshot.Live2DModelId;
        }
        SaveConfig(root);
        SaveCurrentLive2DProfile(snapshot);
    }

    public void StartPet()
    {
        SyncActiveLive2DModelToRuntime();
        StartBundledPet();
    }

    public void RestartPet()
    {
        foreach (var process in Process.GetProcessesByName("BongoCatMver"))
        {
            try
            {
                process.Kill();
                process.WaitForExit(2_000);
            }
            catch
            {
                // Best-effort restart.
            }
        }

        StartPet();
    }

    private void StartBundledPet()
    {
        var petPath = Path.Combine(rootDirectory, "BongoCatMver.exe");
        if (!File.Exists(petPath))
        {
            throw new FileNotFoundException(L.Text("找不到 BongoCatMver.exe。"), petPath);
        }

        var target = Path.GetFullPath(petPath);
        foreach (var process in Process.GetProcessesByName("BongoCatMver"))
        {
            try
            {
                if (string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? ""), target, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                process.Kill();
                process.WaitForExit(2_000);
            }
            catch (Exception ex)
            {
                try
                {
                    if (process.HasExited)
                    {
                        continue;
                    }
                }
                catch
                {
                    // Fall through to the actionable error below.
                }

                throw new InvalidOperationException(L.Text("检测到另一个 BongoCatMver 实例，但无法关闭。请先从托盘退出旧桌宠后重试。"), ex);
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = petPath,
            WorkingDirectory = rootDirectory,
            UseShellExecute = false
        };
        // The bundled pet requests elevation in its legacy manifest. It needs no
        // administrative access, and matching our integrity level lets the
        // companion maintain silhouette-only pointer interaction.
        startInfo.Environment["__COMPAT_LAYER"] = "RunAsInvoker";
        Process.Start(startInfo);
    }

    public void StartSteamBongoCat()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = $"steam://run/{SteamBongoCatAppId}",
            UseShellExecute = true
        });
    }

    public void SyncActiveLive2DModelToRuntime()
    {
        var root = LoadConfig();
        var standard = GetObject(root, "standard");
        var activeId = ReadActiveLive2DModelId(standard);
        if (string.IsNullOrWhiteSpace(activeId))
        {
            return;
        }

        var sourceDirectory = ResolveLive2DModelDirectory(activeId);
        if (!IsValidLive2DModelDirectory(sourceDirectory))
        {
            return;
        }

        SyncLive2DModelDirectoryToRuntime(sourceDirectory);
    }

    public string GetAssetPath(BongoAssetKind kind, int index = 0)
    {
        return kind switch
        {
            BongoAssetKind.Cat => Path.Combine(standardAssetDirectory, "cat.png"),
            BongoAssetKind.Arm => Path.Combine(standardAssetDirectory, "arm.png"),
            BongoAssetKind.Mouse => Path.Combine(standardAssetDirectory, "mouse.png"),
            BongoAssetKind.MouseLeft => Path.Combine(standardAssetDirectory, "mouse_left.png"),
            BongoAssetKind.MouseRight => Path.Combine(standardAssetDirectory, "mouse_right.png"),
            BongoAssetKind.MouseSide => Path.Combine(standardAssetDirectory, "mouse_side.png"),
            BongoAssetKind.MouseBackground => Path.Combine(standardAssetDirectory, "mousebg.png"),
            BongoAssetKind.Tablet => Path.Combine(standardAssetDirectory, "tablet.png"),
            BongoAssetKind.TabletBackground => Path.Combine(standardAssetDirectory, "tabletbg.png"),
            BongoAssetKind.TabletLeft => Path.Combine(standardAssetDirectory, "tablet_left.png"),
            BongoAssetKind.TabletRight => Path.Combine(standardAssetDirectory, "tablet_right.png"),
            BongoAssetKind.Up => Path.Combine(standardAssetDirectory, "up.png"),
            BongoAssetKind.Live2DTexture => GetCurrentLive2DTexturePath(),
            BongoAssetKind.Keyboard => Path.Combine(standardAssetDirectory, "keyboard", $"{Math.Max(0, index)}.png"),
            BongoAssetKind.Hand => Path.Combine(standardAssetDirectory, "hand", $"{Math.Max(0, index)}.png"),
            BongoAssetKind.Face => Path.Combine(standardAssetDirectory, "face", $"{Math.Max(0, index)}.png"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public void ReplaceAsset(BongoAssetKind kind, string sourcePath, int index = 0)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(L.Text("找不到要导入的图片。"), sourcePath);
        }

        if (!string.Equals(Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(L.Text("目前只支持导入 PNG 图片。"));
        }

        var targetPath = GetAssetPath(kind, index);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        BackupFile(targetPath);
        File.Copy(sourcePath, targetPath, overwrite: true);
        if (kind == BongoAssetKind.Live2DTexture)
        {
            ReplaceActiveLive2DLibraryTexture(targetPath);
        }
    }

    public void RestoreDefaultStandardPet()
    {
        var sourceDirectory = GetDefaultStandardModelDirectory();
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(L.Text("找不到默认桌宠模型备份。"));
        }

        BackupDirectoryTree(standardModelDirectory, Path.Combine("img", "standard", "cat_model"));

        var tempPath = Path.Combine(standardAssetDirectory, $"cat_model_restore_tmp_{DateTime.Now:yyyyMMdd_HHmmss}");
        CopyDirectory(sourceDirectory, tempPath);
        try
        {
            if (Directory.Exists(standardModelDirectory))
            {
                Directory.Delete(standardModelDirectory, recursive: true);
            }

            Directory.Move(tempPath, standardModelDirectory);
        }
        catch
        {
            if (Directory.Exists(tempPath) && IsInsideDirectory(tempPath, standardAssetDirectory))
            {
                Directory.Delete(tempPath, recursive: true);
            }

            throw;
        }

        var root = LoadConfig();
        root["mode"] = 1;
        var standard = GetObject(root, "standard");
        standard["l2d"] = true;
        standard["live2d_model"] = EnsureModelInLibrary(sourceDirectory, "default");
        SaveConfig(root);
    }

    public Live2DModelInfo ImportLive2DModel(string sourceDirectory)
    {
        var modelRoot = FindLive2DModelRoot(sourceDirectory);
        ValidateLive2DModel(modelRoot);
        _ = EnsureCurrentLive2DModelRegistered();

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var modelName = ReadModelNameFromDirectory(modelRoot);
        var modelId = UniqueModelId($"{SanitizeModelId(modelName)}_{stamp}");
        var targetPath = Path.Combine(live2DLibraryDirectory, modelId);
        var tempPath = Path.Combine(live2DLibraryDirectory, $"import_tmp_{stamp}");

        Directory.CreateDirectory(live2DLibraryDirectory);
        CopyDirectory(modelRoot, tempPath);
        try
        {
            NormalizeModelEntryFile(tempPath);
            ValidateLive2DModel(tempPath);
            Directory.Move(tempPath, targetPath);
        }
        catch
        {
            if (Directory.Exists(tempPath) && IsInsideDirectory(tempPath, live2DLibraryDirectory))
            {
                Directory.Delete(tempPath, recursive: true);
            }

            throw;
        }

        SelectLive2DModel(modelId);
        return TryCreateLive2DModelInfo(targetPath, modelId)
            ?? new Live2DModelInfo(modelId, modelName, targetPath, Path.Combine(targetPath, "cat.model3.json"), "", true);
    }

    public void DeleteLive2DModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException(L.Text("请选择一个要删除的 Live2D 模型。"));
        }

        var activeId = ReadActiveLive2DModelId(GetObject(LoadConfig(), "standard"));
        if (string.Equals(modelId, activeId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(L.Text("当前正在使用的 Live2D 模型不能删除，请先切换到其他模型。"));
        }

        var targetDirectory = Path.Combine(live2DLibraryDirectory, modelId);
        if (!Directory.Exists(targetDirectory))
        {
            throw new DirectoryNotFoundException(L.Text("找不到要删除的 Live2D 模型文件夹。"));
        }

        if (!IsInsideDirectory(targetDirectory, live2DLibraryDirectory))
        {
            throw new InvalidOperationException(L.Text("模型目录不在 Live2D 模型库中。"));
        }

        Directory.Delete(targetDirectory, recursive: true);
    }

    public void SelectLive2DModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException(L.Text("请选择一个 Live2D 模型。"));
        }

        var sourceDirectory = ResolveLive2DModelDirectory(modelId);
        ValidateLive2DModel(sourceDirectory);

        SyncLive2DModelDirectoryToRuntime(sourceDirectory);

        var root = LoadConfig();
        root["mode"] = 1;
        var standard = GetObject(root, "standard");
        standard["l2d"] = true;
        standard["live2d_model"] = modelId;
        ApplyLive2DProfile(sourceDirectory, standard);
        SaveConfig(root);
    }

    private void SyncLive2DModelDirectoryToRuntime(string sourceDirectory)
    {
        ValidateLive2DModel(sourceDirectory);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var tempPath = Path.Combine(standardAssetDirectory, $"cat_model_select_tmp_{stamp}");
        CopyDirectory(sourceDirectory, tempPath);
        try
        {
            NormalizeModelEntryFile(tempPath);
            if (Directory.Exists(standardModelDirectory))
            {
                if (!IsInsideDirectory(standardModelDirectory, standardAssetDirectory))
                {
                    throw new InvalidOperationException("cat_model path is outside the standard asset directory.");
                }

                Directory.Delete(standardModelDirectory, recursive: true);
            }

            Directory.Move(tempPath, standardModelDirectory);
        }
        catch
        {
            if (Directory.Exists(tempPath) && IsInsideDirectory(tempPath, standardAssetDirectory))
            {
                Directory.Delete(tempPath, recursive: true);
            }

            throw;
        }
    }

    public void OpenAssetsFolder()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = standardAssetDirectory,
            UseShellExecute = true
        });
    }

    public void OpenLive2DLibraryFolder()
    {
        Directory.CreateDirectory(live2DLibraryDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = live2DLibraryDirectory,
            UseShellExecute = true
        });
    }

    public Live2DModelInfo ImportLive2DPackage(string packagePath)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException(L.Text("找不到 Live2D 整包。"), packagePath);
        }

        Directory.CreateDirectory(live2DLibraryDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var tempPath = Path.Combine(live2DLibraryDirectory, $"package_tmp_{stamp}");
        Directory.CreateDirectory(tempPath);
        try
        {
            ZipFile.ExtractToDirectory(packagePath, tempPath);
            var sourceDirectory = FindPackageModelRoot(tempPath);
            return ImportLive2DModel(sourceDirectory);
        }
        finally
        {
            if (Directory.Exists(tempPath) && IsInsideDirectory(tempPath, live2DLibraryDirectory))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    public void ExportLive2DModelPackage(string modelId, string packagePath, BongoCustomizationSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException(L.Text("请选择一个要导出的 Live2D 模型。"));
        }

        if (string.Equals(snapshot.Live2DModelId, modelId, StringComparison.OrdinalIgnoreCase))
        {
            SaveCurrentLive2DProfile(snapshot);
        }

        var sourceDirectory = ResolveLive2DModelDirectory(modelId);
        ValidateLive2DModel(sourceDirectory);
        if (IsInsideDirectory(packagePath, sourceDirectory))
        {
            throw new InvalidOperationException(L.Text("导出文件不能放在模型目录里面。"));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        ZipFile.CreateFromDirectory(sourceDirectory, packagePath, CompressionLevel.Optimal, includeBaseDirectory: true);
    }

    public void SaveCurrentLive2DProfile(BongoCustomizationSnapshot snapshot)
    {
        var modelId = string.IsNullOrWhiteSpace(snapshot.Live2DModelId)
            ? ReadActiveLive2DModelId(GetObject(LoadConfig(), "standard"))
            : snapshot.Live2DModelId;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        var modelDirectory = ResolveLive2DModelDirectory(modelId);
        if (!Directory.Exists(modelDirectory))
        {
            return;
        }

        WriteLive2DProfile(modelDirectory, snapshot);
        MirrorRuntimeAssetsToProfile(modelDirectory, snapshot);
    }

    private JsonObject LoadConfig()
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(L.Text("找不到 config.json。"), configPath);
        }

        return JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject
            ?? throw new InvalidOperationException(L.Text("config.json 不是有效对象。"));
    }

    private void SaveConfig(JsonObject root)
    {
        File.WriteAllText(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private string ReadModelName()
    {
        var modelPath = Path.Combine(standardModelDirectory, "cat.model3.json");
        if (!File.Exists(modelPath))
        {
            return L.Text("未找到模型");
        }

        try
        {
            var model = JsonNode.Parse(File.ReadAllText(modelPath)) as JsonObject;
            var moc = model?["FileReferences"]?["Moc"]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(moc) ? "cat.model3.json" : moc;
        }
        catch
        {
            return "cat.model3.json";
        }
    }

    private string EnsureCurrentLive2DModelRegistered()
    {
        var root = LoadConfig();
        var standard = GetObject(root, "standard");
        var activeId = ReadActiveLive2DModelId(standard);
        if (!string.IsNullOrWhiteSpace(activeId)
            && IsValidLive2DModelDirectory(ResolveLive2DModelDirectory(activeId)))
        {
            return activeId;
        }

        if (!IsValidLive2DModelDirectory(standardModelDirectory))
        {
            return "";
        }

        var modelId = EnsureModelInLibrary(standardModelDirectory, "current");
        standard["live2d_model"] = modelId;
        SaveConfig(root);
        return modelId;
    }

    private string EnsureModelInLibrary(string sourceDirectory, string prefix)
    {
        ValidateLive2DModel(sourceDirectory);
        Directory.CreateDirectory(live2DLibraryDirectory);

        if (IsInsideDirectory(sourceDirectory, live2DLibraryDirectory))
        {
            return Path.GetFileName(Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        var modelId = UniqueModelId($"{prefix}_{SanitizeModelId(ReadModelNameFromDirectory(sourceDirectory))}");
        var tempPath = Path.Combine(live2DLibraryDirectory, $"library_tmp_{DateTime.Now:yyyyMMdd_HHmmss}");
        var targetPath = Path.Combine(live2DLibraryDirectory, modelId);
        CopyDirectory(sourceDirectory, tempPath);
        try
        {
            NormalizeModelEntryFile(tempPath);
            ValidateLive2DModel(tempPath);
            Directory.Move(tempPath, targetPath);
        }
        catch
        {
            if (Directory.Exists(tempPath) && IsInsideDirectory(tempPath, live2DLibraryDirectory))
            {
                Directory.Delete(tempPath, recursive: true);
            }

            throw;
        }

        return modelId;
    }

    private Live2DModelInfo? TryCreateLive2DModelInfo(string directory, string activeId)
    {
        var modelRoot = ResolveLive2DModelDirectory(Path.GetFileName(directory));
        var modelPath = FindModelFile(modelRoot);
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            return null;
        }

        try
        {
            var model = JsonNode.Parse(File.ReadAllText(modelPath)) as JsonObject;
            var references = model?["FileReferences"] as JsonObject;
            var moc = references?["Moc"]?.GetValue<string>() ?? "";
            var profile = ReadLive2DProfile(modelRoot);
            var name = profile?.ModelName ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Path.GetFileNameWithoutExtension(moc);
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Path.GetFileName(directory);
            }

            var texturePath = GetLive2DTexturePath(modelRoot, references);
            var id = Path.GetFileName(directory);
            if (id == PinkSkinId) name = L.Text("粉色暖暖");
            if (id == PurpleSkinId) name = L.Text("紫色暖暖");
            return new Live2DModelInfo(id, name, modelRoot, modelPath, texturePath, string.Equals(id, activeId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private string ResolveLive2DModelDirectory(string modelId)
    {
        var librarySlot = Path.Combine(live2DLibraryDirectory, modelId);
        if (!Directory.Exists(librarySlot) || IsValidLive2DModelDirectory(librarySlot))
        {
            return librarySlot;
        }

        try
        {
            return FindLive2DModelRoot(librarySlot);
        }
        catch
        {
            return librarySlot;
        }
    }

    private void ReplaceActiveLive2DLibraryTexture(string sourceTexturePath)
    {
        var root = LoadConfig();
        var standard = GetObject(root, "standard");
        var activeId = ReadActiveLive2DModelId(standard);
        if (string.IsNullOrWhiteSpace(activeId))
        {
            return;
        }

        var modelDirectory = ResolveLive2DModelDirectory(activeId);
        var modelPath = FindModelFile(modelDirectory);
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            return;
        }

        try
        {
            var model = JsonNode.Parse(File.ReadAllText(modelPath)) as JsonObject;
            var references = model?["FileReferences"] as JsonObject;
            var targetTexturePath = GetLive2DTexturePath(modelDirectory, references);
            if (string.IsNullOrWhiteSpace(targetTexturePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetTexturePath)!);
            File.Copy(sourceTexturePath, targetTexturePath, overwrite: true);
        }
        catch
        {
            // Keep the immediate texture replacement even if the library mirror cannot be updated.
        }
    }

    private void ApplyLive2DProfile(string modelDirectory, JsonObject standard)
    {
        var profile = ReadLive2DProfile(modelDirectory);
        if (profile is null)
        {
            return;
        }

        standard["l2d"] = profile.Live2DEnabled;
        standard["keyboard"] = ToHotkeyArray(profile.AnimationKeys, profile.AnimationKeys.Count);
        standard["hand"] = ToHotkeyArray(profile.AnimationKeys, profile.AnimationKeys.Count);
        standard["face"] = ToHotkeyArray(profile.FaceKeys, profile.FaceKeys.Count);
        standard["l2d_expression"] = ToHotkeyArray(profile.Live2DExpressionKeys, profile.Live2DExpressionKeys.Count);
        if (profile.Live2DMotionKeys.Count > 0)
        {
            standard["l2d_motion"] = ToHotkeyArray(profile.Live2DMotionKeys, profile.Live2DMotionKeys.Count);
            standard["l2d_motion_lockhand"] = ToHotkeyArray(profile.Live2DMotionKeys, profile.Live2DMotionKeys.Count);
        }

        ApplyProfileAssetsToRuntime(modelDirectory);
    }

    private Live2DModelProfile? ReadLive2DProfile(string modelDirectory)
    {
        var profilePath = Path.Combine(modelDirectory, ProfileFileName);
        if (!File.Exists(profilePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Live2DModelProfile>(File.ReadAllText(profilePath));
        }
        catch
        {
            return null;
        }
    }

    private void WriteLive2DProfile(string modelDirectory, BongoCustomizationSnapshot snapshot)
    {
        var existingProfile = ReadLive2DProfile(modelDirectory);
        var profile = new Live2DModelProfile
        {
            Version = 1,
            ModelName = string.IsNullOrWhiteSpace(existingProfile?.ModelName)
                ? ReadModelNameFromDirectory(modelDirectory)
                : existingProfile.ModelName,
            Live2DEnabled = snapshot.Live2DEnabled,
            AnimationKeys = snapshot.AnimationKeys,
            FaceKeys = snapshot.FaceKeys,
            Live2DExpressionKeys = snapshot.Live2DExpressionKeys,
            Live2DMotionKeys = snapshot.Live2DMotionKeys.Count > 0
                ? snapshot.Live2DMotionKeys
                : ReadCurrentMotionKeys()
        };

        File.WriteAllText(
            Path.Combine(modelDirectory, ProfileFileName),
            JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
    }

    private List<string> ReadCurrentMotionKeys()
    {
        var standard = GetObject(LoadConfig(), "standard");
        return ReadHotkeys(standard, "l2d_motion", Math.Max(1, CountHotkeys(standard, "l2d_motion")));
    }

    private void MirrorRuntimeLive2DModelToLibrary(string modelDirectory)
    {
        var tempPath = Path.Combine(live2DLibraryDirectory, $"mirror_tmp_{DateTime.Now:yyyyMMdd_HHmmss}");
        CopyDirectory(standardModelDirectory, tempPath);
        try
        {
            NormalizeModelEntryFile(tempPath);
            ValidateLive2DModel(tempPath);
            if (Directory.Exists(modelDirectory))
            {
                Directory.Delete(modelDirectory, recursive: true);
            }

            Directory.Move(tempPath, modelDirectory);
        }
        catch
        {
            if (Directory.Exists(tempPath) && IsInsideDirectory(tempPath, live2DLibraryDirectory))
            {
                Directory.Delete(tempPath, recursive: true);
            }

            throw;
        }
    }

    private void MirrorRuntimeAssetsToProfile(string modelDirectory, BongoCustomizationSnapshot snapshot)
    {
        var assetsDirectory = Path.Combine(modelDirectory, ProfileAssetsDirectoryName);
        if (Directory.Exists(assetsDirectory))
        {
            Directory.Delete(assetsDirectory, recursive: true);
        }

        Directory.CreateDirectory(assetsDirectory);
        foreach (var fileName in ProfileRootAssetFiles())
        {
            CopyFileIfExists(Path.Combine(standardAssetDirectory, fileName), Path.Combine(assetsDirectory, fileName));
        }

        CopyIndexedProfileAssets("keyboard", snapshot.AnimationKeys.Count, assetsDirectory);
        CopyIndexedProfileAssets("hand", snapshot.AnimationKeys.Count, assetsDirectory);
        CopyIndexedProfileAssets("face", snapshot.FaceKeys.Count, assetsDirectory);

        var soundsDirectory = Path.Combine(standardAssetDirectory, "sounds");
        if (Directory.Exists(soundsDirectory))
        {
            CopyDirectory(soundsDirectory, Path.Combine(assetsDirectory, "sounds"));
        }
    }

    private void CopyIndexedProfileAssets(string directoryName, int count, string assetsDirectory)
    {
        var sourceDirectory = Path.Combine(standardAssetDirectory, directoryName);
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        var targetDirectory = Path.Combine(assetsDirectory, directoryName);
        Directory.CreateDirectory(targetDirectory);
        for (var index = 0; index < count; index++)
        {
            CopyFileIfExists(Path.Combine(sourceDirectory, $"{index}.png"), Path.Combine(targetDirectory, $"{index}.png"));
        }
    }

    private void ApplyProfileAssetsToRuntime(string modelDirectory)
    {
        var assetsDirectory = Path.Combine(modelDirectory, ProfileAssetsDirectoryName);
        if (!Directory.Exists(assetsDirectory))
        {
            return;
        }

        foreach (var fileName in ProfileRootAssetFiles())
        {
            CopyFileIfExists(Path.Combine(assetsDirectory, fileName), Path.Combine(standardAssetDirectory, fileName));
        }

        foreach (var directoryName in ProfileAssetDirectories())
        {
            var sourceDirectory = Path.Combine(assetsDirectory, directoryName);
            if (!Directory.Exists(sourceDirectory))
            {
                continue;
            }

            var targetDirectory = Path.Combine(standardAssetDirectory, directoryName);
            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, recursive: true);
            }

            CopyDirectory(sourceDirectory, targetDirectory);
        }
    }

    private static string FindLive2DModelRoot(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(L.Text("找不到 Live2D 模型文件夹。"));
        }

        if (IsValidLive2DModelDirectory(sourceDirectory))
        {
            return sourceDirectory;
        }

        var candidates = Directory.GetFiles(sourceDirectory, "*.model3.json", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsValidLive2DModelDirectory)
            .OrderByDescending(directory => File.Exists(Path.Combine(directory, ProfileFileName)))
            .ThenBy(directory => directory.Length)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(L.Text("这个文件夹里没有找到有效的 Live2D 模型。"));
        }

        return candidates[0];
    }

    private static string FindPackageModelRoot(string extractedDirectory)
    {
        var candidates = Directory.GetFiles(extractedDirectory, "*.model3.json", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsValidLive2DModelDirectory)
            .OrderByDescending(directory => File.Exists(Path.Combine(directory, ProfileFileName)))
            .ThenBy(directory => directory.Length)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(L.Text("整包里没有找到有效的 Live2D 模型。"));
        }

        return candidates[0];
    }

    private static void CopyFileIfExists(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: true);
    }

    private static string[] ProfileRootAssetFiles()
    {
        return
        [
            "cat.png",
            "arm.png",
            "mouse.png",
            "mouse_left.png",
            "mouse_right.png",
            "mouse_side.png",
            "mousebg.png",
            "tablet.png",
            "tabletbg.png",
            "tablet_left.png",
            "tablet_right.png",
            "up.png"
        ];
    }

    private static string[] ProfileAssetDirectories()
    {
        return ["keyboard", "hand", "face", "sounds"];
    }

    private static string ReadModelNameFromDirectory(string modelDirectory)
    {
        var modelPath = FindModelFile(modelDirectory);
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            return "model";
        }

        try
        {
            var model = JsonNode.Parse(File.ReadAllText(modelPath)) as JsonObject;
            var moc = model?["FileReferences"]?["Moc"]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(moc) ? Path.GetFileNameWithoutExtension(modelPath) : Path.GetFileNameWithoutExtension(moc);
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(modelPath);
        }
    }

    private static string ReadActiveLive2DModelId(JsonObject standard)
    {
        return standard["live2d_model"]?.GetValue<string>() ?? "";
    }

    private static string FindModelFile(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return "";
        }

        var standardModel = Path.Combine(directory, "cat.model3.json");
        if (File.Exists(standardModel))
        {
            return standardModel;
        }

        var modelFiles = Directory.GetFiles(directory, "*.model3.json", SearchOption.TopDirectoryOnly);
        return modelFiles.Length == 1 ? modelFiles[0] : "";
    }

    private static bool IsValidLive2DModelDirectory(string directory)
    {
        try
        {
            ValidateLive2DModel(directory);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void NormalizeModelEntryFile(string directory)
    {
        var modelPath = FindModelFile(directory);
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return;
        }

        var targetPath = Path.Combine(directory, "cat.model3.json");
        if (!string.Equals(Path.GetFullPath(modelPath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Move(modelPath, targetPath, overwrite: true);
        }
    }

    private string UniqueModelId(string preferredId)
    {
        var id = string.IsNullOrWhiteSpace(preferredId) ? "model" : preferredId;
        var candidate = id;
        var suffix = 2;
        while (Directory.Exists(Path.Combine(live2DLibraryDirectory, candidate)))
        {
            candidate = $"{id}_{suffix++}";
        }

        return candidate;
    }

    private static string SanitizeModelId(string value)
    {
        var characters = value
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();
        var sanitized = new string(characters).Trim('_');
        while (sanitized.Contains("__", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("__", "_", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "model" : sanitized;
    }

    private string GetLive2DTexturePath(JsonObject? references = null)
    {
        if (references is null)
        {
            var modelPath = Path.Combine(standardModelDirectory, "cat.model3.json");
            if (!File.Exists(modelPath))
            {
                return "";
            }

            var model = JsonNode.Parse(File.ReadAllText(modelPath)) as JsonObject;
            references = model?["FileReferences"] as JsonObject;
        }

        if (references?["Textures"] is not JsonArray textures || textures.Count == 0)
        {
            return "";
        }

        var texture = textures[0]?.GetValue<string>() ?? "";
        return string.IsNullOrWhiteSpace(texture) ? "" : Path.Combine(standardModelDirectory, texture);
    }

    private static string GetLive2DTexturePath(string modelDirectory, JsonObject? references)
    {
        if (references?["Textures"] is not JsonArray textures || textures.Count == 0)
        {
            return "";
        }

        var texture = textures[0]?.GetValue<string>() ?? "";
        return string.IsNullOrWhiteSpace(texture) ? "" : Path.Combine(modelDirectory, texture);
    }

    private void EnsureStandardLive2DEnabled()
    {
        var root = LoadConfig();
        root["mode"] = 1;
        var standard = GetObject(root, "standard");
        standard["l2d"] = true;
        SaveConfig(root);
    }

    private static string ReadExpressionSummary(string expressionPath)
    {
        if (!File.Exists(expressionPath))
        {
            return L.Text("找不到表情文件");
        }

        try
        {
            var expression = JsonNode.Parse(File.ReadAllText(expressionPath)) as JsonObject;
            if (expression?["Parameters"] is not JsonArray parameters || parameters.Count == 0)
            {
                return L.Text("默认表情");
            }

            var rows = parameters
                .OfType<JsonObject>()
                .Select(parameter =>
                {
                    var id = parameter["Id"]?.GetValue<string>() ?? "Param";
                    var value = parameter["Value"]?.ToString() ?? "";
                    var blend = parameter["Blend"]?.GetValue<string>() ?? "";
                    return string.IsNullOrWhiteSpace(blend) ? $"{id}={value}" : $"{id}={value} {blend}";
                })
                .Take(3)
                .ToArray();
            return string.Join(", ", rows);
        }
        catch
        {
            return Path.GetFileName(expressionPath);
        }
    }

    private static JsonObject GetObject(JsonObject root, string name)
    {
        if (root[name] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        root[name] = created;
        return created;
    }

    private static bool ReadBool(JsonObject root, string name)
    {
        return root[name]?.GetValue<bool>() ?? false;
    }

    private static string ReadHotkey(JsonObject root, string arrayName, int index)
    {
        if (root[arrayName] is not JsonArray rows || index >= rows.Count || rows[index] is not JsonArray keys)
        {
            return "";
        }

        return string.Join("+", keys.Select(KeyName));
    }

    private static List<string> ReadHotkeys(JsonObject root, string arrayName, int count)
    {
        var keys = new List<string>();
        for (var index = 0; index < count; index++)
        {
            keys.Add(ReadHotkey(root, arrayName, index));
        }

        return keys;
    }

    private static int CountHotkeys(JsonObject root, string arrayName)
    {
        return root[arrayName] is JsonArray rows ? rows.Count : 0;
    }

    private static JsonArray ToHotkeyArray(IReadOnlyList<string> hotkeys, int count)
    {
        var array = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            array.Add(ParseHotkey(index < hotkeys.Count ? hotkeys[index] : ""));
        }

        return array;
    }

    private static JsonArray ParseHotkey(string hotkey)
    {
        var array = new JsonArray();
        foreach (var part in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryKeyCode(part, out var code))
            {
                array.Add(code);
            }
        }

        return array;
    }

    private static bool TryKeyCode(string raw, out int code)
    {
        var key = raw.Trim().ToUpperInvariant();
        code = key switch
        {
            "CTRL" or "CONTROL" => 17,
            "SHIFT" => 16,
            "ALT" => 18,
            "SPACE" => 32,
            "ENTER" => 13,
            "TAB" => 9,
            "ESC" or "ESCAPE" => 27,
            "BACKSPACE" or "BKSP" => 8,
            "DELETE" or "DEL" => 46,
            "INSERT" or "INS" => 45,
            "HOME" => 36,
            "END" => 35,
            "PGUP" or "PAGEUP" => 33,
            "PGDN" or "PAGEDOWN" => 34,
            "UP" => 38,
            "DOWN" => 40,
            "LEFT" => 37,
            "RIGHT" => 39,
            "NUM0" or "NUMPAD0" => 96,
            "NUM1" or "NUMPAD1" => 97,
            "NUM2" or "NUMPAD2" => 98,
            "NUM3" or "NUMPAD3" => 99,
            "NUM4" or "NUMPAD4" => 100,
            "NUM5" or "NUMPAD5" => 101,
            "NUM6" or "NUMPAD6" => 102,
            "NUM7" or "NUMPAD7" => 103,
            "NUM8" or "NUMPAD8" => 104,
            "NUM9" or "NUMPAD9" => 105,
            "NUM+" or "NUMADD" => 107,
            "NUM-" or "NUMSUBTRACT" => 109,
            _ => 0
        };
        if (code > 0)
        {
            return true;
        }

        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z')
        {
            code = key[0];
            return true;
        }

        if (key.Length == 1 && key[0] is >= '0' and <= '9')
        {
            code = key[0];
            return true;
        }

        if (key.StartsWith('F') && int.TryParse(key[1..], out var fn) && fn is >= 1 and <= 24)
        {
            code = 111 + fn;
            return true;
        }

        return int.TryParse(key, out code);
    }

    private static string KeyName(JsonNode? node)
    {
        var code = node?.GetValue<int>() ?? 0;
        return code switch
        {
            8 => "Backspace",
            9 => "Tab",
            13 => "Enter",
            16 => "Shift",
            17 => "Ctrl",
            18 => "Alt",
            27 => "Esc",
            32 => "Space",
            33 => "PageUp",
            34 => "PageDown",
            35 => "End",
            36 => "Home",
            37 => "Left",
            38 => "Up",
            39 => "Right",
            40 => "Down",
            45 => "Insert",
            46 => "Delete",
            >= 48 and <= 57 => ((char)code).ToString(),
            >= 65 and <= 90 => ((char)code).ToString(),
            >= 96 and <= 105 => $"Num{code - 96}",
            107 => "Num+",
            109 => "Num-",
            >= 112 and <= 135 => $"F{code - 111}",
            _ => code.ToString()
        };
    }

    private void BackupFile(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            return;
        }

        var relativePath = Path.GetRelativePath(rootDirectory, targetPath);
        var backupPath = Path.Combine(backupDirectory, DateTime.Now.ToString("yyyyMMdd_HHmmss"), relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        File.Copy(targetPath, backupPath, overwrite: false);
    }

    private void BackupDirectoryTree(string targetDirectory, string relativeDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            return;
        }

        var backupPath = Path.Combine(backupDirectory, DateTime.Now.ToString("yyyyMMdd_HHmmss"), relativeDirectory);
        CopyDirectory(targetDirectory, backupPath);
    }

    private string GetDefaultStandardModelDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(live2DLibraryDirectory, DefaultLive2DModelId),
            Path.Combine(live2DLibraryDirectory, "nuannuan")
        };

        return candidates
            .FirstOrDefault(IsNuannuanModelDirectory)
            ?? "";
    }

    private static bool IsNuannuanModelDirectory(string directory)
    {
        var modelPath = Path.Combine(directory, "cat.model3.json");
        if (!File.Exists(modelPath))
        {
            return false;
        }

        try
        {
            var model = JsonNode.Parse(File.ReadAllText(modelPath)) as JsonObject;
            var references = model?["FileReferences"] as JsonObject;
            var moc = references?["Moc"]?.GetValue<string>() ?? "";
            var textures = references?["Textures"] as JsonArray;
            var texture = textures?.FirstOrDefault()?.GetValue<string>() ?? "";
            return moc.Contains("nuannuan", StringComparison.OrdinalIgnoreCase)
                && texture.Contains("nuannuan", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void ValidateLive2DModel(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(L.Text("找不到 Live2D 模型文件夹。"));
        }

        var modelFiles = Directory.GetFiles(sourceDirectory, "*.model3.json", SearchOption.TopDirectoryOnly);
        if (modelFiles.Length != 1)
        {
            throw new InvalidOperationException(L.Text("请选择只包含一个 .model3.json 的 Live2D 模型根目录。"));
        }

        var modelRoot = JsonNode.Parse(File.ReadAllText(modelFiles[0])) as JsonObject
            ?? throw new InvalidOperationException(L.Text("model3.json 不是有效 JSON。"));
        var references = modelRoot["FileReferences"] as JsonObject
            ?? throw new InvalidOperationException(L.Text("model3.json 缺少 FileReferences。"));
        var moc = references["Moc"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(moc) || !File.Exists(Path.Combine(sourceDirectory, moc)))
        {
            throw new InvalidOperationException(L.Text("Live2D 模型缺少有效的 Moc 文件。"));
        }

        if (references["Textures"] is not JsonArray textures || textures.Count == 0)
        {
            throw new InvalidOperationException(L.Text("Live2D 模型缺少贴图列表。"));
        }

        foreach (var texture in textures)
        {
            var texturePath = texture?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(texturePath) || !File.Exists(Path.Combine(sourceDirectory, texturePath)))
            {
                throw new InvalidOperationException(L.Format($"Live2D 贴图不存在：{texturePath}"));
            }
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            File.Copy(file, Path.Combine(targetDirectory, relativePath), overwrite: true);
        }
    }

    private static bool IsInsideDirectory(string path, string parentDirectory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullParent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), fullParent, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(fullParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(fullParent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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

public sealed class Live2DConfigSnapshot
{
    public bool Enabled { get; set; }
    public string ModelName { get; set; } = "";
    public List<string> ExpressionKeys { get; set; } = new();
    public List<string> MotionKeys { get; set; } = new();
}

public sealed record Live2DExpressionInfo(string Name, string File, string ParameterSummary, string TexturePath);

public sealed record BuiltInSkinInfo(string Id, string Name, bool IsActive);

public sealed record Live2DModelInfo(string Id, string Name, string Directory, string ModelFile, string PreviewTexturePath, bool IsActive)
{
    public override string ToString()
    {
        return IsActive ? L.Format($"{Name}（当前）") : Name;
    }
}


public sealed class BongoCustomizationSnapshot
{
    public bool Live2DEnabled { get; set; }
    public string ModelName { get; set; } = "";
    public string Live2DModelId { get; set; } = "";
    public List<string> AnimationKeys { get; set; } = new();
    public List<string> FaceKeys { get; set; } = new();
    public List<string> Live2DExpressionKeys { get; set; } = new();
    public List<string> Live2DMotionKeys { get; set; } = new();
}

public sealed class Live2DModelProfile
{
    public int Version { get; set; } = 1;
    public string ModelName { get; set; } = "";
    public bool Live2DEnabled { get; set; } = true;
    public List<string> AnimationKeys { get; set; } = new();
    public List<string> FaceKeys { get; set; } = new();
    public List<string> Live2DExpressionKeys { get; set; } = new();
    public List<string> Live2DMotionKeys { get; set; } = new();
}

public enum BongoAssetKind
{
    Cat,
    Arm,
    Mouse,
    MouseLeft,
    MouseRight,
    MouseSide,
    MouseBackground,
    Tablet,
    TabletBackground,
    TabletLeft,
    TabletRight,
    Up,
    Keyboard,
    Hand,
    Face,
    Live2DTexture
}
