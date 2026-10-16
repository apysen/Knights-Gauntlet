"""Separate gate and vaulted hall scenery; no buildings in the grass sky."""
import bpy, math
from pathlib import Path
ROOT=Path("D:/GameDev/Knight's Gauntlet App/My project")
OUT=ROOT/'Assets/Resources/Horizon'; SOURCE=ROOT/'ArtSource/Models/Horizon'
scene=bpy.data.scenes.get('KG_BiomeHorizons') or bpy.data.scenes.new('KG_BiomeHorizons')
bpy.context.window.scene=scene
for o in list(scene.objects): bpy.data.objects.remove(o,do_unlink=True)
def material(name,rgb):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color=(*rgb,1);m.use_nodes=True
    n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*rgb,1);n.inputs['Roughness'].default_value=.9
    return m
stone=material('Gate Weathered Stone',(.24,.29,.31))
trim=material('Gate Pale Trim',(.40,.44,.43))
dark=material('Gate Recesses',(.035,.055,.07))
iron=material('Gate Black Iron',(.065,.095,.11))
roof=material('Gate Slate',(.085,.15,.19))
vault=material('Hall Vault Stone',(.17,.20,.23))
rib=material('Hall Limestone Ribs',(.33,.32,.29))
warm=material('Hall Warm Sconce',(.95,.52,.14))
def box(name,pos,size,mat,bevel=.035):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat)
    if bevel:
        mod=o.modifiers.new('Worn edges','BEVEL');mod.width=bevel;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
    return o
def join(name,parts):
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:p.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
    scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');return o
def mesh(name,vertices,faces,mat):
    data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update()
    o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);data.materials.append(mat);return o
def arch(name,y,width,spring,height,depth,thickness,mat):
    verts=[];faces=[]
    for i in range(33):
        x=-width+2*width*i/32
        z=spring+height*(1-(abs(x)/width)**1.4)
        for yy,zz in ((y-depth/2,z),(y+depth/2,z),(y-depth/2,z+thickness),(y+depth/2,z+thickness)):
            verts.append((x,yy,zz))
        if i:
            a=(i-1)*4;b=i*4
            faces.extend([(a,b,b+1,a+1),(a+2,a+3,b+3,b+2),(a,a+2,b+2,b),(a+1,b+1,b+3,a+3)])
    return mesh(name,verts,faces,mat)

parts=[]
for side in (-1,1):
    x=side*3.5
    parts.append(box('Gatehouse tower',(x,.7,2.9),(2.1,2.7,5.8),stone,.09))
    for z in (.35,4.65,5.7):parts.append(box('Tower stone course',(x,.7,z),(2.23,2.83,.22),trim))
    for dx in (-.8,0,.8):parts.append(box('Battlement',(x+dx,.0,6.1),(.44,1.35,.7),stone))
    parts.append(box('Arrow slit',(x,-.67,3.1),(.16,.03,1.25),dark))
    parts.append(box('Bridge abutment',(side*2.1,-1,-.15),(1,4,.4),stone))
    parts.append(box('Gate pier',(side*1.95,.4,1.8),(.7,1.8,3.6),trim))
parts.append(arch('Entrance archivolt',-.3,1.7,2.6,1.35,.65,.32,trim))
parts.append(box('Upper gate wall',(0,.7,5),(5.5,2,1.6),stone))
parts.append(box('Coping',(0,.7,5.8),(5.7,2.15,.18),trim))
parts.append(box('Gate shadow',(0,.7,2),(3.4,.1,4),dark))
for x in (-1.4,-.93,-.47,0,.47,.93,1.4):parts.append(box('Raised portcullis',(x,-.15,3.35),(.055,.08,1.45),iron,.008))
parts.append(box('Portcullis crossbar',(0,-.2,3.3),(3.2,.08,.06),iron,.008))
gate=join('DrawbridgeGate',parts)

parts=[]
# Barrel vault and repeated ribs: a view into a hall, with a dark roof overhead.
for y in (0,2.8,5.6,8.4):
    parts.append(arch('Vault rib',y,4.6,1.4,4.8,.24,.22,rib))
    for side in (-1,1):
        parts.append(box('Hall column',(side*4.6,y,.7),(.5,.6,1.5),rib))
        parts.append(box('Column capital',(side*4.6,y,1.5),(.78,.8,.28),rib))
        parts.append(box('Wall sconce',(side*4.2,y-.2,2.05),(.18,.22,.5),iron))
        parts.append(box('Sconce light',(side*4.2,y-.25,2.35),(.13,.15,.3),warm))
verts=[];faces=[]
for y in (0,9):
    for i in range(33):
        x=-4.8+9.6*i/32;z=1.4+4.8*(1-(abs(x)/4.8)**1.4)+.3
        verts.append((x,y,z))
for i in range(32):faces.append((i,i+1,34+i,33+i))
parts.append(mesh('Vault ceiling',verts,faces,vault))
for side in (-1,1):parts.append(box('Hall side wall',(side*4.95,4.5,.3),(.35,9,3),vault))
parts.append(box('Distant hall shadow',(0,9.2,2.6),(10,.2,7.6),dark))
hall=join('CastleVault',parts)
for o in (gate,hall):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.export_scene.fbx(filepath=str(OUT/(o.name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
bpy.data.libraries.write(str(SOURCE/'biome-horizons.blend'),{scene},fake_user=True)
print('Exported drawbridge gate and vaulted castle hall; grass uses open sky.')
