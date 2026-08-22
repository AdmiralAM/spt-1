from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "client" / "assets" / "source" / "approved-icons.png"
OUT = ROOT / "client" / "assets" / "hud-sprites.png"
CELL = 64
COLS, ROWS = 8, 6

CELLS = {
    "usec": (0, 0), "bear": (1, 0), "scav": (2, 0), "boss": (3, 0),
    "raider": (4, 0), "water": (5, 0), "energy": (6, 0), "weight": (7, 0),
    "weight1": (0, 1), "weight2": (1, 1), "weight3": (2, 1), "head": (3, 1),
    "torso": (4, 1), "stomach": (5, 1), "left_arm": (6, 1), "right_arm": (7, 1),
    "left_leg": (0, 2), "right_leg": (1, 2), "self": (2, 2), "weapon_unknown": (3, 2),
    "weapon_assault": (4, 2), "weapon_carbine": (5, 2), "weapon_smg": (6, 2), "weapon_lmg": (7, 2),
    "weapon_sniper": (0, 3), "weapon_dmr": (1, 3), "weapon_shotgun_pump": (2, 3), "weapon_shotgun_semi": (3, 3),
    "weapon_shotgun_sawedoff": (4, 3), "weapon_pistol": (5, 3), "weapon_revolver": (6, 3), "weapon_launcher": (7, 3),
    "weapon_frag": (0, 4), "weapon_impact": (1, 4), "weapon_incendiary": (2, 4), "weapon_melee": (3, 4),
    "weapon_throwing": (4, 4), "weapon_bolt": (5, 4), "weapon_pcc": (6, 4), "weapon_special": (7, 4),
    "weapon_crossbow": (0, 5), "weapon_tool": (1, 5), "weapon_explosive": (2, 5),
}

ROLE = {"usec", "bear", "scav", "boss", "raider", "self"}
STATUS = {"water", "energy", "weight", "weight1", "weight2", "weight3"}
BODY = {"head", "torso", "stomach", "left_arm", "right_arm", "left_leg", "right_leg"}

TARGETS = {
    "role": (4, 3, 60, 61),
    "status": (7, 5, 57, 59),
    "body": (5, 4, 59, 60),
    "weapon": (2, 10, 62, 54),
}


def fallback_chevrons(count: int) -> Image.Image:
    icon = Image.new("RGBA", (CELL * 4, CELL * 4), (0, 0, 0, 0))
    draw = ImageDraw.Draw(icon)
    start = 18 - (count - 1) * 4
    for index in range(count):
        y = start + index * 9
        draw.line(
            [(16 * 4, y * 4), (32 * 4, (y - 8) * 4), (48 * 4, y * 4)],
            fill=(236, 238, 232, 255),
            width=4 * 4,
            joint="curve",
        )
    return icon.resize((CELL, CELL), Image.Resampling.LANCZOS)


def group(key: str) -> str:
    if key in ROLE:
        return "role"
    if key in STATUS:
        return "status"
    if key in BODY:
        return "body"
    return "weapon"


def alpha_bbox(icon: Image.Image):
    return icon.getchannel("A").point(lambda value: 255 if value >= 12 else 0).getbbox()


def normalize_ink(icon: Image.Image) -> Image.Image:
    """Normalize authored artwork to one coherent, tintable HUD material."""
    alpha = icon.getchannel("A")
    ink = ImageOps.grayscale(icon)
    ink = ImageOps.autocontrast(ink, cutoff=1)
    ink = ImageEnhance.Contrast(ink).enhance(1.35)
    ink = ink.filter(ImageFilter.UnsharpMask(radius=1.1, percent=135, threshold=3))
    result = Image.merge("RGBA", (ink, ink, ink, alpha))
    return result


def fit(icon: Image.Image, target_box: tuple[int, int, int, int]) -> Image.Image:
    icon = icon.convert("RGBA")
    bbox = alpha_bbox(icon)
    if not bbox:
        raise ValueError("empty icon")
    source = icon.crop(bbox)
    x0, y0, x1, y1 = target_box
    max_width, max_height = x1 - x0, y1 - y0
    factor = min(max_width / source.width, max_height / source.height)
    size = (max(1, round(source.width * factor)), max(1, round(source.height * factor)))
    source = source.resize(size, Image.Resampling.LANCZOS)
    source = normalize_ink(source)
    result = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    result.alpha_composite(source, ((CELL - source.width) // 2, (CELL - source.height) // 2))
    return result


def scav_micro_glyph() -> Image.Image:
    """A HUD-scale reduction of the approved balaclava-and-cigarette Scav concept."""
    scale = 4
    icon = Image.new("RGBA", (CELL * scale, CELL * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(icon)

    # Broad shoulders and a large hood make a human silhouette readable before facial detail.
    draw.rounded_rectangle((35, 174, 211, 252), radius=36, fill=(108, 108, 108, 255),
                           outline=(238, 238, 238, 255), width=12)
    draw.ellipse((52, 12, 196, 218), fill=(112, 112, 112, 255),
                 outline=(242, 242, 242, 255), width=13)

    # Oversized balaclava openings survive the final 12-20 px downscale.
    draw.rounded_rectangle((69, 70, 182, 111), radius=18, fill=(8, 8, 8, 255),
                           outline=(220, 220, 220, 255), width=8)
    draw.ellipse((87, 84, 103, 97), fill=(248, 248, 248, 255))
    draw.ellipse((147, 84, 163, 97), fill=(248, 248, 248, 255))
    draw.rounded_rectangle((105, 146, 170, 183), radius=17, fill=(12, 12, 12, 255),
                           outline=(202, 202, 202, 255), width=7)

    # The cigarette deliberately projects beyond the face and uses a heavy outline.
    draw.line((158, 161, 238, 179), fill=(8, 8, 8, 255), width=24)
    draw.line((158, 161, 238, 179), fill=(244, 244, 244, 255), width=12)
    draw.ellipse((226, 168, 246, 190), fill=(184, 184, 184, 255), outline=(8, 8, 8, 255), width=5)

    return icon.resize((CELL, CELL), Image.Resampling.LANCZOS)


def micro_focus(icon: Image.Image, key: str) -> Image.Image:
    if key == "scav":
        return scav_micro_glyph()
    return icon


def load(key: str, approved: Image.Image) -> Image.Image:
    column, row = CELLS[key]
    source_cell = 256
    icon = approved.crop((column * source_cell, row * source_cell, (column + 1) * source_cell, (row + 1) * source_cell))
    if alpha_bbox(icon):
        return icon
    if key.startswith("weight") and key[-1:].isdigit():
        return fallback_chevrons(int(key[-1]))
    raise FileNotFoundError(f"Missing approved HUD source asset: {key}")


def main() -> None:
    approved = Image.open(SOURCE).convert("RGBA")
    if approved.size != (COLS * 256, ROWS * 256):
        raise ValueError(f"approved source atlas is {approved.size}, expected {(COLS * 256, ROWS * 256)}")
    atlas = Image.new("RGBA", (COLS * CELL, ROWS * CELL), (0, 0, 0, 0))
    for key, (column, row) in CELLS.items():
        icon = fit(micro_focus(load(key, approved), key), TARGETS[group(key)])
        atlas.alpha_composite(icon, (column * CELL, row * CELL))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(OUT, optimize=True)
    print(f"Generated {OUT} {atlas.size} from {len(CELLS)} mapped monochrome assets")


if __name__ == "__main__":
    main()
