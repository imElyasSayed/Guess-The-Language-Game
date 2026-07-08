"""
import_meshy.py - Normalize the Meshy GLB cast and export game-ready FBX.

For each GLB: import, recenter feet to world origin, apply a uniform scale so the
set fits our 1u=1m world, parent under a single feet-origin root empty, (giraffe)
add the toggleable BadBreath puff, export FBX into the Unity folder, and render a
studio preview under the final name.

Source models are a Liar's Bar-style animal cast; mapping onto the game's slots:
  Bulldog  -> P1 (seated)   round body
  Giraffe  -> P2 (seated)   long neck + BadBreath toggle
  Horse    -> P3 (seated)   boxer w/ red gloves
  Fox      -> P4 (seated)
  Cat      -> P5 (at bar)
  Announcer(penguin) -> host behind the bar
"""

import os, sys, math
HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import bpy
from mathutils import Matrix, Vector
import gen_lib as L

ROOT = os.path.dirname(HERE)
SRC = os.path.join(ROOT, "meshy_src")
CHAR_DIR = os.path.join(ROOT, "unity", "Assets", "Art", "Characters", "generated")
PREV = os.path.join(HERE, "out", "previews")
os.makedirs(CHAR_DIR, exist_ok=True)

SCALE = 0.75  # uniform: brings the 2.4-3.0m Meshy set into ~1.8-2.25m game scale

# (glb basename, output name, preview cam dist mult, breath)
CAST = [
    ("Bulldog",   "P1_Bulldog",   False),
    ("Giraffe",   "P2_Giraffe",   True),
    ("Horse",     "P3_Horse",     False),
    ("Fox",       "P4_Fox",       False),
    ("Cat",       "P5_Cat",       False),
    ("Announcer", "Announcer_Host", False),
]

BREATH = L.mat("Breath", (0.4, 0.9, 0.25), rough=0.4, emit=(0.3, 0.9, 0.2), emit_strength=1.5)


def world_bounds(objs):
    xs, ys, zs = [], [], []
    for o in objs:
        if o.type != "MESH":
            continue
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            xs.append(w.x); ys.append(w.y); zs.append(w.z)
    return (Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs))))


for glb_name, out_name, breath in CAST:
    L.reset()
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, glb_name + ".glb"))
    roots = [o for o in bpy.context.scene.objects if o.parent is None]
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]

    lo, hi = world_bounds(meshes)
    cx, cy = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
    minz = lo.z
    # feet -> origin, then uniform scale about origin
    T = Matrix.Scale(SCALE, 4) @ Matrix.Translation((-cx, -cy, -minz))
    for r in roots:
        r.matrix_world = T @ r.matrix_world

    # CRITICAL: bake every transform into mesh data (identity object transforms),
    # exactly like the procedural models — otherwise Unity's FBX unit handling
    # scales the unapplied 0.75 down to ~2cm and the model vanishes. Join the body
    # meshes into one clean object so nothing carries a stray transform.
    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    body = bpy.context.active_object
    body.name = "Body"
    L.shade_smooth(body)

    # remove leftover empties from the GLB node hierarchy
    for o in list(bpy.context.scene.objects):
        if o.type == "EMPTY":
            bpy.data.objects.remove(o, do_unlink=True)

    # single clean feet-origin root empty
    rootE = bpy.data.objects.new(out_name, None)
    bpy.context.scene.collection.objects.link(rootE)
    rootE.location = (0, 0, 0)
    body.parent = rootE

    parts = [body]
    if breath:
        hi2 = world_bounds([body])[1]
        bz = hi2.z * 0.92          # mouth ~ front of the head, top of the tall neck
        by = hi2.y + 0.12
        puff = L.ico("BadBreath", radius=0.09, subdiv=2, scale=(1.2, 1.6, 1.0),
                     loc=(0, by, bz), material=BREATH)
        puff2 = L.ico("puff2", radius=0.05, subdiv=2,
                      loc=(0.06, by + 0.14, bz + 0.05), material=BREATH)
        puff = L.join([puff, puff2], "BadBreath")
        puff.parent = rootE
        puff.hide_render = True     # ships toggled off
        parts.append(puff)

    L.export_fbx([rootE], os.path.join(CHAR_DIR, out_name + ".fbx"))

    lo3, hi3 = world_bounds([body])
    h = hi3.z - lo3.z
    tgt = (0, 0, (lo3.z + hi3.z) / 2)
    cam = (h * 0.15, hi3.y + h * 1.7, (lo3.z + hi3.z) / 2 + h * 0.12)
    tris = len(body.data.polygons)
    L.render_preview(os.path.join(PREV, out_name + ".png"), cam, tgt, res=640, studio=True)
    print(f"[MESHY] {out_name:16s} tris={tris:6d} height={h:.2f}m -> {out_name}.fbx")

print("[MESHY] done")
