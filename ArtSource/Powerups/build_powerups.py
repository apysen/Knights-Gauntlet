"""Builds the three powerup icon models and exports one FBX each.

Run headless:
  blender -b --python ArtSource/Powerups/build_powerups.py -- <fbx_out_dir> [<preview_dir>]

Models are Z-up, front facing -Y, roughly 2 units tall, origin at the visual
centre. Material names are what Unity keys off (see PowerupModelIcon.cs):
Glass gets the transparent film shader, everything else the chess piece shader.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
FBX_DIR = argv[0] if argv else os.path.dirname(os.path.abspath(__file__))
PREVIEW_DIR = argv[1] if len(argv) > 1 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ── Materials ────────────────────────────────────────────────────────────────
PALETTE = {
    'Gold':      ((1.00, .74, .26), .9, .25),
    'Wood':      ((.36, .17, .07), 0, .55),
    'Glass':     ((.78, .92, 1.0), 0, .05),
    'Sand':      ((.95, .76, .42), 0, .8),
    'Gem':       ((.18, .45, 1.0), 0, .1),
    'Feather':   ((.97, .96, .92), 0, .7),
    'Parchment': ((.93, .83, .62), 0, .85),
    'Ink':       ((.72, .12, .10), 0, .7),
    'Brass':     ((.86, .62, .28), .85, .3),
    'Dial':      ((.97, .94, .86), 0, .6),
    'Steel':     ((.72, .75, .80), .8, .3),
}
MATS = {}
for name, (color, metal, rough) in PALETTE.items():
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Metallic'].default_value = metal
    bsdf.inputs['Roughness'].default_value = rough
    m.diffuse_color = (*color, .35 if name == 'Glass' else 1)
    MATS[name] = m

# ── Helpers ──────────────────────────────────────────────────────────────────
def obj_from_mesh(name, mesh, mat, parent):
    o = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(o)
    mesh.materials.append(MATS[mat])
    o.parent = parent
    return o

def smooth(o, angle=40):
    for p in o.data.polygons: p.use_smooth = True
    if hasattr(o.data, 'set_sharp_from_angle'):
        o.data.set_sharp_from_angle(angle=math.radians(angle))
    return o

def lathe(name, profile, mat, parent, segments=48, offset=(0, 0, 0)):
    """Revolve a (radius, z) profile around Z. Points with radius 0 become poles."""
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        if r < 1e-5:
            rings.append([bm.verts.new((offset[0], offset[1], offset[2] + z))])
        else:
            rings.append([bm.verts.new((offset[0] + r * math.cos(a), offset[1] + r * math.sin(a), offset[2] + z))
                          for a in (i * 2 * math.pi / segments for i in range(segments))])
    for a, b in zip(rings, rings[1:]):
        for i in range(segments):
            j = (i + 1) % segments
            if len(a) == 1 and len(b) == 1: continue
            if len(a) == 1: bm.faces.new((a[0], b[j], b[i]))
            elif len(b) == 1: bm.faces.new((a[i], a[j], b[0]))
            else: bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
    return smooth(obj_from_mesh(name, mesh, mat, parent))

def prism(name, outline, depth, mat, parent, bevel=.03, y=0.0):
    """Extrude an XZ outline along Y (front face at y - depth/2), then bevel."""
    bm = bmesh.new()
    front = [bm.verts.new((x, y - depth / 2, z)) for x, z in outline]
    back = [bm.verts.new((x, y + depth / 2, z)) for x, z in outline]
    n = len(outline)
    bm.faces.new(front); bm.faces.new(list(reversed(back)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], back[i], back[j], front[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
    o = obj_from_mesh(name, mesh, mat, parent)
    if bevel:
        mod = o.modifiers.new('Bevel', 'BEVEL'); mod.width = bevel; mod.segments = 3
        mod.limit_method = 'ANGLE'
    return smooth(o, 35)

def primitive(op, name, mat, parent, location=(0, 0, 0), rotation=(0, 0, 0), scale=(1, 1, 1), **kw):
    op(location=location, rotation=rotation, **kw)
    o = bpy.context.object; o.name = name; o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(MATS[mat]); o.parent = parent
    return smooth(o)

def root(name):
    o = bpy.data.objects.new(name, None); scene.collection.objects.link(o); return o

# ── Chronokinesis: hourglass ─────────────────────────────────────────────────
def hourglass():
    r = root('Hourglass')
    cap = [(0, 0), (.74, 0), (.78, .03), (.78, .09), (.74, .11), (.70, .11), (.70, .14),
           (.66, .17), (.62, .17), (.62, .21), (.0, .21)]
    lathe('CapBottom', cap, 'Gold', r)
    lathe('CapTop', [(rr, 2.0 - z) for rr, z in cap], 'Gold', r)
    # Turned pillars: beads at each end, a slim waist and a central collar.
    pillar = [(0, .21), (.075, .21), (.095, .25), (.075, .29), (.055, .33), (.05, .55), (.06, .8),
              (.05, .93), (.085, .97), (.09, 1.0), (.085, 1.03), (.05, 1.07), (.06, 1.2), (.05, 1.45),
              (.055, 1.67), (.075, 1.71), (.095, 1.75), (.075, 1.79), (0, 1.79)]
    for i in range(3):
        a = math.radians(90 + i * 120)
        lathe('Pillar%d' % i, pillar, 'Wood', r, segments=20, offset=(.58 * math.cos(a), .58 * math.sin(a), 0))
    bulb = [(0, .23), (.2, .24), (.36, .31), (.45, .44), (.47, .6), (.42, .76), (.27, .89), (.09, .975),
            (.055, 1.0), (.09, 1.025), (.27, 1.11), (.42, 1.24), (.47, 1.4), (.45, 1.56), (.36, 1.69),
            (.2, 1.76), (0, 1.77)]
    lathe('Glass', bulb, 'Glass', r, segments=48)
    # Sand settled in the lower bulb (a heaped cone) and draining from the upper.
    lathe('SandBottom', [(0, .25), (.2, .26), (.34, .32), (.42, .43), (.43, .5), (.25, .57), (0, .63)], 'Sand', r)
    lathe('SandTop', [(0, .99), (.06, 1.015), (.24, 1.11), (.38, 1.23), (.4, 1.27), (0, 1.27)], 'Sand', r)
    lathe('SandStream', [(0, .62), (.014, .62), (.014, 1.0), (0, 1.0)], 'Sand', r, segments=10)
    r.location = (0, 0, -1.0)
    return r

# ── Divine Intervention: winged cross with halo ──────────────────────────────
def divine_cross():
    r = root('DivineCross')
    c, w0, w1 = .17, .17, .33  # centre half-width, arm half-width at root and tip
    up, side, down = .95, .82, 1.15
    outline = [
        (-w0, c), (-w1, up), (w1, up), (w0, c),              # top arm
        (c, w0), (side, w1), (side, -w1), (c, -w0),          # right arm
        (w0, -c), (w1, -down), (-w1, -down), (-w0, -c),      # bottom arm
        (-c, -w0), (-side, -w1), (-side, w1), (-c, w0),      # left arm
    ]
    prism('Cross', outline, .2, 'Gold', r, bevel=.035)
    # Raised inner panel for a second level of relief.
    inner = [(x * .78, z * .86 if z > 0 else z * .9) for x, z in outline]
    prism('CrossInlay', inner, .08, 'Gold', r, bevel=.02, y=-.12)
    primitive(bpy.ops.mesh.primitive_uv_sphere_add, 'Gem', 'Gem', r, location=(0, -.17, 0),
              scale=(.15, .08, .15), segments=24, ring_count=12)
    # Halo ring behind, and gold rays between the arms.
    primitive(bpy.ops.mesh.primitive_torus_add, 'Halo', 'Gold', r, location=(0, .16, 0),
              rotation=(math.pi / 2, 0, 0), major_radius=.62, minor_radius=.045,
              major_segments=64, minor_segments=12)
    # Rays fill the gaps between the arms: a long one on each diagonal, flanked by two short.
    for i in range(12):
        a = math.radians(45 + (i // 3) * 90 + (i % 3 - 1) * 17)
        tip = 1.12 if i % 3 == 1 else .86
        ray = [(-.09, .45), (0, tip), (.09, .45)]
        o = prism('Ray%d' % i, ray, .05, 'Gold', r, bevel=.008, y=.22)
        o.rotation_euler = (0, -a + math.pi / 2, 0)
    # Wings: overlapping feathers fanned up and out behind the side arms.
    for side_sign in (-1, 1):
        for i in range(7):
            t = i / 6
            length = .62 - .26 * t
            angle = math.radians(20 + 62 * t)
            base = Vector((side_sign * (.55 + .05 * i), .1 + .012 * i, .05 + .03 * i))
            o = primitive(bpy.ops.mesh.primitive_uv_sphere_add, 'Feather', 'Feather', r,
                          scale=(.075, .02, length * .5), segments=16, ring_count=10)
            # Pivot the feather from its root, fanning upward and outward.
            o.data.transform(Matrix.Translation((0, 0, length * .5)))
            o.rotation_euler = (0, side_sign * angle, 0)
            o.location = base
    r.location = (0, 0, .05)
    return r

# ── Pathfinder: explorer's map with compass ──────────────────────────────────
def pathfinder():
    r = root('PathfinderMap')
    # Parchment sheet: flat in the middle, curled into a roll at both ends.
    half, flat, radius, depth = 1.05, .62, .13, .78
    cols, rows = 72, 14
    bm = bmesh.new()
    grid = []
    for j in range(rows + 1):
        y = -depth / 2 + depth * j / rows
        row = []
        for i in range(cols + 1):
            u = -half + 2 * half * i / cols
            s = abs(u) - flat
            if s <= 0:
                x, z = u, 0.0
            else:
                ang = s / radius
                x = math.copysign(flat + radius * math.sin(ang), u)
                z = radius * (1 - math.cos(ang))
            # A gentle wave so the paper is not perfectly flat.
            z += .012 * math.sin(u * 7) * math.cos(y * 5)
            row.append(bm.verts.new((x, y, z)))
        grid.append(row)
    for j in range(rows):
        for i in range(cols):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    mesh = bpy.data.meshes.new('Map'); bm.to_mesh(mesh); bm.free()
    paper = smooth(obj_from_mesh('Map', mesh, 'Parchment', r), 60)
    sol = paper.modifiers.new('Solidify', 'SOLIDIFY'); sol.thickness = .014; sol.offset = -1
    # Dashed route across the sheet, ending at an X.
    route = [(-.52, -.22), (-.38, -.05), (-.2, .02), (-.05, -.08), (.08, -.18), (.22, -.1), (.3, .08)]
    pts = []
    for a, b in zip(route, route[1:]):
        for k in range(4):
            t = k / 4
            pts.append(Vector((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, .02)))
    for k in range(0, len(pts) - 1, 2):
        a, b = pts[k], pts[k + 1]
        mid, d = (a + b) / 2, b - a
        o = primitive(bpy.ops.mesh.primitive_cube_add, 'Route', 'Ink', r, location=mid,
                      scale=(d.length * .5, .014, .006))
        o.rotation_euler = (0, 0, math.atan2(d.y, d.x))
    for a in (45, -45):
        o = primitive(bpy.ops.mesh.primitive_cube_add, 'Mark', 'Ink', r, location=(.38, .16, .02),
                      scale=(.075, .016, .007))
        o.rotation_euler = (0, 0, math.radians(a))
    # Brass compass resting on the upper-left corner of the map, clear of the route.
    compass = root('Compass'); compass.parent = r
    compass.location = (-.4, .17, .015); compass.scale = (.72, .72, .72)
    cx, cy, cz = 0, 0, 0
    lathe('CompassCase', [(0, 0), (.25, 0), (.27, .015), (.27, .065), (.25, .085), (.23, .075), (.22, .05),
                          (0, .05)], 'Brass', compass, segments=48, offset=(cx, cy, cz))
    lathe('CompassDial', [(0, .052), (.215, .052), (.215, .056), (0, .056)], 'Dial', compass, offset=(cx, cy, cz))
    lathe('CompassGlass', [(0, .08), (.22, .072), (.23, .07), (0, .07)], 'Glass', compass, offset=(cx, cy, cz))
    primitive(bpy.ops.mesh.primitive_torus_add, 'CompassLoop', 'Brass', compass, location=(cx, cy + .3, cz + .04),
              rotation=(0, math.pi / 2, 0), major_radius=.05, minor_radius=.014, major_segments=24, minor_segments=8)
    for k in range(4):
        a = k * math.pi / 2
        o = primitive(bpy.ops.mesh.primitive_cube_add, 'Tick', 'Ink' if k == 0 else 'Steel', compass,
                      location=(cx + .17 * math.sin(a), cy + .17 * math.cos(a), cz + .058),
                      scale=(.012, .03, .004))
        o.rotation_euler = (0, 0, -a)
    for name, mat, sign in (('NeedleNorth', 'Ink', 1), ('NeedleSouth', 'Steel', -1)):
        bm = bmesh.new()
        tip = bm.verts.new((cx, cy + sign * .16, cz + .062))
        l = bm.verts.new((cx - .028, cy, cz + .062)); rr = bm.verts.new((cx + .028, cy, cz + .062))
        top = bm.verts.new((cx, cy, cz + .072))
        bm.faces.new((tip, l, top)); bm.faces.new((tip, top, rr)); bm.faces.new((tip, rr, l))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
        o = obj_from_mesh(name, mesh, mat, compass)
        o.rotation_euler = (0, 0, 0)
    # Tip the whole map up toward the viewer so it reads in a small icon.
    r.rotation_euler = (math.radians(58), 0, math.radians(-8))
    return r

# ── Export ───────────────────────────────────────────────────────────────────
def bake(root_obj):
    """Apply modifiers and every transform into the meshes, flattening the hierarchy
    to one level under an identity root. FBX export otherwise mangles nested scaled
    empties (the compass came out squashed edge-on in Unity)."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for o in [c for c in root_obj.children_recursive if c.type == 'MESH']:
        mesh = bpy.data.meshes.new_from_object(o.evaluated_get(depsgraph))
        mesh.transform(o.matrix_world)
        o.modifiers.clear()
        o.data = mesh
        o.parent = None
        o.matrix_world = Matrix.Identity(4)
        o.parent = root_obj
    for c in [c for c in root_obj.children_recursive if c.type == 'EMPTY']:
        bpy.data.objects.remove(c)
    root_obj.matrix_world = Matrix.Identity(4)
    for o in root_obj.children: o.matrix_parent_inverse = Matrix.Identity(4)

def export(root_obj, path):
    bake(root_obj)
    bpy.ops.object.select_all(action='DESELECT')
    root_obj.select_set(True)
    for c in root_obj.children_recursive: c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'},
                             use_mesh_modifiers=True, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='FACE', add_leaf_bones=False)

def preview(root_obj, path):
    for o in scene.objects:
        o.hide_render = not (o == root_obj or o in root_obj.children_recursive)
    cam_data = bpy.data.cameras.new('PreviewCam'); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 2.6
    cam = bpy.data.objects.new('PreviewCam', cam_data); scene.collection.objects.link(cam)
    cam.location = (0, -6, 0); cam.rotation_euler = (math.radians(90), 0, 0)
    scene.camera = cam
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_specular_highlight = True
    scene.render.film_transparent = True
    scene.render.resolution_x = scene.render.resolution_y = 384
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)

models = {'Hourglass': hourglass(), 'DivineCross': divine_cross(), 'PathfinderMap': pathfinder()}
# Keep them apart while building previews, then export each at the origin.
os.makedirs(FBX_DIR, exist_ok=True)
for name, obj in models.items():
    export(obj, os.path.join(FBX_DIR, name + '.fbx'))
    if PREVIEW_DIR:
        os.makedirs(PREVIEW_DIR, exist_ok=True)
        preview(obj, os.path.join(PREVIEW_DIR, name + '.png'))
print('POWERUPS_DONE', FBX_DIR)
