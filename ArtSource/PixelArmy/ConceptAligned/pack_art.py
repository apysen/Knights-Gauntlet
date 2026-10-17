"""Sprite-sheet processing only: split, trim alpha, quantize, align and export.
Character design is supplied by generated concept-aligned source PNGs.
"""
from pathlib import Path
import json
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parents[2]
DEST=PROJECT/'Assets/Art/PixelArmy/ConceptAligned'
KINDS=['rook','bishop','queen','pawn']
HEIGHTS={'rook':43,'bishop':48,'queen':48,'pawn':40}
records=[]
for kind in KINDS:
 source=Image.open(ROOT/(kind+'-source.png')).convert('RGBA')
 halves=[source] if kind=='pawn' else [source.crop((0,0,source.width//2,source.height)),source.crop((source.width//2,0,source.width,source.height))]
 if kind=='queen' and (ROOT/'queen-cape-edit.png').exists():
  edited=Image.open(ROOT/'queen-cape-edit.png').convert('RGBA')
  halves[1]=edited.crop((edited.width//2,0,edited.width,edited.height))
 crops=[]
 for im in halves:
  alpha=im.getchannel('A').point(lambda x:255 if x>=128 else 0)
  im.putalpha(alpha);crops.append(im.crop(alpha.getbbox()))
 maxheight=max(im.height for im in crops)
 for size in [48,64,96]:
  folder=DEST/str(size);folder.mkdir(parents=True,exist_ok=True)
  factor=HEIGHTS[kind]*size/64/maxheight
  for view,crop in zip(['front','back'],crops):
   im=crop.resize((round(crop.width*factor),round(crop.height*factor)),Image.Resampling.NEAREST)
   alpha=im.getchannel('A')
   # A compact shared palette for both views of this character; no dithering.
   palette_source=Image.new('RGB',(sum(c.width for c in crops),maxheight),(17,19,30))
   x=0
   for c in crops:palette_source.paste(c,(x,0),c);x+=c.width
   palette=palette_source.quantize(colors=24,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE)
   colored=im.convert('RGB').quantize(palette=palette,dither=Image.Dither.NONE).convert('RGBA');colored.putalpha(alpha)
   frame=Image.new('RGBA',(size,size));x=(size-im.width)//2;y=round(size*.80)-im.height
   frame.paste(colored,(x,y))
   bounds=frame.getbbox()
   assert bounds and min(bounds[:2])>=2 and max(bounds[2:])<=size-2,(kind,view,size,bounds)
   destination=folder/(kind+'-'+view+'.png')
   # Rear-cape revision must preserve the approved front sprites byte for byte.
   if kind=='queen' and view=='front' and destination.exists() and (ROOT/'queen-cape-edit.png').exists():
    frame=Image.open(destination).convert('RGBA');bounds=frame.getbbox()
   else: frame.save(destination)
   records.append({'piece':kind,'view':view,'size':size,'pivot':[.5,.20],'pixelsPerUnit':size,'bounds':bounds,'colors':len(set(p for p in frame.get_flattened_data() if p[3]))})

for size in [48,64,96]:
 cell=288;sheet=Image.new('RGB',(cell*4,cell*2+40),(25,32,45));draw=ImageDraw.Draw(sheet)
 draw.text((16,12),f'CONCEPT-ALIGNED PIXEL ART / {size}px / front and back',fill='white')
 scale={48:5,64:4,96:3}[size]
 for col,kind in enumerate(KINDS):
  for row,view in enumerate(['front','back']):
   x=col*cell;y=40+row*cell
   if kind=='pawn' and view=='back':draw.text((x+66,y+120),'Front view only',fill=(160,174,193));continue
   im=Image.open(DEST/str(size)/(kind+'-'+view+'.png')).convert('RGBA').resize((size*scale,size*scale),Image.Resampling.NEAREST)
   sheet.paste(im,(x+(cell-im.width)//2,y),im)
   draw.text((x+16,y+cell-20),kind.upper()+' / '+view.upper(),fill='white')
 sheet.save(ROOT/f'pixel-army-{size}.png')

# Keep a side-by-side visual audit against the actual approved characters.
reference=Image.open(PROJECT/'ArtSource/Concepts/CharacterMockups/six-piece-lineup.png').convert('RGB')
review=Image.new('RGB',(1152,600),(25,32,45));draw=ImageDraw.Draw(review)
for col,(kind,rect) in enumerate(zip(KINDS,[(260,90,650,660),(980,65,1305,660),(1280,65,1640,660),(20,225,260,660)])):
 im=reference.crop(rect);im.thumbnail((270,285))
 review.paste(im,(col*288+(288-im.width)//2,0))
 sprite=Image.open(DEST/'64'/(kind+'-front.png')).convert('RGBA').resize((256,256),Image.Resampling.NEAREST)
 review.paste(sprite,(col*288+16,310),sprite)
 draw.text((col*288+16,582),kind.upper()+' / CONCEPT ABOVE, SPRITE BELOW',fill='white')
review.save(ROOT/'concept-comparison.png')
(ROOT/'sprites.json').write_text(json.dumps(records,indent=2))
(ROOT/'README.md').write_text('''# Concept-aligned pixel army

Authoritative reference: ArtSource/Concepts/CharacterMockups/six-piece-lineup.png.
Seven views: rook/bishop/queen SOUTH front and NORTH rear; pawn SOUTH front only.
Cardinal views have square shoulders and centered head/torso, no diagonal yaw.
Headgear exaggerates rook battlements, bishop diagonal slit, queen crown and pawn ball.
Generated transparent source sheets are retained here. These are raster art;
the earlier procedural Blender models do not reproduce these revised designs.

Final sprites: Assets/Art/PixelArmy/ConceptAligned/{48,64,96}/.
Point filtering, no mipmaps/compression, common foot pivot (.5,.20), full rect.
24 colors per character, shared across front and back; binary alpha.
The packing script crops and normalizes existing art without drawing new features.
The pawn is intentionally smaller; bishop and queen are taller than the rook.
This pass does not replace the live game pieces or add animations.
''')
print(f'Exported {len(records)} sprites; all bounds and transparency validated.')
