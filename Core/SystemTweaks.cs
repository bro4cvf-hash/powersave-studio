using Microsoft.Win32;

namespace PowerSave.Core;

using PowerSave.Infra;

public static class SystemTweaks
{
    public static void SetKeepAwake(bool enabled)
    {
        try
        {
            Native.SetThreadExecutionState(enabled
                ? Native.ES_CONTINUOUS | Native.ES_DISPLAY_REQUIRED | Native.ES_SYSTEM_REQUIRED
                : Native.ES_CONTINUOUS);
            Logger.Info($"keep awake {(enabled ? "enabled" : "disabled")}");
        }
        catch (Exception ex)
        {
            Logger.Warn("keep awake failed: " + ex.Message);
        }
    }

    public static void ReleaseKeepAwake() => SetKeepAwake(false);

    public static bool QueryUiEffects()
    {
        try
        {
            int result = Native.SystemParametersInfo(Native.SPI_GETUIEFFECTS, 0, IntPtr.Zero, 0);
            return result != 0;
        }
        catch { return true; }
    }

    public static void ApplyVisualEffects(bool reduce, AppSettings settings)
    {
        try
        {
            if (reduce && !settings.VisualFxChangedByUs)
            {
                settings.OriginalUiEffects = QueryUiEffects();
                settings.OriginalVisualFxReg = ReadVisualFxReg();
                settings.VisualFxChangedByUs = true;
            }

            if (!reduce && settings.VisualFxChangedByUs)
            {
                if (settings.OriginalVisualFxReg is int reg) WriteVisualFxReg(reg);
                if (settings.OriginalUiEffects is bool fx) SetUiEffects(fx);
                settings.VisualFxChangedByUs = false;
                Logger.Info("visual effects restored to original state");
                return;
            }

            if (reduce && settings.VisualFxChangedByUs)
            {
                SetUiEffects(false);
                WriteVisualFxReg(2);
                Logger.Info("visual effects reduced");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("visual effects tweak failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Polished global tweaks — only what README promises:
    /// - Disable taskbar & window transparency (EnableTransparency = 0)
    /// - Force best-performance visual effects (VisualFXSetting = 2, UiEffects off)
    /// - Disable window animations (MinAnimate, TaskbarAnimations, AeroPeek, drag etc.)
    /// Earlier build touched Widgets / Search / BackgroundApps / Game DVR — removed for
    /// GitHub polish: those are user choices, not power-saving essentials, and caused backlash.
    /// All tweaks are HKCU (no admin) + snapshot/restore friendly.
    /// </summary>
    public static bool IsGlobalTweaksApplied()
    {
        try
        {
            var trans = ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency");
            var fx = ReadVisualFxReg();
            var anim = ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations");
            return trans == 0 && fx == 2 && anim == 0;
        }
        catch { return false; }
    }

    public static void ApplyGlobalPerformanceTweaks(AppSettings settings)
    {
        try
        {
            bool alreadyOptimized = IsGlobalTweaksApplied() && settings.PerformanceTweaksSnapshotDone;

            if (!settings.PerformanceTweaksSnapshotDone)
            {
                settings.OriginalVisualFxReg ??= ReadVisualFxReg();
                settings.OriginalUiEffects ??= QueryUiEffects();
                settings.OriginalTransparency = ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency");
                settings.OriginalMinAnimate = ReadString(@"Control Panel\Desktop\WindowMetrics", "MinAnimate");
                settings.OriginalDragFullWindows = ReadString(@"Control Panel\Desktop", "DragFullWindows");
                settings.OriginalTaskbarAnimations = ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations");
                settings.OriginalListviewAlphaSelect = ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect");
                settings.OriginalListviewShadow = ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow");
                settings.OriginalDwmAeroPeek = ReadDword(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek");
                settings.OriginalAlwaysHibernateThumbnails ??= ReadDword(@"Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails");
                if (!settings.VisualFxChangedByUs)
                {
                    settings.OriginalVisualFxReg ??= ReadVisualFxReg();
                    settings.OriginalUiEffects ??= QueryUiEffects();
                    settings.VisualFxChangedByUs = true;
                }
                settings.PerformanceTweaksSnapshotDone = true;
                Logger.Info("performance tweaks snapshot taken");
            }

            if (alreadyOptimized)
            {
                Logger.Info("global performance tweaks already applied — skipping");
                return;
            }

            // 1) Transparency OFF — biggest GPU saver
            WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);
            try { WriteDword(@"Software\Microsoft\Windows\DWM", "EnableTransparency", 0); } catch { }

            // 2) Best-performance visuals — animations / fades OFF
            SetUiEffects(false);
            WriteVisualFxReg(2);
            try { WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2); } catch { }

            // 3) Window animations OFF (no bloat removals)
            WriteString(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0");
            WriteString(@"Control Panel\Desktop", "DragFullWindows", "0");
            WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0);
            WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", 0);
            WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", 0);
            WriteDword(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0);
            WriteDword(@"Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails", 0);

            try { Task.Run(() => BroadcastSettingChange()); } catch { }

            Logger.Info("global performance tweaks applied: transparency OFF, animations OFF, best performance (minimal)");
        }
        catch (Exception ex)
        {
            Logger.Warn("global performance tweaks failed: " + ex.Message);
        }
    }

    static void BroadcastSettingChange()
    {
        try
        {
            // Use SendMessageTimeout to avoid hanging on hung windows (HWND_BROADCAST SendMessage can deadlock)
            Native.SendMessageTimeout((IntPtr)0xFFFF, 0x001A, IntPtr.Zero, IntPtr.Zero, 2, 1500, out _);
        }
        catch { }
    }

    public static void RestoreGlobalPerformanceTweaks(AppSettings settings)
    {
        try
        {
            if (!settings.PerformanceTweaksSnapshotDone && !settings.VisualFxChangedByUs) return;

            if (settings.OriginalTransparency is int trans)
                WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", trans);
            if (settings.OriginalMinAnimate is string ma)
                WriteString(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", ma);
            if (settings.OriginalDragFullWindows is string dfw)
                WriteString(@"Control Panel\Desktop", "DragFullWindows", dfw);
            if (settings.OriginalTaskbarAnimations is int ta)
                WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", ta);
            if (settings.OriginalListviewAlphaSelect is int lva)
                WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", lva);
            if (settings.OriginalListviewShadow is int lvs)
                WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", lvs);
            if (settings.OriginalDwmAeroPeek is int ap)
                WriteDword(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek", ap);
            if (settings.OriginalAlwaysHibernateThumbnails is int aht)
                WriteDword(@"Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails", aht);
            // Legacy extended keys — restore if present (older snapshots)
            if (settings.OriginalMenuShowDelay is string msd)
                WriteString(@"Control Panel\Desktop", "MenuShowDelay", msd);
            if (settings.OriginalAutoEndTasks is string aet)
                WriteString(@"Control Panel\Desktop", "AutoEndTasks", aet);
            if (settings.OriginalWaitToKillAppTimeout is string wtk)
                WriteString(@"Control Panel\Desktop", "WaitToKillAppTimeout", wtk);
            if (settings.OriginalHungAppTimeout is string hat)
                WriteString(@"Control Panel\Desktop", "HungAppTimeout", hat);
            if (settings.OriginalLowLevelHooksTimeout is string llht)
                WriteString(@"Control Panel\Desktop", "LowLevelHooksTimeout", llht);
            if (settings.OriginalForegroundLockTimeout is string flt)
                WriteString(@"Control Panel\Desktop", "ForegroundLockTimeout", flt);
            if (settings.OriginalTaskbarMn is int tm)
                WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn", tm);
            if (settings.OriginalTaskbarDa is int td)
                WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", td);
            if (settings.OriginalShowTaskViewButton is int stv)
                WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton", stv);
            if (settings.OriginalSearchBoxTaskbarMode is int sb)
                WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", sb);
            if (settings.OriginalGlobalUserDisabled is int gud)
                WriteDword(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", gud);
            if (settings.OriginalGameDvrEnabled is int gde)
                WriteDword(@"System\GameConfigStore", "GameDVR_Enabled", gde);
            if (settings.OriginalStartupDelayInMSec is int sd)
                WriteDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", sd);
            if (settings.OriginalVisualFxReg is int reg) WriteVisualFxReg(reg);
            if (settings.OriginalUiEffects is bool fx) SetUiEffects(fx);

            // Clear the snapshot so a future "optimize" re-reads the *current* system
            // state instead of restoring values captured before an earlier restore
            // (the user may have changed their theme in between).
            settings.PerformanceTweaksSnapshotDone = false;
            settings.VisualFxChangedByUs = false;
            settings.OriginalTransparency = null;
            settings.OriginalMinAnimate = null;
            settings.OriginalDragFullWindows = null;
            settings.OriginalTaskbarAnimations = null;
            settings.OriginalListviewAlphaSelect = null;
            settings.OriginalListviewShadow = null;
            settings.OriginalDwmAeroPeek = null;
            settings.OriginalAlwaysHibernateThumbnails = null;
            settings.OriginalVisualFxReg = null;
            settings.OriginalUiEffects = null;

            BroadcastSettingChange();
            Logger.Info("global performance tweaks restored");
        }
        catch (Exception ex)
        {
            Logger.Warn("restore performance tweaks failed: " + ex.Message);
        }
    }

    static int? ReadVisualFxReg()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
            return key?.GetValue("VisualFXSetting") as int?;
        }
        catch { return null; }
    }

    static void WriteVisualFxReg(int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
            key.SetValue("VisualFXSetting", value, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            Logger.Warn("visualfx registry write failed: " + ex.Message);
        }
    }

    static void SetUiEffects(bool enabled)
    {
        Native.SystemParametersInfo(
            Native.SPI_SETUIEFFECTS, enabled ? 1 : 0, IntPtr.Zero,
            Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE);
    }

    // ── Registry helpers for global performance tweaks ──
    static int? ReadDword(string subKey, string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey);
            var v = key?.GetValue(name);
            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (v is byte[] bytes && bytes.Length >= 4) return BitConverter.ToInt32(bytes, 0);
            if (v is string s && int.TryParse(s, out var si)) return si;
        }
        catch { }
        return null;
    }

    static void WriteDword(string subKey, string name, int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(subKey);
            key.SetValue(name, value, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            Logger.Warn($"registry WriteDword {subKey}\\{name} failed: {ex.Message}");
        }
    }

    static string? ReadString(string subKey, string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey);
            return key?.GetValue(name)?.ToString();
        }
        catch { return null; }
    }

    static void WriteString(string subKey, string name, string value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(subKey);
            key.SetValue(name, value, RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            Logger.Warn($"registry WriteString {subKey}\\{name} failed: {ex.Message}");
        }
    }

    static void DeleteValue(string subKey, string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: true);
            if (key?.GetValue(name) is not null) key.DeleteValue(name, false);
        }
        catch { }
    }

    static readonly string[] ManagedServices = { "WSearch", "DiagTrack", "SysMain", "Spooler" };

    static void MigrateLegacyIndexingState(AppSettings settings)
    {
        if (!settings.IndexingChangedByUs) return;
        if (!settings.ServicesChangedByUs &&
            settings.ServiceOriginalStart.Count == 0 &&
            !string.IsNullOrEmpty(settings.IndexingOriginalStart))
        {
            settings.ServiceOriginalStart["WSearch"] = settings.IndexingOriginalStart;
            settings.ServicesChangedByUs = true;
        }
        settings.IndexingChangedByUs = false;
        settings.IndexingOriginalStart = null;
    }

    public static async Task ApplyBackgroundServicesAsync(bool pause, AppSettings settings)
    {
        MigrateLegacyIndexingState(settings);

        foreach (var svc in ManagedServices)
        {
            try
            {
                if (pause)
                {
                    if (settings.ServiceOriginalStart.ContainsKey(svc)) continue;
                    var original = await QueryServiceStartTypeAsync(svc).ConfigureAwait(false);
                    if (original is null || original == "disabled")
                    {
                        Logger.Info($"{svc}: absent or already disabled, skipped");
                        continue;
                    }
                    await Shell.RunAsync($"sc stop \"{svc}\"", 10000).ConfigureAwait(false);
                    await Shell.RunAsync($"sc config \"{svc}\" start= disabled", 10000).ConfigureAwait(false);
                    settings.ServiceOriginalStart[svc] = original;
                    settings.ServicesChangedByUs = true;
                    Logger.Info($"{svc} paused (was {original})");
                }
                else if (settings.ServicesChangedByUs && settings.ServiceOriginalStart.Remove(svc, out var original))
                {
                    await Shell.RunAsync($"sc config \"{svc}\" start= {original}", 10000).ConfigureAwait(false);
                    await Shell.RunAsync($"sc start \"{svc}\"", 10000).ConfigureAwait(false);
                    Logger.Info($"{svc} restored (was {original})");
                    if (settings.ServiceOriginalStart.Count == 0)
                        settings.ServicesChangedByUs = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"{svc} tweak failed: {ex.Message}");
            }
        }
    }

    static async Task<string?> QueryServiceStartTypeAsync(string service)
    {
        var (_, output) = await Shell.RunAsync($"sc qc \"{service}\"", 8000).ConfigureAwait(false);
        foreach (var line in output.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("START_TYPE", StringComparison.OrdinalIgnoreCase))
            {
                if (t.Contains("AUTO_START", StringComparison.OrdinalIgnoreCase))
                    return t.Contains("DELAYED", StringComparison.OrdinalIgnoreCase) ? "delayed-auto" : "auto";
                if (t.Contains("DEMAND_START", StringComparison.OrdinalIgnoreCase)) return "demand";
                if (t.Contains("DISABLED", StringComparison.OrdinalIgnoreCase)) return "disabled";
            }
        }
        return null;
    }

    public static int TrimAllProcessesWorkingSets()
    {
        int trimmed = 0;
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                if (process.Id == 0) continue;
                var handle = process.Handle;
                if (Native.SetProcessWorkingSetSize(handle, (IntPtr)(-1), (IntPtr)(-1)))
                    trimmed++;
            }
            catch { }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }
        try
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
        }
        catch { }
        Native.TrimWorkingSet();
        Logger.Info($"working sets trimmed across ~{trimmed} processes");
        return trimmed;
    }

    public static bool SleepNow()
    {
        try
        {
            Logger.Info("sleep requested by user");
            return Application.SetSuspendState(PowerState.Suspend, false, false);
        }
        catch (Exception ex)
        {
            Logger.Error("sleep failed", ex);
            return false;
        }
    }

    public static void LockNow()
    {
        try { Native.LockWorkStation(); }
        catch (Exception ex) { Logger.Error("lock failed", ex); }
    }

    public static void OpenWindowsPowerSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("ms-settings:powersleep") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error("open power settings failed", ex);
        }
    }

    public static bool RelaunchAsAdmin(string arguments = "--elevated")
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (exe is null) return false;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
            {
                Verb = "runas",
                UseShellExecute = true,
                Arguments = arguments
            });
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("admin relaunch failed/canceled: " + ex.Message);
            return false;
        }
    }

    public static void SetStartWithWindows(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null) return;
            const string name = "PowerSave";
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (exe is not null) key.SetValue(name, $"\"{exe}\" --tray");
            }
            else
            {
                if (key.GetValue(name) is not null) key.DeleteValue(name, throwOnMissingValue: false);
            }
            Logger.Info($"start with windows {(enabled ? "on" : "off")}");
        }
        catch (Exception ex)
        {
            Logger.Error("start-with-windows toggle failed", ex);
        }
    }
}
