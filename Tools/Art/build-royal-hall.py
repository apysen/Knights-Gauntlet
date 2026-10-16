"""Royal replacement for the connected hall bay; preserves the open gate model."""
from pathlib import Path
source=Path("D:/GameDev/Knight's Gauntlet App/My project/Tools/Art/build-castle-approach.py").read_text(encoding='utf-8')
exec(source.split('\nparts=[]\nfor y in (0,):',1)[0])
navy=material('Royal Vault Midnight',(.026,.045,.09))
marble=material('Royal Ivory Marble',(.48,.46,.40))
gilt=material('Royal Gilt Carving',(.66,.42,.115))
chandelier_gold=material('Royal Gilt Chandelier',(.71,.47,.14))
blue=material('Royal Banner Sapphire',(.025,.065,.20))
ruby=material('Royal Banner Burgundy',(.20,.025,.05))
embroidery=material('Royal Banner Gold Embroidery',(.78,.51,.15))
banner_mount=material('Royal Gilt Banner Mount',(.66,.42,.115))
glass_blue=material('Stained Glass Sapphire',(.035,.16,.49))
glass_ruby=material('Stained Glass Ruby',(.44,.035,.075))
glass_amber=material('Stained Glass Amber',(.72,.37,.08))
candle=material('Royal Candle Ivory',(.72,.65,.47))
candle_light=material('Royal Candle Flame',(.99,.68,.25))
# Match the pointed hem on the exterior flags too.
flag_vertices=set()
for polygon in gate.data.polygons:
    if 'Banner Teal' in gate.data.materials[polygon.material_index].name:
        flag_vertices.update(polygon.vertices)
for index in flag_vertices:
    v=gate.data.vertices[index].co;centre=2.8 if v.x>0 else -2.8
    v.z+=.30*abs((v.x-centre)/.425)*max(0,min(1,(10.45-v.z)/2))**6
gate.data.update()
parts=[]
from mathutils import Vector

def rod(name,a,b,radius,mat,sides=10):
    a=Vector(a);b=Vector(b);delta=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides,radius=radius,depth=delta.length,location=(a+b)/2)
    o=bpy.context.object;o.name=name;o.rotation_euler=delta.to_track_quat('Z','Y').to_euler();o.data.materials.append(mat)
    return o
def roof_z(x):return 3.8+4.4*(1-(abs(x)/5.7)**1.4)+.33
def cloth(name,vs,fs,mat,cx,top=7.15,height=.95):
    o=mesh(name,vs,fs,mat);uv=o.data.uv_layers.new(name='Banner embroidery UV')
    for polygon in o.data.polygons:
        for li in polygon.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co
            uv.data[li].uv=((v.x-cx)/.80+.5,(top-v.z)/height)
    return o

parts.append(arch('Carved marble transverse rib',0,5.4,3.8,4.4,.35,.32,marble))
parts.append(arch('Gilded rib inner fillet',-.205,5.4,3.8,4.4,.045,.065,gilt))
parts.append(arch('Gilded rib outer fillet',-.205,5.4,4.02,4.4,.045,.045,gilt))
for side in (-1,1):
    x=side*5.4
    parts.append(box('Marble pilaster',(x,0,1.9),(.7,.8,3.8),marble))
    for dx in (-.21,0,.21):parts.append(rod('Gilded flute',(x+dx,-.44,.3),(x+dx,-.44,3.55),.028,gilt,8))
    for z,size in ((.15,1.05),(3.65,.95),(3.86,1.15)):
        parts.append(box('Carved capital course',(x,0,z),(size,.95,.16),marble))
        parts.append(box('Capital gold leaf',(x,-.5,z+.09),(size,.045,.035),gilt,.008))
    # Move inside the camera's side sightlines, beneath the vault.
    bx=side*2.0;top=7.15;bottom=top-.95
    parts.append(box('Royal banner pole',(bx,-.5,top+.07),(1.05,.08,.08),banner_mount,.012))
    vs=[];fs=[]
    for row in range(13):
        for col in range(9):
            u=col/8;t=row/12
            z=top-t*.95+.23*abs(u*2-1)*t**6
            vs.append((bx+(u-.5)*.80,-.55,z))
    for row in range(12):
        for col in range(8):
            i=row*9+col;fs.append((i,i+1,i+10,i+9))
    parts.append(cloth('Royal heraldic velvet',vs,fs,blue if side<0 else ruby,bx))
    for dx in (-.37,.37):
        vs=[(bx+dx-.018,-.565,top),(bx+dx+.018,-.565,top),(bx+dx+.018,-.565,bottom+.23),(bx+dx-.018,-.565,bottom+.23)]
        parts.append(cloth('Gold stitched banner edge',vs,[(0,1,2,3)],embroidery,bx))
    for sign in (-1,1):
        vs=[];fs=[]
        for j in range(9):
            t=j/8;x=bx+sign*.37*t;z=bottom+.23*t
            vs.extend([(x,-.568,z+.018),(x,-.568,z-.008)])
            if j:
                k=j*2;fs.append((k-2,k,k+1,k-1))
        parts.append(cloth('Pointed gold banner hem',vs,fs,embroidery,bx))
    crown=[(-.26,0),(-.32,.34),(-.12,.21),(0,.46),(.12,.21),(.32,.34),(.26,0)]
    vs=[(bx+xx*.70,-.57,6.50+zz*.70) for xx,zz in crown]
    parts.append(cloth('Royal three point crown',vs,[tuple(range(len(vs)))],embroidery,bx))
    vs=[(bx-.20,-.575,6.47),(bx+.20,-.575,6.47),(bx+.20,-.575,6.51),(bx-.20,-.575,6.51)]
    parts.append(cloth('Crown base embroidery',vs,[(0,1,2,3)],embroidery,bx))

vs=[];fs=[]
for y in (0,26):
    for i in range(49):
        x=-5.7+11.4*i/48;vs.append((x,y,roof_z(x)))
for i in range(48):fs.append((i,i+1,50+i,49+i))
parts.append(mesh('Royal blue vaulted ceiling',vs,fs,navy))
for x in (-3.9,-1.45,0,1.45,3.9):
    vs=[(x-.022,0,roof_z(x-.022)-.075),(x+.022,0,roof_z(x+.022)-.075),(x+.022,26,roof_z(x+.022)-.075),(x-.022,26,roof_z(x-.022)-.075)]
    parts.append(mesh('Longitudinal gilded seam',vs,[(0,1,2,3)],gilt))
# Complete pointed windows, with lead tracery and heraldic glasswork.
exec((ROOT/'Tools/Art/royal-stained-glass.py').read_text(encoding='utf-8'))
for side in (-1,1):
    parts.append(box('Royal wall panel',(side*5.85,13,1.8),(.35,26,4.1),navy))

bay=join('CastleHallBay',parts)
# A separate compact lantern is used only in alternating hall sections.
parts=[];cy=18.0
lantern_glass=material('Royal Lantern Glass',(.88,.64,.28))
parts.append(rod('Short lantern suspension',(0,cy,8.35),(0,cy,7.85),.035,chandelier_gold,8))
parts.append(box('Frosted amber glass',(0,cy,7.37),(.52,.52,.78),lantern_glass,.045))
for x in (-.29,.29):
    for dy in (-.29,.29):parts.append(rod('Solid brass lantern corner',(x,cy+dy,6.94),(x,cy+dy,7.82),.035,chandelier_gold,8))
parts.append(box('Lantern brass foot',(0,cy,6.94),(.68,.68,.12),chandelier_gold,.035))
parts.append(box('Lantern brass crown',(0,cy,7.82),(.68,.68,.12),chandelier_gold,.035))
bpy.ops.mesh.primitive_cone_add(vertices=4,radius1=.47,radius2=.11,depth=.23,location=(0,cy,7.99),rotation=(0,0,math.pi/4))
o=bpy.context.object;o.name='Lantern pitched cap';o.data.materials.append(chandelier_gold);parts.append(o)
lantern=join('CastleHallLantern',parts)
for o in (gate,bay,lantern):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.export_scene.fbx(filepath=str(OUT/(o.name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
bpy.data.libraries.write(str(SOURCE/'royal-great-hall.blend'),{scene},fake_user=True)
print('Exported royal great hall: gilded vault, marble, heraldry, stained glass and chandeliers.')
