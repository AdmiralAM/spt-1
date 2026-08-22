from pathlib import Path
from PIL import Image, ImageChops, ImageStat

ROOT = Path(__file__).resolve().parents[1]
SHEET = ROOT / 'SPT-PopCounter' / 'assets' / 'hud-sprites.png'
CELL = 64
EXPECTED = (6 * CELL, 4 * CELL)

CELLS = {
    'usec':(0,0),'bear':(1,0),'scav':(2,0),'boss':(3,0),'raider':(4,0),'water':(5,0),
    'energy':(0,1),'weight':(1,1),'weight1':(2,1),'weight2':(3,1),'weight3':(4,1),'head':(5,1),
    'torso':(0,2),'arm':(1,2),'leg':(2,2),'stomach':(3,2),'ak':(4,2),'ar':(5,2),
    'smg':(0,3),'shotgun':(1,3),'sniper':(2,3),'pistol':(3,3),'weapon':(4,3)
}


def crop(sheet, key):
    x, y = CELLS[key]
    return sheet.crop((x * CELL, y * CELL, (x + 1) * CELL, (y + 1) * CELL))


def alpha_ratio(icon):
    a = icon.getchannel('A')
    hist = a.histogram()
    return sum(hist[1:]) / float(CELL * CELL)


def border_ratio(icon):
    a = icon.getchannel('A')
    px = a.load()
    border = []
    for i in range(CELL):
        border.extend((px[i,0], px[i,CELL-1], px[0,i], px[CELL-1,i]))
    return sum(1 for v in border if v > 8) / float(len(border))


def micro_occupancy(icon, size):
    a = icon.getchannel('A').resize((size,size), Image.Resampling.LANCZOS)
    return sum(1 for v in a.getdata() if v >= 40)


def alpha_distance(a, b):
    aa = a.getchannel('A').resize((24,24), Image.Resampling.LANCZOS)
    bb = b.getchannel('A').resize((24,24), Image.Resampling.LANCZOS)
    diff = ImageChops.difference(aa, bb)
    return ImageStat.Stat(diff).mean[0] / 255.0


def main():
    sheet = Image.open(SHEET).convert('RGBA')
    errors = []
    if sheet.size != EXPECTED:
        errors.append(f'sheet size {sheet.size}, expected {EXPECTED}')

    metrics = {}
    for key in CELLS:
        icon = crop(sheet, key)
        occ = alpha_ratio(icon)
        border = border_ratio(icon)
        micro12 = micro_occupancy(icon, 12)
        metrics[key] = (occ, border, micro12)
        if not 0.025 <= occ <= 0.78:
            errors.append(f'{key}: alpha occupancy {occ:.3f} outside 0.025..0.78')
        if border > 0.18:
            errors.append(f'{key}: touches too much of cell border ({border:.3f})')
        if micro12 < 4:
            errors.append(f'{key}: disappears at 12px ({micro12} visible pixels)')

    # The five role marks must remain visually distinct after aggressive downscaling.
    roles = ['usec','bear','scav','boss','raider']
    for i, left in enumerate(roles):
        for right in roles[i+1:]:
            distance = alpha_distance(crop(sheet,left), crop(sheet,right))
            if distance < 0.035:
                errors.append(f'{left}/{right}: silhouettes too similar ({distance:.3f})')

    print('HUD asset QA:')
    for key, (occ, border, micro12) in metrics.items():
        print(f'  {key:9s} occupancy={occ:.3f} border={border:.3f} micro12={micro12:3d}px')

    if errors:
        print('\nFAILED:')
        for error in errors:
            print(' -', error)
        raise SystemExit(1)

    print('\nPASS: atlas geometry, cell padding, micro-scale visibility and faction silhouette separation')


if __name__ == '__main__':
    main()
