from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SHEET = ROOT / 'SPT-PopCounter' / 'assets' / 'hud-sprites.png'
CELL = 64

CELLS = {
    'usec':(0,0),'bear':(1,0),'scav':(2,0),'boss':(3,0),'raider':(4,0),'water':(5,0),
    'energy':(0,1),'weight':(1,1),'weight1':(2,1),'weight2':(3,1),'weight3':(4,1),'head':(5,1),
    'torso':(0,2),'arm':(1,2),'leg':(2,2),'stomach':(3,2),'ak':(4,2),'ar':(5,2),
    'smg':(0,3),'shotgun':(1,3),'sniper':(2,3),'pistol':(3,3),'weapon':(4,3)
}

ROLES = {'usec','bear','scav','boss','raider'}
STATUS = {'water','energy','weight','weight1','weight2','weight3'}
BODY = {'head','torso','arm','leg','stomach'}
WEAPONS = {'ak','ar','smg','shotgun','sniper','pistol','weapon'}

# Target optical boxes. These are deliberately conservative: the pass only removes
# accidental scale/centering drift between independently-authored glyphs.
BOXES = {
    'role': (5, 4, 59, 60),
    'status': (8, 7, 56, 57),
    'body': (9, 6, 55, 59),
    'weapon': (3, 13, 61, 51),
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
        out.paste(fixed, (cx*CELL, cy*CELL))
    out.save(SHEET, optimize=True)
    print('Normalized HUD optical scale/centering for', len(CELLS), 'sprites')


if __name__ == '__main__':
    main()
