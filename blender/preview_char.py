"""
preview_char.py - Fast single-character preview for iteration.

    blender --background --python blender/preview_char.py -- P1
    blender --background --python blender/preview_char.py -- ALL

Renders dev previews to blender/out/previews/dev_<key>.png without exporting.
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bpy
import gen_lib as L
import gen_characters as C

PREV = os.path.join(HERE, "out", "previews")
os.makedirs(PREV, exist_ok=True)

SPECS = {
    "P1":   (C.char_sphere,  (1.1, 3.4, 1.5), (0, 0.1, 1.05)),
    "P2":   (C.char_giraffe, (1.1, 4.0, 2.1), (0, 0.1, 1.40)),
    "P3":   (C.char_boxer,   (1.1, 3.4, 1.5), (0, 0.1, 1.10)),
    "P4":   (C.char_slice,   (1.1, 3.8, 1.7), (0, 0.1, 1.25)),
    "HOST": (C.announcer,    (1.2, 3.6, 1.6), (0, 0.1, 1.15)),
}

key = "ALL"
if "--" in sys.argv:
    key = sys.argv[sys.argv.index("--") + 1].upper()

for k, (builder, cam, tgt) in SPECS.items():
    if key not in ("ALL", k):
        continue
    L.reset()
    builder()
    C.hide_breath()
    tris = sum(len(o.data.polygons) for o in bpy.context.scene.objects if o.type == "MESH")
    L.render_preview(os.path.join(PREV, f"dev_{k}.png"), cam, tgt, res=700)
    print(f"[PREVIEW] {k} faces={tris}")
print("[DONE]")
