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


def crop(sheet, key):
    x, y = CELLS[key]
    return sheet.crop((x*CELL, y*CELL, (x+1)*CELL, (y+1)*CELL))


def bbox(icon):
    return icon.getchannel('A').point(lambda v: 255 if v >= 12 else 0).getbbox()


def visible(icon, size, threshold=48):
    a = icon.getchannel('A').resize((size,size), Image.Resampling.LANCZOS)
    return sum(1 for v in a.getdata() if v >= threshold)


def main():
    sheet = Image.open(SHEET).convert('RGBA')
    errors = []
    print('HUD optical QA:')
    for key in CELLS:
        icon = crop(sheet, key)
        b = bbox(icon)
        if not b:
            errors.append(f'{key}: empty sprite')
            continue
        x0,y0,x1,y1 = b
        w,h = x1-x0,y1-y0
        cx,cy = (x0+x1)/2.0,(y0+y1)/2.0
        v12,v16 = visible(icon,12),visible(icon,16)
        print(f'  {key:9s} bbox={w:02d}x{h:02d} center=({cx:4.1f},{cy:4.1f}) micro={v12:3d}/{v16:3d}')
        if abs(cx-32) > 4.0 or abs(cy-32) > 4.5:
            errors.append(f'{key}: optical bbox center drift ({cx:.1f},{cy:.1f})')
        if v12 < 5 or v16 < 10:
            errors.append(f'{key}: insufficient micro-scale coverage ({v12}/{v16})')
        if key in ROLES and (w < 38 or h < 40):
            errors.append(f'{key}: faction mark too small ({w}x{h})')
        if key in WEAPONS and w < 38:
            errors.append(f'{key}: weapon silhouette too narrow ({w}px)')
        if key in BODY and max(w,h) < 34:
            errors.append(f'{key}: body glyph too small ({w}x{h})')
        if key in STATUS and max(w,h) < 30:
            errors.append(f'{key}: status glyph too small ({w}x{h})')
    if errors:
        print('\nFAILED:')
        for e in errors: print(' -', e)
        raise SystemExit(1)
    print('\nPASS: optical centering, category scale and 12/16px coverage')


if __name__ == '__main__':
    main()
