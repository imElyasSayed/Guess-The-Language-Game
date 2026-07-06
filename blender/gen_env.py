"""
gen_env.py - Cozy medieval tavern environment kit (low-poly, Liar's-Bar-ish warmth).

Everything is authored in Blender Z-up world space with the table at the origin and
the floor at z=0. build_room() joins all the static shell + fixed props into one mesh
("TavernRoom") and records warm light-source positions so the Unity builder can drop
matching point lights. Table + Stool are exported separately (placed per-seat in Unity).
"""

import json
import bpy
import gen_lib as L

# --- warm tavern palette -------------------------------------------------- #
WOOD_DARK   = L.mat("Wood_Dark",   (0.22, 0.12, 0.06), rough=0.85)
WOOD_MID    = L.mat("Wood_Mid",    (0.38, 0.22, 0.11), rough=0.8)
WOOD_LIGHT  = L.mat("Wood_Light",  (0.55, 0.35, 0.18), rough=0.75)
STONE       = L.mat("Stone",       (0.34, 0.33, 0.30), rough=0.95)
STONE_DARK  = L.mat("Stone_Dark",  (0.22, 0.21, 0.19), rough=0.95)
METAL_IRON  = L.mat("Iron",        (0.10, 0.10, 0.11), rough=0.5, metal=0.9)
METAL_BRASS = L.mat("Brass",       (0.55, 0.42, 0.14), rough=0.35, metal=0.9)
GLASS_GREEN = L.mat("Glass_Green", (0.10, 0.28, 0.12), rough=0.25)
GLASS_AMBER = L.mat("Glass_Amber", (0.45, 0.24, 0.05), rough=0.25)
GLASS_RED   = L.mat("Glass_Red",   (0.35, 0.05, 0.05), rough=0.25)
WAX         = L.mat("Wax",         (0.85, 0.80, 0.62), rough=0.6)
FLAME       = L.mat("Flame",       (1.0, 0.55, 0.12), rough=0.4, emit=(1.0, 0.5, 0.1), emit_strength=8.0)
EMBER       = L.mat("Ember",       (1.0, 0.35, 0.08), rough=0.5, emit=(1.0, 0.3, 0.05), emit_strength=5.0)

# --- room layout constants (Blender space) -------------------------------- #
ROOM_W, ROOM_D, WALL_H = 11.0, 9.0, 3.4
HW, HD = ROOM_W / 2, ROOM_D / 2

# Warm light sources, recorded in Unity space (Ux=Bx, Uy=Bz, Uz=By).
_lights = []
def _record_light(bpos, color, intensity, rng):
    _lights.append({
        "pos": [round(bpos[0], 3), round(bpos[2], 3), round(bpos[1], 3)],
        "color": list(color), "intensity": intensity, "range": rng,
    })

def dump_lights(path):
    with open(path, "w") as f:
        json.dump({"lights": _lights}, f, indent=2)


# --------------------------------------------------------------------------- #
# Props (each returns a single joined mesh, origin at bottom-centre)
# --------------------------------------------------------------------------- #

def barrel(name="Barrel"):
    parts = []
    parts.append(L.cylinder("staves", radius=0.42, depth=0.95, verts=14,
                            loc=(0, 0, 0.5), material=WOOD_MID))
    # bulge belly
    parts.append(L.cylinder("belly", radius=0.48, depth=0.55, verts=14,
                            loc=(0, 0, 0.5), material=WOOD_MID))
    for z in (0.16, 0.5, 0.84):
        parts.append(L.torus(f"hoop{z}", major=0.45, minor=0.035, mseg=14, minseg=6,
                            loc=(0, 0, z), material=METAL_IRON))
    o = L.join(parts, name)
    L.origin_to_bottom(o)
    return o


def bottle(name="Bottle", glass=GLASS_GREEN):
    body = L.cylinder("body", radius=0.05, depth=0.20, verts=10, loc=(0, 0, 0.10), material=glass)
    shoulder = L.cone("shoulder", r1=0.05, r2=0.022, depth=0.06, verts=10,
                     loc=(0, 0, 0.23), material=glass)
    neck = L.cylinder("neck", radius=0.022, depth=0.08, verts=8, loc=(0, 0, 0.30), material=glass)
    cork = L.cylinder("cork", radius=0.02, depth=0.03, verts=8, loc=(0, 0, 0.355), material=WOOD_LIGHT)
    o = L.join([body, shoulder, neck, cork], name)
    L.origin_to_bottom(o)
    return o


def candle(name="Candle", record=True, at=(0, 0, 0)):
    stick = L.cylinder("wax", radius=0.03, depth=0.16, verts=8, loc=(0, 0, 0.08), material=WAX)
    dish = L.cylinder("dish", radius=0.07, depth=0.02, verts=10, loc=(0, 0, 0.01), material=METAL_BRASS)
    flame = L.cone("flame", r1=0.018, r2=0.0, depth=0.06, verts=6, loc=(0, 0, 0.19), material=FLAME)
    o = L.join([dish, stick, flame], name)
    L.origin_to_bottom(o)
    if record:
        _record_light((at[0], at[1], at[2] + 0.2), (1.0, 0.66, 0.32), 1.6, 3.5)
    return o


def lantern(name="Lantern"):
    top = L.cone("cap", r1=0.11, r2=0.0, depth=0.09, verts=6, loc=(0, 0, 0.30), material=METAL_IRON)
    cage = L.cylinder("cage", radius=0.09, depth=0.20, verts=6, loc=(0, 0, 0.15), material=METAL_IRON)
    glass = L.cylinder("glass", radius=0.07, depth=0.16, verts=6, loc=(0, 0, 0.15), material=GLASS_AMBER)
    glow = L.ico("glow", radius=0.045, subdiv=1, loc=(0, 0, 0.15), material=FLAME)
    ring = L.torus("ring", major=0.03, minor=0.008, loc=(0, 0, 0.37), material=METAL_IRON)
    o = L.join([top, cage, glass, glow, ring], name)
    L.origin_to_bottom(o)
    return o


def stool(name="Stool"):
    seat = L.cylinder("seat", radius=0.24, depth=0.06, verts=14, loc=(0, 0, 0.55), material=WOOD_LIGHT)
    parts = [seat]
    import math
    for i in range(3):
        a = i * (2 * math.pi / 3)
        x, y = 0.17 * math.cos(a), 0.17 * math.sin(a)
        parts.append(L.cylinder(f"leg{i}", radius=0.025, depth=0.55, verts=6,
                               loc=(x, y, 0.275), material=WOOD_MID))
    parts.append(L.torus("brace", major=0.16, minor=0.02, mseg=10, minseg=5,
                        loc=(0, 0, 0.2), material=WOOD_MID))
    o = L.join(parts, name)
    L.origin_to_bottom(o)
    return o


def table_round(name="Table", radius=1.7):
    top = L.cylinder("top", radius=radius, depth=0.12, verts=28, loc=(0, 0, 0.78), material=WOOD_LIGHT)
    rim = L.torus("rim", major=radius, minor=0.06, mseg=28, minseg=6, loc=(0, 0, 0.74), material=WOOD_MID)
    apron = L.cylinder("apron", radius=radius * 0.82, depth=0.14, verts=24, loc=(0, 0, 0.66), material=WOOD_MID)
    column = L.cylinder("column", radius=0.22, depth=0.66, verts=12, loc=(0, 0, 0.33), material=WOOD_DARK)
    foot = L.cylinder("foot", radius=0.6, depth=0.08, verts=16, loc=(0, 0, 0.04), material=WOOD_DARK)
    o = L.join([top, rim, apron, column, foot], name)
    L.origin_to_bottom(o)
    return o


# --------------------------------------------------------------------------- #
# Room shell + fixed decor
# --------------------------------------------------------------------------- #

def _fireplace(parts):
    # stone hearth on the +X wall
    x = HW - 0.15
    parts.append(L.box("hearth_base", scale=(0.9, 3.0, 0.35), loc=(x - 0.35, 0, 0.18), material=STONE))
    parts.append(L.box("hearth_l", scale=(0.7, 0.5, 2.4), loc=(x - 0.15, -1.1, 1.2), material=STONE))
    parts.append(L.box("hearth_r", scale=(0.7, 0.5, 2.4), loc=(x - 0.15, 1.1, 1.2), material=STONE))
    parts.append(L.box("mantle", scale=(0.85, 2.9, 0.22), loc=(x - 0.2, 0, 2.15), material=WOOD_DARK))
    parts.append(L.box("chimney", scale=(0.6, 1.6, 1.0), loc=(x - 0.1, 0, 2.9), material=STONE_DARK))
    parts.append(L.box("firebox", scale=(0.5, 1.6, 1.4), loc=(x - 0.2, 0, 1.0), material=STONE_DARK))
    # logs + embers
    parts.append(L.cylinder("log1", radius=0.09, depth=1.0, verts=8, rot=(1.5708, 0, 0),
                            loc=(x - 0.3, -0.2, 0.5), material=WOOD_DARK))
    parts.append(L.cylinder("log2", radius=0.09, depth=1.0, verts=8, rot=(1.5708, 0, 0.3),
                            loc=(x - 0.3, 0.2, 0.55), material=WOOD_DARK))
    parts.append(L.ico("embers", radius=0.28, subdiv=1, scale=(1, 1, 0.4),
                       loc=(x - 0.3, 0, 0.45), material=EMBER))
    _record_light((x - 0.4, 0, 1.0), (1.0, 0.45, 0.15), 4.5, 7.0)


def _bar_counter(parts):
    # bar runs along the -X (left) wall, leaving the front (-Y) open to the camera
    x = -HW + 0.9
    parts.append(L.box("bar_top", scale=(0.9, 5.5, 0.12), loc=(x, -0.5, 1.12), material=WOOD_LIGHT))
    parts.append(L.box("bar_front", scale=(0.2, 5.5, 1.0), loc=(x + 0.35, -0.5, 0.55), material=WOOD_DARK))
    parts.append(L.box("bar_kick", scale=(0.6, 5.5, 0.9), loc=(x - 0.1, -0.5, 0.5), material=WOOD_MID))
    # panel detailing
    for i in range(5):
        parts.append(L.box(f"panel{i}", scale=(0.24, 0.12, 0.7), loc=(x + 0.36, -2.9 + i * 1.1, 0.55), material=WOOD_MID))


def _shelves(parts):
    # back-bar shelves with bottles, on the -X wall behind the bar
    x = -HW + 0.25
    bottle_mats = [GLASS_GREEN, GLASS_AMBER, GLASS_RED]
    for si, z in enumerate((1.6, 2.15, 2.7)):
        parts.append(L.box(f"shelf{si}", scale=(0.3, 4.6, 0.06), loc=(x, -0.5, z), material=WOOD_MID))
        for bi in range(9):
            by = -2.5 + bi * 0.55
            b = bottle(f"sb_{si}_{bi}", glass=bottle_mats[(si + bi) % 3])
            L.bake(b, loc=(x, by, z + 0.03))
            parts.append(b)


def _walls_floor(parts, open_view=False):
    # floor - plank rows
    n = 9
    for i in range(n):
        yy = -HD + (i + 0.5) * (ROOM_D / n)
        m = WOOD_MID if i % 2 == 0 else WOOD_DARK
        parts.append(L.box(f"plank{i}", scale=(ROOM_W, ROOM_D / n - 0.03, 0.06), loc=(0, yy, 0.0), material=m))
    # walls (dark plaster + wainscot)
    parts.append(L.box("wall_back",  scale=(ROOM_W, 0.2, WALL_H), loc=(0, HD, WALL_H / 2), material=STONE_DARK))
    if not open_view:  # near wall skipped for the open hero render
        parts.append(L.box("wall_front", scale=(ROOM_W, 0.2, WALL_H), loc=(0, -HD, WALL_H / 2), material=STONE_DARK))
    parts.append(L.box("wall_left",  scale=(0.2, ROOM_D, WALL_H), loc=(-HW, 0, WALL_H / 2), material=STONE_DARK))
    parts.append(L.box("wall_right", scale=(0.2, ROOM_D, WALL_H), loc=(HW, 0, WALL_H / 2), material=STONE_DARK))
    # wainscot band
    for (nm, sc, lc) in (("wains_b", (ROOM_W, 0.08, 1.0), (0, HD - 0.08, 0.5)),
                          ("wains_f", (ROOM_W, 0.08, 1.0), (0, -HD + 0.08, 0.5)),
                          ("wains_l", (0.08, ROOM_D, 1.0), (-HW + 0.08, 0, 0.5)),
                          ("wains_r", (0.08, ROOM_D, 1.0), (HW - 0.08, 0, 0.5))):
        parts.append(L.box(nm, scale=sc, loc=lc, material=WOOD_MID))


def _solid_ceiling(parts):
    parts.append(L.box("ceiling", scale=(ROOM_W, ROOM_D, 0.12), loc=(0, 0, WALL_H), material=WOOD_DARK))


def _beams(parts):
    # only the two outer beams (near the side walls) so the overview camera's
    # sightline to the table centre stays clear; tucked up under the roofline.
    for x in (-HW + 1.4, HW - 1.4):
        parts.append(L.box("beam", scale=(0.3, ROOM_D, 0.22), loc=(x, 0, WALL_H - 0.12), material=WOOD_DARK))
    # decorative ridge beam along the back wall, also high
    parts.append(L.box("ridge", scale=(ROOM_W, 0.26, 0.2), loc=(0, HD - 0.5, WALL_H - 0.12), material=WOOD_MID))


def _hanging_lanterns(parts):
    positions = [(-2.5, 1.5), (2.5, 1.5), (0, -2.0)]
    for i, (x, y) in enumerate(positions):
        # chain
        parts.append(L.cylinder(f"chain{i}", radius=0.012, depth=0.6, verts=4,
                               loc=(x, y, WALL_H - 0.7), material=METAL_IRON))
        lan = lantern(f"HangLantern{i}")
        L.bake(lan, loc=(x, y, WALL_H - 1.35))
        parts.append(lan)
        _record_light((x, y, WALL_H - 1.2), (1.0, 0.62, 0.28), 2.2, 5.0)


def _table_candles_and_barrels(parts):
    # candelabra-ish table candles are placed with the Table in Unity; here add
    # standing barrels + wall candles for the room itself.
    for i, (x, y) in enumerate([(HW - 0.9, HD - 0.9), (HW - 1.8, HD - 0.9), (HW - 0.9, -HD + 1.0)]):
        b = barrel(f"RoomBarrel{i}")
        L.bake(b, loc=(x, y, 0))
        parts.append(b)
    # wall sconce candles on the back (+Y) and right (+X) walls
    for i, (x, y) in enumerate([(-2.0, HD - 0.15), (2.0, HD - 0.15), (HW - 0.15, 2.5)]):
        bracket = L.box(f"sconce{i}", scale=(0.2, 0.1, 0.05), loc=(x, y, 1.7), material=METAL_IRON)
        c = candle(f"WallCandle{i}", record=False, at=(x, y, 1.75))
        L.bake(c, loc=(x, y, 1.75))
        parts += [bracket, c]
        _record_light((x, y, 1.9), (1.0, 0.66, 0.32), 1.6, 3.5)


def build_room(name="TavernRoom", open_front=False, solid_ceiling=True):
    parts = []
    _walls_floor(parts, open_view=open_front)
    _beams(parts)                       # exposed wooden beams stay for flavour
    if solid_ceiling:
        _solid_ceiling(parts)
    _fireplace(parts)
    _bar_counter(parts)
    _shelves(parts)
    _hanging_lanterns(parts)
    _table_candles_and_barrels(parts)
    room = L.join(parts, name)
    L.origin_to_bottom(room)
    return room
