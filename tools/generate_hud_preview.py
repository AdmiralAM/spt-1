from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT=Path(__file__).resolve().parents[1]
SHEET=ROOT/'SPT-PopCounter'/'assets'/'hud-sprites.png'
OUT=ROOT/'build-status'/'hud-preview.png'
CELL=64

COLORS={
    'pmc':(143,194,130,255),'scav':(196,110,102,255),'boss':(219,158,71,255),'raider':(168,135,199,255),
    'neutral':(204,209,207,255),'muted':(148,153,150,255),'head':(214,84,77,255),
    'water':(125,176,219,255),'energy':(220,179,79,255),'ok':(148,191,132,255),'heavy':(199,174,100,255)
}
CELLS={
    'usec':(0,0),'bear':(1,0),'scav':(2,0),'boss':(3,0),'raider':(4,0),'water':(5,0),
    'energy':(0,1),'weight':(1,1),'weight1':(2,1),'weight2':(3,1),'weight3':(4,1),'head':(5,1),
    'torso':(0,2),'arm':(1,2),'leg':(2,2),'stomach':(3,2),'ak':(4,2),'ar':(5,2),
    'smg':(0,3),'shotgun':(1,3),'sniper':(2,3),'pistol':(3,3),'weapon':(4,3)
}

def font(size):
    for p in [Path('C:/Windows/Fonts/bahnschrift.ttf'),Path('C:/Windows/Fonts/arialn.ttf'),Path('/usr/share/fonts/truetype/dejavu/DejaVuSansCondensed.ttf')]:
        try:
            if p.exists(): return ImageFont.truetype(str(p),size)
        except Exception: pass
    return ImageFont.load_default()

def tint(icon,color):
    alpha=icon.getchannel('A')
    out=Image.new('RGBA',icon.size,color)
    out.putalpha(Image.eval(alpha,lambda a:a*color[3]//255))
    return out

def icon(sheet,key,size,color):
    cx,cy=CELLS[key]
    im=sheet.crop((cx*CELL,cy*CELL,(cx+1)*CELL,(cy+1)*CELL)).resize((size,size),Image.Resampling.LANCZOS)
    return tint(im,color)

def paste_icon(canvas,sheet,key,x,y,size,color):
    im=icon(sheet,key,size,color)
    shadow=Image.new('RGBA',canvas.size,(0,0,0,0)); shadow.alpha_composite(im,(x+1,y+1)); shadow=shadow.filter(ImageFilter.GaussianBlur(.45))
    canvas.alpha_composite(shadow)
    canvas.alpha_composite(im,(x,y))
    return x+size+2

def draw_text(draw,xy,value,size,color):
    f=font(size); x,y=xy
    draw.text((x+1,y+1),value,font=f,fill=(0,0,0,210),stroke_width=1,stroke_fill=(0,0,0,225))
    draw.text((x,y),value,font=f,fill=color,stroke_width=1,stroke_fill=(0,0,0,230))
    box=draw.textbbox((x,y),value,font=f,stroke_width=1)
    return box[2]-box[0]

def population(canvas,sheet,x,y,scale=1.0):
    d=ImageDraw.Draw(canvas); isz=max(11,round(16*scale)); ts=max(9,round(12*scale)); gap=max(5,round(7*scale))
    items=[('usec','12','pmc'),('scav','18','scav'),('boss','2','boss'),('raider','4','raider')]
    for key,num,c in items:
        x=paste_icon(canvas,sheet,key,x,y-2,isz,COLORS[c]); x+=draw_text(d,(x,y),num,ts,COLORS['neutral'])+gap

def status(canvas,sheet,x,y,scale=1.0):
    d=ImageDraw.Draw(canvas); isz=max(11,round(16*scale)); ts=max(9,round(12*scale)); gap=max(6,round(8*scale))
    for key,val,c in [('water','86','water'),('energy','72','energy')]:
        x=paste_icon(canvas,sheet,key,x,y-2,isz,COLORS[c]);x+=draw_text(d,(x,y),val,ts,COLORS['neutral'])+gap
    x=paste_icon(canvas,sheet,'weight',x,y-2,max(11,isz-1),COLORS['muted']);x+=draw_text(d,(x,y),'31',ts,COLORS['neutral'])
    x+=draw_text(d,(x,y+2),'kg',max(8,ts-3),COLORS['muted'])+3
    paste_icon(canvas,sheet,'weight1',x,y-1,max(10,isz-3),COLORS['ok'])

def killrow(canvas,sheet,x,y,killer,kc,weapon,victim,vc,hit,dist,detailed=False,scale=1.0):
    d=ImageDraw.Draw(canvas); role=max(10,round(11*scale)); isz=max(12,round(15*scale)); wisz=max(14,round(17*scale)); small=max(8,round(10*scale))
    x=paste_icon(canvas,sheet,killer.lower(),x,y-2,isz,COLORS[kc]);x+=draw_text(d,(x,y),killer,role,COLORS[kc])+max(5,round(7*scale))
    x=paste_icon(canvas,sheet,weapon,x,y-1,wisz,COLORS['neutral'])
    if detailed:
        name={'ak':'AK-74N','ar':'M4A1','sniper':'M700'}.get(weapon,'Weapon')
        x+=draw_text(d,(x,y+1),name,small,COLORS['muted'])+max(4,round(6*scale))
    else:x+=max(3,round(4*scale))
    x=paste_icon(canvas,sheet,victim.lower(),x,y-2,isz,COLORS[vc]);x+=draw_text(d,(x,y),victim,role,COLORS[vc])+max(4,round(6*scale))
    x=paste_icon(canvas,sheet,hit,x,y-1,max(11,round(13*scale)),COLORS['head'] if hit=='head' else COLORS['muted'])
    draw_text(d,(x+1,y+1),dist,small,COLORS['muted'])

def qa_strip(canvas,sheet,x,y):
    d=ImageDraw.Draw(canvas)
    draw_text(d,(x,y),'MICRO-SCALE QA',10,(170,174,171,255)); y+=18
    keys=['usec','bear','scav','boss','raider','water','energy','weight','head','torso','arm','leg','stomach','ak','ar','smg','shotgun','sniper','pistol']
    for size in (12,16,20):
        draw_text(d,(x,y+2),str(size)+'px',9,(135,139,136,255)); xx=x+34
        for key in keys:
            xx=paste_icon(canvas,sheet,key,xx,y,size,(205,207,202,255))+2
        y+=size+8

def main():
    sheet=Image.open(SHEET).convert('RGBA')
    # Contrast zones intentionally mimic hard cases: foliage-gray, near-black interior and a pale concrete patch.
    bg=Image.new('RGBA',(1920,1080),(33,36,34,255)); d=ImageDraw.Draw(bg)
    for i in range(0,1920,80): d.line((i,0,i-420,1080),fill=(41,44,41,255),width=28)
    d.rectangle((0,760,1920,1080),fill=(27,29,28,255))
    d.rectangle((1180,700,1900,980),fill=(77,78,72,255))
    bg=bg.filter(ImageFilter.GaussianBlur(7))

    # Production-scale layout.
    population(bg,sheet,14,1044)
    status(bg,sheet,14,1018)
    killrow(bg,sheet,1590,86,'USEC','pmc','ak','SCAV','scav','head','187m',False)
    killrow(bg,sheet,1590,106,'BEAR','pmc','ar','BOSS','boss','torso','42m',False)
    killrow(bg,sheet,1515,145,'USEC','pmc','sniper','RAIDER','raider','head','264m',True)

    # Smaller and larger UI scale checks catch details that only work at one resolution.
    population(bg,sheet,14,980,.82)
    status(bg,sheet,14,956,.82)
    killrow(bg,sheet,1610,205,'BEAR','pmc','smg','SCAV','scav','arm','28m',False,.82)
    killrow(bg,sheet,1530,250,'USEC','pmc','shotgun','RAIDER','raider','stomach','16m',True,1.18)

    qa_strip(bg,sheet,1185,835)
    OUT.parent.mkdir(parents=True,exist_ok=True)
    bg.convert('RGB').save(OUT,quality=93,optimize=True)
    print('Generated',OUT,'with 0.82x / 1.0x / 1.18x composition and 12/16/20px icon QA')

if __name__=='__main__': main()