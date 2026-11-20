"""Give an opaque-background illustration the alpha channel the battle view draws it with.

An owner-supplied portrait sometimes comes on an opaque black canvas instead of a transparent one
(docs/art/warped-hybrid-aaa-intake.md). Drawn as is, the battlefield shows a black rectangle around the
creature. This script keeps every pixel of the artwork and only adds alpha:

- the near-black pixels connected to the canvas edge are the background and become transparent;
- wisps and limbs can enclose pockets of that background: a pocket of pure black wide enough to survive a
  9-pixel erosion is background too, while a thin line or a speck of shadow inside the creature stays;
- the edge is softened by a small blur so the silhouette is not stair-stepped.

The result is encoded as the intake's WebP (quality 88, method 6, alpha quality 100) at the source's canvas
size, and its silhouette (alpha >= 128) is printed for the registry.

Usage (Pillow required): python3 scripts/matte-opaque-art.py <source> <destination.webp> [threshold]
"""

import argparse
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

BACKGROUND = 128
POCKET = 14
POCKET_WIDTH = 9


def pockets(luminance: Image.Image) -> list[tuple[int, int]]:
    """A seed in every pure-black pocket wide enough to be background rather than a line of the drawing."""
    dark = luminance.point(lambda value: 255 if value < POCKET else 0)
    core = dark.filter(ImageFilter.MinFilter(POCKET_WIDTH))
    seeds = []
    while (box := core.getbbox()) is not None:
        left, top, right, bottom = box
        seed = next(
            (x, y) for y in range(top, bottom) for x in range(left, right) if core.getpixel((x, y)) == 255
        )
        seeds.append(seed)
        ImageDraw.floodfill(core, seed, 0)
    return seeds


def matte(source: Image.Image, threshold: int = 22) -> Image.Image:
    rgb = source.convert("RGB")
    luminance = rgb.convert("L")
    dark = luminance.point(lambda value: 255 if value < threshold else 0)
    # A one-pixel dark frame joins every dark run touching an edge: one fill from a corner reaches them all.
    framed = ImageOps.expand(dark, border=1, fill=255)
    ImageDraw.floodfill(framed, (0, 0), BACKGROUND)
    for x, y in pockets(luminance):
        if framed.getpixel((x + 1, y + 1)) == 255:
            ImageDraw.floodfill(framed, (x + 1, y + 1), BACKGROUND)
    background = framed.crop((1, 1, framed.width - 1, framed.height - 1)).point(
        lambda value: 255 if value == BACKGROUND else 0
    )
    alpha = ImageChops.invert(background).filter(ImageFilter.GaussianBlur(1.2))
    result = rgb.copy()
    result.putalpha(alpha)
    return result


def outline_matte(source: Image.Image, openings: list[tuple[int, int]]) -> Image.Image:
    """Remove the black field and hand-identified openings of a dark-bodied creature."""
    rgb = source.convert("RGB")
    red, green, blue = rgb.split()
    brightest = ImageChops.lighter(red, ImageChops.lighter(green, blue))
    field = brightest.point(lambda value: 255 if value <= 6 else 0)
    # Shrink before filling so a narrow crease cannot connect the field to the muscles.
    frame = ImageOps.expand(field, border=4, fill=255)
    core = frame.filter(ImageFilter.MinFilter(7))
    ImageDraw.floodfill(core, (0, 0), BACKGROUND)
    for x, y in openings:
        seed = (x + 4, y + 4)
        if not (0 <= x < rgb.width and 0 <= y < rgb.height) or core.getpixel(seed) != 255:
            raise ValueError(f"opening {(x, y)} must lie inside an enclosed black field")
        ImageDraw.floodfill(core, seed, BACKGROUND)
    selected = core.point(lambda value: 255 if value == BACKGROUND else 0)
    field = selected.filter(ImageFilter.MaxFilter(7)).crop((4, 4, rgb.width + 4, rgb.height + 4))
    alpha = ImageOps.invert(field).filter(ImageFilter.GaussianBlur(0.65))
    rgb.putalpha(alpha)
    return rgb


def silhouette(image: Image.Image) -> tuple[int, int, int, int]:
    box = image.getchannel("A").point(lambda value: 255 if value >= 128 else 0).getbbox()
    if box is None:
        raise ValueError("the matte left no opaque pixel")
    return box


def main(arguments: list[str]) -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source")
    parser.add_argument("destination")
    parser.add_argument("threshold", type=int, nargs="?", default=22)
    parser.add_argument("--outline", action="store_true", help="preserve enclosed shadows on a black master")
    parser.add_argument("--opening", type=int, nargs=2, action="append", default=[], metavar=("X", "Y"))
    args = parser.parse_args(arguments)
    if args.opening and not args.outline:
        parser.error("--opening requires --outline")
    destination = args.destination
    with Image.open(args.source) as source:
        result = outline_matte(source, args.opening) if args.outline else matte(source, args.threshold)
    result.save(destination, "WEBP", quality=88, method=6, alpha_quality=100)
    left, top, right, bottom = silhouette(result)
    print(f"{destination} {result.width}x{result.height} bounds [{left}, {top}, {right}, {bottom}]")


if __name__ == "__main__":
    main(sys.argv[1:])
