from PIL import Image, ImageDraw

S=4
CELL=64
W,H=6*CELL,4*CELL
FG=(236,238,232,255)
CUT=(0,0,0,0)
img=Image.new('RGBA',(W*S,H*S),(0,0,0,0))
d=ImageDraw.Draw(img)

def sc(v): return int(round(v*S))
def pts(cx,cy,a):
    ox,oy=cx*CELL*S,cy*CELL*S
    return [(ox+sc(x),oy+sc(y)) for x,y in a]
def poly(cx,cy,a,fill=FG): d.polygon(pts(cx,cy,a),fill=fill)
def line(cx,cy,a,fill=FG,width=2): d.line(pts(cx,cy,a),fill=fill,width=sc(width),joint='curve')
def ellipse(cx,cy,b,fill=FG,outline=None,width=1):
    ox,oy=cx*CELL*S,cy*CELL*S; bb=[ox+sc(b[0]),oy+sc(b[1]),ox+sc(b[2]),oy+sc(b[3])]
    d.ellipse(bb,fill=fill,outline=outline,width=sc(width))
def rect(cx,cy,b,fill=FG,outline=None,width=1,radius=0):
    ox,oy=cx*CELL*S,cy*CELL*S; bb=[ox+sc(b[0]),oy+sc(b[1]),ox+sc(b[2]),oy+sc(b[3])]
    if radius: d.rounded_rectangle(bb,radius=sc(radius),fill=fill,outline=outline,width=sc(width))
    else: d.rectangle(bb,fill=fill,outline=outline,width=sc(width))

# USEC — compact heraldic eagle. Broad wings and negative-space head survive 12–20 px HUD scale.
cx,cy=0,0
poly(cx,cy,[(5,18),(13,11),(24,14),(29,9),(32,4),(35,9),(40,14),(51,11),(59,18),(54,25),(47,23),(52,31),(43,29),(47,38),(37,35),(38,53),(32,60),(26,53),(27,35),(17,38),(21,29),(12,31),(17,23),(10,25)])
poly(cx,cy,[(29,14),(35,14),(37,24),(32,30),(27,24)],CUT)
poly(cx,cy,[(31,16),(36,15),(34,18),(39,20),(34,22),(31,21)],FG)
line(cx,cy,[(11,20),(24,24),(17,27)],CUT,2); line(cx,cy,[(53,20),(40,24),(47,27)],CUT,2)
rect(cx,cy,(20,42,44,48),CUT,radius=1); rect(cx,cy,(23,44,41,46),FG)

# BEAR — shield + four-knuckle assault emblem, intentionally close to the visual grammar of the reference.
cx,cy=1,0
poly(cx,cy,[(7,16),(15,9),(25,11),(32,6),(39,11),(49,9),(57,16),(54,43),(45,54),(32,60),(19,54),(10,43)])
poly(cx,cy,[(12,18),(20,14),(25,17),(23,27),(15,27)],CUT); poly(cx,cy,[(52,18),(44,14),(39,17),(41,27),(49,27)],CUT)
for x in (20,28,36,44): ellipse(cx,cy,(x-4,22,x+3,30),CUT)
poly(cx,cy,[(15,32),(49,32),(46,42),(39,47),(25,47),(18,42)],CUT)
poly(cx,cy,[(20,35),(44,35),(41,39),(23,39)],FG)
rect(cx,cy,(18,49,46,54),CUT,radius=1); rect(cx,cy,(22,51,42,52),FG)

# SCAV — asymmetric balaclava / improvised face covering.
cx,cy=2,0
poly(cx,cy,[(20,8),(42,8),(49,17),(48,43),(41,55),(31,59),(21,54),(15,43),(14,20)])
poly(cx,cy,[(18,20),(45,18),(43,28),(20,29)],CUT); ellipse(cx,cy,(24,20,30,26),FG); ellipse(cx,cy,(35,20,41,26),FG)
poly(cx,cy,[(24,35),(41,34),(38,46),(27,47)],CUT); line(cx,cy,[(18,14),(46,48)],CUT,2)

# BOSS — heavy skull/chevron crest; denser than ordinary faction marks.
cx,cy=3,0
poly(cx,cy,[(9,18),(16,8),(24,14),(32,4),(40,14),(48,8),(55,18),(51,27),(13,27)])
ellipse(cx,cy,(16,22,48,51)); poly(cx,cy,[(20,48),(25,58),(32,53),(39,58),(44,48)])
ellipse(cx,cy,(21,30,29,38),CUT); ellipse(cx,cy,(35,30,43,38),CUT); poly(cx,cy,[(29,40),(35,40),(32,45)],CUT)
for x in (23,29,35,41): rect(cx,cy,(x,46,x+3,52),CUT)

# RAIDER — helmet, visor and respirator/armour planes.
cx,cy=4,0
poly(cx,cy,[(13,22),(17,12),(25,7),(39,7),(47,12),(51,22),(52,34),(46,34),(43,53),(32,59),(21,53),(18,34),(12,34)])
rect(cx,cy,(13,22,51,31),FG,radius=3); rect(cx,cy,(18,24,46,29),CUT,radius=2)
poly(cx,cy,[(21,34),(43,34),(40,50),(32,55),(24,50)],FG); poly(cx,cy,[(26,38),(38,38),(36,45),(28,45)],CUT)
line(cx,cy,[(18,18),(12,23),(11,32)],FG,3); line(cx,cy,[(46,18),(52,23),(53,32)],FG,3)

# Survival/status icons.
poly(5,0,[(32,5),(18,27),(16,36),(18,46),(24,54),(32,59),(40,54),(46,46),(48,36),(46,27)]); poly(5,0,[(27,38),(32,31),(37,38),(35,46),(29,46)],CUT)
poly(0,1,[(36,3),(16,31),(29,31),(22,60),(49,25),(36,25)])
ellipse(1,1,(23,8,41,25),fill=None,outline=FG,width=4); rect(1,1,(13,22,51,55),FG,radius=4); rect(1,1,(21,28,43,36),CUT,radius=2)
for cx,n in ((2,1),(3,2),(4,3)):
    for j in range(n): line(cx,1,[(18,19+j*10),(32,11+j*10),(46,19+j*10)],FG,4)

# Body-part silhouettes.
ellipse(5,1,(18,9,43,34)); poly(5,1,[(20,25),(19,44),(26,55),(37,55),(40,43),(48,41),(45,36),(42,35),(42,24)]); ellipse(5,1,(36,19,40,23),CUT)
poly(0,2,[(19,8),(27,4),(37,4),(45,8),(51,17),(47,57),(17,57),(13,17)]); poly(0,2,[(20,14),(28,10),(36,10),(44,14),(43,51),(21,51)],CUT); line(0,2,[(32,11),(32,50)],FG,2)
ellipse(1,2,(34,8,49,23)); poly(1,2,[(34,18),(42,25),(35,39),(24,47),(14,44),(15,36),(27,31)]); ellipse(1,2,(12,35,26,49))
poly(2,2,[(24,7),(41,7),(39,29),(43,50),(38,59),(29,59),(27,50),(31,29)])
ellipse(3,2,(17,11,47,53),fill=None,outline=FG,width=5); line(3,2,[(22,32),(42,32)],FG,3); line(3,2,[(32,17),(32,47)],FG,2)

# Weapon-family silhouettes. Deliberately exaggerate identifying geometry at tiny HUD scale.
def weapon(cx,cy,k):
    if k=='ak':
        poly(cx,cy,[(6,27),(14,27),(19,22),(25,22),(28,25),(50,25),(54,22),(59,23),(56,28),(45,30),(40,35),(34,35),(31,31),(25,31),(22,38),(16,38),(18,31),(6,31)])
        poly(cx,cy,[(30,31),(39,31),(37,47),(30,45)]); line(cx,cy,[(16,28),(9,19)],FG,3)
    elif k=='ar':
        poly(cx,cy,[(5,27),(15,27),(18,24),(24,24),(27,20),(32,20),(34,24),(51,24),(56,21),(60,22),(58,28),(46,29),(40,34),(34,34),(31,30),(25,30),(21,39),(15,39),(17,31),(5,31)]); rect(cx,cy,(28,31,34,45))
    elif k=='smg':
        poly(cx,cy,[(8,25),(18,25),(21,22),(40,22),(43,25),(53,25),(55,28),(52,31),(40,31),(36,35),(30,35),(28,31),(20,31),(18,40),(13,40),(14,31),(8,31)])
    elif k=='shotgun':
        rect(cx,cy,(6,25,55,30),FG,radius=2); rect(cx,cy,(50,23,60,32)); poly(cx,cy,[(18,30),(32,30),(26,38),(17,38)]); poly(cx,cy,[(7,30),(17,30),(13,37),(6,37)])
    elif k=='sniper':
        rect(cx,cy,(5,25,55,29),FG,radius=1); rect(cx,cy,(51,23,61,31)); poly(cx,cy,[(18,29),(34,29),(29,39),(18,39)]); rect(cx,cy,(28,19,43,24),FG,radius=2); rect(cx,cy,(34,15,37,20))
    elif k=='pistol': poly(cx,cy,[(15,20),(46,20),(49,24),(46,29),(31,29),(29,49),(21,49),(19,29),(15,28)])
    else: poly(cx,cy,[(11,26),(20,23),(49,23),(53,26),(50,31),(37,31),(33,37),(25,37),(24,31),(11,31)])

for cell,k in [((4,2),'ak'),((5,2),'ar'),((0,3),'smg'),((1,3),'shotgun'),((2,3),'sniper'),((3,3),'pistol'),((4,3),'weapon')]: weapon(*cell,k)

img=img.resize((W,H),Image.Resampling.LANCZOS)
img.save('SPT-PopCounter/assets/hud-sprites.png',optimize=True)
print('Generated SPT-PopCounter/assets/hud-sprites.png',img.size)
