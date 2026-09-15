namespace PowerSave.UI;

internal sealed class Animation
{
    public required Control Target { get; init; }
    public required string Key { get; init; }
    public required Action<double> Step { get; init; }
    public required int DurationMs { get; init; }
    public int DelayMs { get; init; }
    public DateTime Started { get; init; } = DateTime.UtcNow;
}

internal static class AnimEngine
{
    static readonly object _lock = new();
    static readonly List<Animation> _active = new();
    static System.Windows.Forms.Timer? _timer;

    public static double EaseOut(double t) => 1 - Math.Pow(1 - t, 3);

    public static double EaseOutBack(double t)
    {
        const double c1 = 1.2, c3 = c1 + 1;
        return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
    }

    public static void Animate(Control target, string key, Action<double> step,
        int durationMs, int delayMs = 0, bool spring = false)
    {
        lock (_lock)
        {
            _active.RemoveAll(a => a.Target == target && a.Key == key);
            Func<double, double> ease = spring ? EaseOutBack : EaseOut;
            _active.Add(new Animation
            {
                Target = target,
                Key = key,
                Step = t =>
                {
                    step(ease(Math.Clamp(t, 0, 1)));
                    target.Invalidate();
                },
                DurationMs = Math.Max(16, durationMs),
                DelayMs = Math.Max(0, delayMs)
            });
            EnsureTimer();
        }
    }

    public static void Cancel(Control target, string key)
    {
        lock (_lock) _active.RemoveAll(a => a.Target == target && a.Key == key);
    }

    static void EnsureTimer()
    {
        if (_timer is not null) return;
        _timer = new System.Windows.Forms.Timer { Interval = 15 };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    static void Tick()
    {
        Animation[] snapshot;
        lock (_lock) snapshot = _active.ToArray();

        bool anyLive = false;
        var now = DateTime.UtcNow;
        foreach (var anim in snapshot)
        {
            double elapsed = (now - anim.Started).TotalMilliseconds - anim.DelayMs;
            if (elapsed < 0) { anyLive = true; continue; }

            double raw = elapsed / anim.DurationMs;
            if (raw >= 1)
            {
                lock (_lock) _active.Remove(anim);
                try { anim.Step(1); anim.Target.Invalidate(); }
                catch { /* target disposed mid-animation */ }
            }
            else
            {
                try { anim.Step(raw); }
                catch { lock (_lock) _active.Remove(anim); continue; }
                anyLive = true;
            }
        }

        if (!anyLive)
        {
            lock (_lock)
            {
                if (_active.Count == 0)
                {
                    _timer?.Stop();
                    _timer?.Dispose();
                    _timer = null;
                }
            }
        }
    }

    public static void Shutdown()
    {
        lock (_lock)
        {
            _active.Clear();
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }
    }
}
