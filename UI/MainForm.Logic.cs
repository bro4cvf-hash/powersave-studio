using PowerSave.Core;
using PowerSave.Infra;

namespace PowerSave.UI;

public sealed partial class MainForm
{
    async Task DetectCurrentModeAsync()
    {
        var active = await _pm.GetActiveSchemeAsync().ConfigureAwait(true);
        string? matched = null;
        if (active is not null)
        {
            foreach (var (key, id) in _settings.PlanIds)
            {
                if (string.Equals(id, active, StringComparison.OrdinalIgnoreCase))
                {
                    matched = key;
                    break;
                }
            }
        }

        if (matched is null)
        {
            var last = ModeKeys.Normalize(_settings.LastMode);
            if (last is not null && _settings.PlanIds.ContainsKey(last)) matched = last;
        }

        if (matched is not null)
        {
            SelectVisual(matched);
            AdoptModeVisual(matched);
        }
        else
        {
            SelectVisual("");
            _pill.Set("READY — CHOOSE A MODE", Theme.TextDim);
        }
    }

    void AdoptModeVisual(string key)
    {
        var spec = ModeCatalog.Get(key);
        _pill.Set($"{spec.Title} ACTIVE", spec.Accent, animate: true);
        SetBorderAccentAnimated(spec.Accent);
        UpdateTrayAccent(spec.Accent);
        _progressBar.SetAccent(spec.Accent);
        if (_progressBar.IsApplying) _progressBar.Complete(true);
        ModeChangedForTray?.Invoke(key);
    }

    void SelectVisual(string key)
    {
        _visualKey = key;
        foreach (var card in _cards)
            card.SetSelected(string.Equals(card.Spec.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public void ApplyFromExternal(string key)
    {
        ShowFromTray();
        _ = ApplyModeAsync(key);
    }

    async Task ApplyModeAsync(string key)
    {
        if (_busy || !ModeKeys.All.Contains(key)) return;
        _busy = true;
        UseWaitCursor = true;
        SelectVisual(key);
        var spec = ModeCatalog.Get(key);
        _pill.Set($"APPLYING {spec.Title}…", spec.Accent, animate: true);
        _progressBar.SetAccent(spec.Accent);
        _progressBar.Start(spec.Title, spec.Accent);
        SafeLayout();

        bool ok = true;
        try
        {
            await _pm.ApplyModeAsync(key).ConfigureAwait(true);
        }
        catch { ok = false; }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
            if (!ok) _progressBar.Fail();
            else if (!_progressBar.IsApplying) { /* already completed via event */ }
            RefreshBattery();
            Native.TrimWorkingSet();
            // if mode apply didn't trigger AdoptModeVisual (e.g., error), complete anyway after short delay
            if (ok && _progressBar.IsApplying)
            {
                await Task.Delay(200);
                _progressBar.Complete(true);
            }
        }
    }

    void RefreshBattery()
    {
        var reading = _battery.Read();
        var estimate = _battery.EstimateRemaining(reading);
        double? drain = _battery.CurrentDrainPercentPerHour();

        _batteryCard.Update(reading, estimate, drain);
        try { _batteryLarge?.Update(reading, estimate, drain); } catch { }

        string src = reading.HasBattery ? (reading.AcOnline ? "AC power" : "On battery") : "Desktop PC";
        _dateLine.Text = $"{DateTime.Now:dddd, MMM d} · {src}";
        _greeting.Text = Greeting();

        BatteryUpdated?.Invoke(reading);
        UpdateTrayTooltip(reading);
    }

    static string Greeting()
    {
        int hour = DateTime.Now.Hour;
        string part = hour < 5 ? "Good night" : hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
        return $"{part}, {Environment.UserName}";
    }
}
