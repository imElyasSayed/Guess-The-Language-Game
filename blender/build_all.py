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


def make(builder, fbx_name, subdir, cam, target, res=640, ortho=None, studio=False):
    L.reset()
    obj = builder()
    C.hide_breath()  # preview renders show the puff toggled off, matching in-game default
    _unwrap_all()
    roots = [obj] if obj is not None else _roots()
    fbx_path = os.path.join(subdir, fbx_name + ".fbx")
    L.export_fbx(roots, fbx_path)
    tris = sum(L.poly_count(o) for o in bpy.context.scene.objects if o.type == "MESH")
    L.render_preview(os.path.join(PREV, fbx_name + ".png"), cam, target, res=res, ortho=ortho,
                     studio=studio)
    print(f"[BUILT] {fbx_name:16s} faces={tris:5d} -> {fbx_path}")
    return tris


def build_props():
    make(lambda: E.table_round(), "Table", ENV_DIR, (2.4, -3.2, 2.2), (0, 0, 0.7))
    make(lambda: E.stool(), "Stool", ENV_DIR, (0.9, -1.4, 0.9), (0, 0, 0.4))
    make(lambda: E.barrel(), "Barrel", ENV_DIR, (1.0, -1.6, 1.1), (0, 0, 0.5))


# The shipping cast now comes from the Meshy animal models (see import_meshy.py),
# not procedural generators. gen_characters.py is kept for history/fallback. The
# hero shot below imports the source GLBs (which import upright + facing +Y) and
# applies the same normalization import_meshy.py uses, so this file still owns the
# props/room + group render without regenerating characters or round-tripping FBX.

MESHY_SRC = os.path.join(ROOT, "meshy_src")
MESHY_SCALE = 0.75  # matches import_meshy.py

# source GLB -> seat/bar placement for the hero group render
HERO_SEATED = ["Bulldog", "Giraffe", "Horse", "Fox"]
HERO_FIFTH = "Cat"
HERO_HOST = "Announcer"


def _import_char(glb_name):
    """Import a source GLB, normalize (feet->origin, uniform scale) and return its
    feet-origin root empty, upright and facing +Y."""
    from mathutils import Matrix
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MESHY_SRC, glb_name + ".glb"))
    new = [o for o in bpy.context.scene.objects if o not in before]
    roots = [o for o in new if o.parent is None]
    meshes = [o for o in new if o.type == "MESH"]
    xs, ys, zs = [], [], []
    for o in meshes:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            xs.append(w.x); ys.append(w.y); zs.append(w.z)
    cx, cy, minz = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)
    T = Matrix.Scale(MESHY_SCALE, 4) @ Matrix.Translation((-cx, -cy, -minz))
    for r in roots:
        r.matrix_world = T @ r.matrix_world
    holder = bpy.data.objects.new(glb_name, None)
    bpy.context.scene.collection.objects.link(holder)
    for r in roots:
        r.parent = holder
        r.matrix_parent_inverse = holder.matrix_world.inverted()
    return holder


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

    # four animals seated around the table, the cat (fifth) hangs out by the bar
    cx, cy = 0.0, 0.4
    seat_r = 2.4
    seats = [235, 305, 25, 155]  # front-left, front-right, back-right, back-left
    for ang_deg, name in zip(seats, HERO_SEATED):
        a = math.radians(ang_deg)
        x, y = cx + seat_r * math.cos(a), cy + seat_r * math.sin(a)
        stool = E.stool()
        L.bake(stool, loc=(x, y, 0))
        ch = _import_char(name)
        # stand just outside the stool (Unity builder does the same +0.25 offset)
        px, py = cx + (seat_r + 0.35) * math.cos(a), cy + (seat_r + 0.35) * math.sin(a)
        face = math.atan2(cy - py, cx - px) - math.pi / 2  # +Y model-front toward centre
        ch.location = (px, py, 0.0)
        ch.rotation_euler = (0, 0, face)

    host = _import_char(HERO_HOST)
    host.location = (-E.HW + 1.7, -0.5, 0.0)    # behind the bar on the -X wall
    host.rotation_euler = (0, 0, -math.pi / 2)   # facing +X into the room
    cat = _import_char(HERO_FIFTH)
    cat.location = (-E.HW + 2.6, 0.6, 0.0)       # leaning at the bar, chatting
    cat.rotation_euler = (0, 0, math.radians(-115))

    L.render_preview(os.path.join(PREV, "HERO_tavern.png"),
                     cam_loc=(0.0, -9.0, 5.4), target=(0, 0.4, 0.9), res=960,
                     sun_energy=2.4, bg=(0.03, 0.025, 0.02))
    print("[BUILT] HERO render -> out/previews/HERO_tavern.png")


if __name__ == "__main__":
    build_props()
    # characters now come from Meshy models via import_meshy.py (not procedural)
    build_room_asset()
    build_hero()
    print("[DONE] all assets built.")
