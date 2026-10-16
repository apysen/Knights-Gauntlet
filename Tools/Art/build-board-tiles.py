"""Run through Unity's Blender bridge. Builds texture-free, quiet board tiles."""
import bpy
from pathlib import Path

ROOT = Path("D:/GameDev/Knight's Gauntlet App/My project")
OUT = ROOT / "Assets/Resources/BoardModels"
SOURCE = ROOT / "ArtSource/Models/BoardTiles"
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)

scene = bpy.data.scenes.get("KG_BoardTiles") or bpy.data.scenes.new("KG_BoardTiles")
bpy.context.window.scene = scene
for obj in list(scene.objects):
    bpy.data.objects.remove(obj, do_unlink=True)

def material(name, color):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = 0.86
    return mat

sage = material("KG Emerald Enamel", (0.075, 0.205, 0.135))
earth = material("KG Tile Bronze", (0.19, 0.135, 0.055))
oak = material("KG Warm Oak", (0.255, 0.125, 0.055))
oak_edge = material("KG Oak Edge", (0.065, 0.035, 0.018))
stone = material("KG Moonstone Enamel", (0.18, 0.245, 0.34))
stone_edge = material("KG Stone Edge", (0.032, 0.047, 0.085))
iron = material("KG Bridge Iron", (0.025, 0.048, 0.095))
walnut = material("KG Frame Walnut", (0.014, 0.025, 0.055))
rim = material("KG Frame Enamel", (0.025, 0.05, 0.105))
brass = material("KG Frame Gold", (0.74, 0.47, 0.12))
hazard = material("KG Garnet Enamel", (0.24, 0.025, 0.035))
ivory = material("KG Hazard Ivory", (0.83, 0.76, 0.60))
jade = material("KG Garden Jade", (.055,.29,.16))
water = material("KG Moat Enamel", (.025,.12,.22))

def box(name, center, size, mat, bevel=0.015):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new("Small readable bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

def combine(name, parts):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return obj

models = []
models.append(combine("GrassTile", [
    box("Ground slab", (0, 0, -0.095), (0.97, 0.97, 0.15), earth, 0.025),
    box("Quiet grass surface", (0, 0, -0.025), (0.945, 0.945, 0.05), sage, 0.023),
]))
parts = [box("Bridge slab", (0, 0, -0.09), (0.97, 0.97, 0.14), oak_edge, 0.022)]
for i in range(3):
    parts.append(box("Broad plank", ((i-1)*0.315, 0, -0.02), (0.307, 0.945, 0.04), oak, 0.01))
models.append(combine("WoodTile", parts))
models.append(combine("StoneTile", [
    box("Stone slab", (0, 0, -0.09), (0.97, 0.97, 0.14), earth, 0.024),
    box("Quiet moonstone surface", (0, 0, -0.025), (0.945, 0.945, 0.05), stone, 0.025),
]))
models.append(combine("BridgeRail", [
    box("Iron curb", (0, 0, 0.065), (0.055, 0.98, 0.10), iron, 0.008),
    box("Iron post", (0, -0.34, 0.09), (0.075, 0.075, 0.18), iron, 0.008),
    box("Iron post", (0, 0.34, 0.09), (0.075, 0.075, 0.18), iron, 0.008),
]))

# Fragile tiles use four physical stone fragments and broad fissures, not a decal.
def fragment(name, polygon):
    area = sum(polygon[i][0]*polygon[(i+1)%len(polygon)][1] -
               polygon[(i+1)%len(polygon)][0]*polygon[i][1] for i in range(len(polygon)))
    if area < 0:
        polygon = list(reversed(polygon))
    n = len(polygon)
    vertices = [(x,y,z) for z in (-0.015,0.012) for x,y in polygon]
    faces = [tuple(reversed(range(n))), tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(stone)
    mesh.materials.append(stone_edge)
    for face in mesh.polygons:
        face.material_index = 0 if face.index == 1 else 1
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj

models.append(combine("CrackedTile", [
    box("Fissure bed", (0,0,-0.004), (0.925,0.925,0.016), stone_edge, 0.002),
    fragment("North west fragment", [(-.46,.46),(-.08,.46),(.015,.09),(-.46,-.015)]),
    fragment("North east fragment", [(-.05,.46),(.46,.46),(.46,.045),(.055,.08)]),
    fragment("South west fragment", [(-.46,-.05),(.005,.045),(-.07,-.46),(-.46,-.46)]),
    fragment("South east fragment", [(.045,.035),(.46,.01),(.46,-.46),(-.035,-.46)]),
]))

parts = [box("Board foundation", (0,0,-.22), (5.42,10.42,.30), walnut, .07)]
for x in (-2.59,2.59):
    parts.append(box("Side rim", (x,0,-.045), (.18,10.38,.17), rim, .035))
    parts.append(box("Side brass inlay", (x,0,.043), (.022,10.10,.012), brass, .004))
for y in (-5.09,5.09):
    parts.append(box("End rim", (0,y,-.045), (5.12,.18,.17), rim, .035))
    parts.append(box("End brass inlay", (0,y,.043), (5.10,.022,.012), brass, .004))
for x in (-2.59,2.59):
    for y in (-5.09,5.09):
        parts.append(box("Brass corner", (x,y,.037), (.12,.12,.025), brass, .02))
for x in (-2.59,2.59):
    for y in (-4,-2,0,2,4):
        stud = box("Gold diamond inlay", (x,y,.047), (.085,.085,.018), brass, .008)
        stud.rotation_euler.z = .785398
        parts.append(stud)
models.append(combine("BoardFrame", parts))

parts = []
for radius, depth, z, mat in ((.33,.045,.0225,brass),(.292,.018,.053,hazard)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=radius, depth=depth, location=(0,0,z))
    obj = bpy.context.object
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("Plaque bevel", 'BEVEL')
    bevel.width = .009
    bevel.segments = 2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    parts.append(obj)
for angle in (-.785398,.785398):
    obj = box("Quiet hazard cross", (0,0,.068), (.32,.045,.015), brass, .009)
    obj.rotation_euler.z = angle
    parts.append(obj)
models.append(combine("HazardPlaque", parts))

# Small crafted dioramas live outside the playable squares.
def cylinder(name, xyz, radius, depth, mat, sides=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=depth, location=xyz)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    mod = obj.modifiers.new("Turned edge", 'BEVEL')
    mod.width = .012
    mod.segments = 2
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

parts = []
for side in (-1,1):
    x = side*2.82
    parts.append(box("Garden border bed", (x,0,-.08), (.26,10.1,.15), walnut, .025))
    for y in (-4.65,-2.35,0,2.35,4.65):
        parts.append(cylinder("Gold garden urn", (x,y,.075), .14,.20,brass))
        parts.append(cylinder("Enamel planter", (x,y,.12), .115,.16,rim))
        for dx,dy,dz in ((0,0,.29),(-.05,.04,.24),(.055,-.04,.25)):
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.115,location=(x+dx,y+dy,dz))
            obj=bpy.context.object
            obj.scale=(.65,.85,1.3)
            obj.data.materials.append(jade)
            parts.append(obj)
models.append(combine("GardenSurround",parts))

parts = []
for side in (-1,1):
    x=side*2.84
    parts.append(box("Inlaid moat", (x,0,-.10), (.30,10.1,.10),water,.025))
    parts.append(box("Gold bridge handrail", (x,0,.25), (.045,10.12,.055),brass,.012))
    for y in (-4.7,-2.35,0,2.35,4.7):
        parts.append(box("Enamel bridge pier",(x,y,.06),(.15,.15,.45),rim,.02))
        parts.append(cylinder("Gold pier cap",(x,y,.30),.10,.08,brass))
    for y in (-3.5,-1.2,1.2,3.5):
        parts.append(box("Moat glint",(x,y,-.04),(.18,.025,.012),ivory,.005))
models.append(combine("BridgeSurround",parts))

parts = []
for side in (-1,1):
    x=side*2.83
    parts.append(box("Castle parapet",(x,0,-.04),(.27,10.15,.25),stone_edge,.025))
    parts.append(box("Parapet gold fillet",(x,0,.09),(.19,10.12,.025),brass,.008))
    for y in (-4.65,-2.3,0,2.3,4.65):
        parts.append(cylinder("Miniature castle turret",(x,y,.14),.17,.43,rim))
        parts.append(cylinder("Turret collar",(x,y,.37),.18,.055,brass))
        for dx,dy in ((-.10,-.10),(-.10,.10),(.10,-.10),(.10,.10)):
            parts.append(box("Crenellation",(x+dx,y+dy,.43),(.075,.075,.12),rim,.009))
        parts.append(box("Turret enamel jewel",(x-side*.16,y,.20),(.025,.065,.08),water,.008))
models.append(combine("CastleSurround",parts))

for obj in models:
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(OUT / (obj.name + '.fbx')),
        use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, bake_anim=False, add_leaf_bones=False)

# A separate editable source file; leave the user's original Blender scene intact.
for i, obj in enumerate(models):
    obj.location.x = (i - 1.5) * 1.3
bpy.data.libraries.write(str(SOURCE / 'board-tiles.blend'), {scene}, fake_user=True)
print("Created",len(models),"royal board models and editable board-tiles.blend")
