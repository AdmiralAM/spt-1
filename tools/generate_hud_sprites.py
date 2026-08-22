from PIL import Image, ImageDraw

S=6
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

# Faction marks share the same visual grammar: broad outer mass, deliberate negative space,
# 2–4 px internal cuts and no fragile hairlines. They are authored for 12–20 px display.

# USEC — compact eagle / SPQR-like military crest. Wing feathers are cut, not stroked.
cx,cy=0,0
poly(cx,cy,[(4,18),(12,10),(22,13),(28,9),(32,3),(36,9),(42,13),(52,10),(60,18),(56,27),(48,24),(52,33),(42,30),(45,39),(37,36),(38,52),(32,61),(26,52),(27,36),(19,39),(22,30),(12,33),(16,24),(8,27)])
# central neck/head negative space + beak
poly(cx,cy,[(28,13),(35,13),(38,22),(34,29),(29,25),(26,20)],CUT)
poly(cx,cy,[(31,15),(36,14),(35,17),(40,20),(35,22),(31,21)],FG)
# feather cuts that remain visible at 14 px
for a in [[(10,18),(23,22),(15,25)],[(13,24),(24,27),(17,30)],[(54,18),(41,22),(49,25)],[(51,24),(40,27),(47,30)]]: poly(cx,cy,a,CUT)
# chest plaque / lower crest
poly(cx,cy,[(21,39),(43,39),(41,49),(32,54),(23,49)],CUT)
rect(cx,cy,(23,42,41,47),FG,radius=1)
line(cx,cy,[(27,44),(37,44)],CUT,2)

# BEAR — shield/knuckle emblem with four unmistakable knuckles and a central assault mass.
cx,cy=1,0
poly(cx,cy,[(6,15),(14,8),(24,11),(32,5),(40,11),(50,8),(58,15),(55,42),(47,53),(32,61),(17,53),(9,42)])
# side claw/wing cuts
poly(cx,cy,[(11,18),(20,13),(25,17),(23,28),(15,27)],CUT)
poly(cx,cy,[(53,18),(44,13),(39,17),(41,28),(49,27)],CUT)
# four knuckles, intentionally oversized
for x in (19,28,37,46): ellipse(cx,cy,(x-4,21,x+3,30),CUT)
# palm/fist void and lower label bar
poly(cx,cy,[(15,32),(49,32),(46,43),(39,48),(25,48),(18,43)],CUT)
poly(cx,cy,[(20,35),(44,35),(41,40),(23,40)],FG)
line(cx,cy,[(24,44),(40,44)],FG,3)
rect(cx,cy,(17,50,47,55),CUT,radius=1)
rect(cx,cy,(21,52,43,53),FG)

# SCAV — improvised balaclava with off-axis seam and large eye opening.
cx,cy=2,0
poly(cx,cy,[(20,7),(42,8),(49,17),(49,42),(42,54),(31,60),(20,54),(14,43),(14,20)])
poly(cx,cy,[(18,19),(46,18),(43,29),(20,30)],CUT)
ellipse(cx,cy,(23,20,30,27),FG); ellipse(cx,cy,(35,20,42,27),FG)
poly(cx,cy,[(23,35),(42,34),(38,47),(27,48)],CUT)
line(cx,cy,[(19,13),(46,49)],CUT,2)
line(cx,cy,[(17,42),(27,50)],CUT,2)

# BOSS — heavy skull/chevron crest. More mass and less whitespace than ordinary faction marks.
cx,cy=3,0
poly(cx,cy,[(8,18),(16,7),(24,13),(32,3),(40,13),(48,7),(56,18),(52,28),(12,28)])
ellipse(cx,cy,(15,21,49,52))
poly(cx,cy,[(19,48),(24,59),(32,53),(40,59),(45,48)])
ellipse(cx,cy,(20,30,29,39),CUT); ellipse(cx,cy,(35,30,44,39),CUT)
poly(cx,cy,[(28,40),(36,40),(32,46)],CUT)
for x in (22,28,34,40): rect(cx,cy,(x,46,x+3,53),CUT)
line(cx,cy,[(13,24),(21,18)],CUT,2); line(cx,cy,[(51,24),(43,18)],CUT,2)

# RAIDER — helmet, wide visor, respirator and armoured jaw planes.
cx,cy=4,0
poly(cx,cy,[(12,22),(16,12),(24,6),(40,6),(48,12),(52,22),(53,34),(47,35),(44,52),(32,60),(20,52),(17,35),(11,34)])
poly(cx,cy,[(16,20),(22,14),(42,14),(48,20),(48,31),(16,31)],FG)
rect(cx,cy,(18,22,46,29),CUT,radius=2)
poly(cx,cy,[(20,34),(44,34),(41,50),(32,56),(23,50)],FG)
poly(cx,cy,[(25,38),(39,38),(37,46),(27,46)],CUT)
line(cx,cy,[(20,49),(27,53)],CUT,2); line(cx,cy,[(44,49),(37,53)],CUT,2)
line(cx,cy,[(17,17),(11,23),(10,33)],FG,3); line(cx,cy,[(47,17),(53,23),(54,33)],FG,3)

# Survival/status icons. All have similar visual mass at identical draw size.
# hydration drop
poly(5,0,[(32,4),(18,26),(15,35),(17,45),(23,54),(32,60),(41,54),(47,45),(49,35),(46,26)])
poly(5,0,[(27,37),(32,30),(37,37),(35,46),(29,46)],CUT)
# energy bolt
poly(0,1,[(37,3),(16,31),(29,31),(22,61),(49,24),(36,24)])
# weight plate/bag
ellipse(1,1,(23,7,41,25),fill=None,outline=FG,width=4)
rect(1,1,(13,22,51,56),FG,radius=4)
rect(1,1,(21,28,43,36),CUT,radius=2)
line(1,1,[(18,49),(46,49)],CUT,2)
# severity chevrons
for cx,n in ((2,1),(3,2),(4,3)):
    start=18-(n-1)*4
    for j in range(n): line(cx,1,[(16,start+j*9),(32,start-8+j*9),(48,start+j*9)],FG,4)

# Body-part silhouettes: simplified anatomy with clear orientation.
# head/profile
ellipse(5,1,(18,8,43,34)); poly(5,1,[(20,25),(19,44),(26,56),(37,56),(40,44),(48,41),(45,36),(42,35),(42,24)]); ellipse(5,1,(36,18,40,22),CUT)
# torso/rib cage
poly(0,2,[(19,8),(27,4),(37,4),(45,8),(52,17),(47,58),(17,58),(12,17)]); poly(0,2,[(20,14),(28,10),(36,10),(44,14),(43,51),(21,51)],CUT); line(0,2,[(32,11),(32,50)],FG,2); line(0,2,[(23,22),(41,22)],FG,2)
# arm
ellipse(1,2,(34,8,49,23)); poly(1,2,[(34,18),(42,25),(35,39),(24,47),(14,44),(15,36),(27,31)]); ellipse(1,2,(12,35,26,49))
# leg
poly(2,2,[(24,7),(41,7),(39,29),(43,50),(38,60),(29,60),(27,50),(31,29)])
# stomach / abdomen
ellipse(3,2,(17,11,47,53),fill=None,outline=FG,width=5); line(3,2,[(21,31),(43,31)],FG,3); line(3,2,[(32,17),(32,47)],FG,2)

# Weapon-family silhouettes. Identifying geometry is exaggerated because these resolve at ~16 px.
def weapon(cx,cy,k):
    if k=='ak':
        poly(cx,cy,[(5,27),(13,27),(18,22),(25,22),(29,25),(50,25),(55,21),(60,22),(57,28),(45,30),(40,35),(34,35),(31,31),(25,31),(22,39),(15,39),(18,31),(5,31)])
        poly(cx,cy,[(30,31),(39,31),(37,48),(30,45)]); line(cx,cy,[(17,28),(9,18)],FG,3)
    elif k=='ar':
        poly(cx,cy,[(5,27),(14,27),(18,24),(24,24),(27,20),(33,20),(35,24),(51,24),(57,20),(61,22),(58,28),(46,29),(40,34),(34,34),(31,30),(25,30),(21,39),(15,39),(17,31),(5,31)]); rect(cx,cy,(28,31,34,46)); line(cx,cy,[(17,27),(11,20)],FG,2)
    elif k=='smg':
        poly(cx,cy,[(8,25),(17,25),(21,22),(40,22),(44,25),(54,25),(56,28),(52,31),(40,31),(36,35),(30,35),(28,31),(20,31),(18,41),(13,41),(14,31),(8,31)]); rect(cx,cy,(36,31,40,44))
    elif k=='shotgun':
        rect(cx,cy,(5,25,55,30),FG,radius=2); rect(cx,cy,(50,23,61,32)); poly(cx,cy,[(18,30),(33,30),(26,39),(17,39)]); poly(cx,cy,[(6,30),(17,30),(13,38),(5,38)]); line(cx,cy,[(34,32),(47,32)],FG,2)
    elif k=='sniper':
        rect(cx,cy,(4,25,56,29),FG,radius=1); rect(cx,cy,(52,23,61,31)); poly(cx,cy,[(18,29),(35,29),(29,40),(18,40)]); rect(cx,cy,(28,19,44,24),FG,radius=2); rect(cx,cy,(34,14,38,20)); line(cx,cy,[(8,25),(4,20)],FG,2)
    elif k=='pistol':
        poly(cx,cy,[(14,20),(47,20),(50,24),(46,30),(31,30),(29,50),(21,50),(19,30),(14,29)]); line(cx,cy,[(22,25),(43,25)],CUT,2)
    else:
        poly(cx,cy,[(10,26),(20,22),(50,22),(55,26),(51,31),(38,31),(34,38),(25,38),(24,31),(10,31)])

for cell,k in [((4,2),'ak'),((5,2),'ar'),((0,3),'smg'),((1,3),'shotgun'),((2,3),'sniper'),((3,3),'pistol'),((4,3),'weapon')]: weapon(*cell,k)

# Downsample once from high-resolution source; this produces stable antialiased edges without
# storing oversized textures in the mod package.
img=img.resize((W,H),Image.Resampling.LANCZOS)
img.save('SPT-PopCounter/assets/hud-sprites.png',optimize=True)
print('Generated SPT-PopCounter/assets/hud-sprites.png',img.size,'supersample',S)