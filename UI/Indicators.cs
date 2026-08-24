using System.Drawing.Drawing2D;

namespace PowerSave.UI;

internal sealed class StatusPill : SkinControl
{
    string _text = "READY";
    Color _color = Theme.TextDim;
    double _fade = 1;

    public int DesiredWidth { get; private set; }
    public event Action? DesiredSizeChanged;

    public StatusPill()
    {
        Height = Theme.S(32);
        Measure();
    }

    public void Set(string text, Color color, bool animate = true)
    {
        _text = text;
        _color = color;
        int before = DesiredWidth;
        Measure();
        if (DesiredWidth != before) DesiredSizeChanged?.Invoke();

        if (animate)
            AnimEngine.Animate(this, "fade", v => { _fade = v; Invalidate(); }, 300);
        else
        {
            _fade = 1;
            Invalidate();
        }
    }

    void Measure()
    {
        int textW = TextRenderer.MeasureText(_text, Theme.Body(8.8f, bold: true)).Width;
        DesiredWidth = Math.Max(Theme.S(148), Theme.S(30) + textW + Theme.S(20));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        int rad = r.Height / 2;

        // Apple capsule — thin material with vibrancy
        Theme.FillSquircle(g, r, rad, Theme.Surface);
        Theme.StrokeSquircle(g, r, rad, Theme.WithAlpha(_color, (int)(95 * _fade) + 32), 1f);

        // Inner top highlight
        using (var hi = new Pen(Color.FromArgb(10, 255, 255, 255), 1f))
        {
            var save = g.Save();
            using var clip = Theme.Squircle(r, rad);
            g.SetClip(clip);
            g.DrawLine(hi, r.X + rad, r.Y + 0.5f, r.Right - rad, r.Y + 0.5f);
            g.Restore(save);
        }

        int dotD = Theme.S(8);
        float slide = (float)((1 - _fade) * Theme.S(6));
        int dotX = Theme.S(13);
        int dotY = Height / 2 - dotD / 2;

        // Pulsing glow — Apple breathe
        float pulse = (float)(0.85 + 0.15 * Math.Sin(Environment.TickCount * 0.003));
        using (var glow = new SolidBrush(Theme.WithAlpha(_color, (int)(52 * Math.Max(0.35, _fade) * pulse))))
            g.FillEllipse(glow, dotX - Theme.S(4) * (float)_fade, dotY - Theme.S(4), dotD + Theme.S(8), dotD + Theme.S(8));
        using (var dot = new SolidBrush(Theme.WithAlpha(_color, (int)(255 * Math.Max(0.45, _fade)))))
            g.FillEllipse(dot, dotX, dotY, dotD, dotD);

        int textLeft = Theme.S(30);
        var textRect = new Rectangle(textLeft + (int)slide, 0, Width - textLeft - Theme.S(12) - (int)slide, Height);
        TextRenderer.DrawText(g, _text, Theme.Body(8.8f, bold: true), textRect,
            Theme.WithAlpha(Theme.Text, (int)(255 * Math.Max(0.5, _fade))),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}

internal sealed class StatusBar : SkinControl
{
    string _message = "Ready";
    bool _ok = true;
    double _appearT = 1;

    public string RightMeta { get; set; } = "";

    public StatusBar() => Height = Theme.S(30);

    public void SetStatus(string message, bool ok)
    {
        bool changed = !string.Equals(_message, message, StringComparison.Ordinal);
        _message = message;
        _ok = ok;
        if (changed)
            AnimEngine.Animate(this, "msg", v => { _appearT = v; Invalidate(); }, 260);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Hairline — Apple separator
        using (var sep = new Pen(Theme.Separator))
            g.DrawLine(sep, Theme.S(20), 0.5f, Width - Theme.S(20), 0.5f);

        float cy = Height / 2f;
        float slide = (float)((1 - _appearT) * Theme.S(4));
        int alpha = (int)(135 + 120 * _appearT);

        // Dot — Apple green/amber with soft glow
        using var glow = new SolidBrush(Color.FromArgb((int)(36 * Math.Max(0.3, _appearT)), _ok ? Theme.Green : Theme.Orange));
        g.FillEllipse(glow, Theme.S(20) - Theme.S(3), cy - Theme.S(5), Theme.S(11), Theme.S(11));

        using var dotBrush = new SolidBrush(Color.FromArgb(
            (int)(255 * Math.Max(0.4, _appearT)), _ok ? Theme.Green : Theme.Orange));
        g.FillEllipse(dotBrush, Theme.S(22), cy - Theme.S(2.8f), Theme.S(6), Theme.S(6));

        var msgFont = Theme.Body(8.4f);
        using var faint = new SolidBrush(Color.FromArgb(Math.Clamp(alpha, 0, 255), Theme.TextFaint));
        g.DrawString(_message, msgFont, faint,
            new PointF(Theme.S(34) + slide, cy - msgFont.GetHeight(g) / 2f));

        if (!string.IsNullOrEmpty(RightMeta))
        {
            var size = g.MeasureString(RightMeta, msgFont);
            // Right meta as capsule — Apple footnote
            var metaRect = new RectangleF(Width - size.Width - Theme.S(28), cy - Theme.S(10), size.Width + Theme.S(12), Theme.S(20));
            Theme.FillSquircle(g, new Rectangle((int)metaRect.X, (int)metaRect.Y, (int)metaRect.Width, (int)metaRect.Height), Theme.S(10), Theme.Surface);
            g.DrawString(RightMeta, msgFont, new SolidBrush(Theme.TextQuart),
                new PointF(Width - size.Width - Theme.S(22), cy - size.Height / 2f));
        }
    }
}
