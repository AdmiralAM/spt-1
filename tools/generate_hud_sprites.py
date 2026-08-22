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
    ox,oy=cx*CELL*S,cy*CELL*S
    bb=[ox+sc(b[0]),oy+sc(b[1]),ox+sc(b[2]),oy+sc(b[3])]
    d.ellipse(bb,fill=fill,outline=outline,width=sc(width))
def rect(cx,cy,b,fill=FG,outline=None,width=1,radius=0):
    ox,oy=cx*CELL*S,cy*CELL*S
    bb=[ox+sc(b[0]),oy+sc(b[1]),ox+sc(b[2]),oy+sc(b[3])]
    if radius: d.rounded_rectangle(bb,radius=sc(radius),fill=fill,outline=outline,width=sc(width))
    else: d.rectangle(bb,fill=fill,outline=outline,width=sc(width))

# USEC: angular eagle/shield insignia, designed to stay readable at 12-20 px.
cx,cy=0,0
poly(cx,cy,[(8,14),(18,8),(28,11),(32,6),(36,11),(46,8),(56,14),(53,44),(42,56),(32,59),(22,56),(11,44)])
poly(cx,cy,[(14,18),(22,14),(28,17),(27,28),(18,27)],CUT)
poly(cx,cy,[(50,18),(42,14),(36,17),(37,28),(46,27)],CUT)
poly(cx,cy,[(29,16),(35,16),(37,32),(32,39),(27,32)],CUT)
poly(cx,cy,[(31,21),(36,20),(34,23),(39,24),(34,27),(31,26)],CUT)
rect(cx,cy,(16,39,48,46),CUT,radius=1); rect(cx,cy,(19,41,45,43),FG); rect(cx,cy,(25,50,39,53),CUT,radius=1)

# BEAR: fortress shield / knuckle motif.
cx,cy=1,0
poly(cx,cy,[(9,13),(19,8),(45,8),(55,13),(53,44),(43,56),(21,56),(11,44)])
for x in (18,26,34,42): poly(cx,cy,[(x,10),(x+4,10),(x+2,16)],CUT)
for x in (21,29,37,45): ellipse(cx,cy,(x-3,20,x+3,27),CUT)
poly(cx,cy,[(18,29),(46,29),(43,40),(37,44),(27,44),(21,40)],CUT)
rect(cx,cy,(16,45,48,51),CUT,radius=1); rect(cx,cy,(20,47,44,49),FG)

# SCAV balaclava.
cx,cy=2,0
ellipse(cx,cy,(14,8,50,54)); poly(cx,cy,[(16,15),(48,15),(45,48),(38,57),(26,57),(19,48)])
ellipse(cx,cy,(23,21,41,27),CUT); ellipse(cx,cy,(29,36,35,42),CUT)
line(cx,cy,[(23,31),(18,39),(22,48)],CUT,2); line(cx,cy,[(41,31),(46,39),(42,48)],CUT,2)

# BOSS: crown/skull crest.
cx,cy=3,0
poly(cx,cy,[(11,20),(17,8),(25,15),(32,5),(39,15),(47,8),(53,20),(49,28),(15,28)])
ellipse(cx,cy,(17,23,47,52)); ellipse(cx,cy,(22,31,29,38),CUT); ellipse(cx,cy,(35,31,42,38),CUT)
poly(cx,cy,[(30,39),(34,39),(32,44)],CUT)
for x in (24,30,36,42): rect(cx,cy,(x,46,x+3,52),CUT)
line(cx,cy,[(16,52),(26,58),(32,55),(38,58),(48,52)],FG,2)

# RAIDER: helmet / visor / lower face armour.
cx,cy=4,0
ellipse(cx,cy,(13,10,51,43)); rect(cx,cy,(10,23,54,31),FG,radius=3); rect(cx,cy,(16,25,48,30),CUT,radius=2)
poly(cx,cy,[(18,32),(46,32),(42,54),(22,54)]); line(cx,cy,[(24,40),(40,40)],CUT,2); line(cx,cy,[(28,46),(36,46)],CUT,2)

# Survival/status icons.
cx,cy=5,0
poly(cx,cy,[(32,6),(18,28),(17,37),(20,47),(26,54),(32,57),(38,54),(44,47),(47,37),(46,28)]); ellipse(cx,cy,(26,35,34,43),CUT)
poly(0,1,[(35,4),(17,31),(29,31),(22,59),(48,26),(35,26)])
ellipse(1,1,(23,8,41,25),fill=None,outline=FG,width=4); rect(1,1,(13,22,51,55),FG,radius=4); rect(1,1,(22,28,42,36),CUT,radius=2)
for cx,n in ((2,1),(3,2),(4,3)):
    for j in range(n): line(cx,1,[(18,19+j*10),(32,11+j*10),(46,19+j*10)],FG,4)

# Body-part set.
ellipse(5,1,(18,9,43,34)); poly(5,1,[(20,25),(19,44),(26,55),(37,55),(40,43),(48,41),(45,36),(42,35),(42,24)]); ellipse(5,1,(36,19,40,23),CUT)
poly(0,2,[(19,8),(27,4),(37,4),(45,8),(51,17),(47,57),(17,57),(13,17)]); poly(0,2,[(20,14),(28,10),(36,10),(44,14),(43,51),(21,51)],CUT); line(0,2,[(32,11),(32,50)],FG,2)
ellipse(1,2,(34,8,49,23)); poly(1,2,[(34,18),(42,25),(35,39),(24,47),(14,44),(15,36),(27,31)]); ellipse(1,2,(12,35,26,49))
poly(2,2,[(24,7),(41,7),(39,29),(43,50),(38,59),(29,59),(27,50),(31,29)])
ellipse(3,2,(17,11,47,53),fill=None,outline=FG,width=5); line(3,2,[(22,32),(42,32)],FG,3); line(3,2,[(32,17),(32,47)],FG,2)

# Weapon-family silhouettes: AK, AR, SMG, shotgun, sniper, pistol, generic.
def weapon(cx,cy,k):
    if k=='ak':
        poly(cx,cy,[(7,27),(15,27),(19,22),(24,22),(27,25),(50,25),(53,22),(57,23),(55,28),(45,30),(40,35),(34,35),(31,31),(25,31),(22,37),(17,37),(18,31),(7,31)])
        poly(cx,cy,[(30,31),(38,31),(36,47),(30,45)]); line(cx,cy,[(16,28),(10,20)],FG,3)
    elif k=='ar':
        poly(cx,cy,[(6,27),(15,27),(17,24),(24,24),(26,21),(31,21),(33,24),(51,24),(55,21),(59,22),(58,28),(46,29),(40,34),(34,34),(31,30),(25,30),(21,38),(16,38),(17,31),(6,31)])
        rect(cx,cy,(28,31,34,45))
    elif k=='smg':
        poly(cx,cy,[(9,25),(19,25),(21,22),(39,22),(42,25),(52,25),(54,28),(51,31),(40,31),(36,35),(30,35),(28,31),(20,31),(18,39),(13,39),(14,31),(9,31)])
    elif k=='shotgun':
        rect(cx,cy,(7,25,54,30),FG,radius=2); rect(cx,cy,(49,23,59,32)); poly(cx,cy,[(18,30),(31,30),(26,38),(17,38)]); poly(cx,cy,[(8,30),(17,30),(13,37),(7,37)])
    elif k=='sniper':
        rect(cx,cy,(6,25,54,29),FG,radius=1); rect(cx,cy,(50,23,60,31)); poly(cx,cy,[(18,29),(33,29),(29,39),(18,39)]); rect(cx,cy,(28,20,42,24),FG,radius=2); rect(cx,cy,(33,16,36,20))
    elif k=='pistol': poly(cx,cy,[(16,20),(45,20),(48,24),(45,29),(31,29),(29,48),(21,48),(19,29),(16,28)])
    else: poly(cx,cy,[(12,26),(20,23),(48,23),(52,26),(49,31),(37,31),(33,37),(25,37),(24,31),(12,31)])

for cell,k in [((4,2),'ak'),((5,2),'ar'),((0,3),'smg'),((1,3),'shotgun'),((2,3),'sniper'),((3,3),'pistol'),((4,3),'weapon')]: weapon(*cell,k)

img=img.resize((W,H),Image.Resampling.LANCZOS)
img.save('SPT-PopCounter/assets/hud-sprites.png',optimize=True)
print('Generated SPT-PopCounter/assets/hud-sprites.png',img.size)
