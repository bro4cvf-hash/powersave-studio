using System.Drawing.Drawing2D;

namespace PowerSave.UI;

using PowerSave.Core;

internal static class Theme
{
    // ── DPI ─────────────────────────────────────────────
    public static float ScaleFactor { get; private set; } = 1f;
    public static void InitScale(int deviceDpi) =>
        ScaleFactor = Math.Clamp(deviceDpi / 96f, 1f, 4f);
    public static int S(double px) => (int)Math.Round(px * ScaleFactor);
    public static float Sf(double px) => (float)(px * ScaleFactor);

    // ── Apple HIG Dark palette (iOS 17 / macOS Sonoma) ──
    // Base window — near-black with subtle blue undertone, like Apple true dark
    public static readonly Color Window    = Color.FromArgb(14, 14, 16);   // #0E0E10
    public static readonly Color WindowTop = Color.FromArgb(22, 22, 24);   // gradient top
    public static readonly Color WindowBot = Color.FromArgb(10, 10, 12);   // gradient bottom

    // Surfaces — Apple grouped backgrounds
    public static readonly Color Surface        = Color.FromArgb(28, 28, 30);  // #1C1C1E secondary
    public static readonly Color SurfaceRaised  = Color.FromArgb(36, 36, 38);  // #242426 elevated
    public static readonly Color SurfaceAlt     = Color.FromArgb(32, 32, 34);  // alt
    public static readonly Color SurfaceHover   = Color.FromArgb(44, 44, 46);  // #2C2C2E hover
    public static readonly Color SurfacePress   = Color.FromArgb(50, 50, 52);
    public static readonly Color GroupedBg      = Color.FromArgb(28, 28, 30);
    public static readonly Color InsetBg        = Color.FromArgb(38, 38, 40);

    // Hairlines / separators — Apple separator @ 0.33 opacity on dark
    public static readonly Color Stroke      = Color.FromArgb(56, 56, 58);   // #38383A opaque
    public static readonly Color StrokeSoft  = Color.FromArgb(42, 42, 44);
    public static readonly Color Separator   = Color.FromArgb(54, 54, 56);
    public static readonly Color Hairline    = Color.FromArgb(36, 36, 38);

    // Text — Apple primary / secondary / tertiary
    public static readonly Color Text       = Color.FromArgb(245, 245, 247); // label primary
    public static readonly Color TextDim    = Color.FromArgb(174, 174, 178); // secondary
    public static readonly Color TextFaint  = Color.FromArgb(120, 120, 128);  // tertiary
    public static readonly Color TextQuart  = Color.FromArgb(88, 88, 92);

    // Semantic — Apple system colors tuned for dark appearance
    // Keep mode tints but shift to Apple system palette
    public static readonly Color Green = Color.FromArgb(48, 209, 88);   // systemGreen dark #30D158
    public static readonly Color Blue  = Color.FromArgb(64, 156, 255);  // systemBlue  dark #0A84FF -> lighter for dark bg
    public static readonly Color Red   = Color.FromArgb(255, 69, 58);   // systemRed dark #FF453A
    public static readonly Color Orange = Color.FromArgb(255, 159, 10); // systemOrange #FF9F0A
    public static readonly Color Purple = Color.FromArgb(191, 90, 242);  // systemPurple #BF5AF2
    public static readonly Color Teal   = Color.FromArgb(100, 210, 255); // systemTeal #64D2FF
    public static readonly Color Danger = Red;
    public static readonly Color Amber  = Orange;

    // Tab / segmented
    public static readonly Color SegmentBg       = Color.FromArgb(42, 42, 44);
    public static readonly Color SegmentSelected = Color.FromArgb(58, 58, 60);
    public static readonly Color SegmentThumb    = Color.FromArgb(72, 72, 74);

    // Apple metrics — continuous corner radii (squircle)
    public const int RadiusWindow = 22;   // outer window squircle
    public const int RadiusCard = 16;    // mode cards, battery
    public const int RadiusGroup = 16;   // grouped inset
    public const int RadiusButton = 12;  // buttons
    public const int RadiusPill = 99;    // pill
    public const int RadiusToggle = 16;  // toggle rows
    public const int RadiusSegment = 10;

    // Blur / depth — Apple elevation
    public static readonly Color ShadowColor = Color.FromArgb(0, 0, 0);
    public static readonly Color HighlightTop = Color.FromArgb(18, 255, 255, 255); // top hairline highlight

    // ── Font cache — San Francisco / Segoe Variable ──
    static readonly Dictionary<string, Font> _cache = new();

    public static Font Font(string family, float sizePt, bool bold = false)
    {
        var key = $"{family}|{sizePt}|{bold}";
        if (_cache.TryGetValue(key, out var cached)) return cached;
        FontStyle style = bold ? FontStyle.Bold : FontStyle.Regular;
        Font f;
        try { f = new Font(family, sizePt, style, GraphicsUnit.Point); }
        catch
        {
            try { f = new Font("Segoe UI Variable Text", sizePt, style, GraphicsUnit.Point); }
            catch
            {
                try { f = new Font("Segoe UI", sizePt, style, GraphicsUnit.Point); }
                catch { f = new Font(FontFamily.GenericSansSerif, sizePt, style); }
            }
        }
        _cache[key] = f;
        return f;
    }

    // Apple type scale — SF Pro Display / Text
    // Display = large titles, Body = secondary, Caption = footnote
    public static Font Display(float pt, bool bold = true)
    {
        // Prefer SF Pro Display if installed, else Segoe Variable Display
        if (IsFontInstalled("SF Pro Display")) return Font("SF Pro Display", pt, bold);
        return Font("Segoe UI Variable Display", pt, bold);
    }
    public static Font Body(float pt, bool bold = false)
    {
        if (IsFontInstalled("SF Pro Text")) return Font("SF Pro Text", pt, bold);
        return Font("Segoe UI Variable Text", pt, bold);
    }
    public static Font Ui(float pt, bool bold = false) => Font("Segoe UI Variable Text", pt, bold);
    public static Font Mono(float pt, bool bold = false) => Font("Cascadia Code", pt, bold);

    static bool IsFontInstalled(string name)
    {
        try
        {
            using var f = new FontFamily(name);
            return true;
        }
        catch { return false; }
    }

    // ── Squircle — Apple continuous corner ─────────────────────
    // Symmetric continuous corner with 4 identical cubic Beziers,
    // like iOS app icons. (The previous version was asymmetric and
    // passed an invalid 6-argument AddBezier — did not compile.)
    public static GraphicsPath Squircle(Rectangle r, int radius)
    {
        if (r.Width <= 0 || r.Height <= 0)
            return new GraphicsPath();

        radius = Math.Clamp(radius, 0, Math.Min(r.Width, r.Height) / 2);
        if (radius <= 0)
        {
            var p0 = new GraphicsPath();
            p0.AddRectangle(r);
            return p0;
        }

        float rad = radius;
        float x = r.X;
        float y = r.Y;
        float w = r.Width;
        float h = r.Height;

        // Symmetric continuous corner: one cubic bezier per corner, identical handle
        // length along both edges. 0.552284 is the circle-arc constant; the 1.08
        // stretch gives a slightly fuller, Apple-squircle feel without overshoot.
        float k = rad * 0.552284f * 1.08f;

        var path = new GraphicsPath();
        path.StartFigure();
        // Top edge
        path.AddLine(x + rad, y, x + w - rad, y);
        // Top-right corner
        path.AddBezier(x + w - rad, y, x + w - rad + k, y, x + w, y + rad - k, x + w, y + rad);
        // Right edge
        path.AddLine(x + w, y + rad, x + w, y + h - rad);
        // Bottom-right corner
        path.AddBezier(x + w, y + h - rad, x + w, y + h - rad + k, x + w - rad + k, y + h, x + w - rad, y + h);
        // Bottom edge
        path.AddLine(x + w - rad, y + h, x + rad, y + h);
        // Bottom-left corner
        path.AddBezier(x + rad, y + h, x + rad - k, y + h, x, y + h - rad + k, x, y + h - rad);
        // Left edge
        path.AddLine(x, y + h - rad, x, y + rad);
        // Top-left corner
        path.AddBezier(x, y + rad, x, y + rad - k, x + rad - k, y, x + rad, y);

        path.CloseFigure();
        return path;
    }

    // Simpler legacy rounded alias — now delegates to Squircle for Apple look
    public static GraphicsPath RoundRect(Rectangle r, int radius) => Squircle(r, radius);

    public static void FillSquircle(Graphics g, Rectangle r, int radius, Color color)
    {
        using var path = Squircle(r, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void FillRound(Graphics g, Rectangle r, int radius, Color color) => FillSquircle(g, r, radius, color);

    public static void StrokeSquircle(Graphics g, Rectangle r, int radius, Color color, float width)
    {
        using var path = Squircle(r, radius);
        using var pen = new Pen(color, width) { Alignment = PenAlignment.Inset };
        g.DrawPath(pen, path);
    }

    public static void StrokeRound(Graphics g, Rectangle r, int radius, Color color, float width) => StrokeSquircle(g, r, radius, color, width);

    // Apple depth — subtle inner highlight + outer shadow
    public static void FillSquircleWithDepth(Graphics g, Rectangle r, int radius, Color fill)
    {
        using var path = Squircle(r, radius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);

        // Top highlight hairline — 1px inner top edge
        using var highlight = new Pen(Color.FromArgb(14, 255, 255, 255), 1f);
        var clipSave = g.Save();
        g.SetClip(path);
        g.DrawLine(highlight, r.X + radius, r.Y + 0.5f, r.Right - radius, r.Y + 0.5f);
        g.Restore(clipSave);
    }

    public static Color WithAlpha(Color c, int alpha) => Color.FromArgb(Math.Clamp(alpha, 0, 255), c);

    // Tracking — Apple letter-spacing
    public static SizeF MeasureTracked(Graphics g, string text, Font font, float trackingPx = 1.2f)
    {
        float width = 0;
        foreach (char c in text)
            width += g.MeasureString(c.ToString(), font, PointF.Empty, StringFormat.GenericTypographic).Width + trackingPx;
        return new SizeF(Math.Max(0, width - trackingPx), font.Height);
    }

    public static SizeF DrawTracked(Graphics g, string text, Font font, Brush brush, PointF origin, float trackingPx = 1.2f)
    {
        float x = origin.X;
        foreach (char c in text)
        {
            g.DrawString(c.ToString(), font, brush, x, origin.Y, StringFormat.GenericTypographic);
            x += g.MeasureString(c.ToString(), font, PointF.Empty, StringFormat.GenericTypographic).Width + trackingPx;
        }
        return new SizeF(x - origin.X - trackingPx, font.Height);
    }

    public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle rect,
        TextFormatFlags flags = TextFormatFlags.Default)
    {
        TextRenderer.DrawText(g, text, font, rect, color, flags | TextFormatFlags.PreserveGraphicsClipping);
    }

    // Apple vibrancy tint
    public static Color Vibrancy(Color baseColor, double intensity = 0.08) =>
        Color.FromArgb(baseColor.A,
            (int)Math.Clamp(baseColor.R + 255 * intensity, 0, 255),
            (int)Math.Clamp(baseColor.G + 255 * intensity, 0, 255),
            (int)Math.Clamp(baseColor.B + 255 * intensity, 0, 255));
}
