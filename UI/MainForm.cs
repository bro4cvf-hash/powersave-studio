using PowerSave.Core;
using PowerSave.Infra;

namespace PowerSave.UI;

public sealed partial class MainForm : Form
{
    readonly AppSettings _settings;
    readonly PowerManager _pm;
    readonly BatteryMonitor _battery = new();
    readonly List<ModeCard> _cards = new();

    TitleBar _titleBar = null!;
    StatusPill _pill = null!;
    ModeProgressBar _progressBar = null!;
    Label _greeting = null!, _dateLine = null!;
    BatteryCard _batteryCard = null!;
    BatteryCard _batteryLarge = null!; // for Battery tab large view
    QuickStrip _quick = null!;
    AdvancedSection _advanced = null!;
    AppleSegmentedControl _segment = null!;

    readonly System.Windows.Forms.Timer _batteryTimer;
    string _visualKey = "";
    bool _busy;
    bool _layoutGuard;
    LayoutTier _tier = Tiers.Normal;
    int _headerHeight;
    int _activeTab; // 0 Modes, 1 Battery, 2 Advanced
    double _tabTransition = 1; // for animation

    public event Action<string>? ModeChangedForTray;
    public event Action<BatteryReading>? BatteryUpdated;

    public MainForm(AppSettings settings, CliOptions cli)
    {
        _settings = settings;
        _pm = new PowerManager(_settings);
        Theme.InitScale(DeviceDpi);

        Text = "PowerSave Studio";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Window;
        DoubleBuffered = true;
        Font = Theme.Ui(9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        _tier = PickTierForTab(_activeTab);

        BuildUi();
        WirePowerManagerEvents();
        WireToggles();
        SetupTrayIcon(ModeCatalog.PowerSave.Accent);

        // Apple: fixed width, height adapts to current tab
        var fit = SolveWindowSizeForTab(_activeTab);
        MinimumSize = new Size(fit.MinWidth, fit.MinHeight);
        ClientSize = new Size(fit.Width, fit.Height);
        PerformLayoutUi();

        _batteryTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _batteryTimer.Tick += (_, _) => RefreshBattery();

        Load += async (_, _) =>
        {
            // Respect reduced-motion / startup minimized
            bool doAnim = cli.StartMinimized ? false : true;
            if (doAnim)
            {
                var final = Location;
                Location = new Point(final.X, final.Y + Theme.S(12));
                AnimEngine.Animate(this, "open", v =>
                    Location = new Point(final.X, final.Y + (int)Math.Round(Theme.S(12) * (1 - v))), 420, spring: true);

                // Subtle window fade-in
                Opacity = 0.96;
                AnimEngine.Animate(this, "fadeIn", v => Opacity = 0.96 + 0.04 * v, 260);
            }

            // OPTIMIZED: global window tweaks (transparency/animations/visuals) applied ONCE at startup,
            // not on every mode change — makes mode switching instant (only powercfg).
            try
            {
                if (_settings.Toggles.ReduceVisualEffectsOnUltraSave)
                {
                    if (!SystemTweaks.IsGlobalTweaksApplied() || !_settings.PerformanceTweaksSnapshotDone)
                    {
                        SystemTweaks.ApplyGlobalPerformanceTweaks(_settings);
                        SettingsStore.Save(_settings);
                    }
                    else Logger.Info("startup: global performance tweaks already active — skip");
                }
                else Logger.Info("startup: visual optimization disabled by toggle — skipping global tweaks");
            }
            catch (Exception ex) { try { Logger.Error("startup tweaks save failed", ex); Logger.Flush(); } catch { } }

            Native.TrimWorkingSet();
            RefreshBattery();
            await DetectCurrentModeAsync().ConfigureAwait(true);
            if (cli.ApplyMode is not null) ApplyFromExternal(cli.ApplyMode);
            _batteryTimer.Start();
            GC.Collect(1, GCCollectionMode.Optimized);
            Native.TrimWorkingSet();
        };

        VisibleChanged += (_, _) =>
        {
            if (_batteryTimer is not null)
                _batteryTimer.Interval = Visible ? 60_000 : 300_000;
        };
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Theme.InitScale(e.DeviceDpiNew);
        PerformLayoutUi();
        Invalidate(true);
    }

    void BuildUi()
    {
        _titleBar = new TitleBar();
        _titleBar.MinimizeClicked += (_, _) => WindowState = FormWindowState.Minimized;
        _titleBar.CloseClicked += (_, _) =>
        {
            Logger.Info("title bar close clicked");
            HideToTray();
        };
        Controls.Add(_titleBar);

        int greetH = TextRenderer.MeasureText("Ag", Theme.Display(14.5f, bold: true)).Height;
        int dateH = TextRenderer.MeasureText("Ag", Theme.Body(8.2f)).Height;
        _headerHeight = greetH + Theme.S(4) + dateH + Theme.S(12);

        _greeting = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            Font = Theme.Display(14.5f, bold: true),
            ForeColor = Theme.Text,
            TabStop = false,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _dateLine = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            Font = Theme.Body(8.2f),
            ForeColor = Theme.TextDim,
            TabStop = false,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(_greeting);
        Controls.Add(_dateLine);

        _pill = new StatusPill();
        _pill.DesiredSizeChanged += () => SafeLayout();
        _pill.Set("READY", Theme.TextDim, animate: false);
        Controls.Add(_pill);

        // Apple segmented control — the tab bar (iOS style)
        _segment = new AppleSegmentedControl("Modes", "Battery", "Advanced");
        _segment.SelectedChanged += (_, idx) => SwitchTab(idx);
        Controls.Add(_segment);

        _progressBar = new ModeProgressBar { Visible = false };
        Controls.Add(_progressBar);

        foreach (var spec in ModeCatalog.All)
        {
            var card = new ModeCard(spec);
            card.SelectedByUser += async (_, _) => await ApplyModeAsync(card.Spec.Key).ConfigureAwait(true);
            Controls.Add(card);
            _cards.Add(card);
        }

        _batteryCard = new BatteryCard();
        Controls.Add(_batteryCard);

        // Large battery for Battery tab — shares same data but bigger presentation
        _batteryLarge = new BatteryCard();
        Controls.Add(_batteryLarge);

        var buttons = new List<(string, EventHandler)>
        {
            ("Sleep now", (_, _) =>
            {
                _pill.Set("SLEEPING…", Theme.Blue, animate: true);
                Task.Run(SystemTweaks.SleepNow);
            }),
            ("Lock workstation", (_, _) =>
            {
                _pill.Set("LOCKING…", Theme.Blue, animate: true);
                SystemTweaks.LockNow();
            }),
            ("Free up memory", async (_, _) =>
            {
                _pill.Set("TRIMMING…", Theme.Teal, animate: true);
                int n = await Task.Run(() => SystemTweaks.TrimAllProcessesWorkingSets()).ConfigureAwait(false);
                SafeUi(() => _pill.Set($"TRIMMED {n} PROCESSES", Theme.Green, animate: true));
                Native.TrimWorkingSet();
                _ = RevertPillAfterDelay();
            }),
            ("Toggle Bluetooth", async (_, _) =>
            {
                _pill.Set("TOGGLING BT…", Theme.Blue, animate: true);
                var (ok, msg) = await Task.Run(BluetoothController.ToggleAsync).ConfigureAwait(false);
                SafeUi(() => _pill.Set(msg.ToUpperInvariant(), ok ? Theme.Green : Theme.Orange, animate: true));
                _ = RevertPillAfterDelay();
            }),
            ("Windows power settings", (_, _) =>
            {
                _pill.Set("OPENING SETTINGS…", Theme.TextDim, animate: true);
                SystemTweaks.OpenWindowsPowerSettings();
                _ = RevertPillAfterDelay();
            }),
        };

        if (_pm.IsElevated)
            buttons.Add(("Running as administrator", (_, _) => { }));
        else
            buttons.Add(("Restart as admin", (_, _) =>
            {
                if (SystemTweaks.RelaunchAsAdmin("--elevated"))
                    BeginInvoke(new Action(async () =>
                    {
                        await Task.Delay(400).ConfigureAwait(false);
                        ForceExit();
                    }));
                else
                    SafeUi(() => _pill.Set("UAC CANCELED", Theme.Orange, animate: true));
            }));

        _quick = new QuickStrip(buttons);
        Controls.Add(_quick);

        _advanced = new AdvancedSection();
        _advanced.ExpandChanged += _ => SafeLayout();
        _advanced.SizeChanged += (_, _) => SafeLayout();
        Controls.Add(_advanced);

        foreach (var row in BuildToggleRows()) _advanced.AddRow(row);

        // Initial tab — suppress event during init
        _activeTab = 0;
        _segment.SetSelectedSilently(0);
    }

    async Task RevertPillAfterDelay(int ms = 2200)
    {
        await Task.Delay(ms);
        SafeUi(() =>
        {
            if (_progressBar.IsApplying) return;
            if (!string.IsNullOrEmpty(_visualKey))
            {
                var spec = ModeCatalog.Get(_visualKey);
                _pill.Set($"{spec.Title} ACTIVE", spec.Accent, animate: true);
            }
            else _pill.Set("READY", Theme.TextDim, animate: true);
        });
    }

    void SwitchTab(int idx)
    {
        if (_activeTab == idx) return;
        int fromTab = _activeTab;
        _activeTab = idx;
        _tier = PickTierForTab(idx);

        var fromFit = SolveWindowSizeForTab(fromTab);
        var toFit = SolveWindowSizeForTab(idx);
        int fromH = ClientSize.Height;
        int toH = toFit.Height;

        _tabTransition = 0;
        AnimEngine.Animate(this, "tabFade", v => { _tabTransition = v; Invalidate(); }, 320);

        if (Math.Abs(fromH - toH) > 6)
        {
            AnimEngine.Animate(this, "tabH", v =>
            {
                int h = (int)Math.Round(fromH + (toH - fromH) * v);
                if (ClientSize.Height != h)
                    ClientSize = new Size(ClientSize.Width, h);
                MinimumSize = new Size(toFit.MinWidth, toFit.MinHeight);
                PerformLayoutUi();
            }, 380, spring: true);
        }
        else
        {
            MinimumSize = new Size(toFit.MinWidth, toFit.MinHeight);
            PerformLayoutUi();
        }
    }

    static string GetTabSubtitle(int idx) => idx switch
    {
        0 => "Choose a power mode",
        1 => "Battery health & estimates",
        2 => "Fine-tune system behaviour",
        _ => ""
    };

    void SafeLayout()
    {
        try
        {
            if (!_layoutGuard && !IsDisposed) PerformLayoutUi();
        }
        catch { }
    }

    List<ToggleRow> BuildToggleRows()
    {
        var t = _settings.Toggles;
        return new List<ToggleRow>
        {
            new("Silence background services on Ultra Save",
                "Pauses Search, telemetry (DiagTrack), SysMain and Print Spooler on Ultra Save",
                t.PauseIndexingOnUltraSave),
            new("Best performance visuals (ALL modes)",
                "Taskbar transparency OFF & animations OFF in every mode for max efficiency",
                t.ReduceVisualEffectsOnUltraSave),
            new("Wi-Fi maximum power saving",
                "Aggressive adapter power saving while in save modes",
                t.WifiPowerSaving),
            new("USB selective suspend off (Performance)",
                "Keeps USB devices at full power in Ultra Performance",
                t.UsbSelectiveSuspendOff),
            new("Keep display awake (Performance)",
                "Blocks screen sleep while Ultra Performance is active",
                t.KeepDisplayAwakeOnPerf),
            new("Start with Windows",
                "Launches minimized to the tray at sign-in",
                t.StartWithWindows),
        };
    }

    void WireToggles()
    {
        var rows = _advanced.Controls.OfType<ToggleRow>().ToList();
        var t = _settings.Toggles;

        void Bind(ToggleRow row, Action<bool> apply, bool deferredEffect)
        {
            row.Toggled += (_, on) =>
            {
                apply(on);
                SettingsStore.Save(_settings);
                _pill.Set($"{row.Title.ToUpperInvariant()}: {(on ? "ON" : "OFF")}", on ? Theme.Green : Theme.TextDim, animate: true);
                _ = RevertPillAfterDelay(1800);
            };
        }

        if (rows.Count >= 6)
        {
            Bind(rows[0], v => t.PauseIndexingOnUltraSave = v, true);
            rows[1].Toggled += (_, on) =>
            {
                t.ReduceVisualEffectsOnUltraSave = on;
                try
                {
                    if (on) SystemTweaks.ApplyGlobalPerformanceTweaks(_settings);
                    else SystemTweaks.RestoreGlobalPerformanceTweaks(_settings);
                    SettingsStore.Save(_settings);
                }
                catch (Exception ex) { Logger.Error("visual toggle failed", ex); }
                _pill.Set($"VISUALS: {(on ? "OPTIMIZED" : "RESTORED")}", on ? Theme.Green : Theme.TextDim, animate: true);
                _ = RevertPillAfterDelay(1800);
            };
            Bind(rows[2], v => t.WifiPowerSaving = v, true);
            Bind(rows[3], v => t.UsbSelectiveSuspendOff = v, true);
            Bind(rows[4], v =>
            {
                t.KeepDisplayAwakeOnPerf = v;
                if (_visualKey == ModeKeys.UltraPerformance)
                    SystemTweaks.SetKeepAwake(v);
            }, false);
            Bind(rows[5], v =>
            {
                t.StartWithWindows = v;
                SystemTweaks.SetStartWithWindows(v);
            }, false);
        }
    }

    void WirePowerManagerEvents()
    {
        _pm.OperationReported += (msg, ok) => SafeUi(() =>
        {
            if (_progressBar.IsApplying) return;
            _pill.Set(msg.ToUpperInvariant(), ok ? Theme.TextDim : Theme.Orange, animate: true);
            if (ok) _ = RevertPillAfterDelay(2000);
        });
        _pm.ModeApplied += key => SafeUi(() => AdoptModeVisual(key));
    }

    void SafeUi(Action action)
    {
        try
        {
            if (IsHandleCreated) BeginInvoke(action);
        }
        catch { }
    }
}
