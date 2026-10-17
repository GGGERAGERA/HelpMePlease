from pathlib import Path
import math
import random
import re
from PIL import Image, ImageDraw, ImageOps, ImageEnhance

# Rebuilds presentation textures from existing Subject42 sprites; no gameplay data is written.
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/_Project/art/UI/SurfaceMap'
OUT.mkdir(parents=True, exist_ok=True)
ART = ROOT / 'Assets/_Project/art/Sprites/Environment'
W, H = 640, 400
INK = (23, 29, 32)
rng = random.Random(42)


def sprite(path, name):
    path = Path(path)
    meta = Path(str(path) + '.meta').read_text(encoding='utf-8')
    pattern = r'      name: ' + re.escape(name) + r'\r?\n      rect:\r?\n        serializedVersion: \d+\r?\n        x: (\d+)\r?\n        y: (\d+)\r?\n        width: (\d+)\r?\n        height: (\d+)'
    m = re.search(pattern, meta)
    if not m:
        raise ValueError((path, name))
    x, y, width, height = map(int, m.groups())
    image = Image.open(path).convert('RGBA')
    return image.crop((x, image.height-y-height, x+width, image.height-y))


def tone(source, color):
    alpha = source.getchannel('A')
    gray = ImageOps.grayscale(source)
    colored = ImageOps.colorize(gray, (0, 0, 0), color).convert('RGBA')
    colored.putalpha(alpha)
    return colored


ground = [sprite(ART/'ColdAsh/ColdAsh.png', f'Ground 4-{i}') for i in range(4)]
base = Image.new('RGBA', (W, H), INK)
for y in range(0, H, 64):
    for x in range(0, W, 64):
        tile = ground[rng.randrange(4)]
        tile = ImageEnhance.Color(tile).enhance(.35)
        tile = ImageEnhance.Brightness(tile).enhance(.84)
        base.alpha_composite(tile, (x, y))

def place(source, center, size=None, color=None, dim=False, shadow=True, flip=False):
    image = source.copy()
    if size:
        image=image.resize(size, Image.Resampling.NEAREST)
    if color:
        image=tone(image,color)
    if dim:
        image=ImageEnhance.Brightness(image).enhance(.47)
    if flip:
        image=ImageOps.mirror(image)
    bbox=image.getbbox()
    if bbox:
        image=image.crop(bbox)
    x=round(center[0]-image.width/2); y=round(center[1]-image.height/2)
    if shadow:
        shade=Image.new('RGBA',image.size,(9,15,17,255))
        shade.putalpha(image.getchannel('A').point(lambda v:int(v*.7)))
        base.alpha_composite(shade,(x+2,y+3))
    base.alpha_composite(image,(x,y))


# Sparse ground details reuse the actual ColdAsh terrain palette and sprites.
micro = [sprite(ART/'ColdAsh/ColdAsh.png', name) for name in ('Dry grass A','Dry grass B','Dry twig','Pebble A','Hairline crack')]
for x,y in [(58,183),(84,280),(136,237),(197,225),(257,91),(317,152),(372,87),(401,257),
            (552,198),(548,348),(322,352),(173,349),(351,283),(89,44),(568,46)]:
    detail=rng.randrange(len(micro))
    if detail==3: continue  # Tiny dark pebbles resembled unexplained interface dots.
    place(micro[detail], (x,y), (22,22), (86,100,96), shadow=False)
base.save(OUT/'AshTerrain.png')

# Miniatures are existing project sprites; terrain/route rebuilding leaves them unchanged.

# A single reusable pixel cloud. Closed nodes and their full roads are hidden separately in the view.
base=Image.new('RGBA',(320,240)); d=ImageDraw.Draw(base)
bayer=((0,8,2,10),(12,4,14,6),(3,11,1,9),(15,7,13,5))
for y in range(0,240,2):
    for x in range(0,320,2):
        u=(x-160)/157; v=(y-120)/118; a=math.atan2(v,u)
        radius=u*u+v*v; edge=1+.06*math.sin(a*7)+.025*math.sin(a*13)
        coverage=min(1,max(0,(edge-radius)/(edge-.22)))**.85
        value=190*coverage
        # Ordered alpha steps on the logical pixel grid, never a smooth Unity blur.
        alpha=max(0,min(192,int(value/16+(bayer[(y//2)%4][(x//2)%4]+.5)/16)*16))
        if alpha:d.rectangle((x,y,x+1,y+1),fill=(29,37,39,alpha))
base.save(OUT/'UnexploredFog.png')

paths=[('B','A1',[(294,237),(267,227),(250,202),(215,194),(190,177),(172,164)]),
('A1','A2',[(167,148),(152,135),(154,112),(134,97),(125,84)]),
('B','C1',[(312,233),(343,228),(365,207),(385,207),(407,183),(420,170)]),
('C1','C2',[(431,151),(454,140),(476,120),(488,94),(515,85)]),
('B','D1',[(285,246),(278,262),(253,267),(242,289),(217,302)]),
('D1','D2',[(228,310),(264,319),(295,311),(326,321),(365,321),(394,333),(432,331),(455,322)])]
def paint_trail(points,seed,fraction=1):
    image=Image.new('RGBA',(W,H)); d=ImageDraw.Draw(image); local=random.Random(seed+1)
    total=sum(math.dist(a,b) for a,b in zip(points,points[1:])); walked=0; limit=total if fraction==1 else min(total*fraction,80)
    for a,b in zip(points,points[1:]):
        length=math.dist(a,b)
        for j in range(int(length)+1):
            distance=walked+j
            if distance>limit:break
            t=j/max(1,int(length)); x=round(a[0]+t*(b[0]-a[0])); y=round(a[1]+t*(b[1]-a[1]))
            spread=local.choice((1,2,2,3)); alpha=255 if fraction==1 else round(255*min(1,max(0,(1-distance/max(1,limit))/.4)))
            d.rectangle((x-spread,y-1,x+spread,y+1),fill=(255,255,255,alpha))
            if j%5==0:d.point((x+local.choice((-5,-3,3,5)),y+local.choice((-3,-2,2,3))),fill=(255,255,255,round(alpha*.55)))
        walked+=length
        if walked>=limit:break
    return image
for seed,(start,end,points) in enumerate(paths):
    name=start+'-'+end
    paint_trail(points,seed).save(OUT/(name+'.png'))
    paint_trail(points,seed,.6).save(OUT/(name+'-HintFrom.png'))
    paint_trail(list(reversed(points)),seed,.6).save(OUT/(name+'-HintTo.png'))
base=Image.new('RGBA',(9,5));ImageDraw.Draw(base).polygon([(0,0),(8,0),(4,4)],fill='white');base.save(OUT/'Selected.png')
print('Rebuilt ColdAsh terrain, reusable pixel fog and route continuations; existing miniature sprites preserved.')
