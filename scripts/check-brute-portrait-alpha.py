"""Check the alpha of known muscle shadows and openings in the shipped Brute WebPs."""

from pathlib import Path

from PIL import Image

PORTRAITS = Path(__file__).resolve().parents[1] / "table/battle-art/evolutions/hybrids"

# Coordinates on the approved masters. These dark body samples disappeared in the first export.
# The optional openings show the battlefield through a gap enclosed by legs or horns.
SAMPLES = [
    ("oppressor-warped", (444, 566), [(1134, 62), (1356, 80)]),
    ("oppressor-predator", (572, 545), []),
    ("oppressor-sibling", (412, 579), []),
    ("oppressor-ultimate", (934, 532), []),
    ("colossus-warped", (1099, 636), [(763, 757)]),
    ("colossus-predator", (403, 618), []),
    ("colossus-sibling", (511, 447), [(765, 762)]),
    ("colossus-ultimate", (415, 560), [(823, 797), (470, 757)]),
    ("ravager-warped", (809, 629), []),
    ("ravager-predator", (1259, 472), [(1319, 664)]),
    ("ravager-sibling", (1046, 568), []),
    ("ravager-ultimate", (1098, 506), []),
]


def check() -> None:
    for name, shadow, openings in SAMPLES:
        with Image.open(PORTRAITS / f"brute-{name}.webp") as image:
            alpha = image.getchannel("A")
            samples = [(shadow, 255), ((1, 1), 0)]
            samples.extend((point, 0) for point in openings)
            for point, expected in samples:
                x, y = point
                values = alpha.crop((x - 1, y - 1, x + 2, y + 2)).getextrema()
                if values != (expected, expected):
                    raise ValueError(f"{name} at {point}: expected alpha {expected}, found {values}")


if __name__ == "__main__":
    check()
