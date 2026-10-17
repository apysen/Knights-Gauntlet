"""Quantize native renders, add knight-matching outlines, export seven views."""
from pathlib import Path
import json, types
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parent
ASSETS=ROOT.parents[1]/'Assets/Art/PixelArmy'
path=ROOT.parent/'PixelKnightTest/pack_sprites.py'
shared=types.ModuleType('army_pack');shared.__file__=str(path)
exec(compile(path.read_text(),str(path),'exec'),shared.__dict__)
palettes=json.loads((ROOT/'palette.json').read_text())
report=[];metadata=[]
for size in [48,64,96]:
 folder=ASSETS/str(size);folder.mkdir(parents=True,exist_ok=True)
 for kind in ['rook','bishop','queen','pawn']:
  colors=palettes[kind]+palettes['navy']+palettes['gold']+palettes['ivory']
  shared.COLORS=[tuple(bytes.fromhex(h)) for h in colors]
  for facing in (['front'] if kind=='pawn' else ['front','back']):
   name=kind+'-'+facing;im=shared.sprite(ROOT/'renders'/str(size)/(name+'.png'))
   bounds=im.getbbox()
   assert bounds and min(bounds[:2])>=2 and max(bounds[2:])<=size-2,(name,size,bounds)
   assert set(im.getchannel('A').getdata())<={0,255}
   im.save(folder/(name+'.png'))
   metadata.append({'piece':kind,'facing':facing,'size':size,'pivot':[.5,.22],'pixelsPerUnit':size,'bounds':bounds,'palette':colors+['101320']})
   report.append(f'{size}px {name}: bounds {bounds}; binary alpha; no clipped pixels.')
(ROOT/'sprites.json').write_text(json.dumps(metadata,indent=2))
(ROOT/'validation.txt').write_text('\n'.join(report))
for size in [48,64,96]:
 scale={48:5,64:4,96:3}[size];cell=288
 sheet=Image.new('RGB',(4*cell,2*cell+64),(23,30,43));draw=ImageDraw.Draw(sheet)
 for col,kind in enumerate(['rook','bishop','queen','pawn']):
  for row,facing in enumerate(['front','back']):
   x=col*cell;y=32+row*cell
   if kind=='pawn' and facing=='back':
    draw.text((x+65,y+125),'Front view only',fill=(135,151,174));continue
   im=Image.open(ASSETS/str(size)/(kind+'-'+facing+'.png')).convert('RGBA')
   im=im.resize((size*scale,size*scale),Image.Resampling.NEAREST)
   sheet.paste(im,(x+(cell-im.width)//2,y),im)
   draw.text((x+16,y+cell-14),kind.upper()+' / '+facing.upper(),fill=(231,236,245))
 draw.text((16,10),f'ORIGINAL PIXEL ARMY / {size}px native / matched knight camera and palette ramps',fill='white')
 sheet.save(ROOT/f'army-{size}-review.png')
(ROOT/'README.md').write_text('''# Pixel army art

Seven original static views: rook, bishop and queen front/back; pawn front only.
The red rook carries a tower shield and hammer; the violet bishop has a mitre
and staff; the magenta queen has a crown and rapier; the teal pawn is a hooded scout.

The editable `pixel-army.blend` contains separate rigid body/head/arm/leg groups
for future animation. These are static art assets, not completed animation rigs.
Native 48/64/96 px renders use the knight camera, stepped shading, binary alpha,
one-pixel outline and a common (.5,.22) pivot. Height differences are intentional.
`sprites.json` records bounds, palette and import settings for every view.

Run `build_army.py` in Blender and call `build_all()`, then run `pack_army.py`.
Final PNG assets are in `Assets/Art/PixelArmy/`; review sheets are beside this file.
The existing gameplay pieces have not been replaced by this art pass.
''')
print('Exported 21 transparent sprites: 7 views at 3 resolutions; bounds and alpha validated.')
