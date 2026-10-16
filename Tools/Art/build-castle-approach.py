"""Connected, open gate and repeating hall bay for the scrolling approach."""
from pathlib import Path
helper=Path("D:/GameDev/Knight's Gauntlet App/My project/Tools/Art/build-biome-horizons.py").read_text(encoding='utf-8')
exec(helper.split('\nparts=[]',1)[0].replace('KG_BiomeHorizons','KG_CastleApproach'))

banner=material('Approach Banner Teal',(.035,.16,.20))
gold=material('Approach Banner Gold',(.61,.39,.12))
flame=material('Approach Torch Flame',(.95,.43,.09))
parts=[]
for side in (-1,1):
    x=side*6.3
    parts.append(box('Gate tower',(x,.8,5.6),(2.6,3.3,11.2),stone,.08))
    for z in (.4,4.0,8.6,11.1):parts.append(box('Tower course',(x,.8,z),(2.78,3.48,.2),trim))
    for dx in (-.9,0,.9):parts.append(box('Tower battlement',(x+dx,-.2,11.5),(.48,1.5,.8),stone))
    for z in (3.0,7.2):parts.append(box('Arrow slit',(x,-.87,z),(.18,.035,1.4),dark))
    parts.append(box('Portal pier',(side*4.95,.6,2),(.65,2.5,4),trim))
    banner_x=side*2.8
    parts.append(box('Banner pole',(banner_x,-1.16,10.5),(1.3,.1,.1),gold))
    # UV.y increases away from the fixed top edge, for wind displacement.
    vs=[];fs=[]
    for row in range(13):
        for col in range(5):vs.append((banner_x+(col/4-.5)*.85,-1.22,10.45-row/12*2.0))
    for row in range(12):
        for col in range(4):
            a=row*5+col;fs.append((a,a+1,a+6,a+5))
    flag=mesh('Hanging banner',vs,fs,banner)
    uv=flag.data.uv_layers.new(name='Cloth UV')
    for poly in flag.data.polygons:
        for li in poly.loop_indices:
            vi=flag.data.loops[li].vertex_index;uv.data[li].uv=(vi%5/4,vi//5/12)
    parts.append(flag)

parts.append(arch('Portal archivolt',-.4,4.7,3.8,4,.8,.45,trim))
# Fill the spandrel above the curved opening, leaving a genuinely open doorway.
vs=[];fs=[]
for i in range(49):
    x=-4.7+9.4*i/48;z=3.8+4*(1-(abs(x)/4.7)**1.4)+.43
    vs.extend([(x,-.75,z),(x,1.8,z),(x,-.75,9.0),(x,1.8,9.0)])
    if i:
        a=(i-1)*4;b=i*4
        fs.extend([(a,a+2,b+2,b),(a+1,b+1,b+3,a+3),(a,b,b+1,a+1),(a+2,a+3,b+3,b+2)])
# Close the two exposed end faces of the curved wall volume.
last=len(vs)-4
fs.extend([(0,1,3,2),(last,last+2,last+3,last+1)])
parts.append(mesh('Stone over portal',vs,fs,stone))
# The curved wall ends at x=4.7, while the towers begin at x=5.0.
# Solid masonry returns bridge that gap above the entrance piers.
for side in (-1,1):
    parts.append(box('Solid portal tower return',(side*4.925,.525,6.45),(.45,2.55,5.3),stone,0))
parts.append(box('Gate upper wall',(0,.5,9.9),(13.1,2.6,1.8),stone))
parts.append(box('Gate cornice',(0,.5,10.85),(13.3,2.85,.2),trim))
for x in (-4,-2,0,2,4):parts.append(box('Wall battlement',(x,.2,11.2),(.65,1.5,.7),stone))
gate=join('CastleApproachGate',parts)

parts=[]
for y in (0,):
    parts.append(arch('Connected hall rib',y,5.4,3.8,4.4,.26,.26,rib))
    for side in (-1,1):
        parts.append(box('Hall pier',(side*5.4,y,1.9),(.6,.7,3.8),rib))
        parts.append(box('Pier capital',(side*5.4,y,3.8),(.85,.9,.3),rib))
        parts.append(box('Iron torch bracket',(side*3.0,y-.25,6.1),(.18,.25,.5),iron))
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.18,location=(side*3,y-.3,6.48))
        o=bpy.context.object;o.name='Torch flame';o.scale=(.6,.7,1.6);o.data.materials.append(flame);parts.append(o)
vs=[];fs=[]
for y in (0,10):
    for i in range(49):
        x=-5.7+11.4*i/48;z=3.8+4.4*(1-(abs(x)/5.7)**1.4)+.33
        vs.append((x,y,z))
for i in range(48):fs.append((i,i+1,50+i,49+i))
parts.append(mesh('Continuous vault ceiling',vs,fs,vault))
for side in (-1,1):parts.append(box('Hall wall',(side*5.85,5,1.8),(.3,10,4.1),vault))
bay=join('CastleHallBay',parts)

for o in (gate,bay):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.export_scene.fbx(filepath=str(OUT/(o.name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
bpy.data.libraries.write(str(SOURCE/'castle-approach.blend'),{scene},fake_user=True)
print('Exported open castle gate and connected ten-unit hall bay, including wind-ready banners and torches.')
