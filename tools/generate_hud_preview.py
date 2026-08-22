from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageOps

ROOT=Path(__file__).resolve().parents[1]
SHEET=ROOT/'SPT-PopCounter'/'assets'/'hud-sprites.png'
OUT=ROOT/'build-status'/'hud-preview.png'
CELL=64

COLORS={
    'pmc':(143,194,130,255),'self':(165,197,107,255),'scav':(196,110,102,255),'boss':(219,158,71,255),'raider':(168,135,199,255),
    'neutral':(204,209,207,255),'muted':(148,153,150,255),'head':(214,84,77,255),
    'water':(125,176,219,255),'energy':(220,179,79,255),'ok':(148,191,132,255),'heavy':(199,174,100,255)
}
CELLS={
    'usec':(0,0),'bear':(1,0),'scav':(2,0),'boss':(3,0),'raider':(4,0),'water':(5,0),'energy':(6,0),'weight':(7,0),
    'weight1':(0,1),'weight2':(1,1),'weight3':(2,1),'head':(3,1),'torso':(4,1),'stomach':(5,1),'left_arm':(6,1),'right_arm':(7,1),
    'left_leg':(0,2),'right_leg':(1,2),'self':(2,2),'weapon_unknown':(3,2),'weapon_assault':(4,2),'weapon_carbine':(5,2),'weapon_smg':(6,2),'weapon_lmg':(7,2),
    'weapon_sniper':(0,3),'weapon_dmr':(1,3),'weapon_shotgun_pump':(2,3),'weapon_shotgun_semi':(3,3),'weapon_shotgun_sawedoff':(4,3),'weapon_pistol':(5,3),'weapon_revolver':(6,3),'weapon_launcher':(7,3),
    'weapon_frag':(0,4),'weapon_impact':(1,4),'weapon_incendiary':(2,4),'weapon_melee':(3,4),'weapon_throwing':(4,4),'weapon_bolt':(5,4),'weapon_pcc':(6,4),'weapon_special':(7,4),
    'weapon_crossbow':(0,5),'weapon_tool':(1,5),'weapon_explosive':(2,5)
}

def font(size):
    for p in [Path('C:/Windows/Fonts/bahnschrift.ttf'),Path('C:/Windows/Fonts/arialn.ttf'),Path('/usr/share/fonts/truetype/dejavu/DejaVuSansCondensed.ttf')]:
        try:
            if p.exists(): return ImageFont.truetype(str(p),size)
        except Exception: pass
    return ImageFont.load_default()

def tint(icon,color):
    alpha=icon.getchannel('A')
    # Keep the authored metal/stone lighting instead of flattening every icon
    # into a solid silhouette. Unity's GUI.color modulation behaves similarly.
    luminance=ImageOps.grayscale(icon)
    dark=tuple(max(0,round(channel*.24)) for channel in color[:3])
    out=ImageOps.colorize(luminance,black=dark,white=color[:3]).convert('RGBA')
    out.putalpha(Image.eval(alpha,lambda a:a*color[3]//255))
    return out

def icon(sheet,key,size,color):
    cx,cy=CELLS[key]
    im=sheet.crop((cx*CELL,cy*CELL,(cx+1)*CELL,(cy+1)*CELL)).resize((size,size),Image.Resampling.LANCZOS)
    return tint(im,color)

def paste_icon(canvas,sheet,key,x,y,size,color):
    pad=max(2,round(size*.14))
    plate_box=(x-pad,y-pad,x+size+pad,y+size+pad)
    plate=Image.new('RGBA',canvas.size,(0,0,0,0)); pd=ImageDraw.Draw(plate)
    pd.ellipse(plate_box,fill=(6,8,7,230),outline=tuple(color[:3])+(158,),width=max(1,round(size*.08)))
    canvas.alpha_composite(plate)
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
        x=paste_icon(canvas,sheet,key,x,y-2,isz,COLORS[c])+3; x+=draw_text(d,(x,y),num,ts,COLORS['neutral'])+gap

def status(canvas,sheet,x,y,scale=1.0):
    d=ImageDraw.Draw(canvas); isz=max(11,round(16*scale)); ts=max(9,round(12*scale)); gap=max(6,round(8*scale))
    for key,val,c in [('water','86','water'),('energy','72','energy')]:
        x=paste_icon(canvas,sheet,key,x,y-2,isz,COLORS[c])+3;x+=draw_text(d,(x,y),val,ts,COLORS['neutral'])+gap
    x=paste_icon(canvas,sheet,'weight',x,y-2,max(11,isz-1),COLORS['muted'])+3;x+=draw_text(d,(x,y),'31',ts,COLORS['neutral'])
    x+=draw_text(d,(x,y+2),'kg',max(8,ts-3),COLORS['muted'])+3
    paste_icon(canvas,sheet,'weight1',x,y+1,max(10,isz-3),COLORS['ok'])

def killrow(canvas,sheet,x,y,killer,kc,weapon,victim,vc,hit,dist,detailed=False,scale=1.0):
    d=ImageDraw.Draw(canvas); isz=max(13,round(16*scale)); wisz=max(18,round(22*scale)); small=max(8,round(10*scale))
    x=paste_icon(canvas,sheet,killer.lower(),x,y-2,isz,COLORS[kc]);x+=max(5,round(7*scale))
    x=paste_icon(canvas,sheet,weapon,x,y-1,wisz,COLORS['neutral'])
    x+=max(3,round(4*scale))
    x=paste_icon(canvas,sheet,victim.lower(),x,y-2,isz,COLORS[vc]);x+=max(4,round(6*scale))
    if detailed:
        x=paste_icon(canvas,sheet,hit,x,y-2,isz,COLORS['head'] if hit=='head' else COLORS['muted'])
    x+=draw_text(d,(x+1,y+1),dist,small,COLORS['muted'])

def qa_strip(canvas,sheet,x,y):
    d=ImageDraw.Draw(canvas)
    draw_text(d,(x,y),'MICRO-SCALE QA',10,(170,174,171,255)); y+=18
    keys=['usec','bear','scav','boss','raider','self','water','energy','weight','head','torso','left_arm','right_arm','left_leg','right_leg','stomach','weapon_assault','weapon_smg','weapon_bolt']
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
    killrow(bg,sheet,1535,86,'SELF','self','weapon_assault','SCAV','scav','head','187m',False)
    killrow(bg,sheet,1535,112,'BEAR','pmc','weapon_carbine','BOSS','boss','torso','42m',False)
    killrow(bg,sheet,1450,150,'USEC','pmc','weapon_bolt','RAIDER','raider','head','264m',True)

    # Smaller and larger UI scale checks catch details that only work at one resolution.
    population(bg,sheet,14,980,.82)
    status(bg,sheet,14,956,.82)
    killrow(bg,sheet,1560,215,'BEAR','pmc','weapon_smg','SCAV','scav','left_arm','28m',False,.82)
    killrow(bg,sheet,1450,260,'SELF','self','weapon_shotgun_semi','RAIDER','raider','stomach','16m',True,1.18)

    qa_strip(bg,sheet,1185,835)
    OUT.parent.mkdir(parents=True,exist_ok=True)
    bg.convert('RGB').save(OUT,quality=93,optimize=True)
    print('Generated',OUT,'with 0.82x / 1.0x / 1.18x composition and 12/16/20px icon QA')

if __name__=='__main__': main()
