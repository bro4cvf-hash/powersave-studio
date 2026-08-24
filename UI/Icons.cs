using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PowerSave.UI;

using PowerSave.Infra;

internal static class Icons
{
    // ── Real Heroicons / Tabler SVG path data (24x24 viewBox, 1.5 stroke) ──
    // Source: https://raw.githubusercontent.com/tailwindlabs/heroicons/master/src/24/outline/*.svg
    // License: MIT

    const string HERO_BOLT = "M3.75 13.5L14.25 2.25L12 10.5H20.25L9.75 21.75L12 13.5H3.75Z";
    const string HERO_MOON = "M21.7519 15.0021C20.597 15.484 19.3296 15.7501 18 15.7501C12.6152 15.7501 8.25 11.3849 8.25 6.00011C8.25 4.67052 8.51614 3.40308 8.99806 2.24817C5.47566 3.71798 3 7.19493 3 11.2501C3 16.6349 7.36522 21.0001 12.75 21.0001C16.8052 21.0001 20.2821 18.5245 21.7519 15.0021Z";
    const string HERO_LOCK = "M16.5 10.5V6.75C16.5 4.26472 14.4853 2.25 12 2.25C9.51472 2.25 7.5 4.26472 7.5 6.75V10.5M6.75 21.75H17.25C18.4926 21.75 19.5 20.7426 19.5 19.5V12.75C19.5 11.5074 18.4926 10.5 17.25 10.5H6.75C5.50736 10.5 4.5 11.5074 4.5 12.75V19.5C4.5 20.7426 5.50736 21.75 6.75 21.75Z";
    const string HERO_COG_OUTER = "M9.59356 3.94014C9.68397 3.39768 10.1533 3.00009 10.7033 3.00009H13.2972C13.8472 3.00009 14.3165 3.39768 14.4069 3.94014L14.6204 5.22119C14.6828 5.59523 14.9327 5.9068 15.2645 6.09045C15.3387 6.13151 15.412 6.17393 15.4844 6.21766C15.8095 6.41393 16.2048 6.47495 16.5604 6.34175L17.7772 5.88587C18.2922 5.69293 18.8712 5.9006 19.1462 6.37687L20.4432 8.6233C20.7181 9.09957 20.6085 9.70482 20.1839 10.0544L19.1795 10.8812C18.887 11.122 18.742 11.4938 18.7491 11.8726C18.7498 11.915 18.7502 11.9575 18.7502 12.0001C18.7502 12.0427 18.7498 12.0852 18.7491 12.1275C18.742 12.5064 18.887 12.8782 19.1795 13.119L20.1839 13.9458C20.6085 14.2953 20.7181 14.9006 20.4432 15.3769L19.1462 17.6233C18.8712 18.0996 18.2922 18.3072 17.7772 18.1143L16.5604 17.6584C16.2048 17.5252 15.8095 17.5862 15.4844 17.7825C15.412 17.8263 15.3387 17.8687 15.2645 17.9097C14.9327 18.0934 14.6828 18.4049 14.6204 18.779L14.4069 20.06C14.3165 20.6025 13.8472 21.0001 13.2972 21.0001H10.7033C10.1533 21.0001 9.68397 20.6025 9.59356 20.06L9.38005 18.779C9.31771 18.4049 9.06774 18.0934 8.73597 17.9097C8.66179 17.8687 8.58847 17.8263 8.51604 17.7825C8.19101 17.5863 7.79568 17.5252 7.44011 17.6584L6.22325 18.1143C5.70826 18.3072 5.12926 18.0996 4.85429 17.6233L3.55731 15.3769C3.28234 14.9006 3.39199 14.2954 3.81657 13.9458L4.82092 13.119C5.11343 12.8782 5.25843 12.5064 5.25141 12.1276C5.25063 12.0852 5.25023 12.0427 5.25023 12.0001C5.25023 11.9575 5.25063 11.915 5.25141 11.8726C5.25843 11.4938 5.11343 11.122 4.82092 10.8812L3.81657 10.0544C3.39199 9.70484 3.28234 9.09958 3.55731 8.62332L4.85429 6.37688C5.12926 5.90061 5.70825 5.69295 6.22325 5.88588L7.4401 6.34176C7.79566 6.47496 8.19099 6.41394 8.51603 6.21767C8.58846 6.17393 8.66179 6.13151 8.73597 6.09045C9.06774 5.9068 9.31771 5.59523 9.38005 5.22119L9.59356 3.94014Z";
    const string HERO_COG_INNER = "M15 12C15 13.6569 13.6569 15 12 15C10.3431 15 9 13.6569 9 12C9 10.3432 10.3431 9.00001 12 9.00001C13.6569 9.00001 15 10.3432 15 12Z";
    const string HERO_POWER = "M5.63604 5.63604C2.12132 9.15076 2.12132 14.8492 5.63604 18.364C9.15076 21.8787 14.8492 21.8787 18.364 18.364C21.8787 14.8492 21.8787 9.15076 18.364 5.63604M12 3V12";
    const string HERO_SIGNAL = "M9.34835 14.6517C7.88388 13.1872 7.88388 10.8128 9.34835 9.34835M14.6517 9.34835C16.1161 10.8128 16.1161 13.1872 14.6517 14.6517M7.22703 16.773C4.59099 14.1369 4.59099 9.86307 7.22703 7.22703M16.773 7.22703C19.409 9.86307 19.409 14.1369 16.773 16.773M5.10571 18.8943C1.2981 15.0867 1.2981 8.91333 5.10571 5.10571M18.8943 5.10571C22.7019 8.91333 22.7019 15.0867 18.8943 18.8943M12 12H12.0075V12.0075H12V12Z";
    const string HERO_SLIDERS = "M10.5 6L20.25 6M10.5 6C10.5 6.82843 9.82843 7.5 9 7.5C8.17157 7.5 7.5 6.82843 7.5 6M10.5 6C10.5 5.17157 9.82843 4.5 9 4.5C8.17157 4.5 7.5 5.17157 7.5 6M3.75 6H7.5M10.5 18H20.25M10.5 18C10.5 18.8284 9.82843 19.5 9 19.5C8.17157 19.5 7.5 18.8284 7.5 18M10.5 18C10.5 17.1716 9.82843 16.5 9 16.5C8.17157 16.5 7.5 17.1716 7.5 18M3.75 18L7.5 18M16.5 12L20.25 12M16.5 12C16.5 12.8284 15.8284 13.5 15 13.5C14.1716 13.5 13.5 12.8284 13.5 12M16.5 12C16.5 11.1716 15.8284 10.5 15 10.5C14.1716 10.5 13.5 11.1716 13.5 12M3.75 12H13.5";
    const string HERO_CHECK = "M4.5 12.75L10.5 18.75L19.5 5.25";
    const string HERO_SCALE = "M12 3V20.25M12 20.25C10.528 20.25 9.1179 20.515 7.81483 21M12 20.25C13.472 20.25 14.8821 20.515 16.1852 21M18.75 4.97089C16.5446 4.66051 14.291 4.5 12 4.5C9.70897 4.5 7.45542 4.66051 5.25 4.97089M18.75 4.97089C19.7604 5.1131 20.7608 5.28677 21.75 5.49087M18.75 4.97089L21.3704 15.6961C21.4922 16.1948 21.2642 16.7237 20.7811 16.8975C20.1468 17.1257 19.4629 17.25 18.75 17.25C18.0371 17.25 17.3532 17.1257 16.7189 16.8975C16.2358 16.7237 16.0078 16.1948 16.1296 15.6961L18.75 4.97089ZM2.25 5.49087C3.23922 5.28677 4.23956 5.1131 5.25 4.97089M5.25 4.97089L7.87036 15.6961C7.9922 16.1948 7.76419 16.7237 7.28114 16.8975C6.6468 17.1257 5.96292 17.25 5.25 17.25C4.53708 17.25 3.8532 17.1257 3.21886 16.8975C2.73581 16.7237 2.5078 16.1948 2.62964 15.6961L5.25 4.97089Z";
    const string HERO_CPU = "M8.25 3V4.5M4.5 8.25H3M21 8.25H19.5M4.5 12H3M21 12H19.5M4.5 15.75H3M21 15.75H19.5M8.25 19.5V21M12 3V4.5M12 19.5V21M15.75 3V4.5M15.75 19.5V21M6.75 19.5H17.25C18.4926 19.5 19.5 18.4926 19.5 17.25V6.75C19.5 5.50736 18.4926 4.5 17.25 4.5H6.75C5.50736 4.5 4.5 5.50736 4.5 6.75V17.25C4.5 18.4926 5.50736 19.5 6.75 19.5ZM7.5 7.5H16.5V16.5H7.5V7.5Z";
    // Tabler leaf (MIT) — two paths, no arc
    const string TABLER_LEAF_1 = "M5 21c.5 -4.5 2.5 -8 7 -10";
    const string TABLER_LEAF_2 = "M9 18c6.218 0 10.5 -3.288 11 -12v-2h-4.014c-9 0 -11.986 4 -12 9c0 1 0 3 2 5h3l.014 0";
    // Lucide clean icons (ISC) — perfect geometry, no arcs
    const string LUCIDE_BLUETOOTH = "m7 7 10 10-5 5V2l5 5L7 17";
    const string LUCIDE_ERASER_1 = "m7 21-4.3-4.3c-1-1-1-2.5 0-3.4l9.6-9.6c1-1 2.5-1 3.4 0l5.6 5.6c1 1 1 2.5 0 3.4L13 21";
    const string LUCIDE_ERASER_2 = "M22 21H7";
    const string LUCIDE_ERASER_3 = "m5 11 9 9";

    static RectangleF CenteredSquare(RectangleF b, float pad = 0.08f)
    {
        float s = Math.Min(b.Width, b.Height) * (1 - pad * 2);
        float x = b.X + (b.Width - s) / 2;
        float y = b.Y + (b.Height - s) / 2;
        return new RectangleF(x, y, s, s);
    }

    // ── SVG path renderer (24x24 viewBox → bounds) ──
    static void DrawSvgPath(Graphics g, string data, RectangleF bounds, Pen pen)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        using var path = ParseSvgPath(data, bounds);
        g.DrawPath(pen, path);
    }

    static GraphicsPath ParseSvgPath(string data, RectangleF bounds)
    {
        var path = new GraphicsPath();
        path.FillMode = FillMode.Winding;

        // Tokenize: letters and numbers
        var tokens = new List<string>();
        var re = new Regex(@"[a-zA-Z]|[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?");
        foreach (Match m in re.Matches(data)) tokens.Add(m.Value);

        float curX = 0, curY = 0;
        float startX = 0, startY = 0;
        float lastCx = 0, lastCy = 0;
        char lastCmd = '\0';
        bool lastWasCurve = false;

        float ScaleX(float x) => bounds.X + (x / 24f) * bounds.Width;
        float ScaleY(float y) => bounds.Y + (y / 24f) * bounds.Height;

        int i = 0;
        while (i < tokens.Count)
        {
            string tok = tokens[i];
            char cmd;
            if (tok.Length == 1 && char.IsLetter(tok[0]))
            {
                cmd = tok[0];
                i++;
            }
            else
            {
                // implicit repeat of last command (for multiple segments without new letter)
                if (lastCmd == '\0') { i++; continue; }
                cmd = lastCmd;
                // for M, subsequent pairs are L
                if (cmd == 'M') cmd = 'L';
                else if (cmd == 'm') cmd = 'l';
            }

            if (cmd == 'Z' || cmd == 'z')
            {
                path.CloseFigure();
                curX = startX; curY = startY;
                lastWasCurve = false;
                lastCmd = cmd;
                continue;
            }

            int need = ParamCount(cmd);
            bool firstSeg = true;
            while (true)
            {
                if (i + need > tokens.Count) break;
                if (need > 0 && i < tokens.Count && tokens[i].Length == 1 && char.IsLetter(tokens[i][0])) break;
                bool hasNumbers = true;
                for (int k = 0; k < need; k++)
                    if (i + k >= tokens.Count || (tokens[i + k].Length == 1 && char.IsLetter(tokens[i + k][0]))) { hasNumbers = false; break; }
                if (!hasNumbers) break;
                double[] v = new double[need];
                for (int k = 0; k < need; k++) v[k] = double.Parse(tokens[i + k], CultureInfo.InvariantCulture);
                i += need;
                char eff = cmd;
                if ((cmd == 'M' || cmd == 'm') && !firstSeg) eff = (cmd == 'M' ? 'L' : 'l');
                switch (eff)
                {
                    case 'M':
                        curX = (float)v[0]; curY = (float)v[1];
                        startX = curX; startY = curY;
                        // move doesn't draw, just set current
                        break;
                    case 'm':
                        curX += (float)v[0]; curY += (float)v[1];
                        startX = curX; startY = curY;
                        break;
                    case 'L':
                        {
                            float x = (float)v[0], y = (float)v[1];
                            path.AddLine(ScaleX(curX), ScaleY(curY), ScaleX(x), ScaleY(y));
                            curX = x; curY = y;
                            lastWasCurve = false;
                        }
                        break;
                    case 'l':
                        {
                            float x = curX + (float)v[0], y = curY + (float)v[1];
                            path.AddLine(ScaleX(curX), ScaleY(curY), ScaleX(x), ScaleY(y));
                            curX = x; curY = y;
                            lastWasCurve = false;
                        }
                        break;
                    case 'H':
                        {
                            float x = (float)v[0];
                            path.AddLine(ScaleX(curX), ScaleY(curY), ScaleX(x), ScaleY(curY));
                            curX = x;
                            lastWasCurve = false;
                        }
                        break;
                    case 'h':
                        {
                            float x = curX + (float)v[0];
                            path.AddLine(ScaleX(curX), ScaleY(curY), ScaleX(x), ScaleY(curY));
                            curX = x;
                            lastWasCurve = false;
                        }
                        break;
                    case 'V':
                        {
                            float y = (float)v[0];
                            path.AddLine(ScaleX(curX), ScaleY(curY), ScaleX(curX), ScaleY(y));
                            curY = y;
                            lastWasCurve = false;
                        }
                        break;
                    case 'v':
                        {
                            float y = curY + (float)v[0];
                            path.AddLine(ScaleX(curX), ScaleY(curY), ScaleX(curX), ScaleY(y));
                            curY = y;
                            lastWasCurve = false;
                        }
                        break;
                    case 'C':
                        {
                            float x1 = (float)v[0], y1 = (float)v[1], x2 = (float)v[2], y2 = (float)v[3], x = (float)v[4], y = (float)v[5];
                            path.AddBezier(ScaleX(curX), ScaleY(curY), ScaleX(x1), ScaleY(y1), ScaleX(x2), ScaleY(y2), ScaleX(x), ScaleY(y));
                            lastCx = x2; lastCy = y2;
                            curX = x; curY = y;
                            lastWasCurve = true;
                        }
                        break;
                    case 'c':
                        {
                            float x1 = curX + (float)v[0], y1 = curY + (float)v[1], x2 = curX + (float)v[2], y2 = curY + (float)v[3], x = curX + (float)v[4], y = curY + (float)v[5];
                            path.AddBezier(ScaleX(curX), ScaleY(curY), ScaleX(x1), ScaleY(y1), ScaleX(x2), ScaleY(y2), ScaleX(x), ScaleY(y));
                            lastCx = x2; lastCy = y2;
                            curX = x; curY = y;
                            lastWasCurve = true;
                        }
                        break;
                    case 'S':
                        {
                            float x2 = (float)v[0], y2 = (float)v[1], x = (float)v[2], y = (float)v[3];
                            float x1, y1;
                            if (lastWasCurve) { x1 = curX * 2 - lastCx; y1 = curY * 2 - lastCy; } else { x1 = curX; y1 = curY; }
                            path.AddBezier(ScaleX(curX), ScaleY(curY), ScaleX(x1), ScaleY(y1), ScaleX(x2), ScaleY(y2), ScaleX(x), ScaleY(y));
                            lastCx = x2; lastCy = y2;
                            curX = x; curY = y;
                            lastWasCurve = true;
                        }
                        break;
                    case 's':
                        {
                            float x2 = curX + (float)v[0], y2 = curY + (float)v[1], x = curX + (float)v[2], y = curY + (float)v[3];
                            float x1, y1;
                            if (lastWasCurve) { x1 = curX * 2 - lastCx; y1 = curY * 2 - lastCy; } else { x1 = curX; y1 = curY; }
                            path.AddBezier(ScaleX(curX), ScaleY(curY), ScaleX(x1), ScaleY(y1), ScaleX(x2), ScaleY(y2), ScaleX(x), ScaleY(y));
                            lastCx = x2; lastCy = y2;
                            curX = x; curY = y;
                            lastWasCurve = true;
                        }
                        break;
                    case 'Q':
                        {
                            float qx = (float)v[0], qy = (float)v[1], x = (float)v[2], y = (float)v[3];
                            // Convert quadratic to cubic
                            float c1x = curX + 2f/3f * (qx - curX);
                            float c1y = curY + 2f/3f * (qy - curY);
                            float c2x = x + 2f/3f * (qx - x);
                            float c2y = y + 2f/3f * (qy - y);
                            path.AddBezier(ScaleX(curX), ScaleY(curY), ScaleX(c1x), ScaleY(c1y), ScaleX(c2x), ScaleY(c2y), ScaleX(x), ScaleY(y));
                            lastCx = qx; lastCy = qy;
                            curX = x; curY = y;
                            lastWasCurve = true;
                        }
                        break;
                    case 'q':
                        {
                            float qx = curX + (float)v[0], qy = curY + (float)v[1], x = curX + (float)v[2], y = curY + (float)v[3];
                            float c1x = curX + 2f/3f * (qx - curX);
                            float c1y = curY + 2f/3f * (qy - curY);
                            float c2x = x + 2f/3f * (qx - x);
                            float c2y = y + 2f/3f * (qy - y);
                            path.AddBezier(ScaleX(curX), ScaleY(curY), ScaleX(c1x), ScaleY(c1y), ScaleX(c2x), ScaleY(c2y), ScaleX(x), ScaleY(y));
                            lastCx = qx; lastCy = qy;
                            curX = x; curY = y;
                            lastWasCurve = true;
                        }
                        break;
                    default:
                        lastWasCurve = false;
                        break;
                }

                // For M initial special handling: after first M, subsequent implicit should be L
                if (firstSeg && (cmd == 'M' || cmd == 'm'))
                {
                    // don't set lastCmd to M for next implicit? Keep as M for now but loop will convert
                }
                firstSeg = false;

                // Check if next tokens are numbers (implicit repeat) -> continue, else break
                if (i >= tokens.Count || (tokens[i].Length == 1 && char.IsLetter(tokens[i][0]))) break;
                // peek if enough numbers for another segment
                if (i + need > tokens.Count) break;
                // continue loop to process next segment with same cmd
            }
            lastCmd = cmd;
        }
        return path;
    }

    static int ParamCount(char cmd) => cmd switch
    {
        'M' or 'm' or 'L' or 'l' or 'T' or 't' => 2,
        'H' or 'h' or 'V' or 'v' => 1,
        'C' or 'c' => 6,
        'S' or 's' or 'Q' or 'q' => 4,
        'A' or 'a' => 7,
        'Z' or 'z' => 0,
        _ => 0
    };

    public static void DrawModeIcon(Graphics g, string modeKey, RectangleF box, Color accent, float strokeW = 2f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var sq = CenteredSquare(box, 0.08f);
        using var pen = new Pen(accent, strokeW) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        pen.MiterLimit = 2f;
        switch (modeKey)
        {
            case ModeKeys.UltraPowerSave:
                // Tabler leaf — two paths
                DrawSvgPath(g, TABLER_LEAF_1, sq, pen);
                DrawSvgPath(g, TABLER_LEAF_2, sq, pen);
                break;
            case ModeKeys.PowerSave:
                DrawSvgPath(g, HERO_SCALE, sq, pen);
                break;
            case ModeKeys.UltraPerformance:
                DrawSvgPath(g, HERO_BOLT, sq, pen);
                break;
        }
    }

    internal static void DrawBoltSF(Graphics g, RectangleF b, Pen pen)
    {
        var sq = CenteredSquare(b, 0.06f);
        DrawSvgPath(g, HERO_BOLT, sq, pen);
    }
    internal static void DrawBolt(Graphics g, RectangleF b, Pen pen) => DrawBoltSF(g, b, pen);

    public static void DrawSFLeaf(Graphics g, RectangleF r, Color c, float w)
    {
        var sq = CenteredSquare(r, 0.06f);
        using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        DrawSvgPath(g, TABLER_LEAF_1, sq, pen);
        DrawSvgPath(g, TABLER_LEAF_2, sq, pen);
    }
    public static void DrawSFBolt(Graphics g, RectangleF r, Color c, float w)
    {
        var sq = CenteredSquare(r, 0.06f);
        using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        DrawSvgPath(g, HERO_BOLT, sq, pen);
    }

    public static void DrawBatterySF(Graphics g, RectangleF b, Color c, float stroke, double level, bool charging)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float x = b.X, y = b.Y, w = b.Width, h = b.Height;
        float capW = w * 0.07f;
        var body = new RectangleF(x, y + h*0.22f, w - capW - 2, h*0.56f);
        float rad = Math.Min(5, body.Height*0.18f);
        using var pen = new Pen(c, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var bodyPath = Theme.Squircle(new Rectangle((int)body.X, (int)body.Y, (int)body.Width, (int)body.Height), (int)rad);
        g.DrawPath(pen, bodyPath);
        var cap = new RectangleF(body.Right + 1, body.Y + body.Height*0.28f, capW, body.Height*0.44f);
        using var capPath = Theme.Squircle(new Rectangle((int)cap.X, (int)cap.Y, (int)cap.Width, (int)cap.Height), 2);
        using var capFill = new SolidBrush(c);
        g.FillPath(capFill, capPath);
        if (level > 0.01)
        {
            float inset = stroke + 2.5f;
            var fillR = new RectangleF(body.X + inset, body.Y + inset, (float)((body.Width - inset*2) * Math.Clamp(level,0,1)), body.Height - inset*2);
            if (fillR.Width > 4)
            {
                Color fillC = level > 0.5 ? Theme.Green : level > 0.2 ? Theme.Orange : Theme.Red;
                if (charging) fillC = Theme.Green;
                using var fill = new SolidBrush(fillC);
                using var fillPath = Theme.Squircle(new Rectangle((int)fillR.X, (int)fillR.Y, (int)fillR.Width, (int)fillR.Height), 2);
                g.FillPath(fill, fillPath);
            }
        }
        if (charging)
        {
            var boltBox = new RectangleF(body.X + body.Width*0.32f, body.Y + body.Height*0.16f, body.Width*0.36f, body.Height*0.68f);
            using var bp = new Pen(Color.White, Math.Max(1.2f, stroke*0.7f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            DrawSvgPath(g, HERO_BOLT, boltBox, bp);
        }
    }

    public static void DrawMoonSF(Graphics g, RectangleF b, Color c, float stroke)
    {
        var sq = CenteredSquare(b, 0.10f);
        using var pen = new Pen(c, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        DrawSvgPath(g, HERO_MOON, sq, pen);
    }

    public static void DrawLockSF(Graphics g, RectangleF b, Color c, float stroke)
    {
        var sq = CenteredSquare(b, 0.10f);
        using var pen = new Pen(c, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        DrawSvgPath(g, HERO_LOCK, sq, pen);
    }

    public static void DrawWaveformSF(Graphics g, RectangleF b, Color c, float stroke)
    {
        // "Free up memory" — lucide eraser (clean, centered, no deformation)
        var sq = CenteredSquare(b, 0.10f);
        using var pen = new Pen(c, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        DrawSvgPath(g, LUCIDE_ERASER_1, sq, pen);
        DrawSvgPath(g, LUCIDE_ERASER_2, sq, pen);
        DrawSvgPath(g, LUCIDE_ERASER_3, sq, pen);
    }

    public static void DrawBluetoothSF(Graphics g, RectangleF b, Color c, float stroke)
    {
        // Lucide bluetooth — iconic B shape, perfectly centered in 24 viewBox
        var sq = CenteredSquare(b, 0.10f);
        using var pen = new Pen(c, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        DrawSvgPath(g, LUCIDE_BLUETOOTH, sq, pen);
    }

    public static void DrawGearSF(Graphics g, RectangleF b, Color c, float stroke)
    {
        var sq = CenteredSquare(b, 0.10f);
        using var pen = new Pen(c, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        DrawSvgPath(g, HERO_COG_OUTER, sq, pen);
        DrawSvgPath(g, HERO_COG_INNER, sq, pen);
    }

    public static void DrawSlidersSF(Graphics g, RectangleF b, Color c, float stroke)
    {
        var sq = CenteredSquare(b, 0.08f);
        using var pen = new Pen(c, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        DrawSvgPath(g, HERO_SLIDERS, sq, pen);
    }

    public static void DrawPowerSF(Graphics g, RectangleF b, Color c, float stroke)
    {
        var sq = CenteredSquare(b, 0.10f);
        using var pen = new Pen(c, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        DrawSvgPath(g, HERO_POWER, sq, pen);
    }

    public static void DrawCheckSF(Graphics g, RectangleF b, Color c, float stroke)
    {
        var sq = CenteredSquare(b, 0.06f);
        using var pen = new Pen(c, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        DrawSvgPath(g, HERO_CHECK, sq, pen);
        // outer circle for check SF
        float s = Math.Min(sq.Width, sq.Height);
        var circle = new RectangleF(sq.X + 1, sq.Y + 1, s - 2, s - 2);
        g.DrawEllipse(pen, circle);
    }

    public static void DrawCheckmarkSimple(Graphics g, RectangleF b, Color c, float stroke)
    {
        var sq = CenteredSquare(b, 0.04f);
        using var pen = new Pen(c, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        DrawSvgPath(g, HERO_CHECK, sq, pen);
    }

    public static Icon CreateAppIcon(Color accent, int size = 32)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var r = new Rectangle(1, 1, size - 2, size - 2);
            int rad = (int)(size * 0.275);
            Theme.FillSquircle(g, r, rad, accent);
            using var hi = new SolidBrush(Color.FromArgb(22, 255,255,255));
            var hiRect = new Rectangle(r.X+1, r.Y+1, r.Width-2, r.Height/2);
            using var hiPath = Theme.Squircle(hiRect, rad-1);
            var clip = g.Save();
            g.SetClip(hiPath);
            g.FillRectangle(hi, hiRect.X, hiRect.Y, hiRect.Width, hiRect.Height/2);
            g.Restore(clip);
            hiPath.Dispose();
            var boltBox = new RectangleF(size * 0.27f, size * 0.16f, size * 0.46f, size * 0.68f);
            using var white = new Pen(Color.White, Math.Max(1.8f, size / 15f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            DrawSvgPath(g, HERO_BOLT, boltBox, white);
        }
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            // Clone so we own a copy and can destroy the original handle
            return (Icon)tmp.Clone();
        }
        catch { return SystemIcons.Application; }
        finally { try { PowerSave.Infra.Native.DestroyIcon(hIcon); } catch { } }
    }
}
