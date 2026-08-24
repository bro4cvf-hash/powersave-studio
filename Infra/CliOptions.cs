namespace PowerSave.Infra;

public static class ModeKeys
{
    public const string UltraPowerSave = "ultrapowersave";
    public const string PowerSave = "powersave";
    public const string UltraPerformance = "ultraperformance";

    public static readonly string[] All = { UltraPowerSave, PowerSave, UltraPerformance };

    public static string? Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var n = name.Trim().Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
        return n switch
        {
            "ultrapowersave" or "ultrasave" or "ultraeco" or "us" or "ups" => UltraPowerSave,
            "powersave" or "balanced" or "save" or "ps" or "eco" => PowerSave,
            "ultraperformance" or "ultraperf" or "performance" or "perf" or "up" => UltraPerformance,
            _ => null
        };
    }
}

public sealed class CliOptions
{
    public string? ApplyMode { get; private set; }
    public bool Silent { get; private set; }
    public bool StartMinimized { get; private set; }
    public bool ElevatedRelaunch { get; private set; }

    public static CliOptions Parse(string[] args)
    {
        var o = new CliOptions();
        foreach (var raw in args)
        {
            var s = raw.TrimStart('-', '/').ToLowerInvariant();
            if (s.StartsWith("apply=", StringComparison.Ordinal))
            {
                o.ApplyMode = ModeKeys.Normalize(s[6..]);
            }
            else if (s is "silent" or "q" or "quiet")
            {
                o.Silent = true;
            }
            else if (s is "tray" or "minimized")
            {
                o.StartMinimized = true;
            }
            else if (s == "elevated")
            {
                o.ElevatedRelaunch = true;
            }
            else if (ModeKeys.Normalize(s) is string mode)
            {
                o.ApplyMode = mode;
            }
        }
        return o;
    }
}
