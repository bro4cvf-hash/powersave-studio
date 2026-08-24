using System.Drawing.Drawing2D;

namespace PowerSave.UI;

// Apple HIG — Advanced is now a TAB, not an expandable section
// This control is the full tab content: grouped inset with header + grid
internal sealed class AdvancedSection : SkinControl
{
    const int Cols = 2;
    int RowH => Theme.S(68);
    int Gap => Theme.S(10);
    int ColGap => Theme.S(12);
    int PadX => Theme.S(16);
    int HeaderH => Theme.S(56);

    readonly List<ToggleRow> _rows = new();

    // Legacy expand api — kept for compat but no longer collapsible
    // In tab mode, Advanced is always expanded
    public bool IsExpanded => true;
    public int CollapsedHeight => HeaderH;
    public int ExpandedHeight => HeaderH + ContentHeight;
    public int TargetHeight => ExpandedHeight;
    public int ContentHeight
    {
        get
        {
            int rows = GridRows;
            return rows * RowH + Math.Max(0, rows - 1) * Gap + Theme.S(12);
        }
    }
    int GridRows => Math.Max(1, (int)Math.Ceiling(_rows.Count / (double)Cols));
    int ActiveCount
    {
        get { int n = 0; foreach (var r in _rows) if (r.IsOn) n++; return n; }
    }

    // Legacy event — never fired in tab mode, but kept to avoid breaking MainForm
#pragma warning disable CS0067
    public event Action<bool>? ExpandChanged;
#pragma warning restore CS0067

    public AdvancedSection()
    {
        TabStop = false;
        // Tab content is always visible — size to fit
        Height = ExpandedHeight;
    }

    public void AddRow(ToggleRow row)
    {
        row.Height = RowH;
        _rows.Add(row);
        Controls.Add(row);
        Relayout();
        Height = ExpandedHeight;
        Invalidate();
    }

    void Relayout()
    {
        if (Width <= 0) return;
        int colW = Math.Max(Theme.S(140), (Width - PadX * 2 - ColGap * (Cols - 1)) / Cols);
        for (int i = 0; i < _rows.Count; i++)
        {
            int col = i % Cols, gridRow = i / Cols;
            _rows[i].SetBounds(
                PadX + col * (colW + ColGap),
                HeaderH + Theme.S(8) + gridRow * (RowH + Gap),
                colW, RowH);
        }
    }

    // No-op in tab mode
    public void SetExpanded(bool value) { /* tabs are always expanded */ }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
        Height = ExpandedHeight;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Theme.S(Theme.RadiusGroup);

        // Premium card — no outline
        Theme.FillSquircle(g, r, radius, Theme.Surface);
        using (var hi = new Pen(Color.FromArgb(9, 255, 255, 255), 1f))
        {
            var save = g.Save();
            using var clip = Theme.Squircle(r, radius);
            g.SetClip(clip);
            g.DrawLine(hi, r.X + radius, r.Y + 0.5f, r.Right - radius, r.Y + 0.5f);
            g.Restore(save);
        }

        DrawHeader(g);

        // Footer hint — active count
        string hint = _rows.Count == 0 ? "No tunables" : $"{ActiveCount} of {_rows.Count} enabled";
        var hintFont = Theme.Body(7.8f, bold: true);
        var hintSize = g.MeasureString(hint, hintFont);
        var pillW = (int)hintSize.Width + Theme.S(16);
        var pillH = Theme.S(18);
        var pillRect = new Rectangle(Width - pillW - Theme.S(16), HeaderH / 2 - pillH / 2, pillW, pillH);
        Theme.FillSquircle(g, pillRect, pillH / 2, Theme.WithAlpha(ActiveCount > 0 ? Theme.Green : Theme.Separator, ActiveCount > 0 ? 16 : 22));
        TextRenderer.DrawText(g, hint.ToUpperInvariant(), hintFont, pillRect,
            ActiveCount > 0 ? Theme.Green : Theme.TextFaint,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    void DrawHeader(Graphics g)
    {
        // Sliders tile — premium, centered, no outline
        int tile = Theme.S(32);
        var tileRect = new Rectangle(PadX, HeaderH / 2 - tile / 2, tile, tile);
        Theme.FillSquircle(g, tileRect, Theme.S(9), Color.FromArgb(22, Theme.Blue));
        // Icon centered
        var iconBox = new RectangleF(tileRect.X + tile * 0.18f, tileRect.Y + tile * 0.18f, tile * 0.64f, tile * 0.64f);
        Icons.DrawSlidersSF(g, iconBox, Theme.Blue, Theme.Sf(1.35f));

        // Title — Apple headline
        Theme.DrawTracked(g, "ADVANCED TUNING", Theme.Body(9.6f, bold: true),
            new SolidBrush(Theme.Text),
            new PointF(PadX + tile + Theme.S(12), HeaderH / 2f - Theme.S(9)), 1.4f);
        // Subtitle — footnote
        TextRenderer.DrawText(g, "Fine-tune power behaviours", Theme.Body(8.0f),
            new Rectangle(PadX + tile + Theme.S(12), HeaderH / 2 + Theme.S(4), Width - PadX - tile - Theme.S(120), Theme.S(16)),
            Theme.TextFaint, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        // Hairline under header — Apple separator
        using var sep = new Pen(Theme.Separator);
        g.DrawLine(sep, PadX, HeaderH - 0.5f, Width - PadX, HeaderH - 0.5f);
    }
}
