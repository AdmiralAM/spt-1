from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SHEET = ROOT / 'SPT-PopCounter' / 'assets' / 'hud-sprites.png'
CELL = 64

CELLS = {
    'usec':(0,0),'bear':(1,0),'scav':(2,0),'boss':(3,0),'raider':(4,0),'water':(5,0),'energy':(6,0),'weight':(7,0),
    'weight1':(0,1),'weight2':(1,1),'weight3':(2,1),'head':(3,1),'torso':(4,1),'stomach':(5,1),'left_arm':(6,1),'right_arm':(7,1),
    'left_leg':(0,2),'right_leg':(1,2),'self':(2,2),'weapon_unknown':(3,2),'weapon_assault':(4,2),'weapon_carbine':(5,2),'weapon_smg':(6,2),'weapon_lmg':(7,2),
    'weapon_sniper':(0,3),'weapon_dmr':(1,3),'weapon_shotgun_pump':(2,3),'weapon_shotgun_semi':(3,3),'weapon_shotgun_sawedoff':(4,3),'weapon_pistol':(5,3),'weapon_revolver':(6,3),'weapon_launcher':(7,3),
    'weapon_frag':(0,4),'weapon_impact':(1,4),'weapon_incendiary':(2,4),'weapon_melee':(3,4),'weapon_throwing':(4,4),'weapon_bolt':(5,4),'weapon_pcc':(6,4),'weapon_special':(7,4),
    'weapon_crossbow':(0,5),'weapon_tool':(1,5),'weapon_explosive':(2,5)
}

ROLES = {'usec','bear','scav','boss','raider','self'}
STATUS = {'water','energy','weight','weight1','weight2','weight3'}
BODY = {'head','torso','stomach','left_arm','right_arm','left_leg','right_leg'}
WEAPONS = {key for key in CELLS if key.startswith('weapon_')}

# Target optical boxes. These are deliberately conservative: the pass only removes
# accidental scale/centering drift between independently-authored glyphs.
BOXES = {
    'role': (4, 3, 60, 61),
    'status': (7, 5, 57, 59),
    'body': (5, 4, 59, 60),
    'weapon': (2, 10, 62, 54),
}

# Fine optical corrections for asymmetric silhouettes whose visible mass does not
# line up with the alpha bounds after downsampling.
NUDGES = {
    'weapon_launcher': (0, 5),
}


def group(key):
    if key in ROLES: return 'role'
    if key in STATUS: return 'status'
    if key in BODY: return 'body'
    return 'weapon'


def alpha_bbox(icon):
    a = icon.getchannel('A')
    # Ignore the faintest Lanczos fringe when measuring authored geometry.
    mask = a.point(lambda v: 255 if v >= 12 else 0)
    return mask.getbbox()


def normalize(icon, target_box):
    bbox = alpha_bbox(icon)
    if not bbox:
        return icon
    src = icon.crop(bbox)
    tx0, ty0, tx1, ty1 = target_box
    max_w, max_h = tx1 - tx0, ty1 - ty0
    scale = min(max_w / src.width, max_h / src.height)
    # Do not magnify a glyph by more than 18%; this protects deliberate detail scale.
    scale = min(scale, 1.18)
    nw = max(1, round(src.width * scale))
    nh = max(1, round(src.height * scale))
    src = src.resize((nw, nh), Image.Resampling.LANCZOS)
    x = round((CELL - nw) / 2)
    y = round((CELL - nh) / 2)
    out = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    out.alpha_composite(src, (x, y))
    return out


def main():
    sheet = Image.open(SHEET).convert('RGBA')
    out = sheet.copy()
    for key, (cx, cy) in CELLS.items():
        box = (cx*CELL, cy*CELL, (cx+1)*CELL, (cy+1)*CELL)
        icon = sheet.crop(box)
        fixed = normalize(icon, BOXES[group(key)])
        dx, dy = NUDGES.get(key, (0, 0))
        if dx or dy:
            shifted = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
            shifted.alpha_composite(fixed, (dx, dy))
            fixed = shifted
        out.paste(fixed, (cx*CELL, cy*CELL))
    out.save(SHEET, optimize=True)
    print('Normalized HUD optical scale/centering for', len(CELLS), 'sprites')


if __name__ == '__main__':
    main()
