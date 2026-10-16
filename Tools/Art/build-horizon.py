"""Run in Blender through Unity's Blender bridge; exports a separate horizon kit."""
import bpy, bmesh, math
from pathlib import Path
from mathutils import Vector

ROOT = Path("D:/GameDev/Knight's Gauntlet App/My project")
OUT = ROOT / 'Assets/Resources/Horizon'
SOURCE = ROOT / 'ArtSource/Models/Horizon'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.get('KG_Horizon') or bpy.data.scenes.new('KG_Horizon')
bpy.context.window.scene = scene
for obj in list(scene.objects):
    bpy.data.objects.remove(obj, do_unlink=True)

def mat(name, rgb):
    m = bpy.data.materials.get('Horizon ' + name) or bpy.data.materials.new('Horizon ' + name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    node = m.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*rgb, 1)
    node.inputs['Roughness'].default_value = .85
    return m

stone = mat('Limestone', (.37,.43,.42))
edge = mat('Dark Slate', (.045,.085,.12))
roof = mat('Patinated Roof', (.075,.20,.21))
gold = mat('Brass', (.57,.36,.105))
leaf = mat('Laurel', (.075,.21,.145))
leaf_light = mat('Sage', (.15,.29,.205))
earth = mat('Garden Ground', (.12,.19,.15))
path = mat('Garden Path', (.32,.34,.275))
water = mat('Reflecting Pool', (.055,.19,.23))
distant = mat('Distant Hills', (.17,.25,.28))

def box(name, xyz, size, material, bevel=.03):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz)
    o = bpy.context.object
    o.name = name
    o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(material)
    if bevel:
        mod = o.modifiers.new('Soft stone edges', 'BEVEL'); mod.width=bevel; mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def cylinder(name, xyz, radius, height, material, sides=12, top=None):
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=radius, radius2=radius if top is None else top,
                                    depth=height, location=xyz)
    o=bpy.context.object; o.name=name; o.data.materials.append(material)
    return o

def join(name, parts):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join()
    o=bpy.context.object; o.name=name
    scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return o

def subdivide(obj, cuts, long_only=False):
    bm=bmesh.new(); bm.from_mesh(obj.data)
    edges=[e for e in bm.edges if not long_only or e.calc_length()>2]
    bmesh.ops.subdivide_edges(bm, edges=edges, cuts=cuts, use_grid_fill=True)
    bm.to_mesh(obj.data); bm.free(); obj.data.update()

models=[]
for name in ('GrassTile','WoodTile','StoneTile','BridgeRail'):
    original=bpy.data.objects.get(name)
    if original is None: raise RuntimeError('Missing original board mesh: '+name)
    o=original.copy(); o.data=original.data.copy(); scene.collection.objects.link(o)
    o.name='Rolling'+name; o.location=(0,0,0)
    subdivide(o,5)
    models.append(o)

# An open U frame. Long strips have enough vertices to bend smoothly in Unity.
parts=[box('Bottom foundation',(0,-4.88,-.22),(5.42,.7,.30),edge)]
for x in (-2.59,2.59):
    for name,z,w,h,m in [('Side foundation',-.22,.24,.30,edge),('Open side rail',-.045,.18,.17,edge),
                          ('Continuous brass inlay',.043,.025,.016,gold)]:
        o=box(name,(x,1.65,z),(w,13.7,h),m,.008)
        subdivide(o,72,True); parts.append(o)
parts.append(box('Bottom rail',(0,-5.09,-.045),(5.12,.18,.17),edge))
parts.append(box('Bottom brass inlay',(0,-5.09,.043),(5.1,.025,.016),gold,.005))
models.append(join('OpenRollingFrame',parts))

# Solid cylinder beneath the belt provides depth occlusion at its crest.
bpy.ops.mesh.primitive_cylinder_add(vertices=96, radius=2.395, depth=5.1,
    location=(0,3,-2.4), rotation=(0,math.pi/2,0))
roller=bpy.context.object; roller.name='HorizonDrum'; roller.data.materials.append(edge)
models.append(roller)

# Environment is built in Blender Z-up, then stood onto the Unity XY board.
# Positive Blender Y is beyond the horizon. Ground is slightly below the belt.
parts=[box('Garden terrace',(0,10,-1.35),(24,9,.3),earth,.12)]
parts.append(box('Central avenue',(0,10,-1.18),(2.4,8,.045),path,.02))
for side in (-1,1):
    x=side*3.8
    parts.append(box('Reflecting pool rim',(x,8.4,-1.10),(2.0,3.0,.18),stone))
    parts.append(box('Still water',(x,8.4,-.997),(1.78,2.78,.025),water,.03))
    for y in (6.4,10.3,12.3):
        parts.append(box('Clipped hedge',(side*6,y,-.57),(1.6,1.25,1.2),leaf,.23))
        parts.append(box('Hedge plinth',(side*6,y,-1.12),(1.8,1.45,.18),stone))
    for x2,y in ((side*2.8,6.6),(side*5,11.2),(side*8,8.8)):
        parts.append(cylinder('Tree planter',(x2,y,-.9),.45,.5,stone))
        parts.append(cylinder('Tree trunk',(x2,y,-.2),.10,1.1,edge,8))
        for z,r in ((.45,.72),(1.1,.58),(1.65,.4)):
            parts.append(cylinder('Cypress crown',(x2,y,z),r,1.25,leaf_light,9,top=.12))

# Low balustrades lead the eye toward a distant gatehouse.
for side in (-1,1):
    x=side*9
    parts.append(box('Terrace parapet',(x,10,-.55),(.35,8,1.3),stone))
    parts.append(box('Parapet coping',(x,10,.13),(.48,8,.12),path))
for side in (-1,1):
    x=side*3.2
    parts.append(box('Gatehouse wing',(x,14.0,.45),(3.3,2.3,3.3),stone,.09))
    parts.append(box('Wing cornice',(x,14.0,2.15),(3.5,2.5,.22),path))
    parts.append(cylinder('Slate pavilion roof',(x,14.0,2.65),2.15,1.0,roof,4,top=.2))
    for wx in (-.9,0,.9):
        parts.append(box('Recessed window',(x+wx,12.83,.75),(.28,.035,.85),edge,.07))
    towerx=side*5.25
    parts.append(cylinder('Distant gate tower',(towerx,14.2,1),.75,4.5,stone,12))
    parts.append(cylinder('Tower eave',(towerx,14.2,3.3),.85,.20,path))
    parts.append(cylinder('Tower roof',(towerx,14.2,3.95),.95,1.2,roof,12,top=.06))
    parts.append(cylinder('Tower finial',(towerx,14.2,4.68),.08,.35,gold,8,top=0))
parts.append(box('Gate lintel',(0,14,2.1),(3.2,1.9,.6),stone,.10))
parts.append(box('Gate roof',(0,14,2.49),(3.4,2.1,.18),roof))
for x in (-1.35,1.35): parts.append(box('Gate pier',(x,14,.2),(.45,1.9,3.2),stone))
for x,y,s in ((-13,18,4),(-7,19,3),(7,19,3),(13,18,4)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=(x,y,-.7))
    o=bpy.context.object; o.name='Distant hill'; o.scale=(s*1.8,s,s*.7); o.data.materials.append(distant); parts.append(o)
models.append(join('RoyalGardenHorizon',parts))

for obj in models:
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.fbx(filepath=str(OUT/(obj.name+'.fbx')),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
bpy.data.libraries.write(str(SOURCE/'royal-garden-horizon.blend'),{scene},fake_user=True)
print('Exported',len(models),'horizon models and editable source; preserved original board scene.')
