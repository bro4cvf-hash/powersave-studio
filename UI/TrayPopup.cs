using System.Drawing.Drawing2D;
using PowerSave.Core;
using PowerSave.Infra;

namespace PowerSave.UI;

internal sealed class TrayPopup : Form
{
    sealed class Entry
    {
        public string Text = "";
        public Action? Action;
        public bool IsSeparator;
        public bool IsChecked;
        public string? ModeKey;
    }

    readonly List<Entry> _entries = new();
    readonly MainForm _main;
    readonly AppSettings _settings;
    readonly Func<string, Task> _applyMode;
    int _hover = -1;
    double _appear;
    readonly int _width;
    readonly int _itemH;
    readonly int _pad;

    bool _progressActive;
    double _progress;
    double _progressShimmer;
    Color _progressAccent = Theme.Blue;
    string _progressLabel = "";
    bool _expandedForProgress;

    public TrayPopup(MainForm main, AppSettings settings, string visualKey, Func<string, Task> applyMode, Action open, Action sleep, Action lockWs, Action quit)
    {
        _main = main;
        _settings = settings;
        _applyMode = applyMode;
        TopMost = true;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        BackColor = Theme.Window;
        _width = Theme.S(280);
        _itemH = Theme.S(32);
        _pad = Theme.S(8);

        // Build Apple HIG entries
        _entries.Add(new Entry { Text = "Open PowerSave", Action = open });
        _entries.Add(new Entry { IsSeparator = true });
        foreach (var spec in ModeCatalog.All)
        {
            // Map to nicer labels: Ultra Power Save / Balanced / Ultra Performance
            string label = spec.Key == ModeKeys.UltraPowerSave ? "Ultra Power Save" : spec.Key == ModeKeys.PowerSave ? "Balanced" : "Ultra Performance";
            string key = spec.Key;
            bool isChecked = string.Equals(visualKey, key, StringComparison.OrdinalIgnoreCase);
            _entries.Add(new Entry { Text = label, ModeKey = key, IsChecked = isChecked, Action = () => { _ = applyMode(key); } });
        }
        _entries.Add(new Entry { IsSeparator = true });
        _entries.Add(new Entry { Text = "Sleep now", Action = () => Task.Run(SystemTweaks.SleepNow) });
        _entries.Add(new Entry { Text = "Lock", Action = () => SystemTweaks.LockNow() });
        _entries.Add(new Entry { IsSeparator = true });
        _entries.Add(new Entry { Text = "Close button hides to tray", IsChecked = settings.Toggles.CloseToTray, Action = () => { settings.Toggles.CloseToTray = !settings.Toggles.CloseToTray; SettingsStore.Save(settings); } });
        _entries.Add(new Entry { Text = "Quit PowerSave", Action = quit });

        int h = _pad * 2 + _entries.Count * _itemH - 4; // separators are shorter
        // Separator reduces height
        foreach (var e in _entries) if (e.IsSeparator) h -= Theme.S(18);
        // Actually compute precisely
        h = _pad;
        foreach (var e in _entries) h += e.IsSeparator ? Theme.S(10) : _itemH;
        h += _pad;

        Size = new Size(_width, h);
        UpdateRegion();
        // Enable drop shadow via DWM
        try { int val = 2; Native.DwmSetWindowAttribute(Handle, 20, ref val, 4); } catch {}
        // DWM border transparent
        try { int c = Theme.Window.R | (Theme.Window.G << 8) | (Theme.Window.B << 16); Native.DwmSetWindowAttribute(Handle, Native.DWMWA_BORDER_COLOR, ref c, 4); } catch {}
        Opacity = 0;
        _appear = 0;
        Load += (_, _) =>
        {
            AnimEngine.Animate(this, "appear", v => { _appear = v; Opacity = v; Invalidate(); if (v < 1) Invalidate(); }, 220, spring: true);
        };
        Deactivate += (_, _) => BeginInvoke(new Action(() => Close()));
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        Resize += (_, _) => UpdateRegion();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int idx = HitTest(e.Location);
        if (idx != _hover) { _hover = idx; Invalidate(); }
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int idx = HitTest(e.Location);
        if (idx >= 0 && idx < _entries.Count)
        {
            var en = _entries[idx];
            if (en.IsSeparator) return;
            // Mode entries show bar animation and stay open until done
            if (en.ModeKey != null)
            {
                StartTrayProgress(en);
                try { en.Action?.Invoke(); } catch {}
                // don't close immediately — bar will close after complete
                // Also complete bar when main progress completes via subscription
                // Fallback auto-complete
                Task.Run(async () =>
                {
                    await Task.Delay(1800);
                    try { BeginInvoke(new Action(() => CompleteTrayProgress(true))); } catch {}
                    await Task.Delay(400);
                    try { BeginInvoke(new Action(() => Close())); } catch {}
                });
            }
            else
            {
                try { en.Action?.Invoke(); } catch {}
                Close();
            }
        }
        base.OnMouseDown(e);
    }

    void StartTrayProgress(Entry en)
    {
        var spec = en.ModeKey != null ? ModeCatalog.Get(en.ModeKey) : null;
        if (!_expandedForProgress)
        {
            _expandedForProgress = true;
            int d = _progressDelta;
            Height += d;
            Top -= d;
        }
        _progressActive = true;
        _progress = 0;
        _progressAccent = spec?.Accent ?? Theme.Blue;
        _progressLabel = spec?.Title ?? en.Text;
        _progressShimmer = 0;
        AnimEngine.Animate(this, "trayProg", v => { _progress = 0 + (0.78 - 0) * v; Invalidate(); }, 900);
        StartTrayShimmer();
        Invalidate();
    }
    void StartTrayShimmer()
    {
        AnimEngine.Animate(this, "trayShimmer", v =>
        {
            _progressShimmer = v;
            if (_progressActive && v >= 1) { _progressShimmer = 0; StartTrayShimmer(); return; }
            Invalidate();
        }, 1100);
    }
    void CompleteTrayProgress(bool success)
    {
        if (!_progressActive) return;
        double from = _progress;
        AnimEngine.Animate(this, "trayProgDone", v => { _progress = from + (1 - from) * v; Invalidate(); }, 360);
        AnimEngine.Cancel(this, "trayShimmer");
        Task.Run(async () =>
        {
            await Task.Delay(600);
            try
            {
                BeginInvoke(new Action(() =>
                {
                    _progressActive = false;
                    if (_expandedForProgress)
                    {
                        _expandedForProgress = false;
                        int d = _progressDelta;
                        Height -= d;
                        Top += d;
                    }
                    Invalidate();
                }));
            }
            catch {}
        });
    }

    int _progressDelta => Theme.S(22);
    int HitTest(Point p)
    {
        int y = _pad + (_progressActive ? _progressDelta : 0);
        for (int i = 0; i < _entries.Count; i++)
        {
            int h = _entries[i].IsSeparator ? Theme.S(10) : _itemH;
            var r = new Rectangle(_pad, y, _width - _pad * 2, h);
            if (r.Contains(p)) return i;
            y += h;
        }
        return -1;
    }

    void UpdateRegion()
    {
        try
        {
            using var p = Theme.Squircle(new Rectangle(0, 0, Width, Height), Theme.S(16));
            Region?.Dispose();
            Region = new Region(p);
        } catch {}
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var outer = new Rectangle(0, 0, Width, Height);
        int rad = Theme.S(16);
        // Background morph dark material — no outline, no shadow halo
        Color bg = Theme.SurfaceRaised;
        using (var bgBrush = new LinearGradientBrush(outer, Color.FromArgb(44,44,46), bg, 90f))
        {
            using var path = Theme.Squircle(outer, rad);
            g.FillPath(bgBrush, path);
        }
        using (var hi = new Pen(Color.FromArgb(14, 255, 255, 255), 1f))
        {
            using var path = Theme.Squircle(outer, rad);
            var save = g.Save();
            g.SetClip(path);
            g.DrawLine(hi, outer.X + rad, outer.Y + 0.5f, outer.Right - rad, outer.Y + 0.5f);
            g.Restore(save);
        }
        // Bar animation — thin top progress (no layout shift, no outline)
        if (_progressActive)
        {
            int barH = Theme.S(3);
            double p = Math.Clamp(_progress, 0, 1);
            // Clip to outer squircle so bar follows rounded top
            using var outerPath = Theme.Squircle(outer, rad);
            var save = g.Save();
            g.SetClip(outerPath);
            // track (subtle)
            using var trackBr = new SolidBrush(Color.FromArgb(24, Theme.SurfaceHover));
            g.FillRectangle(trackBr, outer.X, outer.Y, outer.Width, barH);
            if (p > 0.005)
            {
                int fillW = (int)Math.Round(Width * p);
                var fillRect = new RectangleF(outer.X, outer.Y, fillW, barH);
                using var lg = new LinearGradientBrush(fillRect, ControlPaint.Light(_progressAccent, 0.18f), _progressAccent, 0f);
                g.FillRectangle(lg, fillRect.X, fillRect.Y, fillRect.Width, fillRect.Height);
                // shimmer
                if (_progressShimmer < 1)
                {
                    float band = fillW * 0.45f;
                    float x = fillRect.X - band + (float)_progressShimmer * (fillW + band * 2);
                    var shRect = new RectangleF(x, fillRect.Y, band, barH);
                    using var shBr = new LinearGradientBrush(shRect, Color.FromArgb(0,255,255,255), Color.FromArgb(0,255,255,255), 0f);
                    shBr.InterpolationColors = new ColorBlend(3) { Colors = new[] { Color.FromArgb(0,255,255,255), Color.FromArgb(90,255,255,255), Color.FromArgb(0,255,255,255) }, Positions = new[] { 0f, .5f, 1f } };
                    g.FillRectangle(shBr, shRect);
                }
            }
            g.Restore(save);
            // Percent label centered just below bar (7px gap)
            string pct = $"{(int)Math.Round(p * 100)}%";
            string label = string.IsNullOrEmpty(_progressLabel) ? pct : $"{_progressLabel.ToUpperInvariant()} • {pct}";
            var f = Theme.Body(7.6f, bold: true);
            int labelY = outer.Y + barH + Theme.S(6);
            TextRenderer.DrawText(g, label, f, new Rectangle(outer.X, labelY, outer.Width, Theme.S(16)), _progressAccent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // Items — offset when progress bar active
        int y = _pad + (_progressActive ? _progressDelta : 0);
        for (int i = 0; i < _entries.Count; i++)
        {
            var en = _entries[i];
            int h = en.IsSeparator ? Theme.S(10) : _itemH;
            var rect = new Rectangle(_pad, y, _width - _pad * 2, h);
            if (en.IsSeparator)
            {
                // Apple separator — centered line with 12 pad
                using var sep = new Pen(Color.FromArgb(38, 255, 255, 255))
                {
                    // lighter for dark
                };
                // use Theme.Separator but with alpha
                sep.Color = Color.FromArgb(32, Theme.Separator);
                int mid = rect.Y + rect.Height / 2;
                g.DrawLine(sep, rect.X + Theme.S(12), mid, rect.Right - Theme.S(12), mid);
            }
            else
            {
                bool hov = i == _hover;
                bool isMode = en.ModeKey != null;
                // Hover bg — squircle 8, no outline
                if (hov)
                {
                    var hovRect = new Rectangle(rect.X, rect.Y + Theme.S(2), rect.Width, rect.Height - Theme.S(4));
                    Theme.FillSquircle(g, hovRect, Theme.S(8), Theme.WithAlpha(Theme.SurfaceHover, 180));
                }
                // Check indicator left
                int textLeft = rect.X + Theme.S(10);
                if (en.IsChecked)
                {
                    // Left check circle 18 with green or blue check
                    int sz = Theme.S(18);
                    int cx = rect.X + Theme.S(8);
                    int cy = rect.Y + (rect.Height - sz) / 2;
                    var box = new Rectangle(cx, cy, sz, sz);
                    // Filled accent if mode, else green check for toggle
                    Color checkBg = isMode ? ModeCatalog.Get(en.ModeKey!).Accent : Theme.Green;
                    using var bgBrush = new SolidBrush(Color.FromArgb(36, checkBg));
                    g.FillEllipse(bgBrush, box);
                    var chkRect = new RectangleF(box.X + sz*0.22f, box.Y + sz*0.22f, sz*0.56f, sz*0.56f);
                    Icons.DrawCheckmarkSimple(g, chkRect, checkBg, Theme.Sf(1.4f));
                    textLeft = box.Right + Theme.S(8);
                }
                else if (isMode)
                {
                    // Unchecked mode: show faint ring placeholder at left
                    int sz = Theme.S(10);
                    int cx = rect.X + Theme.S(12);
                    int cy = rect.Y + (rect.Height - sz)/2;
                    using var pen = new Pen(Color.FromArgb(70, Theme.TextQuart), Theme.Sf(1.2f));
                    g.DrawEllipse(pen, cx, cy, sz, sz);
                    textLeft = rect.X + Theme.S(32);
                }
                else
                {
                    textLeft = rect.X + Theme.S(14);
                }

                Color tc = hov ? Theme.Text : Theme.TextDim;
                if (en.Text == "Quit PowerSave") tc = hov ? Theme.Red : Color.FromArgb(210, Theme.TextFaint);
                var font = Theme.Body(9.1f);
                TextRenderer.DrawText(g, en.Text, font, new Rectangle(textLeft, rect.Y, rect.Right - textLeft - Theme.S(12), rect.Height), tc, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }
            y += h;
        }

        // Apply appear transform (morph scale)
        // Note: we animate opacity already; add subtle scale via transform
        // Handled via _appear interpolated in paint if needed
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00080000; // WS_EX_LAYERED for smooth
            cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return cp;
        }
    }

    public void ShowAt(Point screenPos)
    {
        // Position near cursor, ensure inside working area
        var wa = Screen.FromPoint(screenPos).WorkingArea;
        int x = screenPos.X - Width / 2;
        int y = screenPos.Y - Height - Theme.S(12);
        // If y < wa.Top (cursor near top), show below
        if (y < wa.Top + Theme.S(8)) y = screenPos.Y + Theme.S(12);
        x = Math.Clamp(x, wa.Left + Theme.S(8), wa.Right - Width - Theme.S(8));
        y = Math.Clamp(y, wa.Top + Theme.S(8), wa.Bottom - Height - Theme.S(8));
        Location = new Point(x, y);
        Show();
        Activate();
    }
}
