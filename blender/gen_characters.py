"""
gen_characters.py - The five Beta Squad avatars + one announcer host. (v3)

Humorous stylized caricatures built from the reference photos (Chunkz.jpeg,
Niko.jpeg, Kenny.jpeg, sharks.jpeg, AJ.jpeg in the repo root) — exaggerated
cartoon avatars, not realistic likenesses. Signature features per the refs:

  P1_Chunkz : huge round body, TEETH GAP, black hoodie, black cap w/ blonde fringe
  P2_Niko   : giraffe neck + toggleable stinky breath, GREEN sunglasses, suit + gold tie
  P3_Kenny  : lean boxer, shirtless + gold chain, razor-sharp pushed-back hairline, goatee
  P4_Sharky : slim, backwards black cap, full beard, black hoodie with gold patch
  P5_AJ     : big Somali curls with golden tips, blazer + white shirt, chain, goatee

Each avatar is a set of logical meshes (Head / Neck / Torso / Arms / Legs)
parented to a root empty with its origin at the feet (0,0,0), facing +Y.
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
BEARD     = L.mat("Beard",     (0.07, 0.055, 0.045), rough=0.7)
BREATH    = L.mat("Breath",    (0.4, 0.9, 0.25), rough=0.4, emit=(0.3, 0.9, 0.2), emit_strength=1.5)
GOLD      = L.mat("Gold",      (0.75, 0.55, 0.15), rough=0.25, metal=0.9)
SOLE      = L.mat("SoleWhite", (0.92, 0.90, 0.86), rough=0.6)
BLONDE    = L.mat("Blonde",    (0.82, 0.62, 0.28), rough=0.55)
NDL_GREEN = L.mat("NdlGreen",  (0.12, 0.75, 0.12), rough=0.35)
LENS_DRK  = L.mat("LensDark",  (0.02, 0.02, 0.03), rough=0.15)
CURL_TIP  = L.mat("CurlTip",   (0.88, 0.68, 0.26), rough=0.5)


# --------------------------------------------------------------------------- #
# Face / body kit
# --------------------------------------------------------------------------- #

def eye_pair(cy, cz, spacing, r, skin, lid=0.0, look=(0.0, 0.0), squint=1.0):
    """Cartoon eyes: white ball, embedded iris, pupil, specular sparkle, optional
    upper lid (0..1 = how heavy/half-closed). look = (x,z) pupil offset."""
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
    """Eyebrows. tilt > 0 = inner ends raised (worried/clueless), < 0 = confident."""
    parts = []
    for s in (-1, 1):
        b = L.box("brow", scale=(width, 0.05, thick),
                  rot=(0, s * tilt, 0), loc=(s * spacing, cy, cz))
        L.subsurf(b, 1)
        b.data.materials.append(mat)
        parts.append(b)
    return parts


def open_smile(cy, cz, w, h, teeth=True, tongue=False, buck=False, gap=False):
    """Open cartoon mouth: dark interior, then either a full teeth strip, goofy
    buck teeth, or two big front teeth with a GAP between them."""
    parts = []
    interior = L.sphere("mouth", radius=1.0, scale=(w, 0.05, h), segments=16, rings=10,
                        loc=(0, cy, cz), material=MOUTH_IN)
    parts.append(interior)
    if gap:
        # two big front teeth separated by a clear dark gap, proud of the lips
        for s in (-1, 1):
            t = L.box("gaptooth", scale=(w * 0.48, 0.07, h * 1.15),
                      loc=(s * w * 0.38, cy + 0.05, cz + h * 0.22))
            L.subsurf(t, 1)
            t.data.materials.append(TEETH)
            parts.append(t)
    elif buck:
        for s in (-1, 1):
            t = L.box("buck", scale=(w * 0.55, 0.045, h * 1.3),
                      loc=(s * w * 0.32, cy + 0.02, cz + h * 0.25))
            L.subsurf(t, 1)
            t.data.materials.append(TEETH)
            parts.append(t)
    elif teeth:
        t = L.box("teeth", scale=(w * 1.5, 0.045, h * 0.7), loc=(0, cy + 0.012, cz + h * 0.55))
        L.subsurf(t, 1)
        t.data.materials.append(TEETH)
        parts.append(t)
    if tongue:
        tg = L.sphere("tongue", radius=w * 0.45, scale=(1, 0.5, 0.4), segments=12, rings=8,
                      loc=(0, cy + 0.008, cz - h * 0.6), material=TONGUE)
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


def goatee(cy, cz, w=0.09, mustache_z=None):
    """Chin patch + optional thin mustache bar."""
    parts = []
    g = L.sphere("goatee", radius=w, scale=(1.1, 0.6, 0.8), segments=12, rings=8,
                 loc=(0, cy, cz), material=BEARD)
    parts.append(g)
    if mustache_z is not None:
        m = L.box("stache", scale=(w * 1.7, 0.04, 0.028), loc=(0, cy + 0.01, mustache_z))
        L.subsurf(m, 1)
        m.data.materials.append(BEARD)
        parts.append(m)
    return parts


def full_beard(head_c, head_r, y_scale=1.0):
    """Full beard: dark shell hugging the lower half of the head."""
    b = L.sphere("beard", radius=head_r * 1.04, scale=(1.0, y_scale, 0.78),
                 segments=20, rings=14,
                 loc=(head_c[0], head_c[1] + head_r * 0.06, head_c[2] - head_r * 0.38),
                 material=BEARD)
    return [b]


def chain(cz, r, drop=0.05):
    """Thin gold chain around the neck with a slight front droop."""
    c = L.torus("chain", major=r, minor=0.014, mseg=20, minseg=6,
                rot=(0.28, 0, 0), loc=(0, drop, cz), material=GOLD)
    return [c]


def green_shades(cy, cz, spacing, lens_w=0.105, lens_h=0.08):
    """Niko's iconic green-framed sunglasses (worn over the eyes)."""
    parts = []
    for s in (-1, 1):
        frame = L.box("frame", scale=(lens_w * 2.3, 0.05, lens_h * 2.3),
                      loc=(s * spacing, cy, cz))
        L.subsurf(frame, 2)
        frame.data.materials.append(NDL_GREEN)
        lens = L.box("lens", scale=(lens_w * 1.8, 0.055, lens_h * 1.8),
                     loc=(s * spacing, cy + 0.012, cz))
        L.subsurf(lens, 2)
        lens.data.materials.append(LENS_DRK)
        # temple arm running back toward the ear
        arm = L.box("temple", scale=(0.03, 0.22, 0.025),
                    loc=(s * (spacing + lens_w * 1.15), cy - 0.13, cz + 0.01))
        arm.data.materials.append(NDL_GREEN)
        parts += [frame, lens, arm]
    bridge = L.box("bridge", scale=(spacing * 0.9, 0.04, 0.03), loc=(0, cy, cz + 0.01))
    bridge.data.materials.append(NDL_GREEN)
    parts.append(bridge)
    return parts


def cap(head_c, head_r, brim_forward=True, cap_mat=HAIR_BLK, fringe=False):
    """Baseball cap: dome + brim (forward or backwards). Optionally a blonde
    wig fringe poking out under the front (the Chunkz look)."""
    parts = []
    dome = L.sphere("capDome", radius=head_r * 1.06, scale=(1.0, 1.0, 0.62),
                    segments=20, rings=12,
                    loc=(head_c[0], head_c[1] - head_r * 0.04, head_c[2] + head_r * 0.42),
                    material=cap_mat)
    parts.append(dome)
    ydir = 1.0 if brim_forward else -1.0
    brim = L.box("brim", scale=(head_r * 1.5, head_r * 1.1, 0.05),
                 rot=(-0.18 * ydir, 0, 0),
                 loc=(head_c[0], head_c[1] + ydir * head_r * 1.25, head_c[2] + head_r * 0.62))
    L.subsurf(brim, 1)
    brim.data.materials.append(cap_mat)
    parts.append(brim)
    btn = L.sphere("capBtn", radius=0.03, segments=8, rings=6,
                   loc=(head_c[0], head_c[1] - head_r * 0.04, head_c[2] + head_r * 1.06),
                   material=cap_mat)
    parts.append(btn)
    if fringe:
        # blonde wig fringe bulging out under the front edge of the cap
        for sx in (-0.30, -0.15, 0.0, 0.15, 0.30):
            f = L.sphere("fringe", radius=0.075, scale=(1.0, 0.75, 1.25), segments=10, rings=8,
                         loc=(head_c[0] + sx * head_r * 2.2,
                              head_c[1] + head_r * (0.98 - abs(sx) * 0.55),
                              head_c[2] + head_r * 0.38), material=BLONDE)
            parts.append(f)
    return parts


def somali_curls(head_c, head_r):
    """AJ's big curly crown: a cloud of curl balls, dark roots with golden tips."""
    parts = []
    # deterministic ring arrangement, tips golden on the outer/upper curls
    rings_spec = [
        (0.55, 8, 0.115, True),    # outer high ring - golden tips
        (0.30, 8, 0.13, False),    # mid ring - dark
        (0.72, 6, 0.10, True),     # top crown - golden
        (0.10, 4, 0.14, False),    # inner filler - dark
    ]
    base = L.sphere("curlBase", radius=head_r * 0.98, scale=(1.05, 1.0, 0.72),
                    segments=16, rings=10,
                    loc=(head_c[0], head_c[1] - head_r * 0.05, head_c[2] + head_r * 0.52),
                    material=HAIR_BLK)
    parts.append(base)
    for zf, count, cr, tips in rings_spec:
        for i in range(count):
            a = (2 * math.pi / count) * i + zf  # phase offset per ring
            rr = head_r * (1.05 - zf * 0.45)
            x = head_c[0] + rr * math.cos(a)
            y = head_c[1] - head_r * 0.05 + rr * 0.85 * math.sin(a)
            z = head_c[2] + head_r * (0.45 + zf * 0.75)
            c = L.ico("curl", radius=cr, subdiv=1, loc=(x, y, z), material=HAIR_BLK)
            parts.append(c)
            if tips and i % 2 == 0:
                # small golden tip riding on top of the dark curl (dyed ends)
                t = L.ico("curlTip", radius=cr * 0.55, subdiv=1,
                          loc=(x * 1.02, y * 1.02, z + cr * 0.55), material=CURL_TIP)
                parts.append(t)
    return parts


def hand(center, r, skin, thumb=(1, 0, 0)):
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
# P1 - CHUNKZ: huge round body, teeth gap, black hoodie, cap + blonde fringe
# --------------------------------------------------------------------------- #

def char_chunkz(name="P1_Chunkz"):
    skin = L.mat("SkinChunkz", (0.52, 0.34, 0.22))
    hoodie = L.mat("HoodieChunkz", (0.10, 0.10, 0.12))
    trous = L.mat("TrousersChunkz", (0.16, 0.14, 0.22))

    # giant near-spherical hoodie body
    body = L.sphere("body", radius=0.58, scale=(1.0, 0.95, 1.06), segments=24, rings=16,
                    loc=(0, 0, 0.88), material=hoodie)
    hem = L.torus("hem", major=0.36, minor=0.035, mseg=20, minseg=8, loc=(0, 0, 0.40), material=hoodie)
    pocket = L.box("pocket", scale=(0.30, 0.07, 0.14), loc=(0, 0.52, 0.78))
    L.subsurf(pocket, 1)
    pocket.data.materials.append(hoodie)
    strings = []
    for s in (-1, 1):
        st = L.cylinder("string", radius=0.014, depth=0.18, verts=6,
                        rot=(0.2, 0, 0), loc=(s * 0.09, 0.54, 1.28), material=SOLE)
        strings.append(st)

    # round head, chubby face
    head = L.sphere("head", radius=0.34, scale=(1.02, 1.0, 1.0), segments=24, rings=16,
                    loc=(0, 0.02, 1.66), material=skin)
    chin = L.sphere("chin", radius=0.13, scale=(1.5, 0.9, 0.6), segments=12, rings=8,
                    loc=(0, 0.22, 1.46), material=skin)
    face = cheek_pair(0.17, 0.26, 1.56, 0.09, skin)
    face += [nose_ball(0.36, 1.66, 0.055, skin)]
    face += ear_pair(0.33, 0.02, 1.66, 0.07, skin)
    face += eye_pair(0.30, 1.75, 0.13, 0.072, skin, lid=0.10)
    face += brow_pair(0.31, 1.85, 0.13, 0.09, HAIR_BLK, tilt=0.25)
    # signature: big open grin with THE TOOTH GAP
    face += open_smile(0.335, 1.55, 0.16, 0.08, gap=True, tongue=True)
    # chin-strap beard shadow
    face += goatee(0.315, 1.47, w=0.10)
    # black cap with the blonde wig fringe
    face += cap((0, 0.02, 1.72), 0.325, brim_forward=True, cap_mat=HAIR_BLK, fringe=True)

    armL = limb((-0.50, 0.02, 1.24), (-0.66, 0.10, 1.10), (-0.74, 0.18, 0.98), 0.075, hoodie)
    armL += hand((-0.77, 0.22, 0.94), 0.085, skin, thumb=(0.6, 0.6, 0))
    armR = limb((0.50, 0.02, 1.24), (0.66, 0.10, 1.10), (0.74, 0.18, 0.98), 0.075, hoodie)
    armR += hand((0.77, 0.22, 0.94), 0.085, skin, thumb=(-0.6, 0.6, 0))
    legL = limb((-0.22, 0, 0.40), (-0.24, 0.02, 0.24), (-0.25, 0.02, 0.12), 0.085, trous)
    legL += shoe((-0.25, 0.08, 0.0), 0.20, 0.13, 0.10, HAIR_BLK)
    legR = limb((0.22, 0, 0.40), (0.24, 0.02, 0.24), (0.25, 0.02, 0.12), 0.085, trous)
    legR += shoe((0.25, 0.08, 0.0), 0.20, 0.13, 0.10, HAIR_BLK)

    return _finalize(name, {
        "Head": [head, chin] + face,
        "Torso": [body, hem, pocket] + strings,
        "ArmL": armL, "ArmR": armR,
        "LegL": legL, "LegR": legR,
    })


# --------------------------------------------------------------------------- #
# P2 - NIKO: giraffe neck + stinky breath, green shades, suit + gold tie
# --------------------------------------------------------------------------- #

def char_niko(name="P2_Niko"):
    skin = L.mat("SkinNiko", (0.58, 0.40, 0.26))
    suit = L.mat("SuitNiko", (0.13, 0.16, 0.19))
    shirtw = L.mat("ShirtNiko", (0.93, 0.92, 0.88))
    tie = L.mat("TieNiko", (0.85, 0.62, 0.10), rough=0.4)
    trous = L.mat("TrousersNiko", (0.13, 0.16, 0.19))

    # slim suited torso
    torso = L.capsule("torso", (0, 0, 1.02), (0, 0.02, 1.52), 0.18, material=suit)
    shoulders = L.capsule("shoulders", (-0.20, 0.02, 1.52), (0.20, 0.02, 1.52), 0.12, material=suit)
    # white shirt V + gold tie
    shirtV = L.sphere("shirtV", radius=0.085, scale=(1.0, 0.5, 1.3), segments=12, rings=8,
                      loc=(0, 0.155, 1.52), material=shirtw)
    knot = L.sphere("knot", radius=0.045, segments=10, rings=6, loc=(0, 0.185, 1.50), material=tie)
    tieB = L.cone("tie", r1=0.055, r2=0.02, depth=0.34, verts=8,
                  rot=(0.08, 0, 0), loc=(0, 0.185, 1.30), material=tie)
    lapels = []
    for s in (-1, 1):
        lp = L.box("lapel", scale=(0.09, 0.03, 0.22), rot=(0, s * 0.35, 0),
                   loc=(s * 0.10, 0.16, 1.42))
        L.subsurf(lp, 1)
        lp.data.materials.append(suit)
        lapels.append(lp)

    # THE neck — comically long
    neck = L.capsule("neck", (0, 0.03, 1.58), (0, 0.14, 2.28), 0.082, material=skin)
    adam = L.sphere("adam", radius=0.04, segments=10, rings=6, loc=(0, 0.20, 1.96), material=skin)
    head = L.sphere("head", radius=0.26, scale=(0.88, 1.05, 1.05), segments=24, rings=16,
                    loc=(0, 0.16, 2.44), material=skin)
    jaw = L.sphere("jaw", radius=0.10, scale=(1.3, 1.0, 0.8), segments=12, rings=8,
                   loc=(0, 0.30, 2.28), material=skin)
    face = [nose_ball(0.43, 2.40, 0.058, skin)]
    face += ear_pair(0.23, 0.14, 2.44, 0.07, skin)
    # eyes hidden behind the ICONIC GREEN SHADES
    face += eye_pair(0.33, 2.53, 0.10, 0.06, skin)
    face += green_shades(0.36, 2.52, 0.115)
    face += brow_pair(0.34, 2.66, 0.10, 0.08, HAIR_BLK, tilt=0.2)
    # smug grin + chin-strap beard
    face += open_smile(0.375, 2.26, 0.12, 0.07, teeth=True)
    face += goatee(0.36, 2.20, w=0.075, mustache_z=2.335)
    face += cheek_pair(0.15, 0.32, 2.31, 0.06, skin)
    # curly top with fade
    hair = L.sphere("hair", radius=0.235, scale=(0.95, 0.9, 0.5), segments=20, rings=12,
                    loc=(0, 0.10, 2.66), material=HAIR_BLK)
    curls = []
    for i, (sx, sy) in enumerate(((-0.12, 0.02), (0.0, 0.08), (0.12, 0.02), (-0.06, -0.08), (0.06, -0.08))):
        c = L.ico("topcurl", radius=0.055, subdiv=1, loc=(sx, 0.10 + sy, 2.76), material=HAIR_BLK)
        curls.append(c)

    armL = limb((-0.23, 0.02, 1.52), (-0.28, 0.05, 1.14), (-0.30, 0.12, 0.80), 0.055, suit)
    armL += hand((-0.31, 0.15, 0.74), 0.07, skin, thumb=(0.6, 0.5, 0))
    armR = limb((0.23, 0.02, 1.52), (0.28, 0.05, 1.14), (0.30, 0.12, 0.80), 0.055, suit)
    armR += hand((0.31, 0.15, 0.74), 0.07, skin, thumb=(-0.6, 0.5, 0))
    legL = limb((-0.11, 0, 0.95), (-0.12, 0.04, 0.50), (-0.12, 0.02, 0.12), 0.06, trous)
    legL += shoe((-0.12, 0.05, 0.0), 0.16, 0.09, 0.09, HAIR_BLK)
    legR = limb((0.11, 0, 0.95), (0.12, 0.04, 0.50), (0.12, 0.02, 0.12), 0.06, trous)
    legR += shoe((0.12, 0.05, 0.0), 0.16, 0.09, 0.09, HAIR_BLK)

    root = _finalize(name, {
        "Head": [head, jaw, hair] + curls + face,
        "Neck": [neck, adam],
        "Torso": [torso, shoulders, shirtV, knot, tieB] + lapels,
        "ArmL": armL, "ArmR": armR,
        "LegL": legL, "LegR": legR,
    }, marker="BreathOrigin", marker_loc=(0, 0.55, 2.30))
    # toggleable green stinky-breath puff (hidden by default in game + previews)
    puff = L.ico("BadBreath", radius=0.09, subdiv=2, scale=(1.2, 1.6, 1.0),
                 loc=(0, 0.68, 2.28), material=BREATH)
    puff2 = L.ico("puff2", radius=0.05, subdiv=2, loc=(0.06, 0.86, 2.32), material=BREATH)
    puff = L.join([puff, puff2], "BadBreath")
    L.parent_keep(puff, root)
    return root


def hide_breath():
    """Hide Niko's breath puff for preview renders (it ships toggled-off)."""
    o = bpy.data.objects.get("BadBreath")
    if o:
        o.hide_render = True


# --------------------------------------------------------------------------- #
# P3 - KENNY: lean boxer, shirtless + gold chain, razor-sharp hairline, goatee
# --------------------------------------------------------------------------- #

def char_kenny(name="P3_Kenny"):
    skin = L.mat("SkinKenny", (0.32, 0.20, 0.12))
    glove = L.mat("GloveKenny", (0.78, 0.10, 0.10))
    lace = L.mat("LaceKenny", (0.95, 0.95, 0.92), rough=0.5)
    shorts_m = L.mat("ShortsKenny", (0.12, 0.12, 0.14))

    # athletic V-taper torso — shirtless (skin) like the promo photo
    torso = L.cone("torso", r1=0.26, r2=0.30, depth=0.80, verts=14, loc=(0, 0, 1.12), material=skin)
    L.subsurf(torso, 2)
    shoulders = L.capsule("shoulders", (-0.32, 0, 1.52), (0.32, 0, 1.52), 0.155, material=skin)
    pecL = L.sphere("pecL", radius=0.13, scale=(1.2, 0.6, 0.9), segments=12, rings=8,
                    loc=(-0.13, 0.20, 1.38), material=skin)
    pecR = L.sphere("pecR", radius=0.13, scale=(1.2, 0.6, 0.9), segments=12, rings=8,
                    loc=(0.13, 0.20, 1.38), material=skin)
    neckC = chain(1.62, 0.125, drop=0.075)

    neck = L.capsule("neck", (0, 0, 1.58), (0, 0.02, 1.72), 0.10, material=skin)
    head = L.sphere("head", radius=0.25, scale=(1.0, 1.0, 1.08), segments=24, rings=16,
                    loc=(0, 0.02, 1.90), material=skin)
    jaw = L.sphere("jaw", radius=0.13, scale=(1.4, 1.0, 0.7), segments=12, rings=8,
                   loc=(0, 0.08, 1.76), material=skin)
    face = [nose_ball(0.28, 1.90, 0.055, skin)]
    face += ear_pair(0.25, 0.04, 1.90, 0.07, skin)
    face += eye_pair(0.23, 1.96, 0.10, 0.058, skin, look=(0.006, 0))
    face += brow_pair(0.235, 2.05, 0.10, 0.08, HAIR_BLK, tilt=-0.25, thick=0.055)
    # focused little mouth + goatee
    duh = L.sphere("mouthK", radius=0.04, scale=(1.4, 0.4, 0.7), segments=10, rings=8,
                   loc=(0, 0.29, 1.74), material=MOUTH_IN)
    face.append(duh)
    face += goatee(0.27, 1.68, w=0.07, mustache_z=1.80)
    # THE HAIRLINE: a razor-sharp geometric slab, pushed comically high & back
    hairline = L.box("hairline", scale=(0.34, 0.30, 0.14),
                     rot=(-0.10, 0, 0), loc=(0, -0.06, 2.13))
    L.subsurf(hairline, 1)
    hairline.data.materials.append(HAIR_BLK)
    edge = L.box("hairEdge", scale=(0.34, 0.045, 0.10), rot=(-0.10, 0, 0), loc=(0, 0.085, 2.115))
    edge.data.materials.append(HAIR_BLK)

    # guard pose: gloves up
    def arm(s):
        parts = limb((s * 0.34, 0.02, 1.50), (s * 0.50, 0.12, 1.26), (s * 0.32, 0.32, 1.30),
                     0.085, skin)
        g = L.sphere("glove", radius=0.18, scale=(1.0, 1.15, 1.0), segments=18, rings=12,
                     loc=(s * 0.28, 0.42, 1.32), material=glove)
        cuff = L.torus("cuff", major=0.10, minor=0.03, mseg=14, minseg=8,
                       rot=(0, math.radians(90 * s), math.radians(35 * s)),
                       loc=(s * 0.33, 0.28, 1.30), material=lace)
        return parts + [g, cuff]

    shorts = L.cone("shorts", r1=0.24, r2=0.19, depth=0.30, verts=14, loc=(0, 0, 0.62), material=shorts_m)
    L.subsurf(shorts, 2)
    band = L.torus("band", major=0.165, minor=0.035, mseg=16, minseg=8, loc=(0, 0, 0.75), material=glove)

    def leg(s):
        parts = limb((s * 0.13, 0, 0.55), (s * 0.14, 0.03, 0.32), (s * 0.14, 0.0, 0.14), 0.075, skin)
        parts += shoe((s * 0.14, 0.04, 0.0), 0.17, 0.105, 0.10, glove, sole=True)
        st = L.torus("stripe", major=0.08, minor=0.018, mseg=12, minseg=6,
                     loc=(s * 0.14, 0.02, 0.15), material=lace)
        return parts + [st]

    return _finalize(name, {
        "Head": [head, jaw, hairline, edge] + face,
        "Neck": [neck],
        "Torso": [torso, shoulders, pecL, pecR, shorts, band] + neckC,
        "ArmL": arm(-1), "ArmR": arm(1),
        "LegL": leg(-1), "LegR": leg(1),
    })


# --------------------------------------------------------------------------- #
# P4 - SHARKY: slim, backwards cap, full beard, black hoodie w/ gold patch
# --------------------------------------------------------------------------- #

def char_sharky(name="P4_Sharky"):
    skin = L.mat("SkinSharky", (0.48, 0.31, 0.20))
    hoodie = L.mat("HoodieSharky", (0.09, 0.09, 0.10))
    jeans = L.mat("JeansSharky", (0.20, 0.22, 0.28))

    torso = L.capsule("torso", (0, 0, 1.02), (0, 0, 1.50), 0.20, material=hoodie)
    hood = L.torus("hood", major=0.13, minor=0.065, mseg=16, minseg=10, rot=(0.5, 0, 0),
                   loc=(0, -0.06, 1.60), material=hoodie)
    pocket = L.box("pocket", scale=(0.22, 0.06, 0.11), loc=(0, 0.185, 1.02))
    L.subsurf(pocket, 1)
    pocket.data.materials.append(hoodie)
    patch = L.box("patch", scale=(0.10, 0.03, 0.12), loc=(-0.08, 0.195, 1.34))
    L.subsurf(patch, 1)
    patch.data.materials.append(GOLD)

    neck = L.capsule("neck", (0, 0.02, 1.56), (0, 0.03, 1.72), 0.075, material=skin)
    head = L.sphere("head", radius=0.25, scale=(0.95, 1.0, 1.06), segments=24, rings=16,
                    loc=(0, 0.03, 1.92), material=skin)
    # FULL beard hugging the lower face
    face = full_beard((0, 0.03, 1.92), 0.25, y_scale=1.0)
    face += [nose_ball(0.29, 1.92, 0.05, skin)]
    face += ear_pair(0.24, 0.05, 1.92, 0.065, skin)
    face += eye_pair(0.24, 1.99, 0.095, 0.062, skin)
    face += brow_pair(0.245, 2.08, 0.095, 0.08, HAIR_BLK, tilt=-0.1)
    # easy grin peeking through the beard
    face += open_smile(0.30, 1.82, 0.09, 0.045, teeth=True)
    # BACKWARDS black cap
    face += cap((0, 0.03, 1.98), 0.24, brim_forward=False, cap_mat=HAIR_BLK)

    armL = limb((-0.24, 0, 1.46), (-0.30, 0.05, 1.10), (-0.28, 0.13, 0.80), 0.065, hoodie)
    armL += hand((-0.28, 0.17, 0.73), 0.076, skin, thumb=(0.6, 0.5, 0))
    armR = limb((0.24, 0, 1.46), (0.30, 0.05, 1.10), (0.28, 0.13, 0.80), 0.065, hoodie)
    armR += hand((0.28, 0.17, 0.73), 0.076, skin, thumb=(-0.6, 0.5, 0))
    legL = limb((-0.12, 0, 0.90), (-0.13, 0.03, 0.48), (-0.13, 0.0, 0.14), 0.075, jeans)
    legL += shoe((-0.13, 0.05, 0.0), 0.17, 0.10, 0.10, SOLE, sole=False)
    legR = limb((0.12, 0, 0.90), (0.13, 0.03, 0.48), (0.13, 0.0, 0.14), 0.075, jeans)
    legR += shoe((0.13, 0.05, 0.0), 0.17, 0.10, 0.10, SOLE, sole=False)

    return _finalize(name, {
        "Head": [head] + face,
        "Neck": [neck],
        "Torso": [torso, hood, pocket, patch],
        "ArmL": armL, "ArmR": armR,
        "LegL": legL, "LegR": legR,
    })


# --------------------------------------------------------------------------- #
# P5 - AJ: big Somali curls w/ golden tips, blazer + white shirt, chain, goatee
# --------------------------------------------------------------------------- #

def char_aj(name="P5_AJ"):
    skin = L.mat("SkinAJ", (0.50, 0.33, 0.21))
    blazer = L.mat("BlazerAJ", (0.38, 0.36, 0.38))
    shirtw = L.mat("ShirtAJ", (0.94, 0.93, 0.90))
    jeans = L.mat("JeansAJ", (0.12, 0.12, 0.14))
    sneak = L.mat("SneakAJ", (0.92, 0.90, 0.86))

    # slim blazer torso with white shirt V and chain
    torso = L.capsule("torso", (0, 0, 1.02), (0, 0.01, 1.50), 0.185, material=blazer)
    shoulders = L.capsule("shoulders", (-0.21, 0.01, 1.50), (0.21, 0.01, 1.50), 0.115, material=blazer)
    shirtV = L.sphere("shirtV", radius=0.085, scale=(1.0, 0.4, 1.1), segments=12, rings=8,
                      loc=(0, 0.16, 1.48), material=shirtw)
    lapels = []
    for s in (-1, 1):
        lp = L.box("lapel", scale=(0.085, 0.03, 0.20), rot=(0, s * 0.35, 0),
                   loc=(s * 0.10, 0.165, 1.40))
        L.subsurf(lp, 1)
        lp.data.materials.append(blazer)
        lapels.append(lp)
    neckC = chain(1.52, 0.16, drop=0.07)

    neck = L.capsule("neck", (0, 0.02, 1.55), (0, 0.03, 1.70), 0.07, material=skin)
    # slightly narrow head, wide-eyed
    head = L.sphere("head", radius=0.24, scale=(0.90, 1.0, 1.10), segments=24, rings=16,
                    loc=(0, 0.03, 1.92), material=skin)
    jaw = L.sphere("jaw", radius=0.09, scale=(1.2, 1.0, 0.75), segments=12, rings=8,
                   loc=(0, 0.13, 1.76), material=skin)
    face = [nose_ball(0.27, 1.90, 0.048, skin)]
    face += ear_pair(0.22, 0.05, 1.92, 0.06, skin)
    face += eye_pair(0.225, 2.00, 0.095, 0.075, skin, squint=1.1)
    face += brow_pair(0.235, 2.11, 0.10, 0.08, HAIR_BLK, tilt=0.1)
    face += open_smile(0.275, 1.80, 0.09, 0.05, teeth=True)
    face += goatee(0.245, 1.72, w=0.06, mustache_z=1.855)
    face += cheek_pair(0.11, 0.20, 1.85, 0.05, skin)
    # THE curls: big two-tone crown
    face += somali_curls((0, 0.03, 1.98), 0.25)

    armL = limb((-0.24, 0.01, 1.46), (-0.29, 0.05, 1.10), (-0.27, 0.13, 0.80), 0.062, blazer)
    armL += hand((-0.27, 0.17, 0.73), 0.074, skin, thumb=(0.6, 0.5, 0))
    armR = limb((0.24, 0.01, 1.46), (0.29, 0.05, 1.10), (0.27, 0.13, 0.80), 0.062, blazer)
    armR += hand((0.27, 0.17, 0.73), 0.074, skin, thumb=(-0.6, 0.5, 0))
    legL = limb((-0.12, 0, 0.90), (-0.13, 0.03, 0.48), (-0.13, 0.0, 0.14), 0.072, jeans)
    legL += shoe((-0.13, 0.05, 0.0), 0.17, 0.10, 0.10, sneak)
    legR = limb((0.12, 0, 0.90), (0.13, 0.03, 0.48), (0.13, 0.0, 0.14), 0.072, jeans)
    legR += shoe((0.13, 0.05, 0.0), 0.17, 0.10, 0.10, sneak)

    return _finalize(name, {
        "Head": [head, jaw] + face,
        "Neck": [neck],
        "Torso": [torso, shoulders, shirtV] + lapels + neckC,
        "ArmL": armL, "ArmR": armR,
        "LegL": legL, "LegR": legR,
    })


# --------------------------------------------------------------------------- #
# Announcer - "The Host" (unchanged): big-bellied barkeep, handlebar mustache,
# bowtie, monocle, towel on the shoulder, mug raised in a toast.
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
    tw = L.box("towel", scale=(0.14, 0.05, 0.34), rot=(0.15, 0, 0.25), loc=(-0.32, 0.02, 1.62))
    L.subsurf(tw, 2)
    tw.data.materials.append(towel)

    neck = L.capsule("neck", (0, 0, 1.62), (0, 0.02, 1.72), 0.11, material=skin)
    head = L.sphere("head", radius=0.30, scale=(1.05, 1.0, 1.05), segments=24, rings=16,
                    loc=(0, 0.02, 1.92), material=skin)
    sidehair = L.torus("sidehair", major=0.235, minor=0.07, mseg=18, minseg=8,
                       loc=(0, -0.10, 1.85), material=HAIR_BLK)
    face = [nose_ball(0.335, 1.92, 0.075, skin)]
    face += ear_pair(0.30, 0.02, 1.90, 0.08, skin)
    face += cheek_pair(0.15, 0.28, 1.83, 0.075, skin)
    face += eye_pair(0.265, 2.00, 0.115, 0.062, skin, lid=0.2)
    face += brow_pair(0.27, 2.10, 0.115, 0.10, stache, tilt=-0.2, thick=0.07)
    mono = L.torus("monocle", major=0.075, minor=0.012, mseg=16, minseg=6,
                   rot=(math.radians(90), 0, 0), loc=(0.115, 0.315, 2.00), material=GOLD)
    chainM = L.cylinder("chainM", radius=0.006, depth=0.22, verts=6,
                        rot=(0.3, 0.5, 0), loc=(0.20, 0.28, 1.88), material=GOLD)
    face += [mono, chainM]
    for s in (-1, 1):
        bar = L.sphere("stB", radius=0.10, scale=(1.7, 0.65, 0.55), segments=14, rings=10,
                       rot=(0, s * 0.25, 0), loc=(s * 0.12, 0.315, 1.84), material=stache)
        tip = L.sphere("stT", radius=0.045, segments=10, rings=8,
                       loc=(s * 0.265, 0.28, 1.90), material=stache)
        face += [bar, tip]
    face += open_smile(0.325, 1.76, 0.10, 0.05, teeth=True)

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
