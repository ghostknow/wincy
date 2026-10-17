"""Generate the Wincy for Windows icon.

Renders a rounded-square gradient tile with a white clipboard glyph at 4x
supersampling, then downsamples to the sizes Windows asks for and packs a
multi-resolution .ico (plus a PNG preview used by the README).

Run:  python assets/make_icon.py
"""

from __future__ import annotations

import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
S = 4096  # supersampled canvas
K = S / 1024.0  # design coordinates are 1024x1024

TOP = (108, 138, 255)  # #6C8AFF
BOTTOM = (58, 86, 232)  # #3A56E8


def sc(*values: float) -> tuple:
    return tuple(v * K for v in values)


def vertical_gradient(size: int, top: tuple, bottom: tuple) -> Image.Image:
    grad = Image.new("RGB", (1, size))
    px = grad.load()
    for y in range(size):
        t = y / float(size - 1)
        px[0, y] = (
            round(top[0] + (bottom[0] - top[0]) * t),
            round(top[1] + (bottom[1] - top[1]) * t),
            round(top[2] + (bottom[2] - top[2]) * t),
        )
    return grad.resize((size, size), Image.NEAREST)


def build(s: int) -> Image.Image:
    bg = vertical_gradient(s, TOP, BOTTOM).convert("RGBA")

    # Rounded tile mask.
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, s - 1, s - 1], radius=int(228 * s / 1024.0), fill=255)
    tile = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    tile.paste(bg, (0, 0), mask)

    d = ImageDraw.Draw(tile)
    k = s / 1024.0

    def rr(box, radius, fill):
        d.rounded_rectangle([v * k for v in box], radius=int(radius * k), fill=fill)

    # Soft inner highlight along the top edge for a little depth.
    d.rounded_rectangle(
        [0, 0, s - 1, s - 1],
        radius=int(228 * k),
        outline=(255, 255, 255, 46),
        width=max(1, int(6 * k)),
    )

    # Clipboard body + clip tab.
    rr((268, 292, 756, 852), 76, (255, 255, 255, 255))
    rr((416, 226, 608, 330), 46, (255, 255, 255, 255))
    rr((474, 250, 550, 300), 22, (58, 86, 232, 255))

    # Three "text" lines inside the board, tinted by the brand gradient.
    for y, w in ((424, 336), (520, 264), (616, 300)):
        rr((344, y, 344 + w, y + 46), 23, (99, 124, 246, 255))
    return tile


def main() -> None:
    big = build(S)
    icon_sizes = [256, 128, 96, 64, 48, 40, 32, 24, 20, 16]
    frames = []
    for size in icon_sizes:
        frames.append((size, big.resize((size, size), Image.LANCZOS)))

    ico_path = os.path.join(HERE, "wincy.ico")
    frames[0][1].save(
        ico_path,
        format="ICO",
        sizes=[(size, size) for size, _ in frames],
        append_images=[img for size, img in frames[1:]],
    )

    png_path = os.path.join(HERE, "icon-512.png")
    big.resize((512, 512), Image.LANCZOS).save(png_path, format="PNG")

    # Contact sheet so the small sizes can be eyeballed.
    sheet = Image.new("RGBA", (760, 300), (245, 245, 248, 255))
    x = 20
    for size in (16, 24, 32, 48, 64, 128, 256):
        img = big.resize((size, size), Image.LANCZOS)
        sheet.alpha_composite(img, (x, 20 + (256 - size) // 2))
        x += size + 24
    sheet.save(os.path.join(HERE, "icon-sizes.png"), format="PNG")

    print("wrote", ico_path, os.path.getsize(ico_path), "bytes")
    print("wrote", png_path)


if __name__ == "__main__":
    main()
