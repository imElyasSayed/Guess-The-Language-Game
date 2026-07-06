"""
gen_characters.py - Four Beta-Squad-inspired caricatures + one announcer host. (v2)

Humorous, stylized, exaggerated SILHOUETTES (not likenesses) — but finished, not
blockouts: organic capsule limbs with elbows/knees, subdivision-smoothed forms,
full cartoon faces (iris + eye highlight + lids + brows + nose + ears + open
mouths with teeth), mitten hands with thumbs, proper shoes, clothing details
(collars, buttons, laces, bowtie), and an expressive pose per personality.

Each character is a set of logical meshes (Head / Torso / Arms / Legs) parented
to a root empty with its origin at the feet (0,0,0), facing +Y.
"""

import math
import bpy
import gen_lib as L

# --- shared feature materials --------------------------------------------- #
EYE_WHITE = L.mat("EyeWhite",  (0.97, 0.97, 0.94), rough=0.25)
IRIS      = L.mat("IrisBrown", (0.32, 0.18, 0.08), rough=0.3)
PUPIL     = L.mat("Pupil",     (0.03, 0.02, 0.02), rough=0.3)
SPARK     = L.mat("EyeSpark",  (1.0, 1.0, 1.0), rough=0.1, emit=(1, 1, 1), emit_strength=0.6)
MOUTH_IN  = L.mat("MouthIn",   (0.28, 0.07, 0.07), rough=0.7)
TONGUE    = L.mat("Tongue",    (0.75, 0.30, 0.32), rough=0.6)
TEETH     = L.mat("Teeth",     (0.96, 0.96, 0.90), rough=0.35)
HAIR_BLK  = L.mat("HairBlack", (0.05, 0.04, 0.04), rough=0.65)
BREATH    = L.mat("Breath",    (0.4, 0.9, 0.25), rough=0.4, emit=(0.3, 0.9, 0.2), emit_strength=1.5)
GOLD      = L.mat("Gold",      (0.75, 0.55, 0.15), rough=0.25, metal=0.9)
SOLE      = L.mat("SoleWhite", (0.92, 0.90, 0.86), rough=0.6)


# --------------------------------------------------------------------------- #
# Face / body kit
# --------------------------------------------------------------------------- #

def eye_pair(cy, cz, spacing, r, skin, lid=0.0, look=(0.0, 0.0), squint=1.0):
    """Cartoon eyes: white ball, embedded iris, pupil, specular sparkle, optional
    upper lid (0..1 = how heavy/half-closed). look = (x,z) pupil offset. Returns parts."""
    parts = []
    for s in (-1, 1):
        w = L.sphere("eyeW", radius=r, scale=(1, 0.75, squint), loc=(s * spacing, cy, cz))
        w.data.materials.append(EYE_WHITE)
        ix, iz = look
        iris = L.sphere("iris", radius=r * 0.52, segments=12, rings=8,
                        loc=(s * spacing + ix, cy + r * 0.62, cz + iz))
        iris.data.materials.append(IRIS)
        pup = L.sphere("pupil", radius=r * 0.28, segments=10, rings=6,
                       loc=(s * spacing + ix, cy + r * 0.78, cz + iz))
        pup.data.materials.append(PUPIL)
        spark = L.sphere("spark", radius=r * 0.10, segments=8, rings=6,
                         loc=(s * spacing + ix - r * 0.14, cy + r * 0.92, cz + iz + r * 0.20))
        spark.data.materials.append(SPARK)
        parts += [w, iris, pup, spark]
        if lid > 0.0:
            lidS = L.sphere("lid", radius=r * 1.08, scale=(1.05, 0.8, 0.55),
                            loc=(s * spacing, cy - r * 0.06, cz + r * (1.15 - lid * 0.55)))
            lidS.data.materials.append(skin)
            parts.append(lidS)
    return parts


def brow_pair(cy, cz, spacing, width, mat, tilt=0.0, thick=0.05):
    """Eyebrows. tilt > 0 = inner ends raised (worried/clueless), < 0 = angry/confident."""
    parts = []
    for s in (-1, 1):
        b = L.box("brow", scale=(width, 0.05, thick),
                  rot=(0, s * tilt, 0), loc=(s * spacing, cy, cz))
        L.subsurf(b, 1)
        b.data.materials.append(mat)
        parts.append(b)
    return parts


def open_smile(cy, cz, w, h, teeth=True, tongue=False, buck=False):
    """Open cartoon mouth: dark interior, upper teeth strip, optional tongue or
    goofy buck teeth."""
    parts = []
    interior = L.sphere("mouth", radius=1.0, scale=(w, 0.05, h), segments=16, rings=10,
                        loc=(0, cy, cz), material=MOUTH_IN)
    parts.append(interior)
    if teeth and not buck:
        t = L.box("teeth", scale=(w * 1.5, 0.045, h * 0.7), loc=(0, cy + 0.012, cz + h * 0.55))
        L.subsurf(t, 1)
        t.data.materials.append(TEETH)
        parts.append(t)
    if buck:
        for s in (-1, 1):
            t = L.box("buck", scale=(w * 0.55, 0.045, h * 1.3),
                      loc=(s * w * 0.32, cy + 0.02, cz + h * 0.25))
            L.subsurf(t, 1)
            t.data.materials.append(TEETH)
            parts.append(t)
    if tongue:
        tg = L.sphere("tongue", radius=w * 0.55, scale=(1, 0.5, 0.4), segments=12, rings=8,
                      loc=(0, cy + 0.01, cz - h * 0.5), material=TONGUE)
        parts.append(tg)
    return parts


def nose_ball(cy, cz, r, skin):
    n = L.sphere("nose", radius=r, scale=(1.1, 1.0, 0.9), segments=12, rings=8, loc=(0, cy, cz))
    n.data.materials.append(skin)
    return n


def ear_pair(cx, cy, cz, r, skin):
    parts = []
    for s in (-1, 1):
        e = L.sphere("ear", radius=r, scale=(0.5, 0.9, 1.1), segments=10, rings=8,
                     loc=(s * cx, cy, cz))
        e.data.materials.append(skin)
        parts.append(e)
    return parts


def cheek_pair(cx, cy, cz, r, skin):
    parts = []
    for s in (-1, 1):
        c = L.sphere("cheek", radius=r, segments=10, rings=8, loc=(s * cx, cy, cz))
        c.data.materials.append(skin)
        parts.append(c)
    return parts


def hand(center, r, skin, thumb=(1, 0, 0)):
    """Mitten hand with a thumb bump."""
    parts = []
    palm = L.sphere("palm", radius=r, scale=(1.0, 1.15, 0.9), loc=center)
    palm.data.materials.append(skin)
    parts.append(palm)
    t = L.sphere("thumb", radius=r * 0.45, segments=10, rings=6,
                 loc=(center[0] + thumb[0] * r * 0.9,
                      center[1] + thumb[1] * r * 0.9,
                      center[2] + thumb[2] * r * 0.9))
    t.data.materials.append(skin)
    parts.append(t)
    return parts


def shoe(center, length, width, height, mat, sole=True, toe=True):
    """Rounded cartoon shoe pointing +Y, with a sole slab and toe cap."""
    parts = []
    body = L.box("shoe", scale=(width, length, height),
                 loc=(center[0], center[1] + length * 0.1, center[2] + height * 0.5))
    L.subsurf(body, 2)
    body.data.materials.append(mat)
    parts.append(body)
    if sole:
        s = L.box("sole", scale=(width * 1.06, length * 1.08, height * 0.28),
                  loc=(center[0], center[1] + length * 0.1, center[2] + height * 0.12))
        L.subsurf(s, 1)
        s.data.materials.append(SOLE)
        parts.append(s)
    if toe:
        tc = L.sphere("toe", radius=width * 0.5, scale=(1, 0.8, 0.7), segments=12, rings=8,
                      loc=(center[0], center[1] + length * 0.52, center[2] + height * 0.45))
        tc.data.materials.append(mat)
        parts.append(tc)
    return parts


def limb(p_a, p_b, p_c, r, mat, joint_scale=1.15):
    """Two-segment limb (shoulder→elbow→wrist or hip→knee→ankle) with a joint ball."""
    parts = [
        L.capsule("seg1", p_a, p_b, r, material=mat),
        L.capsule("seg2", p_b, p_c, r * 0.92, material=mat),
    ]
    j = L.sphere("joint", radius=r * joint_scale, segments=12, rings=8, loc=p_b)
    j.data.materials.append(mat)
    parts.append(j)
    return parts


def _finalize(name, part_groups, marker=None, marker_loc=None):
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
    skin = L.mat("Skin1", (0.45, 0.28, 0.17))
    shirt = L.mat("Shirt1", (0.72, 0.14, 0.16))
    trous = L.mat("Trousers1", (0.16, 0.14, 0.22))

    # giant near-spherical body
    body = L.sphere("body", radius=0.58, scale=(1.0, 0.95, 1.06), segments=24, rings=16,
                    loc=(0, 0, 0.88), material=shirt)
    hem = L.torus("hem", major=0.36, minor=0.035, mseg=20, minseg=8, loc=(0, 0, 0.40), material=shirt)
    # head atop with no neck
    head = L.sphere("head", radius=0.34, scale=(1.02, 1.0, 1.0), segments=24, rings=16,
                    loc=(0, 0.02, 1.66), material=skin)
    chin = L.sphere("chin", radius=0.13, scale=(1.5, 0.9, 0.6), segments=12, rings=8,
                    loc=(0, 0.22, 1.46), material=skin)  # double chin
    face = cheek_pair(0.17, 0.26, 1.56, 0.09, skin)
    face += [nose_ball(0.36, 1.66, 0.055, skin)]
    face += ear_pair(0.33, 0.02, 1.66, 0.07, skin)
    face += eye_pair(0.30, 1.75, 0.13, 0.072, skin, lid=0.10)
    face += brow_pair(0.31, 1.85, 0.13, 0.09, HAIR_BLK, tilt=0.25)
    face += open_smile(0.335, 1.55, 0.15, 0.075, teeth=True, tongue=True)
    # receding hairline: hair cap pushed high and far back so the dome shines
    hair = L.sphere("hair", radius=0.31, scale=(1.0, 0.80, 0.45), segments=20, rings=12,
                    loc=(0, -0.11, 1.88), material=HAIR_BLK)

    # tiny arms out to the sides, resting on the ball
    armL = limb((-0.50, 0.02, 1.24), (-0.66, 0.10, 1.10), (-0.74, 0.18, 0.98), 0.065, skin)
    armL += hand((-0.77, 0.22, 0.94), 0.085, skin, thumb=(0.6, 0.6, 0))
    armR = limb((0.50, 0.02, 1.24), (0.66, 0.10, 1.10), (0.74, 0.18, 0.98), 0.065, skin)
    armR += hand((0.77, 0.22, 0.94), 0.085, skin, thumb=(-0.6, 0.6, 0))
    # stubby legs + big shoes poking out in front
    legL = limb((-0.22, 0, 0.40), (-0.24, 0.02, 0.24), (-0.25, 0.02, 0.12), 0.085, trous)
    legL += shoe((-0.25, 0.08, 0.0), 0.20, 0.13, 0.10, HAIR_BLK)
    legR = limb((0.22, 0, 0.40), (0.24, 0.02, 0.24), (0.25, 0.02, 0.12), 0.085, trous)
    legR += shoe((0.25, 0.08, 0.0), 0.20, 0.13, 0.10, HAIR_BLK)

    return _finalize(name, {
        "Head": [head, chin, hair] + face,
        "Torso": [body, hem],
        "ArmL": armL, "ArmR": armR,
        "LegL": legL, "LegR": legR,
    })


# --------------------------------------------------------------------------- #
# Character 2 - "The Giraffe": long neck, skinny, goofy grin, bad-breath toggle
# --------------------------------------------------------------------------- #

def char_giraffe(name="P2_Giraffe"):
    skin = L.mat("Skin2", (0.55, 0.36, 0.22))
    shirt = L.mat("Shirt2", (0.88, 0.72, 0.16))
    trous = L.mat("Trousers2", (0.20, 0.20, 0.26))

    # skinny torso with slight slouch
    torso = L.capsule("torso", (0, 0, 1.02), (0, 0.02, 1.52), 0.17, material=shirt)
    shoulders = L.capsule("shoulders", (-0.19, 0.02, 1.52), (0.19, 0.02, 1.52), 0.115, material=shirt)
    collar = L.torus("collar", major=0.105, minor=0.035, mseg=14, minseg=8,
                     loc=(0, 0.03, 1.60), material=shirt)
    # THE neck — comically long, leaning forward
    neck = L.capsule("neck", (0, 0.03, 1.58), (0, 0.14, 2.28), 0.082, material=skin)
    adam = L.sphere("adam", radius=0.04, segments=10, rings=6, loc=(0, 0.20, 1.96), material=skin)
    # head with a small chin, big goofy features
    head = L.sphere("head", radius=0.26, scale=(0.88, 1.05, 1.05), segments=24, rings=16,
                    loc=(0, 0.16, 2.44), material=skin)
    jaw = L.sphere("jaw", radius=0.10, scale=(1.3, 1.0, 0.8), segments=12, rings=8,
                   loc=(0, 0.30, 2.28), material=skin)
    face = [nose_ball(0.43, 2.40, 0.058, skin)]
    face += ear_pair(0.23, 0.14, 2.44, 0.07, skin)
    # half-lidded goofy eyes, brows way up
    face += eye_pair(0.33, 2.53, 0.10, 0.075, skin, lid=0.35)
    face += brow_pair(0.34, 2.62, 0.10, 0.08, HAIR_BLK, tilt=0.3)
    # huge open grin with buck teeth, low on the face
    face += open_smile(0.375, 2.26, 0.13, 0.08, buck=True, tongue=True)
    face += cheek_pair(0.15, 0.32, 2.31, 0.06, skin)
    hair = L.sphere("hair", radius=0.24, scale=(0.95, 0.9, 0.42), segments=20, rings=12,
                    loc=(0, 0.08, 2.68), material=HAIR_BLK)

    # long thin arms, awkward slight bend
    armL = limb((-0.22, 0.02, 1.52), (-0.27, 0.05, 1.14), (-0.29, 0.12, 0.80), 0.05, skin)
    armL += hand((-0.30, 0.15, 0.74), 0.07, skin, thumb=(0.6, 0.5, 0))
    armR = limb((0.22, 0.02, 1.52), (0.27, 0.05, 1.14), (0.29, 0.12, 0.80), 0.05, skin)
    armR += hand((0.30, 0.15, 0.74), 0.07, skin, thumb=(-0.6, 0.5, 0))
    # long thin legs
    legL = limb((-0.11, 0, 0.95), (-0.12, 0.04, 0.50), (-0.12, 0.02, 0.12), 0.06, trous)
    legL += shoe((-0.12, 0.05, 0.0), 0.16, 0.09, 0.09, HAIR_BLK)
    legR = limb((0.11, 0, 0.95), (0.12, 0.04, 0.50), (0.12, 0.02, 0.12), 0.06, trous)
    legR += shoe((0.12, 0.05, 0.0), 0.16, 0.09, 0.09, HAIR_BLK)

    root = _finalize(name, {
        "Head": [head, jaw, hair] + face,
        "Neck": [neck, adam],
        "Torso": [torso, shoulders, collar],
        "ArmL": armL, "ArmR": armR,
        "LegL": legL, "LegR": legR,
    }, marker="BreathOrigin", marker_loc=(0, 0.55, 2.30))
    # toggleable green bad-breath puff, drifting away from the mouth
    # (Unity starts it hidden; previews hide it too — see hide_breath())
    puff = L.ico("BadBreath", radius=0.09, subdiv=2, scale=(1.2, 1.6, 1.0),
                 loc=(0, 0.68, 2.28), material=BREATH)
    puff2 = L.ico("puff2", radius=0.05, subdiv=2, loc=(0.06, 0.86, 2.32), material=BREATH)
    puff = L.join([puff, puff2], "BadBreath")
    L.parent_keep(puff, root)
    return root


def hide_breath():
    """Hide the giraffe's breath puff for preview renders (it ships toggled-off)."""
    o = bpy.data.objects.get("BadBreath")
    if o:
        o.hide_render = True


# --------------------------------------------------------------------------- #
# Character 3 - "The Boxer": huge shoulders, thick gloves, guard pose, clueless
# --------------------------------------------------------------------------- #

def char_boxer(name="P3_Boxer"):
    skin = L.mat("Skin3", (0.30, 0.19, 0.11))
    vest = L.mat("Vest3", (0.16, 0.36, 0.72))
    glove = L.mat("Glove3", (0.78, 0.10, 0.10))
    lace = L.mat("Lace3", (0.95, 0.95, 0.92), rough=0.5)

    # tapered power torso (subsurf-smoothed cone) + colossal shoulder bar
    torso = L.cone("torso", r1=0.34, r2=0.27, depth=0.85, verts=14, loc=(0, 0, 1.10), material=vest)
    L.subsurf(torso, 2)
    shoulders = L.capsule("shoulders", (-0.42, 0, 1.50), (0.42, 0, 1.50), 0.22, material=skin)
    trapL = L.sphere("trapL", radius=0.18, scale=(1.2, 0.9, 0.8), segments=14, rings=10,
                     loc=(-0.20, 0, 1.60), material=skin)
    trapR = L.sphere("trapR", radius=0.18, scale=(1.2, 0.9, 0.8), segments=14, rings=10,
                     loc=(0.20, 0, 1.60), material=skin)
    # vest trim
    trim = L.torus("trim", major=0.285, minor=0.03, mseg=18, minseg=8, loc=(0, 0, 1.44), material=lace)
    belt = L.torus("belt", major=0.235, minor=0.05, mseg=18, minseg=8, loc=(0, 0, 0.76), material=GOLD)
    buckle = L.sphere("buckle", radius=0.06, scale=(1.2, 0.5, 1.2), segments=12, rings=8,
                      loc=(0, 0.235, 0.76), material=GOLD)
    # thick neck, smallish head
    neck = L.capsule("neck", (0, 0, 1.58), (0, 0.02, 1.74), 0.12, material=skin)
    head = L.sphere("head", radius=0.24, scale=(1.05, 1.0, 1.05), segments=24, rings=16,
                    loc=(0, 0.02, 1.90), material=skin)
    jaw = L.sphere("jaw", radius=0.14, scale=(1.5, 1.0, 0.7), segments=12, rings=8,
                   loc=(0, 0.10, 1.76), material=skin)
    # squashed boxer nose, cauliflower ears
    face = [nose_ball(0.27, 1.90, 0.06, skin)]
    face += ear_pair(0.245, 0.04, 1.90, 0.075, skin)
    # tiny wide-set clueless eyes, inner brows raised, little "duh" mouth
    face += eye_pair(0.22, 1.97, 0.105, 0.052, skin, look=(0.008, 0))
    face += brow_pair(0.225, 2.06, 0.105, 0.075, HAIR_BLK, tilt=0.5, thick=0.06)
    duh = L.sphere("duh", radius=0.038, scale=(1.3, 0.4, 0.9), segments=10, rings=8,
                   loc=(0, 0.29, 1.72), material=MOUTH_IN)
    face.append(duh)
    hair = L.sphere("hair", radius=0.245, scale=(1.0, 0.95, 0.45), segments=20, rings=12,
                    loc=(0, 0.0, 2.05), material=HAIR_BLK)

    # guard pose: elbows out, gigantic gloves up in front of the chest
    def arm(s):
        parts = limb((s * 0.44, 0.02, 1.50), (s * 0.58, 0.12, 1.26), (s * 0.36, 0.32, 1.30),
                     0.10, skin)
        g = L.sphere("glove", radius=0.20, scale=(1.0, 1.15, 1.0), segments=18, rings=12,
                     loc=(s * 0.31, 0.42, 1.32), material=glove)
        cuff = L.torus("cuff", major=0.115, minor=0.035, mseg=14, minseg=8,
                       rot=(0, math.radians(90 * s), math.radians(35 * s)),
                       loc=(s * 0.38, 0.28, 1.30), material=lace)
        lc = L.box("laces", scale=(0.06, 0.10, 0.02), rot=(0.4, 0, 0),
                   loc=(s * 0.30, 0.44, 1.44))
        L.subsurf(lc, 1)
        lc.data.materials.append(lace)
        return parts + [g, cuff, lc]

    # shorts + strong legs + boxing boots
    shorts = L.cone("shorts", r1=0.28, r2=0.22, depth=0.30, verts=14, loc=(0, 0, 0.62), material=glove)
    L.subsurf(shorts, 2)
    def leg(s):
        parts = limb((s * 0.15, 0, 0.55), (s * 0.16, 0.03, 0.32), (s * 0.16, 0.0, 0.14), 0.085, skin)
        parts += shoe((s * 0.16, 0.04, 0.0), 0.18, 0.115, 0.11, SOLE, sole=False)
        # boot stripe
        st = L.torus("stripe", major=0.09, minor=0.02, mseg=12, minseg=6,
                     loc=(s * 0.16, 0.02, 0.16), material=glove)
        return parts + [st]

    return _finalize(name, {
        "Head": [head, jaw, hair] + face,
        "Neck": [neck],
        "Torso": [torso, shoulders, trapL, trapR, trim, belt, buckle, shorts],
        "ArmL": arm(-1), "ArmR": arm(1),
        "LegL": leg(-1), "LegR": leg(1),
    })


# --------------------------------------------------------------------------- #
# Character 4 - "The Slice": pizza-wedge head, pointed jaw, wide eyes, tall hair
# --------------------------------------------------------------------------- #

def char_slice(name="P4_Slice"):
    skin = L.mat("Skin4", (0.72, 0.52, 0.36))
    hoodie = L.mat("Hoodie4", (0.14, 0.48, 0.26))
    jeans = L.mat("Jeans4", (0.22, 0.28, 0.44))
    sneak = L.mat("Sneak4", (0.85, 0.25, 0.18))

    # hoodie torso with hood ring, pocket and drawstrings
    torso = L.capsule("torso", (0, 0, 1.00), (0, 0, 1.48), 0.21, material=hoodie)
    hood = L.torus("hood", major=0.14, minor=0.07, mseg=16, minseg=10, rot=(0.5, 0, 0),
                   loc=(0, -0.06, 1.58), material=hoodie)
    pocket = L.box("pocket", scale=(0.24, 0.06, 0.11), loc=(0, 0.19, 1.02))
    L.subsurf(pocket, 1)
    pocket.data.materials.append(hoodie)
    strings = []
    for s in (-1, 1):
        st = L.cylinder("string", radius=0.012, depth=0.16, verts=6,
                        rot=(0.15, 0, 0), loc=(s * 0.07, 0.21, 1.42), material=SOLE)
        strings.append(st)

    # neck connecting the wedge to the hoodie
    neck = L.capsule("neck", (0, 0.02, 1.58), (0, 0.04, 1.74), 0.062, material=skin)
    # pizza-wedge head: 3-sided cone (apex = pointed chin), subsurf-softened
    head = L.cone("head", r1=0.33, r2=0.03, depth=0.55, verts=3,
                  rot=(math.pi, 0, math.radians(60)), loc=(0, 0.05, 1.92), material=skin)
    L.subsurf(head, 2)
    crown = L.sphere("crown", radius=0.27, scale=(1.05, 0.9, 0.42), segments=20, rings=12,
                     loc=(0, 0.05, 2.18), material=skin)
    # tall high-top fade
    fade = L.cylinder("fade", radius=0.20, depth=0.34, verts=14, loc=(0, 0.03, 2.36), material=HAIR_BLK)
    L.subsurf(fade, 2)
    fadeTop = L.sphere("fadeTop", radius=0.20, scale=(1, 1, 0.4), segments=16, rings=10,
                       loc=(0, 0.03, 2.52), material=HAIR_BLK)
    # big wide eyes on the wedge
    face = eye_pair(0.17, 2.03, 0.10, 0.08, skin, look=(0, 0.004), squint=1.1)
    face += brow_pair(0.21, 2.14, 0.11, 0.08, HAIR_BLK, tilt=-0.15)
    face += [nose_ball(0.21, 1.92, 0.045, skin)]
    face += ear_pair(0.19, 0.04, 2.06, 0.065, skin)
    # grin near the pointed chin
    face += open_smile(0.135, 1.78, 0.085, 0.055, teeth=True)
    face += cheek_pair(0.10, 0.14, 1.83, 0.05, skin)

    armL = limb((-0.25, 0, 1.44), (-0.31, 0.05, 1.10), (-0.29, 0.13, 0.80), 0.068, hoodie)
    armL += hand((-0.29, 0.17, 0.73), 0.078, skin, thumb=(0.6, 0.5, 0))
    armR = limb((0.25, 0, 1.44), (0.31, 0.05, 1.10), (0.29, 0.13, 0.80), 0.068, hoodie)
    armR += hand((0.29, 0.17, 0.73), 0.078, skin, thumb=(-0.6, 0.5, 0))
    legL = limb((-0.13, 0, 0.88), (-0.14, 0.03, 0.46), (-0.14, 0.0, 0.14), 0.078, jeans)
    legL += shoe((-0.14, 0.05, 0.0), 0.17, 0.105, 0.10, sneak)
    legR = limb((0.13, 0, 0.88), (0.14, 0.03, 0.46), (0.14, 0.0, 0.14), 0.078, jeans)
    legR += shoe((0.14, 0.05, 0.0), 0.17, 0.105, 0.10, sneak)

    return _finalize(name, {
        "Head": [head, crown, fade, fadeTop] + face,
        "Neck": [neck],
        "Torso": [torso, hood, pocket] + strings,
        "ArmL": armL, "ArmR": armR,
        "LegL": legL, "LegR": legR,
    })


# --------------------------------------------------------------------------- #
# Announcer - "The Host": big-bellied barkeep, handlebar mustache, bowtie,
# monocle, towel on the shoulder, mug raised in a toast.
# --------------------------------------------------------------------------- #

def announcer(name="Announcer_Host"):
    skin = L.mat("SkinH", (0.78, 0.55, 0.40))
    vest = L.mat("VestH", (0.42, 0.10, 0.24))
    shirt = L.mat("ShirtH", (0.93, 0.90, 0.82))
    stache = L.mat("Stache", (0.28, 0.18, 0.09), rough=0.65)
    towel = L.mat("TowelH", (0.85, 0.88, 0.90), rough=0.8)
    trous = L.mat("TrousersH", (0.17, 0.15, 0.13))
    mugm = L.mat("MugH", (0.48, 0.32, 0.18), rough=0.6)
    froth = L.mat("Froth", (0.96, 0.94, 0.86), rough=0.7)

    # grand belly + chest in the maroon vest, white shirt peeking at the chest,
    # gold buttons riding the front curve
    belly = L.sphere("belly", radius=0.52, scale=(1.0, 0.92, 1.05), segments=24, rings=16,
                     loc=(0, 0.05, 1.12), material=vest)
    chest = L.sphere("chest", radius=0.40, scale=(1.05, 0.85, 0.9), segments=20, rings=14,
                     loc=(0, 0, 1.48), material=vest)
    shirtV = L.sphere("shirtV", radius=0.15, scale=(1.0, 0.45, 1.15), segments=14, rings=10,
                      loc=(0, 0.335, 1.54), material=shirt)
    buttons = []
    for y, z in ((0.52, 1.30), (0.55, 1.13), (0.52, 0.96)):
        b = L.sphere("btn", radius=0.03, segments=10, rings=6, loc=(0, y, z), material=GOLD)
        buttons.append(b)
    bow = []
    bowC = L.sphere("bowC", radius=0.035, segments=10, rings=6, loc=(0, 0.36, 1.66), material=vest)
    bow.append(bowC)
    for s in (-1, 1):
        w = L.cone("bowW", r1=0.05, r2=0.015, depth=0.09, verts=8,
                   rot=(0, math.radians(90 * s), 0), loc=(s * 0.06, 0.355, 1.66), material=vest)
        bow.append(w)
    # towel draped over the left shoulder
    tw = L.box("towel", scale=(0.14, 0.05, 0.34), rot=(0.15, 0, 0.25), loc=(-0.32, 0.02, 1.62))
    L.subsurf(tw, 2)
    tw.data.materials.append(towel)

    # head: bald dome, ring of side hair, epic features
    neck = L.capsule("neck", (0, 0, 1.62), (0, 0.02, 1.72), 0.11, material=skin)
    head = L.sphere("head", radius=0.30, scale=(1.05, 1.0, 1.05), segments=24, rings=16,
                    loc=(0, 0.02, 1.92), material=skin)
    sidehair = L.torus("sidehair", major=0.235, minor=0.07, mseg=18, minseg=8,
                       loc=(0, -0.10, 1.85), material=HAIR_BLK)
    face = [nose_ball(0.335, 1.92, 0.075, skin)]
    face += ear_pair(0.30, 0.02, 1.90, 0.08, skin)
    face += cheek_pair(0.15, 0.28, 1.83, 0.075, skin)
    face += eye_pair(0.265, 2.00, 0.115, 0.062, skin, lid=0.2)
    # majestic bushy brows
    face += brow_pair(0.27, 2.10, 0.115, 0.10, stache, tilt=-0.2, thick=0.07)
    # monocle over the right eye
    mono = L.torus("monocle", major=0.075, minor=0.012, mseg=16, minseg=6,
                   rot=(math.radians(90), 0, 0), loc=(0.115, 0.315, 2.00), material=GOLD)
    chain = L.cylinder("chain", radius=0.006, depth=0.22, verts=6,
                       rot=(0.3, 0.5, 0), loc=(0.20, 0.28, 1.88), material=GOLD)
    face += [mono, chain]
    # handlebar mustache: thick bars with upturned curl tips
    for s in (-1, 1):
        bar = L.sphere("stB", radius=0.10, scale=(1.7, 0.65, 0.55), segments=14, rings=10,
                       rot=(0, s * 0.25, 0), loc=(s * 0.12, 0.315, 1.84), material=stache)
        tip = L.sphere("stT", radius=0.045, segments=10, rings=8,
                       loc=(s * 0.265, 0.28, 1.90), material=stache)
        face += [bar, tip]
    # warm smile under the mustache
    face += open_smile(0.325, 1.76, 0.10, 0.05, teeth=True)

    # left arm welcoming outward, right arm raising the mug in a toast
    armL = limb((-0.42, 0, 1.50), (-0.62, 0.12, 1.30), (-0.76, 0.30, 1.22), 0.09, shirt)
    armL += hand((-0.80, 0.35, 1.20), 0.10, skin, thumb=(0.3, 0.6, 0.4))
    cuffL = L.torus("cuffL", major=0.095, minor=0.028, mseg=12, minseg=6,
                    rot=(0, math.radians(60), 0), loc=(-0.70, 0.24, 1.25), material=shirt)
    armR = limb((0.42, 0, 1.50), (0.60, 0.10, 1.38), (0.60, 0.16, 1.66), 0.09, shirt)
    armR += hand((0.60, 0.18, 1.74), 0.10, skin, thumb=(-0.6, 0.4, 0))
    cuffR = L.torus("cuffR", major=0.095, minor=0.028, mseg=12, minseg=6,
                    loc=(0.60, 0.13, 1.56), material=shirt)
    mug = L.cylinder("mug", radius=0.10, depth=0.20, verts=14, loc=(0.60, 0.20, 1.88), material=mugm)
    mugH = L.torus("mugH", major=0.055, minor=0.018, mseg=12, minseg=6,
                   rot=(math.radians(90), 0, 0), loc=(0.72, 0.20, 1.88), material=mugm)
    foam = L.sphere("foam", radius=0.105, scale=(1, 1, 0.55), segments=14, rings=10,
                    loc=(0.60, 0.20, 2.00), material=froth)

    legL = limb((-0.18, 0, 0.62), (-0.19, 0.02, 0.36), (-0.19, 0.0, 0.14), 0.10, trous)
    legL += shoe((-0.19, 0.05, 0.0), 0.19, 0.12, 0.11, HAIR_BLK, sole=False)
    legR = limb((0.18, 0, 0.62), (0.19, 0.02, 0.36), (0.19, 0.0, 0.14), 0.10, trous)
    legR += shoe((0.19, 0.05, 0.0), 0.19, 0.12, 0.11, HAIR_BLK, sole=False)

    return _finalize(name, {
        "Head": [head, sidehair] + face,
        "Neck": [neck],
        "Torso": [belly, chest, shirtV, tw] + buttons + bow,
        "ArmL": armL + [cuffL], "ArmR": armR + [cuffR, mug, mugH, foam],
        "LegL": legL, "LegR": legR,
    })
