"""Builds the 3D UI icon models (currently the settings gear) and exports one FBX each.

Run headless:
  blender -b --python ArtSource/Icons/build_ui_icons.py -- <fbx_out_dir> [<preview_dir>]

Same conventions as ArtSource/Powerups/build_powerups.py: Z-up, front facing -Y,
about 2 units across, origin at the visual centre. Unity renders these with
PowerupModelIcon, which swaps each material for the chess piece shader and keeps
its colour, so the palette here is what shows in game.
"""
import bpy, bmesh, math, os, sys
from mathutils import Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
FBX_DIR = argv[0] if argv else os.path.dirname(os.path.abspath(__file__))
PREVIEW_DIR = argv[1] if len(argv) > 1 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# Shared with the powerup palette so the HUD reads as one family.
PALETTE = {
    'Steel': ((.72, .75, .80), .8, .3),
    'Gold':  ((1.00, .74, .26), .9, .25),
}
MATS = {}
for name, (color, metal, rough) in PALETTE.items():
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Metallic'].default_value = metal
    bsdf.inputs['Roughness'].default_value = rough
    m.diffuse_color = (*color, 1)
    MATS[name] = m


def link(name, mesh, mat, parent):
    o = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(o)
    mesh.materials.append(MATS[mat])
    o.parent = parent
    return o


def smooth(o, angle=35):
    for p in o.data.polygons: p.use_smooth = True
    if hasattr(o.data, 'set_sharp_from_angle'):
        o.data.set_sharp_from_angle(angle=math.radians(angle))
    return o


def bevel(o, width, segments=3):
    mod = o.modifiers.new('Bevel', 'BEVEL')
    mod.width = width; mod.segments = segments; mod.limit_method = 'ANGLE'
    return o


def ring_prism(name, outer, inner, depth, mat, parent):
    """Extrude the band between two closed XZ outlines (same point count, matched
    angles) along Y. Front face sits at y = -depth/2, toward the camera."""
    assert len(outer) == len(inner)
    bm = bmesh.new()
    n = len(outer)
    fo = [bm.verts.new((x, -depth / 2, z)) for x, z in outer]
    fi = [bm.verts.new((x, -depth / 2, z)) for x, z in inner]
    bo = [bm.verts.new((x, depth / 2, z)) for x, z in outer]
    bi = [bm.verts.new((x, depth / 2, z)) for x, z in inner]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((fo[i], fo[j], fi[j], fi[i]))   # front band
        bm.faces.new((bo[j], bo[i], bi[i], bi[j]))   # back band
        bm.faces.new((fo[j], fo[i], bo[i], bo[j]))   # outer wall (teeth)
        bm.faces.new((fi[i], fi[j], bi[j], bi[i]))   # bore
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
    return link(name, mesh, mat, parent)


def circle(radius, angles):
    return [(radius * math.cos(a), radius * math.sin(a)) for a in angles]


# ── Settings: a chunky steel gear with a gold hub ────────────────────────────
# It shows at about 28 px on the menu, so it has few, broad teeth and a wide
# bore that stays readable at that size.
def settings_gear():
    root = bpy.data.objects.new('SettingsGear', None)
    scene.collection.objects.link(root)

    teeth, tip_r, root_r, bore_r, depth = 8, 1.0, .74, .34, .30
    pitch = 2 * math.pi / teeth
    angles, radii = [], []
    for k in range(teeth):
        c = k * pitch + pitch / 2          # a tooth points straight up at k = 0 offset
        for offset, r in ((-.5, root_r), (-.27, root_r), (-.15, tip_r), (.15, tip_r), (.27, root_r)):
            angles.append(c + offset * pitch); radii.append(r)
    # Valley midpoints are extra points on the root circle so the band stays convex.
    outer = [(r * math.cos(a), r * math.sin(a)) for a, r in zip(angles, radii)]
    inner = circle(bore_r, angles)
    body = ring_prism('GearBody', outer, inner, depth, 'Steel', root)
    smooth(bevel(body, .035))

    # Gold hub collar, proud of both faces, lining the bore.
    seg = 48
    hub_a = [i * 2 * math.pi / seg for i in range(seg)]
    hub = ring_prism('GearHub', circle(.50, hub_a), circle(bore_r - .02, hub_a), depth + .14, 'Gold', root)
    smooth(bevel(hub, .04))
    return root


def bake(root_obj):
    """Apply modifiers and transforms into the meshes under an identity root
    (the powerup script found FBX mangles nested scaled empties otherwise)."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for o in [c for c in root_obj.children_recursive if c.type == 'MESH']:
        mesh = bpy.data.meshes.new_from_object(o.evaluated_get(depsgraph))
        mesh.transform(o.matrix_world)
        o.modifiers.clear()
        o.data = mesh
        o.parent = None
        o.matrix_world = Matrix.Identity(4)
        o.parent = root_obj
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
    cam_data = bpy.data.cameras.new('PreviewCam'); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 2.6
    cam = bpy.data.objects.new('PreviewCam', cam_data); scene.collection.objects.link(cam)
    # Same slight downward tilt the Unity icon uses.
    cam.location = (0, -6 * math.cos(math.radians(12)), 6 * math.sin(math.radians(12)))
    cam.rotation_euler = (math.radians(78), 0, 0)
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


models = {'SettingsGear': settings_gear()}
os.makedirs(FBX_DIR, exist_ok=True)
for name, obj in models.items():
    export(obj, os.path.join(FBX_DIR, name + '.fbx'))
    if PREVIEW_DIR:
        os.makedirs(PREVIEW_DIR, exist_ok=True)
        preview(obj, os.path.join(PREVIEW_DIR, name + '.png'))
print('UI_ICONS_DONE', FBX_DIR)
