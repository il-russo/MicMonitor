"""Generates MicMonitor.ico (32-bit BGRA, multiple sizes) without external deps.

The mark mirrors the one in the application header: a dark rounded tile with a
warm border and five orange waveform bars.
"""

import math
import struct

SIZES = [16, 24, 32, 48, 64, 128, 256]
SUPERSAMPLE = 4

# Tile geometry, in unit-square coordinates.
TILE = (0.055, 0.055, 0.945, 0.945)
TILE_RADIUS = 0.215

# Five bars: (centre x, half height).
BAR_WIDTH = 0.078
BARS = [
    (0.240, 0.150),
    (0.370, 0.280),
    (0.500, 0.430),
    (0.630, 0.310),
    (0.760, 0.170),
]


def rounded_rect(x, y, x0, y0, x1, y1, radius):
    if not (x0 <= x <= x1 and y0 <= y <= y1):
        return False
    cx = min(max(x, x0 + radius), x1 - radius)
    cy = min(max(y, y0 + radius), y1 - radius)
    return math.hypot(x - cx, y - cy) <= radius


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def shade(x, y):
    """Returns an (r, g, b) tuple for a point in the unit square, or None."""
    x0, y0, x1, y1 = TILE

    # Bars, brightest at the top.
    for cx, half in BARS:
        if rounded_rect(x, y, cx - BAR_WIDTH / 2, 0.5 - half,
                        cx + BAR_WIDTH / 2, 0.5 + half, BAR_WIDTH / 2):
            t = (y - (0.5 - half)) / (2 * half)
            return mix((0xFF, 0x9E, 0x4D), (0xF9, 0x53, 0x00), t)

    inside = rounded_rect(x, y, x0, y0, x1, y1, TILE_RADIUS)
    if not inside:
        return None

    # Warm rim just inside the tile edge.
    border = 0.030
    if not rounded_rect(x, y, x0 + border, y0 + border,
                        x1 - border, y1 - border, TILE_RADIUS - border):
        return (0xB4, 0x48, 0x05)

    # Tile body: subtle top-lit gradient with an ember glow in the corner.
    t = (y - y0) / (y1 - y0)
    body = mix((0x24, 0x28, 0x33), (0x0E, 0x11, 0x16), t)
    glow = max(0.0, 1.0 - math.hypot(x - 0.28, y - 0.24) * 1.9)
    return mix(body, (0x53, 0x2A, 0x10), glow * 0.55)


def render(size):
    """Returns a bottom-up BGRA byte string for one icon size."""
    rows = []
    step = 1.0 / (size * SUPERSAMPLE)
    for py in range(size):
        row = bytearray()
        for px in range(size):
            hits = 0
            acc = [0, 0, 0]
            for sy in range(SUPERSAMPLE):
                y = (py * SUPERSAMPLE + sy + 0.5) * step
                for sx in range(SUPERSAMPLE):
                    x = (px * SUPERSAMPLE + sx + 0.5) * step
                    color = shade(x, y)
                    if color is not None:
                        hits += 1
                        acc[0] += color[0]
                        acc[1] += color[1]
                        acc[2] += color[2]
            if hits == 0:
                row += b"\x00\x00\x00\x00"
            else:
                alpha = int(round(255.0 * hits / (SUPERSAMPLE * SUPERSAMPLE)))
                r, g, b = (v // hits for v in acc)
                row += bytes((b, g, r, alpha))
        rows.append(bytes(row))
    return b"".join(reversed(rows))


def dib(size, pixels):
    header = struct.pack(
        "<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0, len(pixels), 2835, 2835, 0, 0
    )
    mask_stride = ((size + 31) // 32) * 4
    return header + pixels + b"\x00" * (mask_stride * size)


def main():
    images = [dib(size, render(size)) for size in SIZES]

    out = bytearray(struct.pack("<HHH", 0, 1, len(SIZES)))
    offset = 6 + 16 * len(SIZES)
    for size, image in zip(SIZES, images):
        out += struct.pack(
            "<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(image), offset
        )
        offset += len(image)
    for image in images:
        out += image

    with open("MicMonitor.ico", "wb") as handle:
        handle.write(bytes(out))
    print("wrote MicMonitor.ico", len(out), "bytes")


if __name__ == "__main__":
    main()
