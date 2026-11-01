"""Builds the Ink Porcelain Nightrider inside ink-porcelain-set.blend.

Run inside Blender with ink-porcelain-set.blend open. Reuses the knight's
pedestal so the footprint matches the rest of the set, then adds a sleeker
lunging horse head, a gold crescent moon crest, a swept spiked mane and three
stepping diamonds (the repeated knight leap) in midnight indigo paint.
"""
import bpy, bmesh, math
from mathutils import Vector

NAME = "nightrider"
BODY_T = 0.135      # half thickness of the ivory silhouette (knight uses ~0.136)
MANE_T = 0.155      # half thickness of the painted mane slab (same as knight)
CREST_T = 0.05      # half thickness of the gold crescent
DECAL_IN, DECAL_OUT = 0.131, 0.139
CRACK_IN, CRACK_OUT = 0.133, 0.142

MAT = {n: bpy.data.materials[n + ".001"] for n in
       ("InkPorcelain_Ink", "InkPorcelain_Gold", "InkPorcelain_Ivory")}


def midnight_material():
    name = "InkPorcelain_PaintMidnight"
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials["InkPorcelain_PaintBlue.001"].copy()
        m.name = name
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    col = (0.052, 0.040, 0.235, 1.0)  # ~#403A86 in sRGB
    if bsdf: bsdf.inputs["Base Color"].default_value = col
    m.diffuse_color = col
    return m


MAT_ORDER = [MAT["InkPorcelain_Ink"], MAT["InkPorcelain_Gold"], MAT["InkPorcelain_Ivory"], midnight_material()]
INK, GOLD, IVORY, PAINT = range(4)


def slab(bm, pts, y0, y1, mat, bevel=0.0, segs=1):
    """Extrude an XZ outline between y0 and y1, optionally bevel it."""
    vs0 = [bm.verts.new((x, y0, z)) for x, z in pts]
    vs1 = [bm.verts.new((x, y1, z)) for x, z in pts]
    faces = [bm.faces.new(vs0[::-1]), bm.faces.new(vs1)]
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        faces.append(bm.faces.new((vs0[i], vs0[j], vs1[j], vs1[i])))
    for f in faces: f.material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    if bevel > 0:
        edges = list({e for f in faces for e in f.edges})
        res = bmesh.ops.bevel(bm, geom=edges + vs0 + vs1, offset=bevel, offset_type='OFFSET',
                              profile_type='SUPERELLIPSE', segments=segs, profile=0.5,
                              affect='EDGES', clamp_overlap=True)
        for f in res.get("faces", []): f.material_index = mat


def both_sides(bm, pts, inner, outer, mat, bevel=0.0, segs=1):
    slab(bm, pts, inner, outer, mat, bevel, segs)
    slab(bm, pts, -outer, -inner, mat, bevel, segs)


def ribbon(polyline, width):
    """Turn an XZ polyline into a thin closed outline (for gold cracks)."""
    left, right = [], []
    for i, p in enumerate(polyline):
        p = Vector(p)
        a = Vector(polyline[max(i - 1, 0)]); b = Vector(polyline[min(i + 1, len(polyline) - 1)])
        d = (b - a).normalized()
        n = Vector((-d.y, d.x)) * width * 0.5
        left.append(tuple(p + n)); right.append(tuple(p - n))
    return left + right[::-1]


def crescent(cx, cz, R, d, r, tilt_deg, steps=18):
    s0 = (R * R + d * d - r * r) / (2 * R * d)
    a = math.asin(s0)
    phi1 = math.atan2(R * math.sin(a) - d, R * math.cos(a))
    pts = []
    for i in range(steps + 1):  # outer arc, right tip -> bottom -> left tip
        t = a - (math.pi + 2 * a) * i / steps
        pts.append((R * math.cos(t), R * math.sin(t)))
    for i in range(1, steps):   # inner arc, left tip -> bottom -> right tip
        t = (-math.pi - phi1) + (math.pi + 2 * phi1) * i / steps
        pts.append((r * math.cos(t), d + r * math.sin(t)))
    c, s = math.cos(math.radians(tilt_deg)), math.sin(math.radians(tilt_deg))
    return [(cx + x * c - z * s, cz + x * s + z * c) for x, z in pts]


def diamond(cx, cz, h, w=None):
    w = w or h * 0.8
    return [(cx, cz - h), (cx + w, cz), (cx, cz + h), (cx - w, cz)]


def build():
    old = bpy.data.objects.get(NAME)
    if old: bpy.data.objects.remove(old, do_unlink=True)
    if NAME in bpy.data.meshes: bpy.data.meshes.remove(bpy.data.meshes[NAME])

    bm = bmesh.new()

    # 1. Pedestal: copied from the knight (ink band, gold rings, ivory dome).
    kb = bmesh.new(); kb.from_mesh(bpy.data.objects["knight"].data)
    drop = [f for f in kb.faces if max(v.co.z for v in f.verts) > 0.2035]
    bmesh.ops.delete(kb, geom=drop, context='FACES')
    tmp = bpy.data.meshes.new("_ped"); kb.to_mesh(tmp); kb.free()
    bm.from_mesh(tmp); bpy.data.meshes.remove(tmp)

    # 2. Ivory body: a lunging horse head, muzzle reaching forward (+X).
    body = [(-0.25, 0.19), (0.25, 0.19), (0.27, 0.28), (0.125, 0.475),
            (0.105, 0.58), (0.155, 0.64), (0.33, 0.70), (0.35, 0.80),
            (0.225, 0.955), (0.12, 1.05), (0.02, 1.10), (-0.09, 1.07),
            (-0.17, 0.97), (-0.235, 0.80), (-0.265, 0.60), (-0.27, 0.40)]
    slab(bm, body, -BODY_T, BODY_T, IVORY, 0.035, 4)

    # 3. Swept mane with flame spikes trailing backwards.
    mane = [(0.00, 1.075), (-0.11, 1.00), (-0.175, 0.86), (-0.205, 0.68), (-0.215, 0.47),
            (-0.275, 0.42), (-0.345, 0.50), (-0.285, 0.58), (-0.34, 0.70), (-0.265, 0.77),
            (-0.31, 0.90), (-0.205, 0.95), (-0.235, 1.08), (-0.11, 1.075), (-0.115, 1.17)]
    slab(bm, mane, -MANE_T, MANE_T, PAINT, 0.03, 4)

    # 4. Gold crescent moon crest, leaning slightly forward.
    slab(bm, crescent(0.035, 1.19, 0.13, 0.07, 0.115, -12), -CREST_T, CREST_T, GOLD, 0.014, 3)

    # 5. Three stepping diamonds down the neck (the repeated leap), both sides.
    for cx, cz, h in ((-0.08, 0.77, 0.075), (0.005, 0.56, 0.065), (0.085, 0.36, 0.055)):
        both_sides(bm, diamond(cx, cz, h), DECAL_IN, DECAL_OUT, PAINT, 0.0025, 1)

    # 6. Ink eye slit, slanted down toward the muzzle.
    eye = [(0.085, 0.95), (0.165, 0.925), (0.17, 0.945), (0.09, 0.97)]
    both_sides(bm, eye, DECAL_IN, DECAL_OUT + 0.002, INK, 0.003, 2)

    # 7. Gold kintsugi crack across the lower body.
    crack = [(-0.215, 0.235), (-0.175, 0.30), (-0.20, 0.36), (-0.15, 0.43), (-0.165, 0.50)]
    both_sides(bm, ribbon(crack, 0.0065), CRACK_IN, CRACK_OUT, GOLD)

    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    me = bpy.data.meshes.new(NAME)
    bm.to_mesh(me); bm.free()
    for m in MAT_ORDER: me.materials.append(m)
    # New faces are flat (bmesh default); the copied pedestal keeps the knight's smooth dome.

    # Simple planar UVs (porcelain shader does not sample textures).
    uv = me.uv_layers.new(name="UVMap")
    for loop in me.loops:
        co = me.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (co.x + 0.5, co.z / 1.4)

    ob = bpy.data.objects.new(NAME, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = (3.15, 0, 0)
    return ob


if __name__ == "__main__" or True:
    obj = build()
