namespace PowerSave.Core;

using PowerSave.Infra;

internal static class PwrGuid
{
    public static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid Eco = new("a1841308-3541-4fac-ba81-eddd2f7ecd08");
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

    public static readonly Guid SubProcessor = new("54533251-82be-4824-96c1-47b60b740d00");
    public static readonly Guid SubVideo = new("7516b95f-f776-4464-8c53-06167f40cc99");
    public static readonly Guid SubSleep = new("238c9fa8-0aad-41ed-83f4-97be242c8f20");
    public static readonly Guid SubWireless = new("19cbb8fa-5279-450e-9fac-8a3d5fedd0c1");
    public static readonly Guid SubUsb = new("2a737441-1930-4402-8d77-b2bebba308a3");
    public static readonly Guid SubPcie = new("501a4d13-42af-4429-9fd1-a8218c268e20");

    public static readonly Guid ProcThrottleMin = new("893dee8e-2bef-41e0-89c6-b55d0929964c");
    public static readonly Guid ProcThrottleMax = new("bc5038f7-23e0-4960-96da-33abaf5935ec");
    public static readonly Guid ProcBoostMode = new("be337238-0d82-4146-a960-4f3749d470c7");
    public static readonly Guid VideoBrightness = new("aded5e82-b909-4619-9949-f5d71dac0bcb");
    public static readonly Guid VideoIdleTimeout = new("3c0bc021-c8a8-4e07-a973-adb19a76a883");
    public static readonly Guid SleepIdleTimeout = new("29f6c1db-86da-48c5-9ebf-e6b6bd1cd8fe");
    public static readonly Guid WifiPowerLevel = new("12bbebe6-58d6-4636-95bb-3217ef867c1a");
    public static readonly Guid UsbSelectiveSuspend = new("48e6b7a6-50f5-4782-a5d4-53bb8f07e226");
    public static readonly Guid PcieAspm = new("ee12f906-d277-404b-b6da-e5fa1a576df5");
    public static readonly Guid ProcEnergyPerfPreference = new("36687f9e-e3a5-4dbf-b1dc-15eb381c6863");
}

public sealed class PowerManager
{
    readonly AppSettings _settings;

    public event Action<string, bool>? OperationReported;
    public event Action<string>? ModeApplied;

    public bool IsElevated { get; }

    public PowerManager(AppSettings settings)
    {
        _settings = settings;
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            IsElevated = new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { }
    }

    public async Task<string?> GetActiveSchemeAsync()
    {
        var (ok, output) = await Shell.RunAsync("powercfg /getactivescheme", 8000).ConfigureAwait(false);
        if (!ok) return null;
        var m = System.Text.RegularExpressions.Regex.Match(
            output, "[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}");
        return m.Success ? m.Value : null;
    }

    public async Task<string> EnsurePlanAsync(ModeSpec spec)
    {
        var baseGuid = spec.Key switch
        {
            ModeKeys.UltraPowerSave => PwrGuid.Eco,
            ModeKeys.PowerSave => PwrGuid.Balanced,
            ModeKeys.UltraPerformance => PwrGuid.HighPerformance,
            _ => PwrGuid.Balanced
        };

        if (_settings.PlanIds.TryGetValue(spec.Key, out var existing) &&
            Guid.TryParse(existing, out var planGuid))
        {
            var (alive, _) = await Shell.RunAsync($"powercfg /query {planGuid:D}", 8000).ConfigureAwait(false);
            if (alive) return planGuid.ToString();
            Logger.Warn($"plan {existing} for {spec.Key} not found, recreating");
            _settings.PlanIds.Remove(spec.Key);
        }

        // Some systems (Win 10, older builds) lack Eco/Ultimate — fallback chain
        Guid[] tryOrder = baseGuid == PwrGuid.Balanced
            ? new[] { PwrGuid.Balanced }
            : new[] { baseGuid, PwrGuid.Balanced, PwrGuid.HighPerformance };
        string? lastOutput = null;
        foreach (var guid in tryOrder)
        {
            var (ok, output) = await Shell.RunAsync($"powercfg -duplicatescheme {guid:D}", 12000).ConfigureAwait(false);
            lastOutput = output;
            if (ok)
            {
                var m = System.Text.RegularExpressions.Regex.Match(
                    output, "[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}");
                if (m.Success)
                {
                    var id = m.Value;
                    _settings.PlanIds[spec.Key] = id;
                    SettingsStore.Save(_settings);
                    var (renOk, _) = await Shell.RunAsync(
                        $"powercfg -changename {id} \"{spec.PlanName}\" \"{spec.PlanDescription}\"",
                        8000).ConfigureAwait(false);
                    if (!renOk) Logger.Warn($"rename plan {id} failed, continuing");
                    Logger.Info($"created plan {spec.PlanName} => {id} (cloned from {guid})");
                    return id;
                }
            }
            Logger.Warn($"duplicatescheme {guid} failed for {spec.Key}, trying next");
        }

        Logger.Error($"could not create a dedicated plan for {spec.Key}. Last output: {lastOutput}");
        throw new InvalidOperationException($"Failed to create power plan for {spec.Title}. Try running as administrator.");
    }

    public async Task<bool> ApplyModeAsync(string key)
    {
        var spec = ModeCatalog.Get(key);
        OperationReported?.Invoke($"Applying {spec.Title}…", true);

        try
        {
            var plan = await EnsurePlanAsync(spec).ConfigureAwait(false);
            var commands = BuildPowerCommands(key, plan);
            var ok = await Shell.RunBatchAsync(commands).ConfigureAwait(false);

            await ApplyModeExtrasAsync(key).ConfigureAwait(false);

            if (ok)
            {
                _settings.LastMode = key;
                SettingsStore.Save(_settings);
                ModeApplied?.Invoke(key);
                OperationReported?.Invoke($"{spec.Title} active", true);
                Logger.Info($"mode applied: {key}");
            }
            else
            {
                OperationReported?.Invoke($"{spec.Title}: some values failed — see log", false);
                Logger.Warn($"mode {key} applied with errors");
            }
            return ok;
        }
        catch (Exception ex)
        {
            Logger.Error($"apply mode {key} failed", ex);
            OperationReported?.Invoke($"Failed to apply {spec.Title}", false);
            return false;
        }
    }

    static List<string> BuildPowerCommands(string key, string plan)
    {
        var cmds = new List<string>();
        void Set(Guid sub, Guid setting, int ac, int dc)
        {
            cmds.Add($"powercfg /setacvalueindex {plan} {sub:D} {setting:D} {ac}");
            cmds.Add($"powercfg /setdcvalueindex {plan} {sub:D} {setting:D} {dc}");
        }

        switch (key)
        {
            case ModeKeys.UltraPowerSave:
                Set(PwrGuid.SubProcessor, PwrGuid.ProcThrottleMin, 0, 0);
                Set(PwrGuid.SubProcessor, PwrGuid.ProcThrottleMax, 15, 15);
                Set(PwrGuid.SubProcessor, PwrGuid.ProcBoostMode, 0, 0);
                Set(PwrGuid.SubProcessor, PwrGuid.ProcEnergyPerfPreference, 100, 100);
                // Brightness intentionally not touched per user request - leave slider alone
                Set(PwrGuid.SubVideo, PwrGuid.VideoIdleTimeout, 180, 60);
                Set(PwrGuid.SubSleep, PwrGuid.SleepIdleTimeout, 900, 300);
                Set(PwrGuid.SubWireless, PwrGuid.WifiPowerLevel, 3, 3);
                Set(PwrGuid.SubUsb, PwrGuid.UsbSelectiveSuspend, 1, 1);
                Set(PwrGuid.SubPcie, PwrGuid.PcieAspm, 1, 1);
                break;

            case ModeKeys.PowerSave:
                Set(PwrGuid.SubProcessor, PwrGuid.ProcThrottleMin, 0, 0);
                Set(PwrGuid.SubProcessor, PwrGuid.ProcThrottleMax, 100, 80);
                Set(PwrGuid.SubProcessor, PwrGuid.ProcBoostMode, 1, 1);
                Set(PwrGuid.SubProcessor, PwrGuid.ProcEnergyPerfPreference, 50, 75);
                // Brightness not touched - leave slider alone
                Set(PwrGuid.SubVideo, PwrGuid.VideoIdleTimeout, 300, 180);
                Set(PwrGuid.SubSleep, PwrGuid.SleepIdleTimeout, 1800, 900);
                Set(PwrGuid.SubWireless, PwrGuid.WifiPowerLevel, 2, 2);
                Set(PwrGuid.SubUsb, PwrGuid.UsbSelectiveSuspend, 1, 1);
                Set(PwrGuid.SubPcie, PwrGuid.PcieAspm, 0, 0);
                break;

            case ModeKeys.UltraPerformance:
                Set(PwrGuid.SubProcessor, PwrGuid.ProcThrottleMin, 100, 100);
                Set(PwrGuid.SubProcessor, PwrGuid.ProcThrottleMax, 100, 100);
                Set(PwrGuid.SubProcessor, PwrGuid.ProcBoostMode, 2, 2);
                Set(PwrGuid.SubProcessor, PwrGuid.ProcEnergyPerfPreference, 0, 0);
                // Brightness not touched - leave slider alone
                Set(PwrGuid.SubVideo, PwrGuid.VideoIdleTimeout, 0, 600);
                Set(PwrGuid.SubSleep, PwrGuid.SleepIdleTimeout, 0, 1800);
                Set(PwrGuid.SubWireless, PwrGuid.WifiPowerLevel, 0, 0);
                Set(PwrGuid.SubUsb, PwrGuid.UsbSelectiveSuspend, 0, 0);
                Set(PwrGuid.SubPcie, PwrGuid.PcieAspm, 0, 0);
                break;
        }

        cmds.Add($"powercfg /setactive {plan}");
        return cmds;
    }

    async Task ApplyModeExtrasAsync(string key)
    {
        // Brightness intentionally never touched per user request - leave Windows brightness slider alone

        bool keepAwake = key == ModeKeys.UltraPerformance && _settings.Toggles.KeepDisplayAwakeOnPerf;
        SystemTweaks.SetKeepAwake(keepAwake);

        bool pauseIndexing = key == ModeKeys.UltraPowerSave && _settings.Toggles.PauseIndexingOnUltraSave;
        await SystemTweaks.ApplyBackgroundServicesAsync(pauseIndexing, _settings).ConfigureAwait(false);

        // OPTIMIZED: window optimizations (transparency, animations, visual effects, taskbar bloat, background apps etc.)
        // are now GLOBAL and applied once at app startup — NOT on every mode switch.
        // This makes mode switching ~5x faster (only powercfg + keepAwake + services) and avoids explorer flicker.
        // See MainForm Load + SystemTweaks.ApplyGlobalPerformanceTweaks() for the one-time global path.
        // No per-mode registry writes needed here.

        if (!IsElevated && pauseIndexing)
            OperationReported?.Invoke("Tip: run as administrator to also pause telemetry & background services", false);
    }
}
