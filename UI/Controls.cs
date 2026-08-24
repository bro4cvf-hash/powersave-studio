using System.Drawing.Drawing2D;

namespace PowerSave.UI;

internal abstract class SkinControl : Control
{
    protected SkinControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }
}

public enum ButtonKind { Primary, Ghost, Danger }

// ── Apple segmented control — iOS 17 style ──────────────────
internal sealed class AppleSegmentedControl : SkinControl
{
    readonly string[] _items;
    int _selected;
    double _thumbT; // animated thumb position 0..n-1
    readonly double[] _hoverTs;

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < 0 || value >= _items.Length) return;
            if (_selected == value) return;
            int from = _selected;
            _selected = value;
            double fromT = _thumbT;
            AnimEngine.Animate(this, "thumb", v =>
            {
                _thumbT = fromT + (value - fromT) * v;
                Invalidate();
            }, 360, spring: true);
            SelectedChanged?.Invoke(this, value);
            Invalidate();
        }
    }

    public void SetSelectedSilently(int idx)
    {
        if (idx < 0 || idx >= _items.Length) return;
        _selected = idx;
        _thumbT = idx;
        Invalidate();
    }

    public event EventHandler<int>? SelectedChanged;

    public AppleSegmentedControl(params string[] items)
    {
        _items = items.Length == 0 ? new[] { "One" } : items;
        _hoverTs = new double[_items.Length];
        Height = Theme.S(36);
        Cursor = Cursors.Hand;
        TabStop = false;
        _thumbT = _selected;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (Width <= Theme.S(4)) { base.OnMouseMove(e); return; }
        int segW = Math.Max(1, (Width - Theme.S(4)) / _items.Length);
        int hov = Math.Clamp(e.X / Math.Max(1, segW), 0, _items.Length - 1);
        for (int i = 0; i < _items.Length; i++)
        {
            bool isHov = i == hov && ClientRectangle.Contains(e.Location);
            double target = isHov ? 1 : 0;
            if (Math.Abs(_hoverTs[i] - target) > 0.01)
            {
                int captured = i;
                bool ch = isHov;
                AnimEngine.Animate(this, $"hov{captured}", v => { _hoverTs[captured] = ch ? v : 1 - v; Invalidate(); }, 140);
            }
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        for (int i = 0; i < _items.Length; i++)
        {
            int captured = i;
            AnimEngine.Animate(this, $"hov{captured}", v => { _hoverTs[captured] = 1 - v; Invalidate(); }, 180);
        }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            int segW = Math.Max(1, (Width - Theme.S(4)) / _items.Length);
            int idx = Math.Clamp(e.X / Math.Max(1, segW), 0, _items.Length - 1);
            SelectedIndex = idx;
        }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        int rad = Theme.S(Theme.RadiusSegment);

        // Track — clean, no outline
        Theme.FillSquircle(g, r, rad, Theme.SegmentBg);

        int pad = Theme.S(2);
        int segW = (Width - pad * 2) / _items.Length;
        int segH = Height - pad * 2;

        // Thumb — sliding pill, premium no border
        double thumbX = pad + _thumbT * segW;
        var thumbRect = new Rectangle((int)Math.Round(thumbX), pad, segW, segH);
        thumbRect.Inflate(-1, -1);
        Theme.FillSquircle(g, thumbRect, Theme.S(8), Theme.SegmentSelected);
        // Thumb top highlight only
        using (var hi = new Pen(Color.FromArgb(20, 255, 255, 255), 1f))
        {
            var save = g.Save();
            using var clip = Theme.Squircle(thumbRect, Theme.S(8));
            g.SetClip(clip);
            g.DrawLine(hi, thumbRect.X + Theme.S(8), thumbRect.Y + 0.5f, thumbRect.Right - Theme.S(8), thumbRect.Y + 0.5f);
            g.Restore(save);
        }

        for (int i = 0; i < _items.Length; i++)
        {
            var segRect = new Rectangle(pad + i * segW, pad, segW, segH);
            bool sel = i == _selected;
            bool hov = _hoverTs[i] > 0.1;
            Color tc = sel ? Theme.Text : Color.FromArgb(
                170 + (int)(45 * _hoverTs[i]), Theme.TextDim);
            if (hov && !sel) tc = Theme.Text;

            // Icon placeholder — first segment gets SF symbol tint hint
            var fmt = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            var font = sel ? Theme.Body(9.2f, bold: true) : Theme.Body(9.0f);
            // Nudge selected text for thumb depth
            var txtRect = sel ? new Rectangle(segRect.X, segRect.Y - 1, segRect.Width, segRect.Height)
                              : segRect;
            TextRenderer.DrawText(g, _items[i].ToUpperInvariant(), font, txtRect, tc, fmt);
        }
    }
}

// ── Apple pill button — SF Symbols + squircle ──────────────
internal sealed class FlatButton : SkinControl
{
    public string Label { get => _label; set { _label = value; Invalidate(); } }
    public ButtonKind Kind { get; set; } = ButtonKind.Ghost;
    public Color Accent { get; set; } = Theme.Blue;
    public bool UseSfIcon { get; set; }
    public string SfIconKind { get; set; } = ""; // "moon","lock","wave","bt","gear","power"

    string _label = "";
    bool _down;
    double _hoverT, _pressT, _ripT = 1;
    Point _ripPoint;

    public FlatButton()
    {
        Cursor = Cursors.Hand;
        TabStop = false;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        AnimEngine.Animate(this, "hov", v => { _hoverT = v; Invalidate(); }, 140);
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _down = false;
        AnimEngine.Animate(this, "hov", v => { _hoverT = v; Invalidate(); }, 220);
        AnimEngine.Animate(this, "prs", v => { _pressT = v; Invalidate(); }, 160);
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _down = true;
            _ripPoint = e.Location;
            _ripT = 0;
            AnimEngine.Animate(this, "prs", v => { _pressT = v; Invalidate(); }, 80);
            AnimEngine.Animate(this, "rip", v => { _ripT = v; if (v >= 1) _ripPoint = Point.Empty; Invalidate(); }, 420);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        bool wasDown = _down;
        if (_down && e.Button == MouseButtons.Left)
        {
            _down = false;
            AnimEngine.Animate(this, "prs", v => { _pressT = v; Invalidate(); }, 160);
        }
        if (wasDown && ClientRectangle.Contains(e.Location)) OnClick(EventArgs.Empty);
        base.OnMouseUp(e);
    }

    static Color Mix(Color a, Color b, double t) =>
        Color.FromArgb(
            a.A + (int)((b.A - a.A) * Math.Clamp(t, 0, 1)),
            a.R + (int)((b.R - a.R) * Math.Clamp(t, 0, 1)),
            a.G + (int)((b.G - a.G) * Math.Clamp(t, 0, 1)),
            a.B + (int)((b.B - a.B) * Math.Clamp(t, 0, 1)));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Theme.S(Theme.RadiusButton);
        float hov = (float)_hoverT;

        Color fill, text, stroke;
        switch (Kind)
        {
            case ButtonKind.Primary:
                fill = Mix(Accent, ControlPaint.Light(Accent, 0.12f), hov);
                fill = Mix(fill, ControlPaint.Dark(Accent, 0.10f), (float)_pressT);
                text = Color.White;
                stroke = fill;
                break;
            case ButtonKind.Danger:
                fill = Mix(Theme.Surface, Theme.SurfaceHover, hov);
                fill = Mix(fill, Color.FromArgb(28, Theme.Red), (float)_pressT);
                text = Mix(Theme.TextDim, Theme.Red, 0.45 + hov * 0.55);
                stroke = Theme.WithAlpha(Theme.Red, 90 + (int)(70 * hov));
                break;
            default:
                // Apple ghost — ultra thin material
                fill = Mix(Theme.Surface, Theme.SurfaceHover, hov * 0.9);
                fill = Mix(fill, Theme.SurfacePress, (float)_pressT * 0.8f);
                text = Mix(Theme.TextDim, Theme.Text, 0.35 + hov * 0.65);
                stroke = Mix(Theme.StrokeSoft, Theme.Separator, hov);
                break;
        }

        Theme.FillSquircle(g, r, radius, fill);
        // Premium: no outlines — only inner highlight
        using (var hi = new Pen(Color.FromArgb(Kind == ButtonKind.Primary ? 18 : 10, 255, 255, 255), 1f))
        {
            var save = g.Save();
            using var p = Theme.Squircle(new Rectangle(r.X, r.Y, r.Width, r.Height - 1), radius);
            g.SetClip(p);
            g.DrawLine(hi, r.X + radius, r.Y + 0.5f, r.Right - radius, r.Y + 0.5f);
            g.Restore(save);
        }

        if (_ripPoint != Point.Empty && _ripT < 1)
        {
            using var clipPath = Theme.Squircle(r, radius);
            g.SetClip(clipPath);
            float rad = Math.Max(Width, Height) * (0.18f + 1.15f * (float)_ripT);
            int alpha = Kind == ButtonKind.Primary ? 42 : 28;
            using var br = new SolidBrush(Color.FromArgb((int)(alpha * (1 - _ripT)), 255, 255, 255));
            g.FillEllipse(br, _ripPoint.X - rad, _ripPoint.Y - rad, rad * 2, rad * 2);
            g.ResetClip();
        }

        // Icon + label layout — Apple style leading SF Symbol
        int iconSize = Theme.S(16);
        int gap = Theme.S(8);
        bool hasIcon = UseSfIcon && !string.IsNullOrEmpty(SfIconKind);
        int contentW = TextRenderer.MeasureText(Label, Theme.Body(9.1f)).Width;
        if (hasIcon) contentW += iconSize + gap;

        int cx = r.X + r.Width / 2 - contentW / 2;
        int cy = r.Y + r.Height / 2;

        if (hasIcon)
        {
            var iconRect = new RectangleF(cx, cy - iconSize / 2f, iconSize, iconSize);
            float sw = Theme.Sf(1.5);
            Color ic = Kind == ButtonKind.Primary ? Color.FromArgb(240, 255, 255, 255)
                     : Kind == ButtonKind.Danger ? Mix(Theme.TextFaint, Theme.Red, 0.5)
                     : Mix(Theme.TextFaint, Theme.Text, 0.45 + hov * 0.55);
            DrawSfIcon(g, SfIconKind, iconRect, ic, sw);
            cx += iconSize + gap;
        }

        var textRect = new Rectangle(cx, r.Y, contentW - (hasIcon ? iconSize + gap : 0), r.Height);
        TextRenderer.DrawText(g, Label, Theme.Body(9.1f), textRect, text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    static void DrawSfIcon(Graphics g, string kind, RectangleF rect, Color c, float w)
    {
        switch (kind)
        {
            case "moon": Icons.DrawMoonSF(g, rect, c, w); break;
            case "lock": Icons.DrawLockSF(g, rect, c, w); break;
            case "wave": Icons.DrawWaveformSF(g, rect, c, w); break;
            case "bt": Icons.DrawBluetoothSF(g, rect, c, w); break;
            case "gear": Icons.DrawGearSF(g, rect, c, w); break;
            case "power": Icons.DrawPowerSF(g, rect, c, w); break;
            default: break;
        }
    }
}

// ── iOS Switch — pixel-perfect Apple toggle ─────────────────
internal sealed class SwitchControl : SkinControl
{
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            AnimateKnob();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    public Color AccentColor { get; set; } = Theme.Green;
    public event EventHandler? CheckedChanged;

    bool _checked;
    double _t;
    double _pressT;

    public SwitchControl()
    {
        Size = new Size(Theme.S(51), Theme.S(31));
        Cursor = Cursors.Hand;
        TabStop = false;
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        _t = _checked ? 1 : 0;
    }

    void AnimateKnob()
    {
        double from = _t;
        AnimEngine.Animate(this, "knob", v => { _t = from + (Checked ? 1 - from : -from) * v; Invalidate(); }, 260, spring: true);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            AnimEngine.Animate(this, "press", v => { _pressT = v; Invalidate(); }, 90);
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        AnimEngine.Animate(this, "press", v => { _pressT = v; Invalidate(); }, 180);
        base.OnMouseUp(e);
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var track = new Rectangle(1, 1, Width - 3, Height - 3);
        int rad = track.Height / 2;

        // iOS track — grey when off (#3A3A3C), green when on
        Color off = Color.FromArgb(58, 58, 60);
        Color on = AccentColor;
        Color trackColor = Blend(off, on, (float)_t);
        // Press dim
        if (_pressT > 0.01)
            trackColor = Blend(trackColor, Color.FromArgb(30, 0, 0, 0), (float)(_pressT * 0.12));

        Theme.FillSquircle(g, track, rad, trackColor);

        // Inner shadow when off
        if (_t < 0.5)
        {
            using var inner = new Pen(Color.FromArgb(18, 0, 0, 0), 1f);
            using var p = Theme.Squircle(new Rectangle(track.X, track.Y, track.Width, track.Height), rad);
            g.DrawPath(inner, p);
        }

        // Knob — white with Apple drop shadow
        int knobD = Height - Theme.S(6);
        // iOS stretches knob slightly when pressed
        float stretch = (float)(_pressT * Theme.S(2));
        float x = Theme.S(3) + (float)_t * (Width - knobD - Theme.S(6));
        // Squish horizontally when dragging
        float knobW = knobD + stretch * (0.5f - Math.Abs((float)_t - 0.5f));
        float knobX = x - (knobW - knobD) / 2;

        // Shadow — Apple soft drop
        using (var shadow = new SolidBrush(Color.FromArgb(36, 0, 0, 0)))
        {
            var shRect = new RectangleF(knobX + 0.5f, Theme.S(3.5f) + 1, knobW, knobD);
            g.FillEllipse(shadow, shRect.X, shRect.Y + 1, shRect.Width, shRect.Height);
        }
        using (var shadow2 = new SolidBrush(Color.FromArgb(20, 0, 0, 0)))
        {
            g.FillEllipse(shadow2, knobX + 0.5f, Theme.S(3.5f) + 2.5f, knobW, knobD);
        }
        using var knob = new SolidBrush(Color.White);
        g.FillEllipse(knob, knobX, Theme.S(3), knobW, knobD);

        // Highlight ring on knob
        using var hi = new Pen(Color.FromArgb(12, 0, 0, 0), 1f);
        g.DrawEllipse(hi, knobX + 0.5f, Theme.S(3) + 0.5f, knobW - 1, knobD - 1);
    }

    static Color Blend(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            a.A + (int)((b.A - a.A) * t),
            a.R + (int)((b.R - a.R) * t),
            a.G + (int)((b.G - a.G) * t),
            a.B + (int)((b.B - a.B) * t));
    }
}

// ── Apple grouped inset row — iOS Settings style ───────────
internal sealed class ToggleRow : SkinControl
{
    public string Title { get; }
    public string Description { get; }
    public SwitchControl Switch { get; }
    public bool IsOn => Switch.Checked;

    public event EventHandler<bool>? Toggled;

    double _hoverT, _appearT = 1, _pressT;
    bool _isPressed;

    public ToggleRow(string title, string description, bool initial)
    {
        Title = title;
        Description = description;
        Cursor = Cursors.Hand;

        Switch = new SwitchControl { AccentColor = Theme.Green };
        Controls.Add(Switch);

        // Make entire row toggle — but not when clicking directly on switch (avoid double)
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) SetPressed(true); };
        MouseUp += (_, e) => SetPressed(false);
        Click += OnRowClick;
        // Also allow clicking label area
        Switch.Click += (_, _) => { /* handled via CheckedChanged */ };

        Switch.CheckedChanged += (_, _) =>
        {
            Toggled?.Invoke(this, Switch.Checked);
            Invalidate();
        };

        if (initial) Switch.Checked = true;
    }

    void OnRowClick(object? s, EventArgs e)
    {
        // Toggle unless the click was on the switch itself
        var pt = PointToClient(Cursor.Position);
        if (Switch.Bounds.Contains(pt)) return;
        Switch.Checked = !Switch.Checked;
    }

    void SetPressed(bool p)
    {
        _isPressed = p;
        AnimEngine.Animate(this, "press", v => { _pressT = p ? v : 1 - v; Invalidate(); }, p ? 90 : 160);
    }

    internal void SetAppear(double t) { _appearT = t; Invalidate(); }

    protected override void OnMouseEnter(EventArgs e)
    {
        AnimEngine.Animate(this, "hov", v => { _hoverT = v; Invalidate(); }, 160);
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isPressed = false;
        AnimEngine.Animate(this, "hov", v => { _hoverT = v; Invalidate(); }, 240);
        AnimEngine.Animate(this, "press", v => { _pressT = 1 - v; Invalidate(); }, 160);
        base.OnMouseLeave(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Switch is not null)
            Switch.Location = new Point(Width - Switch.Width - Theme.S(16),
                (Height - Switch.Height) / 2);
    }

    static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            a.A + (int)((b.A - a.A) * t),
            a.R + (int)((b.R - a.R) * t),
            a.G + (int)((b.G - a.G) * t),
            a.B + (int)((b.B - a.B) * t));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        float dy = (float)((1 - Math.Clamp(_appearT, 0, 1)) * Theme.S(6));
        g.TranslateTransform(0, dy);
        float a = (float)Math.Clamp(_appearT, 0, 1);

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Theme.S(12);

        // Premium cell — no outline, just fill with hover
        Color baseBg = Theme.Surface;
        Color hovBg = Mix(baseBg, Theme.SurfaceHover, _hoverT);
        Color pressBg = Mix(hovBg, Theme.SurfacePress, _pressT * 0.6);
        Theme.FillSquircle(g, r, radius, Theme.WithAlpha(pressBg, (int)(a * 255)));
        // inner top highlight for depth (no outline)
        using (var hi = new Pen(Color.FromArgb((int)(10 * a), 255, 255, 255), 1f))
        {
            var save2 = g.Save();
            using var clip = Theme.Squircle(r, radius);
            g.SetClip(clip);
            g.DrawLine(hi, r.X + radius, r.Y + 0.5f, r.Right - radius, r.Y + 0.5f);
            g.Restore(save2);
        }

        // Leading dot — perfectly vertically centered in row (fixed)
        int dotD = Theme.S(8);
        int dotX = Theme.S(16);
        int dotCY = Height / 2;
        bool on = Switch.Checked;
        if (on)
        {
            using var halo = new SolidBrush(Theme.WithAlpha(Theme.Green, (int)(32 * a)));
            g.FillEllipse(halo, dotX - Theme.S(6), dotCY - Theme.S(6), dotD + Theme.S(12), dotD + Theme.S(12));
        }
        using (var dot = new SolidBrush(Theme.WithAlpha(on ? Theme.Green : Theme.TextQuart, (int)(a * 255))))
            g.FillEllipse(dot, dotX - dotD / 2f, dotCY - dotD / 2f, dotD, dotD);

        int padX = Theme.S(32);
        int switchReserve = Switch.Width + Theme.S(24);
        int textW = Width - padX - switchReserve;
        int titleH = TextRenderer.MeasureText("Ag", Theme.Body(9.5f, bold: true)).Height;
        TextRenderer.DrawText(g, Title, Theme.Body(9.5f, bold: true),
            new Rectangle(padX, Theme.S(12), textW, titleH),
            Theme.WithAlpha(Mix(Theme.TextDim, Theme.Text, 0.35 + _hoverT * 0.65), (int)(a * 255)),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        // Description — footnote tertiary
        var descRect = new Rectangle(padX, Theme.S(12) + titleH + Theme.S(2), textW + Theme.S(6),
            Height - Theme.S(14) - titleH);
        TextRenderer.DrawText(g, Description, Theme.Body(8.1f),
            descRect,
            Theme.WithAlpha(Theme.TextFaint, (int)(a * 255)),
            TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);

        g.ResetTransform();
    }
}
