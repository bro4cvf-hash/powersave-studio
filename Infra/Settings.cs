using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerSave.Infra;

public sealed class TogglesConfig
{
    public bool PauseIndexingOnUltraSave { get; set; } = true;
    public bool ReduceVisualEffectsOnUltraSave { get; set; } = true;
    public bool WifiPowerSaving { get; set; } = true;
    public bool UsbSelectiveSuspendOff { get; set; } = true;
    public bool KeepDisplayAwakeOnPerf { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool CloseToTray { get; set; } = true;
}

public sealed class AppSettings
{
    public string Version { get; set; } = "1.0";
    public string LastMode { get; set; } = "";
    public Dictionary<string, string> PlanIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public TogglesConfig Toggles { get; set; } = new();

    public string? IndexingOriginalStart { get; set; }
    public bool IndexingChangedByUs { get; set; }
    public Dictionary<string, string> ServiceOriginalStart { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool ServicesChangedByUs { get; set; }
    public int? OriginalVisualFxReg { get; set; }
    public bool? OriginalUiEffects { get; set; }
    public bool VisualFxChangedByUs { get; set; }
    public int? OriginalTransparency { get; set; }
    public string? OriginalMinAnimate { get; set; }
    public string? OriginalDragFullWindows { get; set; }
    public int? OriginalTaskbarAnimations { get; set; }
    public int? OriginalListviewAlphaSelect { get; set; }
    public int? OriginalListviewShadow { get; set; }
    public int? OriginalDwmAeroPeek { get; set; }
    // ── Extended snapshot for ultra low-RAM / no-bullshit Windows 11 ──
    public string? OriginalMenuShowDelay { get; set; }
    public string? OriginalAutoEndTasks { get; set; }
    public string? OriginalWaitToKillAppTimeout { get; set; }
    public string? OriginalHungAppTimeout { get; set; }
    public string? OriginalLowLevelHooksTimeout { get; set; }
    public string? OriginalForegroundLockTimeout { get; set; }
    public int? OriginalTaskbarMn { get; set; }
    public int? OriginalTaskbarDa { get; set; }
    public int? OriginalShowTaskViewButton { get; set; }
    public int? OriginalSearchBoxTaskbarMode { get; set; }
    public int? OriginalGlobalUserDisabled { get; set; }
    public int? OriginalGameDvrEnabled { get; set; }
    public int? OriginalStartupDelayInMSec { get; set; }
    public int? OriginalAlwaysHibernateThumbnails { get; set; }
    public bool PerformanceTweaksSnapshotDone { get; set; }
    public bool FirstTrayHintShown { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

public static class SettingsStore
{
    static string FilePath => Path.Combine(Paths.DataDir, "settings.json");
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                // try source-gen first, fallback to reflection-based for forward compat
                try
                {
                    var s = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);
                    if (s is not null) return s;
                }
                catch { }
                var fallback = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
                if (fallback is not null) return fallback;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("settings load failed", ex);
        }
        return new AppSettings();
    }

    static readonly object _saveLock = new();

    public static void Save(AppSettings settings)
    {
        lock (_saveLock)
        {
            try
            {
                Paths.EnsureDataDir();
                var tmp = FilePath + ".tmp";
                var json = JsonSerializer.Serialize(settings, JsonOpts);
                File.WriteAllText(tmp, json);
                // Atomic replace — handle case where target locked by AV
                for (int i = 0; i < 3; i++)
                {
                    try { File.Move(tmp, FilePath, true); break; }
                    catch (IOException) when (i < 2) { Thread.Sleep(80); }
                }
                Logger.Info($"settings saved ({json.Length} bytes, snapshotDone={settings.PerformanceTweaksSnapshotDone})");
            }
            catch (Exception ex)
            {
                Logger.Error("settings save failed", ex);
            }
            finally { try { Logger.Flush(); } catch { } }
        }
    }
}
