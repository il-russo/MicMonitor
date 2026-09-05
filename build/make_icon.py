"""Generates MicMonitor.ico (32-bit BGRA, multiple sizes) without external deps."""

import math
import struct

MINT = (0x3D, 0xDC, 0x97)
MINT_LIGHT = (0x7C, 0xF0, 0xBE)
SIZES = [16, 24, 32, 48, 64, 128, 256]
SUPERSAMPLE = 4


def rounded_rect(x, y, x0, y0, x1, y1, radius):
    if not (x0 <= x <= x1 and y0 <= y <= y1):
        return False
    cx = min(max(x, x0 + radius), x1 - radius)
    cy = min(max(y, y0 + radius), y1 - radius)
    return math.hypot(x - cx, y - cy) <= radius


def shade(x, y):
    """Returns an (r, g, b) tuple for a point in the unit square, or None."""
    # Microphone capsule.
    if rounded_rect(x, y, 0.385, 0.130, 0.615, 0.560, 0.115):
        return MINT_LIGHT

    # Bracket arc under the capsule.
    dx, dy = x - 0.5, y - 0.44
    distance = math.hypot(dx, dy)
    if y >= 0.47 and 0.255 <= distance <= 0.315:
        return MINT

    # Stem.
    if 0.472 <= x <= 0.528 and 0.735 <= y <= 0.855:
        return MINT

    # Base.
    if rounded_rect(x, y, 0.335, 0.845, 0.665, 0.902, 0.028):
        return MINT

    return None


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
