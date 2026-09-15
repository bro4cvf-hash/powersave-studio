namespace PowerSave.UI;

internal sealed class QuickStrip : SkinControl
{
    public readonly List<Control> Buttons = new();
    readonly List<QuickTile> _tiles = new();

    public QuickStrip(IEnumerable<(string Label, EventHandler Handler)> buttons)
    {
        var iconMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sleep now"] = "moon",
            ["Lock workstation"] = "lock",
            ["Free up memory"] = "wave",
            ["Toggle Bluetooth"] = "bt",
            ["Windows power settings"] = "gear",
            ["Open Windows power settings"] = "gear",
            ["Restart as admin"] = "power",
            ["Running as administrator"] = "power",
        };

        foreach (var (label, handler) in buttons)
        {
            string kind = iconMap.TryGetValue(label, out var k) ? k : "gear";
            bool isPrimary = label.Equals("Restart as admin", StringComparison.OrdinalIgnoreCase);
            bool isDisabled = label.StartsWith("Running", StringComparison.OrdinalIgnoreCase);
            var tile = new QuickTile(label, kind, isPrimary, isDisabled);
            if (!isDisabled) tile.Click += handler;
            // Accessibility
            tile.AccessibleName = label;
            tile.AccessibleRole = AccessibleRole.PushButton;
            Controls.Add(tile);
            Buttons.Add(tile);
            _tiles.Add(tile);
        }
        Height = Theme.S(78);
    }

    public void LayoutButtons()
    {
        int pad = Theme.S(16);
        int gap = Theme.S(8);
        int top = Theme.S(26);
        int count = Math.Max(1, _tiles.Count);

        // Single-row always — responsive: shrink to fit, never wrap (polished)
        int available = Math.Max(Theme.S(80), Width - pad * 2);
        // Ideal width per tile, clamp to fit available without wrapping
        int idealW = Theme.S(120);
        int w = (available - gap * (count - 1)) / count;
        // Allow shrinking down to 84 on narrow windows instead of wrapping to 2 rows
        w = Math.Clamp(w, Theme.S(84), idealW);
        // If still doesn't fit (very narrow), reduce gap
        int totalW = w * count + gap * (count - 1);
        if (totalW > available)
        {
            gap = Math.Max(Theme.S(4), (available - w * count) / Math.Max(1, count - 1));
            totalW = w * count + gap * (count - 1);
        }
        // Center
        int startX = Math.Max(pad, (Width - totalW) / 2);
        int h = Math.Max(Theme.S(48), Height - top - Theme.S(6));
        for (int i = 0; i < _tiles.Count; i++)
            _tiles[i].SetBounds(startX + i * (w + gap), top, w, h);
        Height = Theme.S(78);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        // Centered label with even tracking — premium
        string t = "QUICK ACTIONS";
        var font = Theme.Body(7.6f, bold: true);
        // draw centered? keep left but with padding consistent
        Theme.DrawTracked(g, t, font, new SolidBrush(Theme.TextFaint), new PointF(Theme.S(22), Theme.S(6)), 1.8f);
    }

    sealed class QuickTile : SkinControl
    {
        readonly string _label;
        readonly string _iconKind;
        readonly bool _primary;
        readonly bool _disabled;
        double _hov, _press, _rip;
        Point _ripPt;
        bool _down;

        public QuickTile(string label, string iconKind, bool primary, bool disabled)
        {
            _label = label;
            _iconKind = iconKind;
            _primary = primary;
            _disabled = disabled;
            Cursor = disabled ? Cursors.Default : Cursors.Hand;
            TabStop = false;
            Enabled = !disabled;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (_disabled) return;
            AnimEngine.Animate(this, "hov", v => { _hov = v; Invalidate(); }, 140);
            base.OnMouseEnter(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            _down = false;
            AnimEngine.Animate(this, "hov", v => { _hov = v; Invalidate(); }, 200);
            AnimEngine.Animate(this, "prs", v => { _press = v; Invalidate(); }, 160);
            base.OnMouseLeave(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_disabled) return;
            if (e.Button == MouseButtons.Left)
            {
                _down = true;
                _ripPt = e.Location;
                _rip = 0;
                AnimEngine.Animate(this, "prs", v => { _press = v; Invalidate(); }, 80);
                AnimEngine.Animate(this, "rip", v => { _rip = v; if (v >= 1) _ripPt = Point.Empty; Invalidate(); }, 420);
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool wasDown = _down;
            if (_down && e.Button == MouseButtons.Left)
            {
                _down = false;
                AnimEngine.Animate(this, "prs", v => { _press = v; Invalidate(); }, 160);
            }
            if (wasDown && ClientRectangle.Contains(e.Location) && !_disabled) OnClick(EventArgs.Empty);
            base.OnMouseUp(e);
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
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = Theme.S(14);

            Color fill;
            Color text;
            Color iconColor;
            if (_disabled)
            {
                fill = Theme.Surface;
                text = Theme.TextQuart;
                iconColor = Theme.TextQuart;
            }
            else if (_primary)
            {
                Color baseFill = Theme.Blue;
                fill = Mix(baseFill, ControlPaint.Light(baseFill, 0.10f), _hov);
                fill = Mix(fill, ControlPaint.Dark(baseFill, 0.10f), _press);
                text = Color.White;
                iconColor = Color.FromArgb(235, 255, 255, 255);
            }
            else
            {
                fill = Mix(Theme.Surface, Theme.SurfaceHover, _hov * 0.9);
                fill = Mix(fill, Theme.SurfacePress, _press * 0.5);
                text = Mix(Theme.TextDim, Theme.Text, 0.35 + _hov * 0.65);
                iconColor = Mix(Theme.TextFaint, Theme.Text, 0.42 + _hov * 0.58);
            }

            Theme.FillSquircle(g, r, rad, fill);
            // inner highlight only, no outline
            using (var hi = new Pen(Color.FromArgb(_primary ? 18 : 10, 255, 255, 255), 1f))
            {
                var save = g.Save();
                using var clip = Theme.Squircle(r, rad);
                g.SetClip(clip);
                g.DrawLine(hi, r.X + rad, r.Y + 0.5f, r.Right - rad, r.Y + 0.5f);
                g.Restore(save);
            }

            if (_ripPt != Point.Empty && _rip < 1)
            {
                using var clip = Theme.Squircle(r, rad);
                g.SetClip(clip);
                float radRip = Math.Max(Width, Height) * (0.16f + 0.95f * (float)_rip);
                int alpha = _primary ? 40 : 24;
                using var br = new SolidBrush(Color.FromArgb((int)(alpha * (1 - _rip)), 255, 255, 255));
                g.FillEllipse(br, _ripPt.X - radRip, _ripPt.Y - radRip, radRip * 2, radRip * 2);
                g.ResetClip();
            }

            // Layout: icon on top centered, label below centered — perfectly centered
            int iconBoxSize = Theme.S(28);
            float iconStroke = Theme.Sf(1.45f);
            // vertical centering: icon + gap + text
            var labelFont = Theme.Body(8.4f);
            Size labelSize = TextRenderer.MeasureText(_label, labelFont);
            int gap = Theme.S(6);
            int totalH = iconBoxSize + gap + labelSize.Height;
            int topY = (Height - totalH) / 2;

            // icon rect centered horizontally
            var iconRect = new RectangleF(
                (Width - iconBoxSize) / 2f,
                topY,
                iconBoxSize, iconBoxSize);

            // draw circular tint behind icon for premium feel (only non-primary)
            if (!_primary)
            {
                int bgSize = iconBoxSize + Theme.S(8);
                var bgRect = new RectangleF((Width - bgSize) / 2f, iconRect.Y - Theme.S(4), bgSize, bgSize);
                // Use subtle tint of accent per icon kind?
                Color tint = _iconKind == "power" ? Theme.Blue : Theme.TextFaint;
                tint = Color.FromArgb(_disabled ? 10 : (int)(14 + 8 * _hov), tint);
                // circular background
                g.FillEllipse(new SolidBrush(tint), bgRect);
            }

            DrawIcon(g, _iconKind, iconRect, iconColor, iconStroke);

            // label centered below icon
            var textRect = new Rectangle(0, topY + iconBoxSize + gap, Width, labelSize.Height + 2);
            TextRenderer.DrawText(g, _label, labelFont, textRect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            if (_disabled)
            {
                // Dimmed overlay
                Theme.FillSquircle(g, r, rad, Color.FromArgb(26, 0, 0, 0));
            }
        }

        static void DrawIcon(System.Drawing.Graphics g, string kind, RectangleF rect, Color c, float w)
        {
            // rect is already iconBoxSize centered, pass centered square to draw func
            switch (kind)
            {
                case "moon": Icons.DrawMoonSF(g, rect, c, w); break;
                case "lock": Icons.DrawLockSF(g, rect, c, w); break;
                case "wave": Icons.DrawWaveformSF(g, rect, c, w); break;
                case "bt": Icons.DrawBluetoothSF(g, rect, c, w); break;
                case "gear": Icons.DrawGearSF(g, rect, c, w); break;
                case "power": Icons.DrawPowerSF(g, rect, c, w); break;
                default: Icons.DrawGearSF(g, rect, c, w); break;
            }
        }
    }
}
