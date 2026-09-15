using PowerSave.Core;
using PowerSave.Infra;

namespace PowerSave.UI;

internal sealed record LayoutTier(
    string Name,
    int CardMin,
    int CardMax,
    int CardPref,
    int QuickHeight,
    int SectionGap,
    int BottomGap);

internal static class Tiers
{
    // Compact Apple HIG — tighter, premium density
    public static LayoutTier Normal => new("normal",
        Theme.S(72), Theme.S(92), Theme.S(84), Theme.S(78), Theme.S(10), Theme.S(10));
    public static LayoutTier Compact => new("compact",
        Theme.S(66), Theme.S(84), Theme.S(76), Theme.S(70), Theme.S(8), Theme.S(8));
    public static LayoutTier Ultra => new("ultra",
        Theme.S(60), Theme.S(76), Theme.S(68), Theme.S(64), Theme.S(6), Theme.S(6));

    public static LayoutTier[] All => new[] { Normal, Compact, Ultra };
}

public sealed partial class MainForm
{
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            // Handle the case where the initial DPI differs from ctor-time DeviceDpi
            // (PerMonitorV2) — everything was laid out at the old scale.
            if (Math.Abs(DeviceDpi / 96f - Theme.ScaleFactor) > 0.01f)
            {
                Theme.InitScale(DeviceDpi);
                RecomputeHeaderHeight();
            }

            // Windows 11 rounded corners + dark mode
            int pref = Native.DWMWCP_ROUND;
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4);
            int dark = 1;
            // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
            Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4);
            // Subtle border accent — matches Blue by default
            SetBorderAccent(ModeCatalog.PowerSave.Accent);
        }
        catch { }
    }

    Color _borderCurrent = ModeCatalog.PowerSave.Accent;

    void SetBorderAccent(Color accent)
    {
        _borderCurrent = accent;
        ApplyBorderRaw(accent);
    }

    void SetBorderAccentAnimated(Color target)
    {
        if (_borderCurrent.ToArgb() == target.ToArgb() || !IsHandleCreated) { SetBorderAccent(target); return; }
        Color from = _borderCurrent;
        AnimEngine.Animate(this, "border", v =>
        {
            var c = Color.FromArgb(
                from.A + (int)((target.A - from.A) * v),
                from.R + (int)((target.R - from.R) * v),
                from.G + (int)((target.G - from.G) * v),
                from.B + (int)((target.B - from.B) * v));
            _borderCurrent = c;
            ApplyBorderRaw(c);
        }, 460);
    }

    void ApplyBorderRaw(Color accent)
    {
        try
        {
            // Premium: no visible window outline — match window background
            var b = Theme.Window;
            int colorref = b.R | (b.G << 8) | (b.B << 16);
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_BORDER_COLOR, ref colorref, 4);
        }
        catch { }
    }

    // ── Apple HIG window sizing ──────────────────────────
    (int Width, int Height, int MinWidth, int MinHeight) SolveWindowSizeForTab(int tab)
    {
        return SolveWindowSizeInternal(tab, _tier);
    }

    // Legacy entry kept for initial call; maps to tab 0
    (int Width, int Height, int MinWidth, int CollapsedRequired) SolveWindowSize(bool expanded)
    {
        var r = SolveWindowSizeForTab(0);
        return (r.Width, r.Height, r.MinWidth, r.MinHeight);
    }

    (int Width, int Height, int MinWidth, int MinHeight) SolveWindowSizeInternal(int tab, LayoutTier tier)
    {
        // Multi-monitor aware: use screen that will host the window (handle if created, else primary)
        Screen screen;
        try { screen = IsHandleCreated ? Screen.FromHandle(Handle) : Screen.PrimaryScreen ?? Screen.AllScreens[0]; }
        catch { screen = Screen.PrimaryScreen ?? Screen.AllScreens[0]; }
        int workW = screen?.WorkingArea.Width ?? 1920;
        int workH = screen?.WorkingArea.Height ?? 1080;
        int maxH = workH - Theme.S(36);
        int maxW = workW - Theme.S(24);

        int contentH = tab switch
        {
            0 => RequiredHeightModes(tier),
            1 => RequiredHeightBattery(tier),
            2 => RequiredHeightAdvanced(tier),
            _ => RequiredHeightModes(tier)
        };

        // Responsive width: prefer 860 but clamp to available width
        int desiredW = Theme.S(860);
        int minW = Theme.S(720);
        int w = Math.Clamp(desiredW, minW, Math.Max(minW, maxW - Theme.S(16)));
        // On very narrow screens (<900px avail), allow smaller
        if (workW < Theme.S(900)) w = Math.Max(Theme.S(360), maxW - Theme.S(16));

        int h = Theme.S(TitleBarH) + Theme.S(6) + _headerHeight + Theme.S(8) + Theme.S(36) + Theme.S(8) + contentH + tier.BottomGap;
        foreach (var t in Tiers.All)
        {
            int ch = tab switch { 0 => RequiredHeightModes(t), 1 => RequiredHeightBattery(t), 2 => RequiredHeightAdvanced(t), _ => RequiredHeightModes(t) };
            int th = Theme.S(TitleBarH) + Theme.S(6) + _headerHeight + Theme.S(8) + Theme.S(36) + Theme.S(8) + ch + t.BottomGap;
            if (th <= maxH) return (w, th, minW, th);
        }
        var ultra = Tiers.Ultra;
        int ultraH = Theme.S(TitleBarH) + Theme.S(6) + _headerHeight + Theme.S(8) + Theme.S(36) + Theme.S(8) +
                     (tab == 0 ? RequiredHeightModes(ultra) : tab == 1 ? RequiredHeightBattery(ultra) : RequiredHeightAdvanced(ultra)) + ultra.BottomGap;
        return (w, Math.Min(ultraH, maxH), minW, Math.Min(h, maxH));
    }

    int RequiredHeightModes(LayoutTier tier)
    {
        int cardsBlock = tier.CardPref * 3 + Theme.S(CardGapPx) * 2;
        // In modes tab, battery side card height = cardsBlock; quick strip below
        return cardsBlock + tier.SectionGap + tier.QuickHeight;
    }

    int RequiredHeightBattery(LayoutTier tier)
    {
        // Battery tab: large centered battery widget + stats + quick
        return Theme.S(320) + tier.SectionGap + Theme.S(88) + tier.SectionGap + tier.QuickHeight;
    }

    int RequiredHeightAdvanced(LayoutTier tier)
    {
        // Advanced tab: advanced panel height + quick
        if (_advanced is null) return Theme.S(360) + tier.QuickHeight;
        return _advanced.ExpandedHeight + tier.SectionGap + tier.QuickHeight;
    }

    const float TitleBarH = 36f;
    const float CardGapPx = 8f;

    // Legacy expand handler — now no-ops as Advanced is a tab; keep for compat
    void OnAdvancedExpandChanged(bool expanded)
    {
        // In Apple tab design, Advanced never collapses — animate tab switch instead
        var fit = SolveWindowSizeForTab(_activeTab);
        MinimumSize = new Size(fit.MinWidth, fit.MinHeight);
        SafeLayout();
    }

    LayoutTier PickTier(bool expanded) => PickTierForTab(_activeTab);
    LayoutTier PickTierForTab(int tab)
    {
        Screen screen;
        try { screen = IsHandleCreated ? Screen.FromHandle(Handle) : Screen.PrimaryScreen ?? Screen.AllScreens[0]; }
        catch { screen = Screen.PrimaryScreen ?? Screen.AllScreens[0]; }
        int workH = screen?.WorkingArea.Height ?? 1080;
        int maxH = workH - Theme.S(28);
        foreach (var tier in Tiers.All)
        {
            int h = Theme.S(TitleBarH) + Theme.S(6) + _headerHeight + Theme.S(8) + Theme.S(36) + Theme.S(8) +
                    (tab == 0 ? RequiredHeightModes(tier) : tab == 1 ? RequiredHeightBattery(tier) : RequiredHeightAdvanced(tier)) + tier.BottomGap;
            if (h <= maxH) return tier;
        }
        return Tiers.Ultra;
    }

    public void PerformLayoutUiPublic() => PerformLayoutUi();

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        SafeLayout();
    }

    void PerformLayoutUi()
    {
        if (_titleBar is null || _advanced is null || _quick is null || _segment is null || _progressBar is null) return;
        _layoutGuard = true;
        SuspendLayout();
        try
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            var tier = _tier;

            int pad = Theme.S(16);
            _titleBar.SetBounds(0, 0, w, Theme.S(TitleBarH));
            int headerTop = Theme.S(TitleBarH) + Theme.S(6);
            int pillW = _pill.DesiredWidth;
            int contentRight = w - pad;

            int greetW = Math.Max(Theme.S(140), w - pad * 2 - pillW - Theme.S(24));
            int greetH = TextRenderer.MeasureText("Ag", _greeting.Font).Height;
            int dateH = TextRenderer.MeasureText("Ag", _dateLine.Font).Height;
            _greeting.SetBounds(pad, headerTop + Theme.S(1), greetW, greetH + Theme.S(2));
            _dateLine.SetBounds(pad + Theme.S(1), headerTop + greetH + Theme.S(5),
                greetW, dateH + Theme.S(2));

            _pill.SetBounds(contentRight - pillW,
                headerTop + (_headerHeight - _pill.Height) / 2 - Theme.S(1),
                pillW, _pill.Height);

            int segW = Math.Min(Theme.S(340), w - pad * 2);
            _segment.SetBounds((w - segW) / 2, headerTop + _headerHeight + Theme.S(8), segW, Theme.S(34));
            int progH = Theme.S(32);
            int progW = Math.Min(Theme.S(480), w - pad * 2);
            int progX = (w - progW) / 2;
            int progY = _segment.Bottom + Theme.S(8);
            _progressBar.SetBounds(progX, progY, progW, progH);
            _progressBar.BringToFront();
            int contentTop = _segment.Bottom + Theme.S(8) + (_progressBar.Visible && _progressBar.IsApplying ? progH + Theme.S(6) : 0);

            int quickH = _quick?.Height ?? tier.QuickHeight;
            int quickTopReserved = h - tier.BottomGap - quickH;

            if (_activeTab == 0)
            {
                // Ensure cards / battery visibility managed inside LayoutModesTab (handles narrow)
                foreach (var c in _cards) c.Visible = true;
                _batteryLarge.Visible = false;
                _advanced.Visible = false;
                LayoutModesTab(pad, contentTop, w, quickTopReserved, tier);
            }
            else if (_activeTab == 1)
            {
                foreach (var c in _cards) c.Visible = false;
                _batteryCard.Visible = false;
                _advanced.Visible = false;
                _batteryLarge.Visible = true;

                int bw = Math.Min(Theme.S(460), w - pad * 2);
                int bh = Math.Min(Theme.S(320), quickTopReserved - contentTop - tier.SectionGap);
                bh = Math.Max(Theme.S(240), bh);
                int bx = (w - bw) / 2;
                _batteryLarge.SetBounds(bx, contentTop, bw, bh);
            }
            else
            {
                foreach (var c in _cards) c.Visible = false;
                _batteryCard.Visible = false;
                _batteryLarge.Visible = false;
                _advanced.Visible = true;

                int advH = _advanced.ExpandedHeight;
                int maxAdvH = quickTopReserved - contentTop - tier.SectionGap;
                if (advH > maxAdvH && maxAdvH > Theme.S(200))
                    advH = maxAdvH;
                _advanced.SetBounds(pad, contentTop, w - pad * 2, advH);
            }

            int quickY = quickTopReserved;
            if (_activeTab == 2)
            {
                int advBottom = _advanced.Bottom + tier.SectionGap;
                quickY = Math.Max(advBottom, quickY);
            }
            else if (_activeTab == 1)
            {
                int battBottom = _batteryLarge.Bottom + tier.SectionGap;
                quickY = Math.Max(battBottom, quickY);
            }

            _quick!.SetBounds(0, quickY, w, quickH);
            _quick.LayoutButtons();
        }
        finally
        {
            _layoutGuard = false;
            ResumeLayout(false);
        }
    }

    void LayoutModesTab(int pad, int contentTop, int w, int quickTopReserved, LayoutTier tier)
    {
        // Responsive two-column vs single-column fallback for narrow windows (<760)
        bool narrow = w < Theme.S(760);
        int batteryW = narrow ? 0 : Theme.S(340);
        int gapCol = Theme.S(16);
        if (narrow)
        {
            // Stack: cards full width on top, battery below if space, else hide battery in this tab (Battery tab covers it)
            int available = quickTopReserved - contentTop - tier.SectionGap;
            // In narrow, show battery as collapsed height if room, else prioritize cards
            int cardGap = Theme.S(CardGapPx);
            int cardH = (available - cardGap * 2) / 3;
            cardH = Math.Clamp(cardH, tier.CardMin, tier.CardMax);
            // If battery would also fit, reduce card area
            if (available > Theme.S(360))
            {
                int battH = Theme.S(140);
                available -= battH + tier.SectionGap;
                cardH = Math.Clamp((available - cardGap * 2) / 3, tier.CardMin, tier.CardMax);
                for (int idx = 0; idx < _cards.Count; idx++)
                    _cards[idx].SetBounds(pad, contentTop + idx * (cardH + cardGap), w - pad * 2, cardH);
                int cardsBottom = contentTop + _cards.Count * cardH + (_cards.Count - 1) * cardGap;
                _batteryCard.SetBounds(pad, cardsBottom + tier.SectionGap, w - pad * 2, battH);
                _batteryCard.Visible = true;
            }
            else
            {
                for (int idx = 0; idx < _cards.Count; idx++)
                    _cards[idx].SetBounds(pad, contentTop + idx * (cardH + cardGap), w - pad * 2, cardH);
                _batteryCard.Visible = false;
            }
            return;
        }

        int cardsW = Math.Max(Theme.S(380), w - pad * 2 - batteryW - gapCol);
        int battX = Math.Max(pad + cardsW + gapCol, w - pad - batteryW);
        if (battX + batteryW > w - Theme.S(8))
            batteryW = Math.Max(Theme.S(300), w - Theme.S(8) - battX);

        int advGapPlaceholder = tier.SectionGap;
        int availableForCards = quickTopReserved - advGapPlaceholder - contentTop;
        int cardGap2 = Theme.S(CardGapPx);
        int cardH2 = (availableForCards - cardGap2 * 2) / 3;
        cardH2 = Math.Clamp(cardH2, tier.CardMin, tier.CardMax);

        for (int idx = 0; idx < _cards.Count; idx++)
            _cards[idx].SetBounds(pad, contentTop + idx * (cardH2 + cardGap2), cardsW, cardH2);

        int cardsBottom2 = contentTop + _cards.Count * cardH2 + (_cards.Count - 1) * cardGap2;
        _batteryCard.SetBounds(battX, contentTop, batteryW, cardsBottom2 - contentTop);
        _batteryCard.Visible = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Apple dark gradient — subtle depth from top slightly lighter to bottom darker
        var rect = new Rectangle(0, 0, Width, Height);
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            rect,
            Theme.WindowTop, Theme.WindowBot, 90f);
        // Add Apple-like color blend
        var blend = new System.Drawing.Drawing2D.ColorBlend(3)
        {
            Colors = new[] { Theme.WindowTop, Color.FromArgb(16, 16, 18), Theme.WindowBot },
            Positions = new[] { 0f, 0.55f, 1f }
        };
        brush.InterpolationColors = blend;
        e.Graphics.FillRectangle(brush, e.ClipRectangle);

        // Top hairline highlight for depth
        using var hi = new Pen(Color.FromArgb(10, 255, 255, 255), 1f);
        e.Graphics.DrawLine(hi, 0, 0.5f, Width, 0.5f);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;
        const int WM_POWERBROADCAST = 0x218;
        const int PBT_APMPOWERSTATUSCHANGE = 0x14;

        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);
            if (m.Result == (IntPtr)1)
            {
                var p = PointToClient(new Point(
                    (short)((long)m.LParam & 0xFFFF),
                    (short)(((long)m.LParam >> 16) & 0xFFFF)));
                int edge = Theme.S(8);
                bool lft = p.X < edge, rgt = p.X >= Width - edge;
                bool top = p.Y < edge, bot = p.Y >= Height - edge;
                int ht = bot
                    ? (lft ? 16 : rgt ? 17 : 15)
                    : top
                        ? (lft ? 13 : rgt ? 14 : 12)
                        : lft ? 10 : rgt ? 11 : 0;
                if (ht != 0) m.Result = (IntPtr)ht;
            }
            return;
        }

        if (m.Msg == WM_POWERBROADCAST && m.WParam.ToInt64() == PBT_APMPOWERSTATUSCHANGE)
        {
            try { BeginInvoke(new Action(RefreshBattery)); } catch { }
        }

        base.WndProc(ref m);
    }
}
