#!/usr/bin/env python3
"""Generate Assets/app.ico for PowerSave Studio.

Renders the app icon (green squircle + white lightning bolt, matching the
in-app tray icon drawn by UI/Icons.cs CreateAppIcon) entirely in pure Python
(no third-party deps) and writes a multi-size Windows .ico file.

Run:  python3 tools/make_icon.py
"""

import math
import struct
import zlib
from pathlib import Path

# Heroicons "bolt" path (MIT) in a 24x24 viewBox:
#   M3.75 13.5 L14.25 2.25 L12 10.5 H20.25 L9.75 21.75 L12 13.5 H3.75 Z
BOLT = [(3.75, 13.5), (14.25, 2.25), (12, 10.5), (20.25, 10.5),
        (9.75, 21.75), (12, 13.5), (3.75, 13.5)]

SIZES = [16, 20, 24, 32, 48, 64, 128, 256]
SS = 4  # supersampling factor
SQUIRCLE_N = 5.0  # superellipse exponent

GREEN_TOP = (78, 230, 152)
GREEN_BOT = (38, 182, 102)
BOLT_WHITE = (255, 255, 255)


def lerp(a, b, t):
    return a + (b - a) * t


def bolt_polygon(size):
    """Scale the 24-unit bolt into the icon box used by CreateAppIcon."""
    x0, y0 = 0.27 * size, 0.16 * size
    sx, sy = 0.46 * size / 24.0, 0.68 * size / 24.0
    return [(x0 + px * sx, y0 + py * sy) for (px, py) in BOLT]


def spans_at_row(poly, y):
    """Even-odd scanline spans of `poly` at ordinate y."""
    xs = []
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if y1 == y2:
            continue
        if min(y1, y2) <= y < max(y1, y2):
            xs.append(x1 + (y - y1) * (x2 - x1) / (y2 - y1))
    xs.sort()
    return [(xs[i], xs[i + 1]) for i in range(0, len(xs) - 1, 2)]


def render(size):
    """Render the icon at `size` px (RGBA rows) with SSx supersampling."""
    poly = bolt_polygon(size)
    N = size * SS
    margin = size * 0.02
    half = (size - 2.0 * margin) / 2.0
    c = size / 2.0

    # straight-alpha accumulation buffer
    acc = [[0, 0, 0, 0] for _ in range(size * size)]

    for sub_row in range(N):
        y = (sub_row + 0.5) / SS
        out_row = int(y)
        if not (0 <= out_row < size):
            continue

        # squircle horizontal span at this row
        dy = abs(y - c) / half
        sq = None
        if dy < 1.0:
            hw = half * (1.0 - dy ** SQUIRCLE_N) ** (1.0 / SQUIRCLE_N)
            sq = (c - hw, c + hw)

        bolt = spans_at_row(poly, y)
        if sq is None and not bolt:
            continue

        x_lo = int(max(0.0, math.floor(min([sq[0]] + [s[0] for s in bolt]) if sq else min(s[0] for s in bolt))))
        x_hi = int(math.ceil(max([sq[1]] + [s[1] for s in bolt]) if sq else max(s[1] for s in bolt)))
        # span bounds are in pixel units — convert to subpixel columns
        x_lo = max(0, x_lo * SS)
        x_hi = min(N, int(math.ceil(x_hi * SS)))

        t = y / size
        bg_r = int(lerp(GREEN_TOP[0], GREEN_BOT[0], t))
        bg_g = int(lerp(GREEN_TOP[1], GREEN_BOT[1], t))
        bg_b = int(lerp(GREEN_TOP[2], GREEN_BOT[2], t))
        # subtle top highlight
        if t < 0.45:
            lift = int(24 * ((0.45 - t) / 0.45))
            bg_r, bg_g, bg_b = min(255, bg_r + lift), min(255, bg_g + lift), min(255, bg_b + lift)

        for sub_col in range(x_lo, x_hi):
            x = (sub_col + 0.5) / SS
            out_col = int(x)
            if not (0 <= out_col < size):
                continue
            inside_sq = sq is not None and sq[0] <= x <= sq[1]
            inside_bolt = any(a <= x <= b for (a, b) in bolt)
            if inside_bolt:
                r, g, b, a = BOLT_WHITE[0], BOLT_WHITE[1], BOLT_WHITE[2], 255
            elif inside_sq:
                r, g, b, a = bg_r, bg_g, bg_b, 255
            else:
                continue
            cell = acc[out_row * size + out_col]
            cell[0] += r; cell[1] += g; cell[2] += b; cell[3] += a

    samples = SS * SS
    rows = []
    for yy in range(size):
        row = bytearray()
        for xx in range(size):
            r, g, b, a = acc[yy * size + xx]
            row += bytes((r // samples if a else 0,
                          g // samples if a else 0,
                          b // samples if a else 0,
                          a // samples))
        rows.append(bytes(row))
    return rows


def png_encode(rows, w, h):
    """Encode RGBA rows as a PNG (filter 0 per scanline)."""
    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    raw = b"".join(b"\x00" + r for r in rows)
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def write_ico(path, images):
    """images: list of (size, png_bytes) — PNG-compressed entries (Vista+)."""
    out = bytearray()
    out += struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    for size, png in images:
        dim = 0 if size >= 256 else size
        out += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(png), offset)
        offset += len(png)
    for _, png in images:
        out += png
    Path(path).write_bytes(bytes(out))


def main():
    root = Path(__file__).resolve().parent.parent
    assets = root / "Assets"
    assets.mkdir(exist_ok=True)
    images = []
    for s in SIZES:
        rows = render(s)
        images.append((s, png_encode(rows, s, s)))
        print(f"rendered {s}x{s}")
    write_ico(assets / "app.ico", images)
    print(f"wrote {assets / 'app.ico'} "
          f"({(assets / 'app.ico').stat().st_size} bytes)")


if __name__ == "__main__":
    main()
