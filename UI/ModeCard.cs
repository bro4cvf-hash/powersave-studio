using System.Drawing.Drawing2D;

namespace PowerSave.UI;

using PowerSave.Core;
using PowerSave.Infra;

internal sealed class ModeCard : SkinControl
{
    public ModeSpec Spec { get; }
    public bool Selected { get; private set; }

    double _glow, _hoverT, _pressT, _shineT = 1, _ripT = 1;
    bool _down;
    Point _ripPoint;

    public event EventHandler? SelectedByUser;

    public ModeCard(ModeSpec spec)
    {
        Spec = spec;
        Cursor = Cursors.Hand;
        TabStop = false;
    }

    public void SetSelected(bool selected, bool animate = true)
    {
        if (Selected == selected) return;
        Selected = selected;
        if (!animate)
        {
            _glow = selected ? 1 : 0;
            _shineT = 1;
            Invalidate();
            return;
        }

        double from = _glow;
        AnimEngine.Animate(this, "sel", v => { _glow = from + ((selected ? 1 : 0) - from) * v; Invalidate(); }, 320);

        if (selected)
            AnimEngine.Animate(this, "shine", v => { _shineT = v; Invalidate(); }, 700, 120);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        AnimEngine.Animate(this, "hov", v => { _hoverT = v; Invalidate(); }, 150);
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
            AnimEngine.Animate(this, "rip", v => { _ripT = v; if (v >= 1) _ripPoint = Point.Empty; Invalidate(); }, 440);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_down && e.Button == MouseButtons.Left)
        {
            _down = false;
            AnimEngine.Animate(this, "prs", v => { _pressT = v; Invalidate(); }, 170);
        }
        base.OnMouseUp(e);
    }

    protected override void OnClick(EventArgs e)
    {
        SelectedByUser?.Invoke(this, EventArgs.Empty);
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Theme.S(Theme.RadiusCard);

        // Lift — Apple subtle elevation on hover
        float lift = Theme.Sf(2.0f) * (float)_hoverT * (Selected ? 0.35f : 1f)
                   - Theme.Sf(1.2f) * (float)_pressT;
        g.TranslateTransform(0, lift);

        // Premium: no outer outline — soft inner glow when selected
        if (Selected && _glow > 0.01)
        {
            // subtle accent tint glow inside
            using var glowBrush = new SolidBrush(Theme.WithAlpha(Spec.Accent, (int)(10 * _glow)));
            Theme.FillSquircle(g, Rectangle.Inflate(r, Theme.S(1), Theme.S(1)), radius + Theme.S(1), Theme.WithAlpha(Spec.Accent, (int)(8 * _glow)));
        }

        // Background — premium flat without border
        Color bg = Selected
            ? Mix(Theme.Surface, Spec.Accent, 0.055 + 0.025 * (float)_glow)
            : Mix(Theme.Surface, Theme.SurfaceHover, (float)_hoverT * 0.62);
        if (_pressT > 0.01)
            bg = Mix(bg, Theme.SurfacePress, (float)_pressT * 0.5f);

        Theme.FillSquircle(g, r, radius, bg);

        // Inner top highlight only — no outline
        using (var hi = new Pen(Color.FromArgb(Selected ? 13 : 8, 255, 255, 255), 1f))
        {
            var save = g.Save();
            using var clip = Theme.Squircle(r, radius);
            g.SetClip(clip);
            g.DrawLine(hi, r.X + radius, r.Y + 0.5f, r.Right - radius, r.Y + 0.5f);
            g.Restore(save);
        }

        // Ripple / press dim
        if (_ripPoint != Point.Empty && _ripT < 1)
        {
            using var clip = Theme.Squircle(r, radius);
            g.SetClip(clip);
            float rad = Math.Max(Width, Height) * (0.14f + 1.0f * (float)_ripT);
            using var br = new SolidBrush(Color.FromArgb((int)(28 * (1 - _ripT)), 255, 255, 255));
            g.FillEllipse(br, _ripPoint.X - rad, _ripPoint.Y - rad, rad * 2, rad * 2);
            g.ResetClip();
        }

        // ── Leading squircle icon chip — perfectly centered ────────
        int chip = Theme.S(42);
        float chipGrow = chip * 0.04f * (float)_hoverT;
        var iconRect = new RectangleF(
            Theme.S(16) - chipGrow / 2, (Height - chip) / 2f - chipGrow / 2,
            chip + chipGrow, chip + chipGrow);
        int chipRad = Theme.S(11);
        Color chipBg = Selected
            ? Theme.WithAlpha(Spec.Accent, 32)
            : Theme.WithAlpha(Spec.Accent, 16 + (int)(8 * _hoverT));
        Theme.FillSquircle(g,
            new Rectangle((int)Math.Round(iconRect.X), (int)Math.Round(iconRect.Y), (int)Math.Round(iconRect.Width), (int)Math.Round(iconRect.Height)),
            chipRad, chipBg);
        // Chip inner highlight
        using (var chipHi = new Pen(Color.FromArgb(14, 255, 255, 255), 1f))
        {
            var save = g.Save();
            using var cp = Theme.Squircle(new Rectangle((int)iconRect.X, (int)iconRect.Y, (int)iconRect.Width, (int)iconRect.Height), chipRad);
            g.SetClip(cp);
            g.DrawLine(chipHi, iconRect.X + chipRad, iconRect.Y + 0.5f, iconRect.Right - chipRad, iconRect.Y + 0.5f);
            g.Restore(save);
        }

        float inner = iconRect.Width * 0.48f;
        Icons.DrawModeIcon(g, Spec.Key,
            new RectangleF(iconRect.X + (iconRect.Width - inner) / 2, iconRect.Y + (iconRect.Height - inner) / 2, inner, inner),
            Spec.Accent, Theme.Sf(2.0f));

        // ── Text — SF Pro ───────────────────────────────────
        float textX = Theme.S(16) + chip + Theme.S(14);
        int textW = Width - (int)textX - Theme.S(56);

        Color titleColor = Selected ? Theme.Text : Mix(Theme.TextDim, Theme.Text, (float)_hoverT * 0.85);
        // Uppercase tracked like Apple section header but slightly tighter for body
        Theme.DrawTracked(g, Spec.Title, Theme.Display(11.0f, bold: true),
            new SolidBrush(titleColor), new PointF(textX, Height / 2f - Theme.S(22)), 0.9f);

        var subRect = new Rectangle((int)textX, (int)(Height / 2f + Theme.S(5)), textW, Theme.S(26));
        TextRenderer.DrawText(g, Spec.Subtitle, Theme.Body(8.2f), subRect,
            Mix(Theme.TextFaint, Theme.TextDim, 0.22f * (float)_hoverT),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

        // ── Trailing selection — Apple checkmark circle ─────
        int cx = Width - Theme.S(28);
        int cy = Height / 2;
        int outerD = Theme.S(24);
        var outerRect = new Rectangle(cx - outerD / 2, cy - outerD / 2, outerD, outerD);

        if (Selected)
        {
            // Filled accent circle with checkmark — iOS selected style
            using var circle = new SolidBrush(Spec.Accent);
            g.FillEllipse(circle, outerRect);

            // Checkmark
            Icons.DrawCheckmarkSimple(g,
                new RectangleF(outerRect.X + outerD * 0.18f, outerRect.Y + outerD * 0.18f, outerD * 0.64f, outerD * 0.64f),
                Color.White, Theme.Sf(1.9f));

            // Glow around selected circle
            using var glow = new SolidBrush(Theme.WithAlpha(Spec.Accent, (int)(30 * _glow)));
            g.FillEllipse(glow, cx - outerD * 0.42f - Theme.S(4), cy - outerD * 0.42f - Theme.S(4), outerD * 0.84f + Theme.S(8), outerD * 0.84f + Theme.S(8));
        }
        else
        {
            // Unselected — thin ring, fills subtly on hover
            int alpha = (int)(Selected ? 0 : 55 + 45 * _hoverT);
            using var ring = new Pen(Theme.WithAlpha(Mix(Theme.Stroke, Spec.Accent, 0.32f * (float)_hoverT), alpha), Theme.Sf(1.6f));
            g.DrawEllipse(ring, outerRect);

            if (_hoverT > 0.2)
            {
                using var dot = new SolidBrush(Theme.WithAlpha(Spec.Accent, (int)(52 * _hoverT)));
                float d = outerD * 0.38f;
                g.FillEllipse(dot, cx - d / 2, cy - d / 2, d, d);
            }
        }

        // Shine sweep when selected
        if (Selected && _shineT < 1)
        {
            using var clip = Theme.Squircle(r, radius);
            g.SetClip(clip);
            float band = Width * 0.34f;
            float x = (float)(-band + _shineT * (Width + band * 2));
            var rect = new RectangleF(x, 0, band, Height);
            using var lg = new LinearGradientBrush(rect, Color.Transparent, Color.Transparent, 28f);
            lg.InterpolationColors = new ColorBlend(3)
            {
                Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(22, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) },
                Positions = new[] { 0f, 0.50f, 1f }
            };
            g.FillRectangle(lg, x, 0, band, Height);
            g.ResetClip();
        }

        g.ResetTransform();
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
}
