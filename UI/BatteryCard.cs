using System.Drawing.Drawing2D;

namespace PowerSave.UI;

using PowerSave.Core;

internal sealed class BatteryCard : SkinControl
{
    BatteryReading _reading;
    TimeSpan? _estimate;
    double? _drainPerHour;
    double _arcT;
    double _shownPct;
    double _pulseT = 1;
    bool _pulsing;

    public BatteryCard()
    {
        var initial = BatteryMonitor.ReadOnce();
        _reading = initial;
        _shownPct = initial.HasBattery ? initial.Percent : 0;
        TabStop = false;
    }

    public void Update(BatteryReading reading, TimeSpan? estimate, double? drainPerHour)
    {
        bool wasCharging = _reading.Charging;
        _reading = reading;
        _estimate = estimate;
        _drainPerHour = drainPerHour;

        double target = reading.HasBattery ? reading.Percent / 100.0 : 0;
        if (Math.Abs(target - _arcT) > 0.001)
        {
            double from = _arcT;
            AnimEngine.Animate(this, "arc", v => { _arcT = from + (target - from) * v; Invalidate(); }, 620);
        }

        if (reading.HasBattery && Math.Abs(reading.Percent - _shownPct) > 0.01)
        {
            double fromPct = _shownPct;
            AnimEngine.Animate(this, "pct", v => { _shownPct = fromPct + (reading.Percent - fromPct) * v; Invalidate(); }, 620);
        }

        if (reading.HasBattery && reading.Charging)
            EnsurePulse();
        else if (wasCharging || _pulsing)
        {
            AnimEngine.Cancel(this, "pulse");
            _pulsing = false;
            _pulseT = 1;
            Invalidate();
        }
        else Invalidate();
    }

    void EnsurePulse()
    {
        if (_pulsing) return;
        _pulsing = true;
        _pulseT = 0;
        AnimEngine.Animate(this, "pulse", v =>
        {
            if (v >= 1) { _pulsing = false; EnsurePulse(); return; }
            _pulseT = v; Invalidate();
        }, 1600);
    }

    static Color LevelColor(int pct) =>
        pct > 50 ? Theme.Green : pct >= 20 ? Theme.Orange : Theme.Red;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Theme.S(Theme.RadiusCard);

        // Premium card — no outline, subtle depth
        Theme.FillSquircle(g, r, radius, Theme.Surface);
        // inner highlight only
        using (var hi = new Pen(Color.FromArgb(9, 255, 255, 255), 1f))
        {
            var save = g.Save();
            using var clip = Theme.Squircle(r, radius);
            g.SetClip(clip);
            g.DrawLine(hi, r.X + radius, r.Y + 0.5f, r.Right - radius, r.Y + 0.5f);
            g.Restore(save);
        }

        // Header — Apple caption tracked
        Theme.DrawTracked(g, "BATTERY", Theme.Body(8.4f, bold: true), new SolidBrush(Theme.TextFaint),
            new PointF(Theme.S(20), Theme.S(16)), 1.8f);

        // Charging pill — top-right if charging
        if (_reading.HasBattery && _reading.Charging)
        {
            string pillTxt = "CHARGING";
            var pillFont = Theme.Body(7.6f, bold: true);
            int pillW = TextRenderer.MeasureText(pillTxt, pillFont).Width + Theme.S(18);
            int pillH = Theme.S(20);
            var pillRect = new Rectangle(Width - pillW - Theme.S(16), Theme.S(14), pillW, pillH);
            Theme.FillSquircle(g, pillRect, pillH / 2, Theme.WithAlpha(Theme.Green, 22));
            Theme.StrokeSquircle(g, pillRect, pillH / 2, Theme.WithAlpha(Theme.Green, 70), 1f);
            TextRenderer.DrawText(g, pillTxt, pillFont, pillRect, Theme.Green,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        int statsBlockH = Height >= Theme.S(240) ? Theme.S(88) : 0;
        int topPad = Theme.S(48);

        int available = Height - statsBlockH - Theme.S(34) - topPad;
        int diameter = Math.Min(Theme.S(144), Math.Max(Theme.S(72), available));
        int ringR = diameter / 2;
        var center = new Point(Width / 2, topPad + ringR);
        var ringRect = new Rectangle(center.X - ringR, center.Y - ringR, ringR * 2, ringR * 2);

        // Track — Apple thin separator
        using (var track = new Pen(Color.FromArgb(34, 58, 58, 60), Theme.Sf(10)))
        {
            track.StartCap = LineCap.Round;
            track.EndCap = LineCap.Round;
            g.DrawArc(track, ringRect, -90, 360);
        }

        Color levelColor = _reading.AcOnline ? Theme.Blue : LevelColor(_reading.Percent);
        float sweep = (float)(360 * Math.Clamp(_arcT, 0, 1));

        // Pulse ring when charging — Apple breathing
        if (_reading.HasBattery && _reading.Charging && _pulseT < 1)
        {
            int inflate = (int)(Theme.Sf(8) + Theme.Sf(10) * _pulseT);
            var pulseRect = Rectangle.Inflate(ringRect, inflate, inflate);
            using var pulsePen = new Pen(Theme.WithAlpha(levelColor, (int)(55 * (1 - _pulseT))), Theme.Sf(2.5f));
            pulsePen.StartCap = LineCap.Round;
            pulsePen.EndCap = LineCap.Round;
            // Subtle arc following progress
            g.DrawArc(pulsePen, pulseRect, -90, 70 + sweep * 0.15f);
        }

        // Value arc — Apple system color with round caps
        if (sweep > 0.5f)
        {
            using var valuePen = new Pen(levelColor, Theme.Sf(10))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            // Add slight glow
            using (var glow = new Pen(Theme.WithAlpha(levelColor, 30), Theme.Sf(14)))
            {
                glow.StartCap = LineCap.Round;
                glow.EndCap = LineCap.Round;
                g.DrawArc(glow, ringRect, -90, sweep);
            }
            g.DrawArc(valuePen, ringRect, -90, sweep);
            // Cap dot at end of arc for Apple precision
            double ang = (-90 + sweep) * Math.PI / 180.0;
            float ex = center.X + ringR * (float)Math.Cos(ang);
            float ey = center.Y + ringR * (float)Math.Sin(ang);
            using var tip = new SolidBrush(levelColor);
            g.FillEllipse(tip, ex - Theme.Sf(5), ey - Theme.Sf(5), Theme.Sf(10), Theme.Sf(10));
        }
        else if (sweep > 0)
        {
            using var valuePen = new Pen(levelColor, Theme.Sf(10)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(valuePen, ringRect, -90, sweep);
        }

        // Center percentage — San Francisco Display bold
        string pctText = _reading.HasBattery
            ? Math.Clamp((int)Math.Round(_shownPct), 0, 100).ToString()
            : "--";
        var pctFont = ringR >= Theme.S(58) ? Theme.Display(28f, bold: true) : Theme.Display(20f, bold: true);
        // Use TextRenderer for crisp center
        var pctRect = new Rectangle(center.X - ringR, center.Y - pctFont.Height / 2 - Theme.S(6),
            ringR * 2, pctFont.Height + Theme.S(4));
        TextRenderer.DrawText(g, pctText, pctFont, pctRect, Theme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // Percent sign — smaller, secondary
        if (_reading.HasBattery)
        {
            var percFont = Theme.Body(9.6f, bold: true);
            var sz = g.MeasureString(pctText, pctFont);
            float pctW = sz.Width;
            // Draw % immediately after number
            g.DrawString("%", percFont, new SolidBrush(Theme.TextFaint),
                new PointF(center.X + pctW / 2f + Theme.S(2), center.Y - Theme.S(10)));
        }

        // State line — footnote
        string stateText;
        if (!_reading.HasBattery) stateText = "No battery detected";
        else if (_reading.Charging) stateText = "Charging • Optimised";
        else if (_reading.AcOnline) stateText = "Power Adapter • Charged";
        else if (_estimate is TimeSpan est && est.TotalMinutes >= 1)
            stateText = $"About {(int)est.TotalHours} hr {est.Minutes:D2} min remaining";
        else stateText = "Calculating remaining…";

        Color stateColor = !_reading.HasBattery ? Theme.TextFaint
            : _reading.AcOnline && _reading.Charging ? Theme.Green
            : !_reading.AcOnline && _reading.HasBattery ? LevelColor(_reading.Percent)
            : Theme.TextDim;

        var stateFont = Theme.Body(9.0f);
        var stateSize = g.MeasureString(stateText, stateFont);
        float stateX = center.X - stateSize.Width / 2f;
        // Pill background for state for Apple grouped feel when on battery low
        if (!_reading.AcOnline && _reading.HasBattery && _reading.Percent <= 20 && _estimate is not null)
        {
            var pill = new RectangleF(stateX - Theme.S(10), ringRect.Bottom + Theme.S(8) - Theme.S(2), stateSize.Width + Theme.S(20), Theme.S(20));
            Theme.FillSquircle(g, new Rectangle((int)pill.X, (int)pill.Y, (int)pill.Width, (int)pill.Height), Theme.S(10), Theme.WithAlpha(Theme.Red, 14));
            g.DrawString(stateText, stateFont, new SolidBrush(Theme.Red), stateX, ringRect.Bottom + Theme.S(10) - 1);
        }
        else
        {
            g.DrawString(stateText, stateFont, new SolidBrush(stateColor),
                new PointF(stateX, ringRect.Bottom + Theme.S(10)));
        }

        if (statsBlockH <= 0) return;

        // Divider — Apple hairline
        int divY = Height - statsBlockH + Theme.S(2);
        using (var div = new Pen(Theme.Separator))
            g.DrawLine(div, Theme.S(18), divY, Width - Theme.S(18), divY);

        string source = _reading.HasBattery
            ? (_reading.AcOnline ? (_reading.Charging ? "AC • Charging" : "Power Adapter") : "Battery Power")
            : "Desktop";

        DrawStat(g, "Source", source, divY + Theme.S(12));
        DrawStat(g, "Drain", !_reading.HasBattery || _reading.AcOnline
                ? "—"
                : _drainPerHour is double d ? $"-{d:0.0} % / h" : "Measuring…",
            divY + Theme.S(38));
    }

    void DrawStat(Graphics g, string label, string value, int y)
    {
        // Apple footnote style — label tertiary, value secondary with semibold
        TextRenderer.DrawText(g, label.ToUpperInvariant(), Theme.Body(7.8f, bold: true),
            new Rectangle(Theme.S(20), y, Width / 2 - Theme.S(24), Theme.S(16)),
            Theme.TextFaint, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, value, Theme.Body(9.0f, bold: false),
            new Rectangle(Width / 2, y, Width / 2 - Theme.S(20), Theme.S(18)),
            Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
