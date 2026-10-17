"""Package native Blender sprite renders; fixed palette, alpha, pivot and timing.
Outputs sprite atlases, frame metadata, and nearest-neighbor animation previews.
"""
from pathlib import Path
import json, base64
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parents[1]
ASSETS=PROJECT/'Assets/Art/KnightPixelTest'
SIZES=(48,64,96)
HEX=('131A30','263650','43566A','1B2857','354D9A','6A82CC',
     '8E9AAE','CDD5D5','FFF0CE','92502F','CC8C3E','F4C866')
COLORS=[tuple(bytes.fromhex(h)) for h in HEX]
DURATIONS=[125]*8+[70,35,145]
FRAME_COUNT=len(DURATIONS)

def gif_durations(indices):
    # GIF stores centiseconds. Preserve the intended 250 ms total attack.
    return [max(10,round(REVIEW_DURATIONS[i]/10)*10) for i in indices]

def sprite(path):
    source=Image.open(path).convert('RGBA'); w,h=source.size
    out=Image.new('RGBA',source.size)
    cache={}
    for y in range(h):
        for x in range(w):
            r,g,b,a=source.getpixel((x,y))
            if a<128: continue
            key=(r,g,b)
            if key not in cache:
                cache[key]=min(COLORS,key=lambda c:sum((c[i]-key[i])**2 for i in range(3)))
            out.putpixel((x,y),cache[key]+(255,))
    result=out.copy()
    for y in range(h):
        for x in range(w):
            if out.getpixel((x,y))[3]: continue
            if any(0<=xx<w and 0<=yy<h and out.getpixel((xx,yy))[3] for xx,yy in ((x-1,y),(x+1,y),(x,y-1),(x,y+1))):
                result.putpixel((x,y),(16,19,32,255))
    return result

def checker(size):
    image=Image.new('RGBA',size,(27,35,49,255)); draw=ImageDraw.Draw(image)
    for y in range(0,size[1],16):
        for x in range(0,size[0],16):
            if (x//16+y//16)%2: draw.rectangle((x,y,x+15,y+15),fill=(31,41,55,255))
    return image

def main():
    ASSETS.mkdir(parents=True,exist_ok=True)
    frames={}; report=[]
    for size in SIZES:
        frames[size]=[sprite(ROOT/'renders'/str(size)/('%02d.png'%i)) for i in range(1,FRAME_COUNT+1)]
        sheet=Image.new('RGBA',(size*FRAME_COUNT,size))
        boxes=[]
        for i,image in enumerate(frames[size]):
            box=image.getbbox(); boxes.append(box)
            if box is None or box[0]<2 or box[1]<2 or box[2]>size-2 or box[3]>size-2:
                raise ValueError('Empty/clipped sprite: %dpx frame %d, bounds %s'%(size,i+1,box))
            sheet.paste(image,(i*size,0))
        sheet.save(ASSETS/('knight-%d.png'%size))
        metadata={'size':size,'frameCount':FRAME_COUNT,'durationsMs':DURATIONS,'pivot':[.5,.22],
                  'pixelsPerUnit':size,'idle':[0,7],'capture':[8,10],'view':'rear',
                  'palette':list(HEX)+['101320'],'visibleBounds':boxes}
        (ASSETS/('knight-%d.json'%size)).write_text(json.dumps(metadata,indent=2))
        report.append('%dpx: %d frames, no clipped pixels, %d colors including outline.'%(size,FRAME_COUNT,len(set(p for im in frames[size] for p in im.get_flattened_data() if p[3]))))
    review=frames
    timeline_path=ROOT/'review-timeline.json'
    samples=json.loads(timeline_path.read_text())['samples'] if timeline_path.exists() else []
    if samples and all((ROOT/'shader-review'/str(size)/('%02d.png'%i)).exists() for size in SIZES for i in range(len(samples))):
        review={size:[Image.open(ROOT/'shader-review'/str(size)/('%02d.png'%i)).convert('RGBA') for i in range(len(samples))] for size in SIZES}
    else: samples=[{'bodyFrame':i,'durationMs':d} for i,d in enumerate(DURATIONS)]
    global REVIEW_DURATIONS
    REVIEW_DURATIONS=[s['durationMs'] for s in samples]
    review_count=len(samples)
    # Native pixels are enlarged by integer factors only.
    for label,indices in [('idle',list(range(8))),('capture',list(range(8,review_count))),('animation',list(range(review_count)))]:
        images=[]
        for i in indices:
            image=checker((256,256)); image.alpha_composite(review[64][i].resize((256,256),Image.Resampling.NEAREST)); images.append(image.convert('RGB'))
        images[0].save(ROOT/('knight-'+label+'.gif'),save_all=True,append_images=images[1:],duration=gif_durations(indices),loop=0,disposal=2)
        if label=='capture':
            images[0].save(ROOT/'knight-capture-slow.gif',save_all=True,append_images=images[1:],duration=[d*10 for d in gif_durations(indices)],loop=0,disposal=2)
    comparison=Image.new('RGB',(960,720),(23,30,43)); draw=ImageDraw.Draw(comparison)
    for col,size in enumerate(SIZES):
        scale={48:6,64:4,96:3}[size]
        draw.text((col*320+16,12),'%d x %d pixel frames'%(size,size),fill='white')
        for row,index in enumerate((0,10)):
            image=frames[size][index].resize((size*scale,size*scale),Image.Resampling.NEAREST)
            x=col*320+(320-image.width)//2; y=48+row*330
            comparison.paste(image,(x,y),image)
            draw.text((col*320+16,y+296),'IDLE' if row==0 else 'CAPTURE / STRIKE',fill=(202,212,225))
    comparison.save(ROOT/'pixel-size-comparison.png')
    poses=Image.new('RGB',(960,380),(23,30,43)); draw=ImageDraw.Draw(poses)
    for col,(index,label) in enumerate(zip((8,9,10),('RAISED / 70 ms','DIAGONAL CUT / 35 ms','LOW FINISH / 145 ms'))):
        image=frames[64][index].resize((320,320),Image.Resampling.NEAREST)
        poses.paste(image,(col*320,28),image)
        draw.text((col*320+16,352),label,fill=(225,231,242))
    poses.save(ROOT/'attack-three-poses.png')
    board_files=[ROOT/'board'/('%02d.png'%i) for i in range(review_count)]
    if all(p.exists() for p in board_files):
        palette=Image.open(board_files[0]).convert('RGB').quantize(colors=256,dither=Image.Dither.NONE)
        board_frames=[Image.open(p).convert('RGB').quantize(palette=palette,dither=Image.Dither.NONE) for p in board_files]
        board_frames[0].save(ROOT/'board-animation.gif',save_all=True,append_images=board_frames[1:],duration=gif_durations(range(review_count)),loop=0,disposal=2)
        Image.open(board_files[0]).save(ROOT/'board-idle.png')
        Image.open(board_files[min(11,review_count-1)]).save(ROOT/'board-strike.png')
    sheets={}
    for size in SIZES:
        sheet=Image.new('RGBA',(size*review_count,size))
        for i,im in enumerate(review[size]): sheet.paste(im,(i*size,0))
        import io
        buffer=io.BytesIO(); sheet.save(buffer,format='PNG')
        sheets[str(size)]='data:image/png;base64,'+base64.b64encode(buffer.getvalue()).decode()
    html=(ROOT/'preview-template.html').read_text(encoding='utf-8').replace('__SHEETS__',json.dumps(sheets)).replace('__DURATIONS__',json.dumps(REVIEW_DURATIONS)).replace('__BODY_FRAMES__',json.dumps([s['bodyFrame'] for s in samples]))
    (ROOT/'preview.html').write_text(html,encoding='utf-8')
    (ROOT/'validation.txt').write_text('\n'.join(report)+'\nRear view; 8 idle frames; 3 capture poses at 70/35/145 ms; independent 220 ms Unity GPU slash; fixed camera and pivot.\n')
    print('\n'.join(report))

if __name__=='__main__': main()
