using System.Drawing.Drawing2D;

namespace PowerSave.UI;

internal sealed class ModeProgressBar : SkinControl
{
    double _progress; // 0..1 display smoothed
    double _target = 0;
    double _appear; // 0 hidden 1 visible
    bool _isApplying;
    string _title = "";
    Color _accent = Theme.Blue;
    double _shimmerT;
    double _successT;
    bool _isCompleting;

    public bool IsApplying => _isApplying;

    public ModeProgressBar()
    {
        Height = Theme.S(36);
        Visible = false;
        TabStop = false;
    }

    public void SetAccent(Color c) { _accent = c; Invalidate(); }

    public void Start(string title, Color accent)
    {
        _title = title;
        _accent = accent;
        _target = 0;
        _progress = 0;
        _successT = 0;
        _isApplying = true;
        _isCompleting = false;
        _shimmerT = 0;

        AnimEngine.Animate(this, "appear", v => { _appear = v; Invalidate(); }, 260, spring: true);
        Visible = true;

        // fake progress: 0 -> 0.78 quickly with interval
        _ = AnimateTo(0.78, 900);

        // shimmer loop
        StartShimmer();
        Invalidate();
    }

    void StartShimmer()
    {
        AnimEngine.Animate(this, "shimmer", v =>
        {
            _shimmerT = v;
            if (_isApplying && v >= 1)
            {
                _shimmerT = 0;
                StartShimmer();
                return;
            }
            Invalidate();
        }, 1400);
    }

    async Task AnimateTo(double target, int duration)
    {
        double from = _target;
        _target = Math.Clamp(target, 0, 1);
        double to = _target;
        // Fixed key: Animate() cancels any running animation with the same key,
        // so overlapping progress tweens can't fight over _progress.
        AnimEngine.Animate(this, "prog", v =>
        {
            _progress = from + (to - from) * v;
            Invalidate();
        }, duration);
        await Task.Delay(duration);
    }

    public void SetProgress(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        double from = _progress;
        _target = fraction;
        AnimEngine.Animate(this, "prog_real", v => { _progress = from + (fraction - from) * v; Invalidate(); }, 380);
    }

    public async void Complete(bool success = true)
    {
        if (!_isApplying || _isCompleting) return;
        _isCompleting = true;

        await AnimateTo(1.0, 420);
        AnimEngine.Cancel(this, "shimmer");

        if (success)
        {
            AnimEngine.Animate(this, "success", v => { _successT = v; Invalidate(); }, 360, spring: true);
            await Task.Delay(900);
        }
        else
            await Task.Delay(400);

        AnimEngine.Animate(this, "appear", v =>
        {
            _appear = 1 - v;
            Invalidate();
            if (v >= 1)
            {
                Visible = false;
                _progress = 0; _target = 0; _successT = 0;
                _isApplying = false;
                _isCompleting = false;
                if (Parent is MainForm mf) mf.BeginInvoke(new Action(() => mf.PerformLayoutUiPublic()));
            }
        }, 260);
    }

    public void Fail() => Complete(false);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        float a = (float)Math.Clamp(_appear, 0, 1);
        if (a < 0.02f) return;

        // slide + fade
        float slide = Theme.Sf(8) * (1 - a);
        g.TranslateTransform(0, -slide);
        // alpha via container
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Height / 2;

        // outer track — premium glass pill, NO outline stroke
        Color trackBg = Color.FromArgb((int)(200 * a), Theme.SurfaceRaised);
        Theme.FillSquircle(g, rect, radius, trackBg);

        // subtle inner highlight only (no outline)
        using (var hi = new Pen(Color.FromArgb((int)(14 * a), 255, 255, 255), 1f))
        {
            var save = g.Save();
            using var clip = Theme.Squircle(rect, radius);
            g.SetClip(clip);
            g.DrawLine(hi, rect.X + radius, rect.Y + 0.5f, rect.Right - radius, rect.Y + 0.5f);
            g.Restore(save);
        }

        // fill pill
        double p = Math.Clamp(_progress, 0, 1);
        if (p > 0.005)
        {
            int fillW = (int)Math.Round((Width - Theme.S(4)) * p);
            fillW = Math.Max(Theme.S(28), fillW);
            var fillRect = new Rectangle(Theme.S(2), Theme.S(2), fillW, Height - Theme.S(4));
            int fillRad = fillRect.Height / 2;

            // accent gradient — horizontal sheen
            using var lg = new LinearGradientBrush(fillRect,
                ControlPaint.Light(_accent, 0.12f), _accent, 0f);
            var blend = new ColorBlend(3)
            {
                Colors = new[] { ControlPaint.Light(_accent, 0.18f), _accent, ControlPaint.Dark(_accent, 0.06f) },
                Positions = new[] { 0f, 0.55f, 1f }
            };
            lg.InterpolationColors = blend;
            using var fillPath = Theme.Squircle(fillRect, fillRad);
            g.FillPath(lg, fillPath);

            // shimmer sweep — premium sliding highlight
            if (_isApplying && _shimmerT < 1)
            {
                g.SetClip(fillPath);
                float shimmerBand = fillRect.Width * 0.38f;
                float x = fillRect.X - shimmerBand + (float)_shimmerT * (fillRect.Width + shimmerBand * 2);
                var shimmerRect = new RectangleF(x, fillRect.Y, shimmerBand, fillRect.Height);
                using var shBrush = new LinearGradientBrush(shimmerRect,
                    Color.FromArgb(0, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255), 15f);
                shBrush.InterpolationColors = new ColorBlend(3)
                {
                    Colors = new[] { Color.FromArgb(0, 255,255,255), Color.FromArgb(70,255,255,255), Color.FromArgb(0,255,255,255) },
                    Positions = new[] { 0f, .5f, 1f }
                };
                g.FillRectangle(shBrush, shimmerRect.X, shimmerRect.Y, shimmerRect.Width, shimmerRect.Height);
                g.ResetClip();
            }

            // inner top gloss on fill
            using (var fhi = new Pen(Color.FromArgb((int)(22 * a), 255, 255, 255), 1f))
            {
                var save = g.Save();
                g.SetClip(fillPath);
                g.DrawLine(fhi, fillRect.X + fillRad, fillRect.Y + 0.5f, fillRect.Right - fillRad, fillRect.Y + 0.5f);
                g.Restore(save);
            }
        }

        // centered content — icon + title + percent, perfectly centered
        string pct = $"{(int)Math.Round(p * 100)}%";
        string label = string.IsNullOrEmpty(_title) ? pct : $"{_title}  •  {pct}";
        if (_successT > 0.45)
            label = "Done";

        // Measure to center truly
        var font = Theme.Body(8.6f, bold: true);
        var totalText = label;
        Size textSize = TextRenderer.MeasureText(totalText, font);
        int textW = textSize.Width;
        int iconSize = Theme.S(14);
        int gap = Theme.S(6);
        bool showIcon = !_isApplying && _successT > 0.3;
        int contentW = textW + (showIcon ? iconSize + gap : 0);
        int cx = (Width - contentW) / 2;
        int cy = Height / 2;

        // draw check circle when done
        if (showIcon)
        {
            float scale = (float)Math.Clamp(_successT, 0, 1);
            float s = iconSize * scale;
            float ix = cx - (iconSize - s) / 2;
            float iy = cy - s / 2;
            var icRect = new RectangleF(ix, iy, s, s);
            using var bg = new SolidBrush(Color.FromArgb((int)(255 * a), Color.White));
            g.FillEllipse(bg, icRect);
            // checkmark
            if (scale > 0.6f)
            {
                float prog = Math.Clamp((scale - 0.6f) / 0.4f, 0, 1);
                float alphaCk = prog;
                using var ckPen = new Pen(Color.FromArgb((int)(255 * alphaCk), _accent), Theme.Sf(1.6f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                var b = new RectangleF(icRect.X + s * 0.18f, icRect.Y + s * 0.18f, s * 0.64f, s * 0.64f);
                var pts = new PointF[]
                {
                    new(b.X + b.Width*0.18f, b.Y + b.Height*0.52f),
                    new(b.X + b.Width*0.42f, b.Y + b.Height*0.70f),
                    new(b.X + b.Width*0.82f, b.Y + b.Height*0.26f),
                };
                g.DrawLines(ckPen, pts);
            }
            cx += (int)(s + gap);
        }

        // text — centered, no offset, tracking
        // choose contrast: if progress fills past text center, make text white else dim
        bool overFill = p > 0.52; // when fill covers center
        Color textColor = overFill ? Color.FromArgb((int)(255 * a), 255, 255, 255)
                                   : Color.FromArgb((int)(255 * a), Theme.Text);
        // subtle shadow for legibility on fill
        if (overFill)
        {
            using var sh = new SolidBrush(Color.FromArgb((int)(40 * a), 0, 0, 0));
            TextRenderer.DrawText(g, totalText, font,
                new Rectangle(cx + 1, cy - textSize.Height / 2 + 1, textW + 2, textSize.Height),
                Color.FromArgb((int)(60 * a), 0, 0, 0),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
        TextRenderer.DrawText(g, totalText, font,
            new Rectangle(cx, cy - textSize.Height / 2, textW + 2, textSize.Height),
            textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        g.ResetTransform();
    }
}
