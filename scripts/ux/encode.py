"""Encode a page's pictures for scripts/ux/pictures.mjs (ADR 0198).

Reads a list of jobs as JSON on standard input. A job names its sources, the width one of them is drawn at,
where to write, and the WebP settings. One source gives a picture at that width; several give a strip, the
frames side by side at that width each, in one file. Nothing is cropped, matted or retouched: a painting is
only made smaller, with a Lanczos filter.

Usage (Pillow required): python3 scripts/ux/encode.py < jobs.json
"""

import json
import sys

from PIL import Image


def frame(path: str, width: int) -> Image.Image:
    """One painting at the width it is sent, keeping its proportions and its transparency."""
    with Image.open(path) as source:
        picture = source.convert("RGBA" if "A" in source.getbands() else "RGB")
    return picture.resize((width, round(picture.height * width / picture.width)), Image.Resampling.LANCZOS)


def encode(job: dict) -> None:
    frames = [frame(path, job["width"]) for path in job["sources"]]
    first = frames[0]
    if any(one.size != first.size or one.mode != first.mode for one in frames):
        raise ValueError(f"the frames of {job['out']} differ in size or in transparency")
    sheet = first if len(frames) == 1 else Image.new(first.mode, (first.width * len(frames), first.height))
    if len(frames) > 1:
        for index, one in enumerate(frames):
            sheet.paste(one, (index * first.width, 0))
    settings = {"quality": job["quality"], "method": job["method"], "alpha_quality": job["alphaQuality"]}
    sheet.save(job["out"], "WEBP", **settings)


if __name__ == "__main__":
    for one in json.load(sys.stdin):
        encode(one)
