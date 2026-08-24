using System.Diagnostics;

namespace PowerSave.Core;

using PowerSave.Infra;

public sealed record ModeSpec(
    string Key,
    string Title,
    string Subtitle,
    Color Accent,
    string PlanName,
    string PlanDescription);

public static class ModeCatalog
{
    public static readonly ModeSpec UltraPowerSave = new(
        ModeKeys.UltraPowerSave,
        "ULTRA POWER SAVE",
        "CPU capped 15% · transparency off · best performance · background silenced",
        Color.FromArgb(57, 217, 138),
        "PowerSave — Ultra Power Save",
        "Maximum battery runtime. CPU capped at 15%, transparency & animations off, everything trimmed for power saving.");

    public static readonly ModeSpec PowerSave = new(
        ModeKeys.PowerSave,
        "POWER SAVE",
        "Balanced efficiency · transparency off · visuals optimized · sensible timeouts",
        Color.FromArgb(96, 165, 250),
        "PowerSave — Balanced",
        "Everyday mode. Good performance, all transparency & animations disabled for power saving.");

    public static readonly ModeSpec UltraPerformance = new(
        ModeKeys.UltraPerformance,
        "ULTRA PERFORMANCE",
        "CPU pinned at 100% · boost unlocked · transparency off · visuals optimized · zero latency",
        Color.FromArgb(255, 99, 99),
        "PowerSave — Ultra Performance",
        "Absolute maximum speed. Every throttle removed, transparency & animations off for raw performance.");

    public static ModeSpec Get(string key) => key switch
    {
        ModeKeys.UltraPowerSave => UltraPowerSave,
        ModeKeys.PowerSave => PowerSave,
        ModeKeys.UltraPerformance => UltraPerformance,
        _ => throw new ArgumentException($"Unknown mode '{key}'", nameof(key))
    };

    public static IReadOnlyList<ModeSpec> All => new[] { UltraPowerSave, PowerSave, UltraPerformance };
}

internal static class Shell
{
    public static async Task<(bool Ok, string Output)> RunAsync(string arguments, int timeoutMs = 20000)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/c {arguments}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process is null) return (false, "process failed to start");

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            var exitTask = process.WaitForExitAsync();
            var winner = await Task.WhenAny(exitTask, Task.Delay(timeoutMs)).ConfigureAwait(false);

            string output;
            try { output = (await stdout.ConfigureAwait(false)) + "\n" + (await stderr.ConfigureAwait(false)); }
            catch { output = ""; }

            if (winner != exitTask)
            {
                try { process.Kill(true); } catch { }
                Logger.Warn($"cmd> {arguments} => timeout");
                return (false, "timeout");
            }

            output = output.Trim();
            var ok = process.ExitCode == 0;
            Logger.Info($"cmd> {arguments} => exit {process.ExitCode} :: {Truncate(output)}");
            return (ok, output);
        }
        catch (Exception ex)
        {
            Logger.Error("shell run failed", ex);
            return (false, ex.Message);
        }
    }

    static string Truncate(string s) => s.Length <= 400 ? s : s[..400] + "…";

    public static async Task<bool> RunBatchAsync(IEnumerable<string> commands, int timeoutMs = 30000)
    {
        var list = commands.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        if (list.Count == 0) return true;

        var batches = new List<string>();
        var current = new List<string>();
        int length = 0;
        foreach (var c in list)
        {
            current.Add(c);
            length += c.Length + 3;
            if (length > 6500)
            {
                batches.Add(string.Join(" & ", current));
                current.Clear();
                length = 0;
            }
        }
        if (current.Count > 0) batches.Add(string.Join(" & ", current));

        bool allOk = true;
        foreach (var batch in batches)
        {
            var (ok, _) = await RunAsync(batch, timeoutMs).ConfigureAwait(false);
            allOk &= ok;
        }
        return allOk;
    }
}
