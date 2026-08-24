namespace PowerSave.Core;

using PowerSave.Infra;

public sealed class BatteryReading
{
    public bool HasBattery { get; init; }
    public bool AcOnline { get; init; }
    public bool Charging { get; init; }
    public int Percent { get; init; }
    public int? LifeSeconds { get; init; }
    public DateTime At { get; init; } = DateTime.Now;
}

public sealed class BatteryMonitor
{
    readonly Queue<(DateTime Time, int Percent)> _samples = new();

    public BatteryReading Read()
    {
        var r = ReadOnce();
        if (r.HasBattery && !r.AcOnline)
        {
            _samples.Enqueue((r.At, r.Percent));
            while (_samples.Count > 0 && (r.At - _samples.Peek().Time).TotalMinutes > 20)
                _samples.Dequeue();
        }
        else
        {
            _samples.Clear();
        }
        return r;
    }

    public TimeSpan? EstimateRemaining(BatteryReading current)
    {
        if (!current.HasBattery || current.AcOnline) return null;

        if (current.LifeSeconds is int secs && secs > 90)
            return TimeSpan.FromSeconds(secs);

        if (_samples.Count >= 3)
        {
            var first = _samples.Peek();
            var minutes = (current.At - first.Time).TotalMinutes;
            var dropped = first.Percent - current.Percent;
            if (minutes >= 2 && dropped > 0.5)
            {
                var perHour = dropped / minutes * 60.0;
                if (perHour > 0.1)
                {
                    var hours = current.Percent / perHour;
                    if (hours is > 0 and < 48) return TimeSpan.FromHours(hours);
                }
            }
        }
        return null;
    }

    public double? CurrentDrainPercentPerHour()
    {
        if (_samples.Count < 3) return null;
        var arr = _samples.ToArray();
        var first = arr[0];
        var last = arr[^1];
        var minutes = (last.Time - first.Time).TotalMinutes;
        if (minutes < 2) return null;
        var dropped = first.Percent - last.Percent;
        if (dropped <= 0) return null;
        return dropped / minutes * 60.0;
    }

    public static BatteryReading ReadOnce()
    {
        try
        {
            if (Native.GetSystemPowerStatus(out var s))
            {
                bool hasBattery = s.BatteryFlag != 128 && s.BatteryLifePercent <= 100;
                return new BatteryReading
                {
                    HasBattery = hasBattery,
                    AcOnline = s.ACLineStatus == 1,
                    Charging = (s.BatteryFlag & 8) != 0,
                    Percent = hasBattery ? Math.Clamp((int)s.BatteryLifePercent, 0, 100) : 0,
                    LifeSeconds = s.BatteryLifeTime > 0 ? s.BatteryLifeTime : null
                };
            }
        }
        catch { }
        return new BatteryReading();
    }
}
