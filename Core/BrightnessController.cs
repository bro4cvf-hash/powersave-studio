using System.Management;

namespace PowerSave.Core;

using PowerSave.Infra;

public static class BrightnessController
{
    public static bool TrySet(int percent)
    {
        try
        {
            percent = Math.Clamp(percent, 0, 100);
            var scope = new ManagementScope(@"root\wmi");
            var query = new SelectQuery("SELECT * FROM WmiMonitorBrightnessMethods");
            using var searcher = new ManagementObjectSearcher(scope, query);
            bool touched = false;
            foreach (var o in searcher.Get())
            {
                if (o is not ManagementObject mo) continue;
                using (mo)
                {
                    using var args = mo.GetMethodParameters("WmiSetBrightness");
                    args["Timeout"] = 0;
                    args["Brightness"] = (byte)percent;
                    mo.InvokeMethod("WmiSetBrightness", args, null);
                }
                touched = true;
            }
            Logger.Info($"brightness set to {percent}% {(touched ? "" : "(no supported display found)")}");
            return touched;
        }
        catch (Exception ex)
        {
            Logger.Warn($"brightness set failed: {ex.Message}");
            return false;
        }
    }

    public static int? GetCurrent()
    {
        try
        {
            var scope = new ManagementScope(@"root\wmi");
            var query = new SelectQuery("SELECT * FROM WmiMonitorBrightness");
            using var searcher = new ManagementObjectSearcher(scope, query);
            foreach (var o in searcher.Get())
            {
                if (o is not ManagementObject mo) continue;
                using (mo)
                {
                    if (mo["CurrentBrightness"] is byte b) return b;
                }
            }
        }
        catch { }
        return null;
    }
}
