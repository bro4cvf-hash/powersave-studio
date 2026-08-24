namespace PowerSave.UI;

using PowerSave.Core;
using PowerSave.Infra;

internal sealed class TitleBar : SkinControl
{
    public event EventHandler? MinimizeClicked;
    public event EventHandler? CloseClicked;

    const int BtnW = 46;
    Rectangle MinRect => new(Width - BtnW * 2, 0, BtnW, Height);
    Rectangle CloseRect => new(Width - BtnW, 0, BtnW, Height);

    bool _hovMin, _hovClose;
    double _tMin, _tClose;

    public TitleBar()
    {
        Dock = DockStyle.None;
        Height = Theme.S(36);
        Cursor = Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovMin = _hovClose = false;
        TweenButtons();
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool hm = MinRect.Contains(e.Location);
        bool hc = CloseRect.Contains(e.Location);
        if (hm != _hovMin || hc != _hovClose)
        {
            _hovMin = hm;
            _hovClose = hc;
            TweenButtons();
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    void TweenButtons()
    {
        AnimEngine.Animate(this, "min", v => { _tMin = _hovMin ? v : 1 - v; Invalidate(); }, 150);
        AnimEngine.Animate(this, "close", v => { _tClose = _hovClose ? v : 1 - v; Invalidate(); }, 150);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (MinRect.Contains(e.Location)) { MinimizeClicked?.Invoke(this, EventArgs.Empty); return; }
        if (CloseRect.Contains(e.Location)) { CloseClicked?.Invoke(this, EventArgs.Empty); return; }
        var form = FindForm();
        if (form is null) return;
        Native.ReleaseCapture();
        Native.SendMessage(form.Handle, Native.WM_NCLBUTTONDOWN, Native.HTCAPTION, 0);
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        // Ultra-minimal draggable bar — no logo Heading (user requested)
        // Only subtle window controls; hit test handled via WndProc
        DrawButton(g, MinRect, _tMin, false);
        DrawButton(g, CloseRect, _tClose, true);
    }

    static void DrawButton(Graphics g, Rectangle r, double t, bool isClose)
    {
        if (t > 0.01)
        {
            Color bg = isClose
                ? Color.FromArgb((int)(200 * t), 255, 69, 58) // Apple red
                : Color.FromArgb((int)(90 * t), Theme.SurfaceHover);
            // Squircle button bg
            var bgR = new Rectangle(r.X + Theme.S(8), r.Y + Theme.S(14), r.Width - Theme.S(16), r.Height - Theme.S(28));
            Theme.FillSquircle(g, bgR, Theme.S(8), bg);
        }
        // Glyph — Apple SF weight
        float alpha = (float)Math.Clamp(0.55 + t * 0.45, 0, 1);
        var pen = new Pen(Color.FromArgb((int)(alpha * 255), isClose && t > 0.5 ? Color.White : Theme.TextDim), Theme.Sf(1.5f))
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        float cy = r.Top + r.Height / 2f;
        float cx = r.Left + r.Width / 2f;
        if (isClose)
        {
            float s = Theme.Sf(5.5f);
            g.DrawLine(pen, cx - s, cy - s, cx + s, cy + s);
            g.DrawLine(pen, cx + s, cy - s, cx - s, cy + s);
        }
        else
        {
            float s = Theme.Sf(7);
            g.DrawLine(pen, cx - s, cy, cx + s, cy);
        }
        pen.Dispose();
    }
}

internal static class RectExt
{
    public static RectangleF InflateRect(this RectangleF r, float dx, float dy) =>
        new(r.X - dx, r.Y - dy, r.Width + dx * 2, r.Height + dy * 2);
}
