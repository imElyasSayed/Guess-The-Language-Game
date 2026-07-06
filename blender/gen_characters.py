"""
gen_characters.py - Four Beta-Squad-inspired caricatures + one announcer host.

Humorous, low-poly, exaggerated SILHOUETTES (not likenesses). Each character is a
set of logical meshes (head / torso / arms / legs / accessories) parented to a root
empty whose origin is at the feet (0,0,0), facing +Y. Facial detail is joined into
the head mesh so heads stay animatable as one unit.
"""

import math
import bpy
import gen_lib as L

# --- shared feature materials --------------------------------------------- #
EYE_WHITE = L.mat("EyeWhite", (0.95, 0.95, 0.92), rough=0.3)
PUPIL     = L.mat("Pupil",    (0.04, 0.03, 0.03), rough=0.3)
MOUTH     = L.mat("Mouth",    (0.25, 0.08, 0.08), rough=0.6)
TEETH     = L.mat("Teeth",    (0.96, 0.96, 0.9), rough=0.4)
HAIR_BLK  = L.mat("HairBlack",(0.05, 0.04, 0.04), rough=0.7)
BREATH    = L.mat("Breath",   (0.4, 0.9, 0.25), rough=0.4, emit=(0.3, 0.9, 0.2), emit_strength=1.5)


def _eyes(cx, cz, depth, spacing=0.14, r=0.075, root=None, parent=None):
    parts = []
    for s in (-1, 1):
        w = L.sphere("eyeW", radius=r, loc=(s * spacing, depth, cz))
        w.data.materials.append(EYE_WHITE)
        p = L.sphere("pupil", radius=r * 0.45, loc=(s * spacing, depth + r * 0.7, cz + 0.01))
        p.data.materials.append(PUPIL)
        parts += [w, p]
    return parts


def _grin(cz, depth, w=0.16, root=None):
    mouth = L.sphere("mouth", radius=1.0, scale=(w, 0.06, 0.06), loc=(0, depth, cz), material=MOUTH)
    teeth = L.box("teeth", scale=(w * 1.5, 0.04, 0.045), loc=(0, depth + 0.01, cz + 0.02), material=TEETH)
    return [mouth, teeth]


def _finalize(name, part_groups, marker=None, marker_loc=None):
    """part_groups: dict of {mesh_name: [objs]} -> joins each group, parents to root empty."""
    root = L.empty(name, (0, 0, 0))
    for mesh_name, objs in part_groups.items():
        objs = [o for o in objs if o is not None]
        if not objs:
            continue
        m = objs[0] if len(objs) == 1 else L.join(objs, mesh_name)
        m.name = mesh_name
        L.parent_keep(m, root)
    if marker:
        mk = L.empty(marker, marker_loc or (0, 0, 0))
        L.parent_keep(mk, root)
    return root


# --------------------------------------------------------------------------- #
# Character 1 - "The Sphere": near-spherical body, tiny limbs, receding hairline
# --------------------------------------------------------------------------- #

def char_sphere(name="P1_Sphere"):
    skin = L.mat("Skin1", (0.55, 0.36, 0.24))
    shirt = L.mat("Shirt1", (0.6, 0.12, 0.14))
    # giant round torso
    torso = L.sphere("torso", radius=0.85, scale=(1.0, 0.95, 1.0), loc=(0, 0, 0.95), material=shirt)
    belly = L.sphere("belly", radius=0.6, scale=(1.05, 0.6, 0.9), loc=(0, 0.35, 0.8), material=shirt)
    # head sits atop with almost no neck
    head = L.sphere("head", radius=0.42, scale=(1.0, 1.0, 1.05), loc=(0, 0, 2.0), material=skin)
    # receding hairline: thin hair band around back/sides only
    hair = L.sphere("hair", radius=0.44, scale=(1.0, 1.0, 0.7), loc=(0, -0.08, 2.12), material=HAIR_BLK)
    face = _eyes(0, 2.02, 0.38, spacing=0.15, r=0.08)
    face += _grin(1.88, 0.4, w=0.22)
    head_grp = [head] + face
    # tiny arms
    armL = L.sphere("armL", radius=0.16, scale=(1, 1, 1.6), rot=(0, 0.5, 0), loc=(-0.85, 0, 1.0), material=skin)
    armR = L.sphere("armR", radius=0.16, scale=(1, 1, 1.6), rot=(0, -0.5, 0), loc=(0.85, 0, 1.0), material=skin)
    # tiny legs
    legL = L.cylinder("legL", radius=0.12, depth=0.35, verts=8, loc=(-0.28, 0, 0.17), material=(L.mat("Trousers1",(0.15,0.13,0.2))))
    legR = L.cylinder("legR", radius=0.12, depth=0.35, verts=8, loc=(0.28, 0, 0.17), material=(L.mat("Trousers1",(0.15,0.13,0.2))))
    footL = L.sphere("footL", radius=0.14, scale=(1, 1.4, 0.6), loc=(-0.28, 0.06, 0.04), material=HAIR_BLK)
    footR = L.sphere("footR", radius=0.14, scale=(1, 1.4, 0.6), loc=(0.28, 0.06, 0.04), material=HAIR_BLK)
    return _finalize(name, {
        "Head": head_grp + [hair],
        "Torso": [torso, belly],
        "ArmL": [armL], "ArmR": [armR],
        "LegL": [legL, footL], "LegR": [legR, footR],
    })


# --------------------------------------------------------------------------- #
# Character 2 - "The Giraffe": long neck, skinny, goofy grin, bad-breath marker
# --------------------------------------------------------------------------- #

def char_giraffe(name="P2_Giraffe"):
    skin = L.mat("Skin2", (0.48, 0.30, 0.18))
    shirt = L.mat("Shirt2", (0.85, 0.7, 0.15))
    torso = L.box("torso", scale=(0.42, 0.28, 0.8), loc=(0, 0, 1.05), material=shirt)
    torso2 = L.sphere("torsoTop", radius=0.26, scale=(1.0, 0.8, 0.7), loc=(0, 0, 1.5), material=shirt)
    # very long neck (awkward forward lean)
    neck = L.cylinder("neck", radius=0.09, depth=0.9, verts=8, rot=(0.2, 0, 0), loc=(0, 0.09, 2.05), material=skin)
    head = L.sphere("head", radius=0.3, scale=(0.85, 1.15, 1.0), loc=(0, 0.28, 2.6), material=skin)
    hair = L.sphere("hair", radius=0.3, scale=(0.9, 1.0, 0.6), loc=(0, 0.22, 2.78), material=HAIR_BLK)
    face = _eyes(0, 2.64, 0.28, spacing=0.11, r=0.075)
    grin = _grin(2.5, 0.29, w=0.15)
    # legs - long and thin
    legL = L.cylinder("legL", radius=0.07, depth=0.72, verts=8, loc=(-0.16, 0, 0.36), material=(L.mat("Trousers2",(0.2,0.2,0.25))))
    legR = L.cylinder("legR", radius=0.07, depth=0.72, verts=8, loc=(0.16, 0, 0.36), material=(L.mat("Trousers2",(0.2,0.2,0.25))))
    footL = L.sphere("footL", radius=0.1, scale=(1, 1.5, 0.5), loc=(-0.16, 0.05, 0.03), material=HAIR_BLK)
    footR = L.sphere("footR", radius=0.1, scale=(1, 1.5, 0.5), loc=(0.16, 0.05, 0.03), material=HAIR_BLK)
    armL = L.cylinder("armL", radius=0.06, depth=0.7, verts=6, rot=(0, 0.15, 0), loc=(-0.42, 0, 1.1), material=skin)
    armR = L.cylinder("armR", radius=0.06, depth=0.7, verts=6, rot=(0, -0.15, 0), loc=(0.42, 0, 1.1), material=skin)
    root = _finalize(name, {
        "Head": [head] + face + grin + [hair],
        "Neck": [neck],
        "Torso": [torso, torso2],
        "ArmL": [armL], "ArmR": [armR],
        "LegL": [legL, footL], "LegR": [legR, footR],
    }, marker="BreathOrigin", marker_loc=(0, 0.55, 2.48))
    # optional toggleable green bad-breath puff (disabled visibility flag left to Unity;
    # here we include a small mesh the Unity builder can also drive)
    puff = L.ico("BadBreath", radius=0.12, subdiv=1, scale=(1.4, 1.2, 1.0), loc=(0, 0.62, 2.48), material=BREATH)
    L.parent_keep(puff, root)
    return root


# --------------------------------------------------------------------------- #
# Character 3 - "The Boxer": huge shoulders, thick gloves, confident, clueless
# --------------------------------------------------------------------------- #

def char_boxer(name="P3_Boxer"):
    skin = L.mat("Skin3", (0.36, 0.22, 0.13))
    vest = L.mat("Vest3", (0.15, 0.35, 0.7))
    glove = L.mat("Glove3", (0.75, 0.1, 0.1))
    # broad trapezoid torso
    torso = L.cone("torso", r1=0.62, r2=0.42, depth=0.95, verts=12, loc=(0, 0, 1.15), material=vest)
    shoulders = L.sphere("shoulders", radius=0.72, scale=(1.35, 0.8, 0.55), loc=(0, 0, 1.55), material=skin)
    head = L.sphere("head", radius=0.32, scale=(1.0, 1.0, 1.05), loc=(0, 0, 2.05), material=skin)
    hair = L.sphere("hair", radius=0.33, scale=(1.0, 1.0, 0.55), loc=(0, -0.02, 2.2), material=HAIR_BLK)
    # slightly clueless: eyes wide + spaced, small flat mouth
    face = _eyes(0, 2.08, 0.30, spacing=0.15, r=0.07)
    mouth = L.box("mouth", scale=(0.14, 0.04, 0.03), loc=(0, 0.3, 1.94), material=MOUTH)
    # big arms angled out, huge gloves
    upperL = L.cylinder("upperL", radius=0.15, depth=0.6, verts=8, rot=(0, 0.6, 0), loc=(-0.7, 0, 1.35), material=skin)
    upperR = L.cylinder("upperR", radius=0.15, depth=0.6, verts=8, rot=(0, -0.6, 0), loc=(0.7, 0, 1.35), material=skin)
    gloveL = L.sphere("gloveL", radius=0.26, loc=(-1.0, 0.15, 1.05), material=glove)
    gloveR = L.sphere("gloveR", radius=0.26, loc=(1.0, 0.15, 1.05), material=glove)
    # legs / shorts
    shortsL = L.box("shortsL", scale=(0.3, 0.34, 0.4), loc=(-0.22, 0, 0.75), material=glove)
    shortsR = L.box("shortsR", scale=(0.3, 0.34, 0.4), loc=(0.22, 0, 0.75), material=glove)
    legL = L.cylinder("legL", radius=0.12, depth=0.6, verts=8, loc=(-0.22, 0, 0.3), material=skin)
    legR = L.cylinder("legR", radius=0.12, depth=0.6, verts=8, loc=(0.22, 0, 0.3), material=skin)
    bootL = L.box("bootL", scale=(0.26, 0.4, 0.14), loc=(-0.22, 0.06, 0.07), material=(L.mat("Boot3",(0.9,0.9,0.9))))
    bootR = L.box("bootR", scale=(0.26, 0.4, 0.14), loc=(0.22, 0.06, 0.07), material=(L.mat("Boot3",(0.9,0.9,0.9))))
    return _finalize(name, {
        "Head": [head, hair, mouth] + face,
        "Torso": [torso, shoulders],
        "ArmL": [upperL, gloveL], "ArmR": [upperR, gloveR],
        "LegL": [shortsL, legL, bootL], "LegR": [shortsR, legR, bootR],
    })


# --------------------------------------------------------------------------- #
# Character 4 - "The Slice": pizza-wedge head, pointed jaw, wide eyes, big hair
# --------------------------------------------------------------------------- #

def char_slice(name="P4_Slice"):
    skin = L.mat("Skin4", (0.62, 0.42, 0.28))
    hoodie = L.mat("Hoodie4", (0.15, 0.5, 0.25))
    torso = L.box("torso", scale=(0.5, 0.32, 0.85), loc=(0, 0, 1.1), material=hoodie)
    hood = L.sphere("hood", radius=0.32, scale=(1.0, 1.0, 0.7), loc=(0, -0.1, 1.55), material=hoodie)
    # pizza-slice head: cone pointing DOWN (wide top, pointed jaw), rotated so apex is chin
    head = L.cone("head", r1=0.38, r2=0.02, depth=0.7, verts=3, rot=(math.pi, 0, 0), loc=(0, 0.05, 2.05), material=skin)
    # round off the top of the wedge a touch
    crown = L.sphere("crown", radius=0.38, scale=(1.0, 0.85, 0.45), loc=(0, 0.05, 2.35), material=skin)
    # exaggerated tall hair
    hair1 = L.cone("hair1", r1=0.34, r2=0.05, depth=0.5, verts=10, loc=(0, 0.0, 2.65), material=HAIR_BLK)
    hair2 = L.sphere("hair2", radius=0.3, scale=(1.1, 0.9, 0.7), loc=(0, -0.02, 2.5), material=HAIR_BLK)
    # wide eyes set far apart
    face = _eyes(0, 2.28, 0.28, spacing=0.2, r=0.095)
    grin = _grin(2.05, 0.22, w=0.14)
    armL = L.cylinder("armL", radius=0.09, depth=0.7, verts=6, rot=(0, 0.25, 0), loc=(-0.5, 0, 1.15), material=hoodie)
    armR = L.cylinder("armR", radius=0.09, depth=0.7, verts=6, rot=(0, -0.25, 0), loc=(0.5, 0, 1.15), material=hoodie)
    handL = L.sphere("handL", radius=0.11, loc=(-0.62, 0.05, 0.82), material=skin)
    handR = L.sphere("handR", radius=0.11, loc=(0.62, 0.05, 0.82), material=skin)
    legL = L.cylinder("legL", radius=0.11, depth=0.72, verts=8, loc=(-0.2, 0, 0.36), material=(L.mat("Jeans4",(0.2,0.25,0.4))))
    legR = L.cylinder("legR", radius=0.11, depth=0.72, verts=8, loc=(0.2, 0, 0.36), material=(L.mat("Jeans4",(0.2,0.25,0.4))))
    footL = L.box("footL", scale=(0.22, 0.36, 0.12), loc=(-0.2, 0.06, 0.06), material=(L.mat("Sneak4",(0.9,0.3,0.2))))
    footR = L.box("footR", scale=(0.22, 0.36, 0.12), loc=(0.2, 0.06, 0.06), material=(L.mat("Sneak4",(0.9,0.3,0.2))))
    return _finalize(name, {
        "Head": [head, crown, hair1, hair2] + face + grin,
        "Torso": [torso, hood],
        "ArmL": [armL, handL], "ArmR": [armR, handR],
        "LegL": [legL, footL], "LegR": [legR, footR],
    })


# --------------------------------------------------------------------------- #
# Announcer - "The Host": larger-than-life barkeep with a huge mustache + apron
# --------------------------------------------------------------------------- #

def announcer(name="Announcer_Host"):
    skin = L.mat("SkinH", (0.72, 0.52, 0.38))
    vest = L.mat("VestH", (0.4, 0.1, 0.25))
    shirt = L.mat("ShirtH", (0.9, 0.85, 0.75))
    apron = L.mat("ApronH", (0.35, 0.28, 0.2))
    stache = L.mat("Stache", (0.25, 0.16, 0.08), rough=0.7)
    # big barrel chest
    torso = L.sphere("torso", radius=0.72, scale=(1.0, 0.85, 1.15), loc=(0, 0, 1.35), material=shirt)
    vestM = L.sphere("vest", radius=0.74, scale=(1.02, 0.7, 1.0), loc=(0, -0.12, 1.35), material=vest)
    apronM = L.box("apron", scale=(0.9, 0.1, 1.1), loc=(0, 0.5, 1.0), material=apron)
    head = L.sphere("head", radius=0.4, scale=(1.05, 1.0, 1.05), loc=(0, 0, 2.35), material=skin)
    # bald shiny top + side hair
    sidehair = L.torus("sidehair", major=0.36, minor=0.09, mseg=16, minseg=6, rot=(1.5708,0,0), loc=(0, 0, 2.28), material=HAIR_BLK)
    # gigantic handlebar mustache
    m1 = L.sphere("stacheL", radius=0.15, scale=(1.6, 0.7, 0.7), loc=(-0.16, 0.36, 2.2), material=stache)
    m2 = L.sphere("stacheR", radius=0.15, scale=(1.6, 0.7, 0.7), loc=(0.16, 0.36, 2.2), material=stache)
    eyebrows = L.box("brows", scale=(0.34, 0.05, 0.05), loc=(0, 0.36, 2.46), material=stache)
    face = _eyes(0, 2.38, 0.36, spacing=0.16, r=0.08)
    # welcoming arms held out
    armL = L.cylinder("armL", radius=0.14, depth=0.75, verts=8, rot=(0, 0.9, 0), loc=(-0.7, 0.1, 1.5), material=shirt)
    armR = L.cylinder("armR", radius=0.14, depth=0.75, verts=8, rot=(0, -0.9, 0), loc=(0.7, 0.1, 1.5), material=shirt)
    handL = L.sphere("handL", radius=0.13, loc=(-1.05, 0.35, 1.35), material=skin)
    # host holds a frothy mug
    mug = L.cylinder("mug", radius=0.12, depth=0.24, verts=10, loc=(1.05, 0.35, 1.4), material=(L.mat("MugH",(0.5,0.35,0.2))))
    froth = L.sphere("froth", radius=0.13, scale=(1, 1, 0.5), loc=(1.05, 0.35, 1.55), material=(L.mat("Froth",(0.95,0.93,0.85))))
    legL = L.cylinder("legL", radius=0.15, depth=0.7, verts=8, loc=(-0.24, 0, 0.35), material=(L.mat("TrousersH",(0.18,0.16,0.14))))
    legR = L.cylinder("legR", radius=0.15, depth=0.7, verts=8, loc=(0.24, 0, 0.35), material=(L.mat("TrousersH",(0.18,0.16,0.14))))
    bootL = L.box("bootL", scale=(0.3, 0.42, 0.16), loc=(-0.24, 0.06, 0.08), material=HAIR_BLK)
    bootR = L.box("bootR", scale=(0.3, 0.42, 0.16), loc=(0.24, 0.06, 0.08), material=HAIR_BLK)
    return _finalize(name, {
        "Head": [head, sidehair, m1, m2, eyebrows] + face,
        "Torso": [torso, vestM, apronM],
        "ArmL": [armL, handL], "ArmR": [armR, mug, froth],
        "LegL": [legL, bootL], "LegR": [legR, bootR],
    })
