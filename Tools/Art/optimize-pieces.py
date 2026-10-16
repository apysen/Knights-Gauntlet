"""Build mobile meshes from untouched Blender sources via the Unity Blender bridge.

UVs/materials are retained. Source files are never saved or overwritten.
"""
import bpy
import json
from pathlib import Path

ROOT = Path("D:/GameDev/Knight's Gauntlet App/My project")
OUT = ROOT / "Assets/Resources/Pieces/Optimized"
OUT.mkdir(parents=True, exist_ok=True)
BUDGETS = dict(knight=12000, rook=6500, bishop=8000, queen=10000, pawn=5000)
previous = bpy.context.window.scene
scene = bpy.data.scenes.new("KG_OptimizePieces")
bpy.context.window.scene = scene
report = []
try:
    for name, budget in BUDGETS.items():
        path = ROOT / "ArtSource/Models/ChessPieces" / (name + ".blend")
        if not path.exists():
            path = ROOT / "Assets/Resources/Pieces" / (name + ".blend")
        with bpy.data.libraries.load(str(path), link=False) as (src, dst):
            dst.objects = src.objects
        objects = [o for o in dst.objects if o.type == 'MESH']
        before = 0
        for obj in objects:
            scene.collection.objects.link(obj)
            obj.data.calc_loop_triangles()
            before += len(obj.data.loop_triangles)
        for obj in objects:
            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            modifier = obj.modifiers.new("Mobile silhouette budget", 'DECIMATE')
            modifier.ratio = min(1.0, budget / before)
            modifier.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            obj.data.calc_loop_triangles()
        after = sum(len(o.data.loop_triangles) for o in objects)
        bpy.ops.export_scene.fbx(filepath=str(OUT / (name + ".fbx")),
            use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
            bake_anim=False, add_leaf_bones=False, path_mode='STRIP', use_mesh_modifiers=True)
        report.append(dict(name=name, originalTriangles=before, triangles=after, budget=budget))
        print(name, before, '->', after)
        for obj in dst.objects:
            mesh = obj.data if obj.type == 'MESH' else None
            bpy.data.objects.remove(obj, do_unlink=True)
            if mesh and mesh.users == 0:
                bpy.data.meshes.remove(mesh)
    (ROOT / 'ArtSource/Reports/model-optimization.json').write_text(json.dumps(report, indent=2))
finally:
    bpy.context.window.scene = previous
    bpy.data.scenes.remove(scene)
