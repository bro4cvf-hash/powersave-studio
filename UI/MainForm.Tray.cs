using PowerSave.Core;
using PowerSave.Infra;

namespace PowerSave.UI;

public sealed partial class MainForm
{
    NotifyIcon? _trayIcon;
    TrayPopup? _trayPopup;
    readonly Dictionary<int, Icon> _iconCache = new();
    bool _exiting;

    void SetupTrayIcon(Color accent)
    {
        _trayIcon = new NotifyIcon
        {
            Text = "PowerSave",
            Visible = true,
            Icon = CachedIcon(accent)
        };

        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                Logger.Info("tray: left click, restoring window");
                ShowFromTray();
            }
        };
        // Right-click via MouseUp (more reliable on older Windows)
        _trayIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right) ShowTrayPopup();
        };

        ModeChangedForTray += key =>
        {
            // If popup is open, refresh it
            if (_trayPopup is not null && !_trayPopup.IsDisposed)
            {
                _trayPopup.Close();
                // Optionally reopen? No
            }
        };
    }

    void ShowTrayPopup()
    {
        try
        {
            if (_trayPopup is not null && !_trayPopup.IsDisposed)
            {
                _trayPopup.Close();
                _trayPopup = null;
            }
            var popup = new TrayPopup(this, _settings, _visualKey,
                async key => await ApplyModeAsync(key).ConfigureAwait(true),
                () => ShowFromTray(),
                () => Task.Run(SystemTweaks.SleepNow),
                () => SystemTweaks.LockNow(),
                () => ForceExit());
            _trayPopup = popup;
            popup.FormClosed += (_, _) => { if (_trayPopup == popup) _trayPopup = null; };
            popup.ShowAt(Cursor.Position);
        }
        catch (Exception ex) { Logger.Error("tray popup failed", ex); }
    }

    Icon CachedIcon(Color accent)
    {
        int key = accent.ToArgb();
        if (_iconCache.TryGetValue(key, out var icon)) return icon;
        icon = Icons.CreateAppIcon(accent, 32);
        _iconCache[key] = icon;
        return icon;
    }

    void UpdateTrayAccent(Color accent)
    {
        if (_trayIcon is null) return;
        try { _trayIcon.Icon = CachedIcon(accent); } catch { }
    }

    void UpdateTrayTooltip(BatteryReading r)
    {
        if (_trayIcon is null) return;
        string mode = ModeKeys.All.Contains(_visualKey) ? ModeCatalog.Get(_visualKey).Title : "No mode";
        string suffix = "";
        if (r.HasBattery && !r.AcOnline && _battery.EstimateRemaining(r) is TimeSpan est && est.TotalHours < 48)
            suffix = $" (~{(int)est.TotalHours}h {est.Minutes}m)";
        string text = $"PowerSave - {mode}" + (r.HasBattery ? $" | {r.Percent}%" : "") + suffix;
        _trayIcon.Text = text.Length <= 63 ? text : text[..63];
    }

    public void HideToTray(bool silent = false)
    {
        Logger.Info("window hidden to tray");
        try
        {
            if (Visible) Hide();
            Opacity = 1; // ensure not stuck at fade-in 0.92 on quick hide
        }
        catch { }

        if (!silent && !_settings.FirstTrayHintShown && _trayIcon is not null)
        {
            _settings.FirstTrayHintShown = true;
            SettingsStore.Save(_settings);
            try
            {
                _trayIcon.BalloonTipTitle = "PowerSave is still running";
                _trayIcon.BalloonTipText = "Click the tray icon to reopen. Right-click for the menu.";
                _trayIcon.ShowBalloonTip(2500);
            }
            catch { }
        }
    }

    public void ShowFromTray()
    {
        try
        {
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            if (!Visible)
                Show();
            BringToFront();
            Native.ForceForeground(Handle);
            RefreshBattery();
            Logger.Info("window restored from tray");
        }
        catch (Exception ex)
        {
            Logger.Error("show from tray failed", ex);
        }
    }

    public void ForceExit()
    {
        _exiting = true;
        Logger.Info("exiting");
        try
        {
            SystemTweaks.ReleaseKeepAwake();
            SettingsStore.Save(_settings);
            Logger.Flush();
        }
        catch { }
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !_exiting && _settings.Toggles.CloseToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try { _batteryTimer?.Stop(); _batteryTimer?.Dispose(); } catch { }
        try { AnimEngine.Shutdown(); } catch { }
        try
        {
            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            foreach (var ic in _iconCache.Values) try { ic.Dispose(); } catch { }
            _iconCache.Clear();
        }
        catch { }
        base.OnFormClosed(e);
    }
}
