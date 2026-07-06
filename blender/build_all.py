"""
build_all.py - Orchestrator. Run headless:

    blender --background --python blender/build_all.py

For each asset: reset scene -> build -> UV unwrap -> export FBX (game-ready:
transforms baked, origins set) -> render a preview PNG. Finally assembles an
open "hero" scene (room + table + 4 seats + 4 players + announcer) and renders it.
"""

import os
import sys
import math

# make sibling modules importable under `blender --python`
HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bpy
import gen_lib as L
import gen_env as E
import gen_characters as C

ROOT = os.path.dirname(HERE)                      # repo root
ART = os.path.join(ROOT, "unity", "Assets", "Art")
ENV_DIR = os.path.join(ART, "Env")
CHAR_DIR = os.path.join(ART, "Characters", "generated")
PREV = os.path.join(HERE, "out", "previews")
for d in (ENV_DIR, CHAR_DIR, PREV):
    os.makedirs(d, exist_ok=True)


def _unwrap_all():
    for o in bpy.context.scene.objects:
        if o.type == "MESH" and len(o.data.polygons):
            L.uv_unwrap(o)


def _roots():
    return [o for o in bpy.context.scene.objects if o.parent is None]


def make(builder, fbx_name, subdir, cam, target, res=640, ortho=None):
    L.reset()
    obj = builder()
    C.hide_breath()  # preview renders show the puff toggled off, matching in-game default
    _unwrap_all()
    roots = [obj] if obj is not None else _roots()
    fbx_path = os.path.join(subdir, fbx_name + ".fbx")
    L.export_fbx(roots, fbx_path)
    tris = sum(L.poly_count(o) for o in bpy.context.scene.objects if o.type == "MESH")
    L.render_preview(os.path.join(PREV, fbx_name + ".png"), cam, target, res=res, ortho=ortho)
    print(f"[BUILT] {fbx_name:16s} faces={tris:5d} -> {fbx_path}")
    return tris


def build_props():
    make(lambda: E.table_round(), "Table", ENV_DIR, (2.4, -3.2, 2.2), (0, 0, 0.7))
    make(lambda: E.stool(), "Stool", ENV_DIR, (0.9, -1.4, 0.9), (0, 0, 0.4))
    make(lambda: E.barrel(), "Barrel", ENV_DIR, (1.0, -1.6, 1.1), (0, 0, 0.5))


def build_characters():
    # front-facing 3/4 previews; characters face +Y so shoot from +Y
    make(C.char_sphere,  "P1_Sphere",       CHAR_DIR, (1.1, 3.4, 1.5), (0, 0.1, 1.05))
    make(C.char_giraffe, "P2_Giraffe",      CHAR_DIR, (1.1, 4.0, 2.1), (0, 0.1, 1.40))
    make(C.char_boxer,   "P3_Boxer",        CHAR_DIR, (1.1, 3.4, 1.5), (0, 0.1, 1.10))
    make(C.char_slice,   "P4_Slice",        CHAR_DIR, (1.1, 3.8, 1.7), (0, 0.1, 1.25))
    make(C.announcer,    "Announcer_Host",  CHAR_DIR, (1.2, 3.6, 1.6), (0, 0.1, 1.15))


def build_room_asset():
    L.reset()
    # Game stage: "dollhouse" tavern - open front + no solid ceiling so a fixed
    # exterior camera sees the table, and warm point lights aren't sealed in.
    room = E.build_room("TavernRoom", open_front=True, solid_ceiling=False)
    _unwrap_all()
    L.export_fbx([room], os.path.join(ENV_DIR, "TavernRoom.fbx"))
    E.dump_lights(os.path.join(ENV_DIR, "tavern_lights.json"))
    tris = L.poly_count(room)
    print(f"[BUILT] TavernRoom       faces={tris:5d} -> {ENV_DIR}/TavernRoom.fbx")


def build_hero():
    """Assemble an open room with everyone placed, for a single showcase render."""
    L.reset()
    E.build_room("TavernRoom_Open", open_front=True, solid_ceiling=False)
    table = E.table_round()
    L.bake(table, loc=(0, 0.4, 0))  # nudge toward back, leaving front open to camera

    # four players evenly around the table, each facing the centre (0, 0.4)
    cx, cy = 0.0, 0.4
    seat_r = 2.4
    seats = [235, 305, 25, 155]  # front-left, front-right, back-right, back-left
    builders = [C.char_sphere, C.char_giraffe, C.char_boxer, C.char_slice]
    for ang_deg, b in zip(seats, builders):
        a = math.radians(ang_deg)
        x, y = cx + seat_r * math.cos(a), cy + seat_r * math.sin(a)
        stool = E.stool()
        L.bake(stool, loc=(x, y, 0))
        ch = b()
        # stand just outside the stool (Unity builder does the same +0.25 offset)
        px, py = cx + (seat_r + 0.35) * math.cos(a), cy + (seat_r + 0.35) * math.sin(a)
        face = math.atan2(cy - py, cx - px) - math.pi / 2  # +Y model-front toward centre
        ch.location = (px, py, 0.0)
        ch.rotation_euler = (0, 0, face)
    C.hide_breath()

    host = C.announcer()
    host.location = (-E.HW + 1.7, -0.5, 0.0)    # behind the bar on the -X wall
    host.rotation_euler = (0, 0, -math.pi / 2)   # facing +X into the room

    L.render_preview(os.path.join(PREV, "HERO_tavern.png"),
                     cam_loc=(0.0, -9.0, 5.4), target=(0, 0.4, 0.9), res=960,
                     sun_energy=2.4, bg=(0.03, 0.025, 0.02))
    print("[BUILT] HERO render -> out/previews/HERO_tavern.png")


if __name__ == "__main__":
    build_props()
    build_characters()
    build_room_asset()
    build_hero()
    print("[DONE] all assets built.")
