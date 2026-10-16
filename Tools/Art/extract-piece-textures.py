"""Extract the supplied models' packed base colors through the Blender bridge."""
import bpy
import json
from pathlib import Path

ROOT = Path("D:/GameDev/Knight's Gauntlet App/My project")
OUT = ROOT / "Assets/Resources/Pieces/Textures"
OUT.mkdir(parents=True, exist_ok=True)
footprints = []
for name in ("knight", "rook", "bishop", "queen", "pawn"):
    path = ROOT / "ArtSource/Models/ChessPieces" / (name + ".blend")
    with bpy.data.libraries.load(str(path), link=False) as (source, loaded):
        loaded.meshes = list(source.meshes)
    vertices = [v.co.copy() for mesh in loaded.meshes for v in mesh.vertices]
    low = min(v.z for v in vertices)
    high = max(v.z for v in vertices)
    base = [v for v in vertices if v.z < low + (high - low) * 0.12]
    xmin, xmax = min(v.x for v in vertices), max(v.x for v in vertices)
    ymin, ymax = min(v.y for v in vertices), max(v.y for v in vertices)
    bxmin, bxmax = min(v.x for v in base), max(v.x for v in base)
    bymin, bymax = min(v.y for v in base), max(v.y for v in base)
    full = max(xmax - xmin, ymax - ymin)
    footprints.append({"name": name, "baseRatio": max(bxmax-bxmin, bymax-bymin)/full,
        "baseCenterX": ((bxmin+bxmax)-(xmin+xmax))*0.5/full,
        "baseCenterY": ((bymin+bymax)-(ymin+ymax))*0.5/full})
    found = False
    for mesh in loaded.meshes:
        for mat in mesh.materials:
            if not mat or not mat.use_nodes:
                continue
            bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if not bsdf or not bsdf.inputs['Base Color'].links:
                continue
            node = bsdf.inputs['Base Color'].links[0].from_node
            if node.type == 'TEX_IMAGE' and node.image:
                image = node.image
                image.filepath_raw = str(OUT / (name + "-basecolor.png"))
                image.file_format = 'PNG'
                image.save()
                print(name, image.size[:], image.filepath_raw)
                found = True
                break
        if found:
            break
    for mesh in loaded.meshes:
        bpy.data.meshes.remove(mesh)
    if not found:
        raise RuntimeError("No base color image in supplied model: " + name)
(ROOT / "Assets/Resources/Pieces/base-footprints.json").write_text(
    json.dumps({"items": footprints}, indent=2), encoding='utf-8')
