"""Original low-poly horse-helmet knight. Blender 5.x, Z up, forward -Y.
Editable rigid parts, ready for future rigging. Studio excluded from exports.
"""
import bpy, math, os, bmesh
from mathutils import Vector
OUT = os.path.dirname(os.path.abspath(__file__))
scene=bpy.data.scenes.new('Enchanted Knight Studio')
bpy.context.window.scene=scene
def mat(name,h):
    c=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    c=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in c]
    m=bpy.data.materials.new('EK_'+name); m.diffuse_color=(*c,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*c,1); bs.inputs['Roughness'].default_value=.8
    return m
blue=mat('Cobalt','294FA5'); lightBlue=mat('Crest highlights','4272C2'); darkBlue=mat('Deep blue','172C60')
navy=mat('Joint navy','121C30'); ivory=mat('Ivory armor','E5DFC9'); ivoryShade=mat('Ivory bevel','B8B49F')
gold=mat('Antique gold','D9A842'); goldLight=mat('Gold edge','F0CA67'); goldDark=mat('Gold shade','9D6A29'); steel=mat('Blade','F1EEDB')
def empty(name,parent=None):
    o=bpy.data.objects.new(name,None); scene.collection.objects.link(o); o.parent=parent; return o
root=empty('EnchantedKnight'); body=empty('Torso',root); head=empty('HorseHelmet',root); cape=empty('GoldenCape',root)
arms=[empty('LeftArm',root),empty('SwordArm',root)]; legs=[empty('LeftLeg',root),empty('RightLeg',root)]; sword=empty('Falchion',arms[1])
def mesh(name,vs,fs,m,parent=body):
    d=bpy.data.meshes.new(name); d.from_pydata(vs,[],fs); d.update()
    o=bpy.data.objects.new(name,d); scene.collection.objects.link(o); o.parent=parent; d.materials.append(m); return o
def bevel(o,w):
    mod=o.modifiers.new('Single-plane bevel','BEVEL'); mod.width=w; mod.segments=1
    bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=mod.name)
def box(name,loc,size,m,parent=body,w=.04):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc); o=bpy.context.object; o.name=name; o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.parent=parent; o.data.materials.append(m)
    if w: bevel(o,w)
    return o
def loft(name,rings,m,parent=body,n=8):
    vs=[]
    for x,y,z,rx,ry in rings:
        for i in range(n):
            a=2*math.pi*i/n+math.pi/8; vs.append((x+rx*math.cos(a),y+ry*math.sin(a),z))
    fs=[tuple(reversed(range(n)))]
    for j in range(len(rings)-1):
        for i in range(n):
            a=j*n+i; b=j*n+(i+1)%n; fs.append((a,b,b+n,a+n))
    fs.append(tuple((len(rings)-1)*n+i for i in range(n)))
    return mesh(name,vs,fs,m,parent)
def link(name,a,b,r1,r2,m,parent,n=8):
    d=Vector(b)-Vector(a)
    bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r1,radius2=r2,depth=d.length,location=(Vector(a)+Vector(b))*.5)
    o=bpy.context.object; o.name=name; o.rotation_euler=d.to_track_quat('Z','Y').to_euler(); o.parent=parent; o.data.materials.append(m); return o
def plate(name,pts,depth,m,parent=body):
    vs=pts+[(x,y+depth,z) for x,y,z in pts]; n=len(pts)
    fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vs,fs,m,parent)
for i,s in enumerate((-1,1)):
    x=s*.235; p=legs[i]
    box('Boot sole',(x,-.11,.075),(.33,.5,.14),navy,p,.035)
    box('Ivory sabaton',(x,-.155,.19),(.31,.43,.22),ivory,p,.065)
    box('Gold toe',(x,-.345,.18),(.27,.055,.10),gold,p,.02)
    link('Greave',(x,.025,.23),(x,.035,.56),.145,.135,darkBlue,p)
    plate('Shin face',[(x-.105,-.126,.27),(x+.105,-.126,.27),(x+.12,-.13,.5),(x,-.16,.59),(x-.12,-.13,.5)],.045,ivory,p)
    box('Knee gold rim',(x,-.085,.60),(.30,.23,.16),gold,p,.045)
    box('Knee blue face',(x,-.16,.615),(.23,.12,.14),blue,p,.035)
    link('Thigh',(x,.045,.63),(s*.19,.02,.96),.135,.16,navy,p)
    box('Thigh armor',(x,-.10,.79),(.25,.16,.28),blue,p,.045)
loft('Waist undersuit',[(0,0,.85,.28,.19),(0,0,1.2,.28,.20)],navy)
loft('Cuirass',[(0,0,1.02,.31,.22),(0,0,1.30,.42,.245),(0,.015,1.63,.43,.25),(0,.02,1.75,.27,.20)],blue)
loft('Gold belt',[(0,0,1.015,.34,.245),(0,0,1.11,.35,.25)],gold)
box('Belt buckle',(0,-.25,1.065),(.17,.07,.14),goldLight,w=.025)
box('Buckle inset',(0,-.292,1.065),(.075,.014,.065),darkBlue,w=.01)
plate('Ivory chest chevron',[(-.33,-.235,1.55),(0,-.29,1.43),(.33,-.235,1.55),(.27,-.25,1.36),(0,-.31,1.27),(-.27,-.25,1.36)],.035,ivory)
plate('Chest gold crest',[(-.105,-.286,1.62),(0,-.32,1.71),(.105,-.286,1.62),(0,-.32,1.49)],.025,goldLight)
for s in (-1,1):
    plate('Blue tasset',[(s*.04,-.24,1.02),(s*.31,-.20,1.02),(s*.40,-.23,.72),(s*.21,-.30,.69),(s*.07,-.29,.79)],.09,blue)
    plate('Tasset ivory inlay',[(s*.11,-.308,.96),(s*.22,-.305,.95),(s*.30,-.324,.77),(s*.22,-.334,.76)],.015,ivory)
loft('Gold gorget',[(0,.015,1.64,.26,.21),(0,.015,1.77,.285,.23)],gold)
loft('Neck shadow',[(0,.01,1.73,.19,.15),(0,.01,1.93,.20,.16)],navy,head)
for i,s in enumerate((-1,1)):
    p=arms[i]; hy=-.25 if s==1 else -.11
    link('Upper arm',(s*.44,0,1.56),(s*.61,-.01,1.27),.16,.145,navy,p)
    loft('Gold pauldron rim',[(s*.49,.01,1.46,.245,.24),(s*.50,.01,1.62,.25,.25),(s*.43,.01,1.76,.16,.20)],gold,p)
    loft('Cobalt pauldron',[(s*.51,-.015,1.50,.21,.22),(s*.51,-.015,1.63,.215,.22),(s*.44,-.015,1.77,.13,.17)],blue,p)
    box('Shoulder ivory badge',(s*.51,-.224,1.63),(.21,.04,.12),ivory,p,.025)
    link('Elbow hinge',(s*.61,-.02,1.25),(s*.63,-.05,1.20),.15,.15,navy,p)
    link('Forearm vambrace',(s*.63,-.04,1.22),(s*.68,hy,1.05),.18,.145,blue,p)
    link('Wrist cuff',(s*.68,hy,1.09),(s*.69,hy-.04,1.02),.16,.15,gold,p)
    box('Gauntlet',(s*.69,hy-.05,.96),(.24,.25,.23),navy,p,.06)
    box('Gauntlet back',(s*.70,hy-.15,1.00),(.21,.08,.17),darkBlue,p,.025)
    for k in range(3): box('Finger plate',(s*.69+(k-1)*.065,hy-.185,.96),(.055,.055,.10),ivoryShade,p,.013)

# Horse head profile, beveled to read as forged helmet rather than an animal.
profile=[(.20,1.81,.19),(.33,2.06,.25),(.23,2.35,.26),(-.02,2.47,.25),(-.24,2.35,.23),(-.43,2.16,.19),(-.72,2.02,.17),(-.76,1.86,.16),(-.57,1.80,.17),(-.32,1.94,.20),(-.09,1.91,.22)]
vs=[(s*w,y,z) for s in (-1,1) for y,z,w in profile]; n=len(profile)
fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
helmet=mesh('Ivory horse helmet',vs,fs,ivory,head); bevel(helmet,.035)
for s in (-1,1):
    mesh('Dark swept visor',[(s*.252,-.18,2.29),(s*.211,-.43,2.16),(s*.217,-.34,2.12),(s*.263,-.13,2.21)],[(0,1,2,3)],navy,head)
    mesh('Cheek blue panel',[(s*.263,.14,2.13),(s*.239,-.10,2.17),(s*.22,-.27,1.98),(s*.197,.10,1.87)],[(0,1,2),(0,2,3)],darkBlue,head)
    link('Temple medallion',(s*.257,.14,2.15),(s*.305,.14,2.15),.105,.105,gold,head,10)
    link('Temple center',(s*.307,.14,2.15),(s*.315,.14,2.15),.061,.061,goldLight,head)
    plate('Pointed ivory ear',[(s*.13,.00,2.38),(s*.29,.035,2.40),(s*.28,.07,2.77),(s*.13,-.045,2.59)],.10,ivory,head)
    mesh('Ear inset',[(s*.165,-.008,2.47),(s*.255,.025,2.50),(s*.25,.055,2.69)],[(0,1,2)],darkBlue,head)
    mesh('Nostril',[(s*.177,-.65,1.99),(s*.177,-.715,1.97),(s*.177,-.714,1.90),(s*.177,-.65,1.92)],[(0,1,2,3)],navy,head)
link('Muzzle gold band',(-.175,-.735,1.885),(.175,-.735,1.885),.035,.035,gold,head,6)
crest=[(-.01,2.45),(.10,2.65),(.32,2.61),(.48,2.44),(.55,2.23),(.51,2.04),(.40,1.86)]
for j in range(len(crest)-1):
    y,z=crest[j]; yy,zz=crest[j+1]
    vs=[(-.075,y-.07,z-.08),(.075,y-.07,z-.08),(-.10,y+.18,z+.07),(.10,y+.18,z+.07),(-.075,yy+.09,zz),(.075,yy+.09,zz)]
    mesh('Crest segment %02d'%j,vs,[(0,2,4),(1,5,3),(0,1,3,2),(2,3,5,4),(4,5,1,0)],blue if j%2 else lightBlue,head)

# Pleated cape with geometric folds and physical thickness.
vs=[]
for row in range(3):
    w=[.29,.48,.57][row]; z=[1.67,1.08,.43][row]; y=[.25,.43,.66][row]
    for i in range(7): vs.append(((i/6*2-1)*w,y+(.065 if i%2 else 0),z+(.06 if row==2 and i%2 else 0)))
fs=[(r*7+i,r*7+i+1,(r+1)*7+i+1,(r+1)*7+i) for r in range(2) for i in range(6)]
cloth=mesh('Pleated gold cape',vs,fs,gold,cape); cloth.data.materials.append(goldDark)
for p in cloth.data.polygons: p.material_index=p.index%2
mod=cloth.modifiers.new('Cloth thickness','SOLIDIFY'); mod.thickness=.025
bpy.context.view_layer.objects.active=cloth; bpy.ops.object.modifier_apply(modifier=mod.name)
for s in (-1,1): link('Cape clasp',(s*.24,-.07,1.68),(s*.24,-.13,1.68),.065,.065,goldLight,cape)

link('Sword grip',(.68,-.33,.87),(.76,-.33,1.19),.055,.055,darkBlue,sword)
link('Pommel',(.665,-.33,.82),(.69,-.33,.91),.08,.065,gold,sword)
link('Crossguard',(.59,-.33,1.17),(.93,-.33,1.10),.055,.045,goldLight,sword,6)
plate('Curved ivory blade',[(.76,-.33,1.18),(.87,-.33,1.17),(1.06,-.33,1.53),(1.20,-.33,1.95),(1.18,-.33,2.26),(1.08,-.33,2.09),(.98,-.33,1.79),(.88,-.33,1.51)],.045,steel,sword)
plate('Blade bevel',[(.79,-.338,1.19),(.91,-.338,1.52),(1.06,-.338,1.89),(1.18,-.338,2.26),(1.20,-.338,1.95),(1.06,-.338,1.53),(.87,-.338,1.17)],.008,ivoryShade,sword)
for o in root.children_recursive:
    if o.type=='MESH':
        bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm,faces=bm.faces); bm.to_mesh(o.data); bm.free()
root['design']='Original knight based on approved concept; cobalt, ivory, gold.'
root['rig_status']='Editable rigid parts; no skeletal rig or animation clips yet.'

# Studio is outside the model hierarchy and never exported.
box('Studio floor',(0,0,-.065),(200,200,.1),mat('Studio slate','7E9CAD'),None,0)
world=bpy.data.worlds.new('Knight studio world'); world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.32,.40,.55,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.35; scene.world=world
def area(name,loc,power,size):
    d=bpy.data.lights.new(name,'AREA'); d.energy=power; d.shape='DISK'; d.size=size
    o=bpy.data.objects.new(name,d); scene.collection.objects.link(o); o.location=loc
    o.rotation_euler=(Vector((0,0,1.3))-o.location).to_track_quat('-Z','Y').to_euler()
area('Key',(-3,-4,6),550,4); area('Fill',(4,-2,3),250,3); area('Rim',(1,4,5),650,3)
d=bpy.data.cameras.new('KnightCamera'); cam=bpy.data.objects.new('KnightCamera',d); scene.collection.objects.link(cam); scene.camera=cam
cam.location=(4,-7,3.6); cam.rotation_euler=(Vector((.15,0,1.35))-cam.location).to_track_quat('-Z','Y').to_euler()
d.type='ORTHO'; d.ortho_scale=3.65
scene.render.engine='CYCLES'; scene.cycles.samples=32; scene.cycles.use_denoising=True
scene.render.resolution_x=1000; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'; scene.render.image_settings.file_format='PNG'
scene.render.filepath=os.path.join(OUT,'knight-three-quarter.png')
os.makedirs(OUT,exist_ok=True)
bpy.ops.object.select_all(action='DESELECT')
for o in [root]+list(root.children_recursive): o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'enchanted-knight.blend'))
print('EnchantedKnight: %d triangles, %d mesh parts.'%(sum(len(p.vertices)-2 for o in root.children_recursive if o.type=='MESH' for p in o.data.polygons),len([o for o in root.children_recursive if o.type=='MESH'])))
