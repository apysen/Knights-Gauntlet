"""Build three separate ceramic chess prototypes through Unity's Blender bridge.

Original models and the user's current Blender scene are preserved.
"""
import bpy
import math
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/Pieces/CeramicPrototype'
SOURCE = ROOT / 'ArtSource/Models/CeramicPrototype'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
previous = bpy.context.window.scene
scene = bpy.data.scenes.new('KG_CeramicPrototype')
bpy.context.window.scene = scene

def material(name, rgb):
    mat = bpy.data.materials.new('KG_Ceramic_' + name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*rgb, 1)
    shader.inputs['Roughness'].default_value = .84
    shader.inputs['Metallic'].default_value = 0
    shader.inputs['Specular IOR Level'].default_value = .22
    return mat

blue = material('Cornflower', (.26, .43, .73))
clay = material('Terracotta', (.66, .31, .22))
teal = material('SeaGlass', (.31, .60, .56))
ivory = material('UnglazedFoot', (.78, .73, .62))
ink = material('Eye', (.055, .095, .16))

def finish(obj, mat, bevel=0, smooth=True):
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    if bevel:
        mod = obj.modifiers.new('Soft ceramic edges', 'BEVEL')
        mod.width = bevel
        mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for face in obj.data.polygons:
        face.use_smooth = smooth
    return obj

def lathe(name, profile, mat, segments=48):
    verts = [(r*math.cos(i*math.tau/segments), r*math.sin(i*math.tau/segments), z)
             for r, z in profile for i in range(segments)]
    faces = []
    for ring in range(len(profile)-1):
        for i in range(segments):
            a = ring*segments+i
            b = ring*segments+(i+1)%segments
            faces.append((a,b,b+segments,a+segments))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return finish(obj, mat)

def box(name, center, size, mat, bevel=.02):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, mat, bevel, False)

def base(mat):
    return [
        lathe('Quiet ceramic pedestal', [(0,0),(.31,0),(.345,.015),(.36,.045),
            (.36,.085),(.345,.12),(.30,.15),(.255,.17),(0,.17)], mat),
        lathe('Single unglazed foot', [(0,.008),(.344,.008),(.362,.020),
            (.362,.039),(.351,.048),(0,.048)], ivory),
    ]

def extruded_profile(name, profile, half_depth, mat, bevel):
    n = len(profile)
    verts = [(x,y,z) for y in (-half_depth,half_depth) for x,z in profile]
    faces = [tuple(reversed(range(n))), tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    return finish(obj, mat, bevel, False)

def combine(name, parts):
    bpy.ops.object.select_all(action='DESELECT')
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    scene.cursor.location = (0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return obj

models = []
try:
    parts = base(blue)
    parts.append(extruded_profile('Sculpted horse silhouette', [
        (-.23,.15),(.24,.15),(.25,.39),(.21,.64),(.11,.88),(.055,1.115),
        (-.015,1.16),(-.085,1.005),(-.18,.98),(-.32,.86),(-.40,.78),
        (-.38,.68),(-.29,.65),(-.14,.69),(-.12,.52),(-.18,.35),
    ], .135, blue, .028))
    for y in (-.148,.148):
        eye = box('Restrained eye slit', (-.18,y,.91), (.034,.012,.016), ink, .004)
        parts.append(eye)
    models.append(combine('knight', parts))

    parts = base(clay)
    parts.append(lathe('Broad tower', [(0,.15),(.255,.15),(.23,.23),(.205,.31),
        (.205,.57),(.24,.65),(.285,.69),(0,.69)], clay))
    for sign in (-1,1):
        parts.append(box('Square tower wall', (sign*.23,0,.745), (.10,.56,.15), clay, .014))
        parts.append(box('Square tower wall', (0,sign*.23,.745), (.36,.10,.15), clay, .014))
    parts.append(box('Recessed tower interior', (0,0,.695), (.35,.35,.015), ink, .004))
    for x in (-.22,.22):
        for y in (-.22,.22):
            parts.append(box('Corner battlement', (x,y,.865), (.16,.16,.19), clay, .018))
    models.append(combine('rook', parts))

    parts = base(teal)
    parts.append(lathe('One flowing stem', [(0,.15),(.25,.15),(.22,.20),(.17,.28),
        (.135,.45),(.14,.59),(.18,.64),(.225,.665),(.225,.70),(.19,.73),(0,.73)], teal))
    head = lathe('Oversized split mitre', [(0,.70),(.14,.72),(.20,.79),(.225,.89),
        (.19,1.045),(.13,1.24),(.055,1.46),(0,1.56)], teal)
    solid = head.copy()
    solid.data = head.data.copy()
    scene.collection.objects.link(solid)
    cut = box('Mitre slit cutter', (.10,0,1.17), (.095,.8,.65), teal, 0)
    cut.rotation_euler[1] = math.radians(25)
    bpy.context.view_layer.objects.active = head
    boolean = head.modifiers.new('Readable bishop slit', 'BOOLEAN')
    boolean.operation = 'DIFFERENCE'
    boolean.solver = 'EXACT'
    boolean.object = cut
    bpy.ops.object.modifier_apply(modifier=boolean.name)
    bpy.data.objects.remove(cut, do_unlink=True)
    cavity = box('Dark inside of carved slit', (.10,0,1.17), (.085,.02,.65), ink, 0)
    cavity.rotation_euler[1] = math.radians(25)
    bpy.context.view_layer.objects.active = cavity
    interior = cavity.modifiers.new('Keep cavity within mitre', 'BOOLEAN')
    interior.operation = 'INTERSECT'
    interior.object = solid
    bpy.ops.object.modifier_apply(modifier=interior.name)
    cavity.data.materials.clear()
    cavity.data.materials.append(ink)
    for face in cavity.data.polygons:
        face.material_index = 0
    bpy.data.objects.remove(solid, do_unlink=True)
    parts.append(cavity)
    parts.append(head)
    models.append(combine('bishop', parts))

    report = []
    for obj in models:
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        obj.data.calc_loop_triangles()
        bpy.ops.export_scene.fbx(filepath=str(OUT / (obj.name+'.fbx')), use_selection=True,
            object_types={'MESH'}, axis_forward='-Z', axis_up='Y', bake_anim=False,
            add_leaf_bones=False, path_mode='STRIP', use_mesh_modifiers=True)
        report.append({'name':obj.name,'triangles':len(obj.data.loop_triangles),
                       'height':round(obj.dimensions.z,4),'diameter':.72})
    bpy.data.libraries.write(str(SOURCE / 'ceramic-trio.blend'), {scene}, compress=True)
    (SOURCE / 'build-report.json').write_text(json.dumps(report, indent=2))
    print(json.dumps(report))
finally:
    bpy.context.window.scene = previous
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(scene)
