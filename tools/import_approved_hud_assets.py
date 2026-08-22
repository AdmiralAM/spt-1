from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageEnhance, ImageFilter


SIZE = 256
COLS, ROWS = 8, 6

CELLS = {
    "usec": (0, 0), "bear": (1, 0), "boss": (3, 0), "raider": (4, 0),
    "water": (5, 0), "energy": (6, 0), "weight": (7, 0), "head": (3, 1),
    "torso": (4, 1), "stomach": (5, 1), "left_arm": (6, 1), "right_arm": (7, 1),
    "left_leg": (0, 2), "right_leg": (1, 2), "self": (2, 2), "weapon_unknown": (3, 2),
    "weapon_assault": (4, 2), "weapon_carbine": (5, 2), "weapon_smg": (6, 2), "weapon_lmg": (7, 2),
    "weapon_sniper": (0, 3), "weapon_dmr": (1, 3), "weapon_shotgun_pump": (2, 3), "weapon_shotgun_semi": (3, 3),
    "weapon_shotgun_sawedoff": (4, 3), "weapon_pistol": (5, 3), "weapon_revolver": (6, 3), "weapon_launcher": (7, 3),
    "weapon_frag": (0, 4), "weapon_impact": (1, 4), "weapon_incendiary": (2, 4), "weapon_melee": (3, 4),
    "weapon_throwing": (4, 4), "weapon_bolt": (5, 4), "weapon_pcc": (6, 4), "weapon_special": (7, 4),
    "weapon_crossbow": (0, 5), "weapon_tool": (1, 5), "weapon_explosive": (2, 5),
}

SINGLES = {
    "usec": "Metallic Eagle USEC Crest.png",
    "bear": "Металлический герб «BEAR» с медвежьей лапой.png",
    "boss": "Металлический череп в тактическом гербе.png",
    "raider": "Брутальная эмблема рейдера с черепом и винтовками.png",
    "water": "Глянцевый водный кристалл в металлической оправе.png",
    "energy": "Сияющий металлический符молот молнии.png",
    "weight": "Металлический медальон «KG».png",
    "head": "Треснувший череп в металлическом медальоне.png",
    "torso": "Каменный медальон с доспехами.png",
    "stomach": "Каменный медальон с рельефными доспехами.png",
    "self": "Каменный медальон тактического бойца.png",
}

PAIRS = {
    "Парные каменные эмблемы силы.png": ("left_arm", "right_arm"),
    "Каменные медальоны с броненогами.png": ("left_leg", "right_leg"),
}

WEAPON_KEYS = (
    "weapon_unknown", "weapon_assault", "weapon_carbine", "weapon_smg", "weapon_lmg", "weapon_sniper",
    "weapon_dmr", "weapon_shotgun_pump", "weapon_shotgun_semi", "weapon_shotgun_sawedoff", "weapon_pistol", "weapon_revolver",
    "weapon_launcher", "weapon_frag", "weapon_impact", "weapon_incendiary", "weapon_melee", "weapon_throwing",
    "weapon_bolt", "weapon_pcc", "weapon_special", "weapon_crossbow", "weapon_tool", "weapon_explosive",
)

# The approved weapon board uses four rows of six. These bands intentionally omit
# headings and footer notes; alpha extraction then isolates the actual silhouette.
WEAPON_ROWS = ((135, 275), (335, 470), (548, 700), (770, 915))


def black_to_alpha(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    existing = rgba.getchannel("A")
    extrema = existing.getextrema()
    if extrema[0] < 250:
        alpha = existing
    else:
        rgb = rgba.convert("RGB")
        r, g, b = rgb.split()
        brightest = ImageChops.lighter(ImageChops.lighter(r, g), b)
        # Backgrounds are authored as near-black textured fields. A soft ramp keeps
        # metallic edge antialiasing while removing the field and its compression noise.
        alpha = brightest.point(lambda v: 0 if v <= 10 else 255 if v >= 48 else round((v - 10) * 255 / 38))
        alpha = alpha.filter(ImageFilter.GaussianBlur(0.45))
    rgba.putalpha(ImageChops.multiply(existing, alpha))
    return rgba


def normalize(image: Image.Image, padding: int = 14) -> Image.Image:
    rgba = black_to_alpha(image)
    alpha = rgba.getchannel("A")
    bbox = alpha.point(lambda v: 255 if v >= 14 else 0).getbbox()
    if not bbox:
        raise ValueError("asset became empty after background extraction")
    rgba = rgba.crop(bbox)
    max_side = SIZE - 2 * padding
    scale = min(max_side / rgba.width, max_side / rgba.height)
    resized = rgba.resize((max(1, round(rgba.width * scale)), max(1, round(rgba.height * scale))), Image.Resampling.LANCZOS)
    resized = ImageEnhance.Contrast(resized).enhance(1.08)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(resized, ((SIZE - resized.width) // 2, (SIZE - resized.height) // 2))
    return canvas


def main() -> None:
    parser = argparse.ArgumentParser(description="Import the approved Pictures SPT icon set")
    parser.add_argument("incoming", type=Path, help="Folder containing the approved generated PNG files")
    parser.add_argument("output", type=Path, help="Destination for normalized transparent source icons")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)

    icons: dict[str, Image.Image] = {}
    manifest: dict[str, str] = {}
    for key, filename in SINGLES.items():
        path = args.incoming / filename
        icons[key] = normalize(Image.open(path), 10 if key in {"water", "energy"} else 14)
        manifest[key] = filename

    for filename, keys in PAIRS.items():
        source = Image.open(args.incoming / filename)
        midpoint = source.width // 2
        halves = (source.crop((0, 0, midpoint, source.height)), source.crop((midpoint, 0, source.width, source.height)))
        for key, half in zip(keys, halves):
            icons[key] = normalize(half, 18)
            manifest[key] = f"{filename}::{key}"

    board_name = "Категории оружия в тактическом HUD.png"
    board = Image.open(args.incoming / board_name)
    col_width = board.width / 6.0
    for index, key in enumerate(WEAPON_KEYS):
        row, col = divmod(index, 6)
        y0, y1 = WEAPON_ROWS[row]
        x0 = round(col * col_width + 12)
        x1 = round((col + 1) * col_width - 12)
        icons[key] = normalize(board.crop((x0, y0, x1, y1)), 10)
        manifest[key] = f"{board_name}::row{row + 1}/col{col + 1}"

    atlas = Image.new("RGBA", (COLS * SIZE, ROWS * SIZE), (0, 0, 0, 0))
    for key, icon in icons.items():
        column, row = CELLS[key]
        atlas.alpha_composite(icon, (column * SIZE, row * SIZE))
    atlas.save(args.output / "approved-icons.png", optimize=True)

    (args.output / "manifest.json").write_text(
        json.dumps({"source": "Pictures SPT approved set 2026-08-22", "icons": manifest}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"Packed {len(manifest)} approved icons into {args.output / 'approved-icons.png'}")


if __name__ == "__main__":
    main()
