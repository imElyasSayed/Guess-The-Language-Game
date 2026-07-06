"""
gen_lib.py - Low-poly procedural asset helpers for the Say Again? 3D tavern.

Headless-safe (works under `blender --background --python`). Everything is built
in world space at the object's identity transform by baking geometry directly into
mesh data (obj.data.transform), which sidesteps operator-context problems that plague
transform_apply/origin_set in background mode.

Conventions
-----------
* 1 Blender unit == 1 metre == 1 Unity unit.
* +Z is up (world). Characters are modelled facing +Y; the Unity scene builder
  rotates them into place, so facing here only needs to be self-consistent.
* Props are joined into a single mesh with origin at bottom-centre (base sits on
  the floor at the object origin). Characters keep parts SEPARATE (parented to a
  root empty) so they can be rigged/animated later.
"""

import bpy
from mathutils import Matrix, Vector, Euler

# --------------------------------------------------------------------------- #
# Scene lifecycle
# --------------------------------------------------------------------------- #

def reset():
    """Wipe the scene to a clean slate and purge orphaned datablocks."""
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    # Purge orphaned data, but KEEP materials: module-level palette constants hold
    # references to them and reuse them across every asset (mat() is get-or-create).
    for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.cameras, bpy.data.lights):
        for block in list(coll):
            if block.users == 0:
                coll.remove(block)


# --------------------------------------------------------------------------- #
# Transform helper (data-level, headless-safe)
# --------------------------------------------------------------------------- #

def _xform_matrix(scale=(1, 1, 1), rot=(0, 0, 0), loc=(0, 0, 0)):
    S = Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0))
    R = Euler(rot, "XYZ").to_matrix().to_4x4()
    T = Matrix.Translation(loc)
    return T @ R @ S


def bake(obj, scale=(1, 1, 1), rot=(0, 0, 0), loc=(0, 0, 0)):
    """Bake a scale/rotate/translate straight into the mesh; object stays at identity."""
    obj.data.transform(_xform_matrix(scale, rot, loc))
    return obj


# --------------------------------------------------------------------------- #
# Unit primitives (edge/diameter 1, centred on origin) + placement
# --------------------------------------------------------------------------- #

def _finish(name, scale, rot, loc, material):
    o = bpy.context.active_object
    o.name = name
    bake(o, scale, rot, loc)
    if material:
        o.data.materials.append(material)
    return o


def box(name="box", scale=(1, 1, 1), rot=(0, 0, 0), loc=(0, 0, 0), material=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    return _finish(name, scale, rot, loc, material)


def cylinder(name="cyl", radius=0.5, depth=1.0, verts=16,
             rot=(0, 0, 0), loc=(0, 0, 0), scale=(1, 1, 1), material=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth)
    return _finish(name, scale, rot, loc, material)


def cone(name="cone", r1=0.5, r2=0.0, depth=1.0, verts=16,
         rot=(0, 0, 0), loc=(0, 0, 0), scale=(1, 1, 1), material=None):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth)
    return _finish(name, scale, rot, loc, material)


def sphere(name="sph", radius=0.5, segments=16, rings=10,
           scale=(1, 1, 1), rot=(0, 0, 0), loc=(0, 0, 0), material=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius)
    o = _finish(name, scale, rot, loc, material)
    shade_smooth(o)
    return o


def ico(name="ico", radius=0.5, subdiv=1, scale=(1, 1, 1), rot=(0, 0, 0),
        loc=(0, 0, 0), material=None):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius)
    o = _finish(name, scale, rot, loc, material)
    shade_smooth(o)
    return o


def torus(name="torus", major=0.5, minor=0.15, mseg=16, minseg=8,
          rot=(0, 0, 0), loc=(0, 0, 0), scale=(1, 1, 1), material=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     major_segments=mseg, minor_segments=minseg)
    o = _finish(name, scale, rot, loc, material)
    shade_smooth(o)
    return o


def capsule(name, p1, p2, radius, material=None, verts=14, rings=8):
    """Organic limb segment: a cylinder with spherical caps, laid from p1 to p2.
    The workhorse for arms/legs/necks/torsos — chain them through joint points."""
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    length = max(d.length, 1e-4)
    mid = (p1 + p2) * 0.5
    cyl = cylinder("c", radius=radius, depth=length, verts=verts)
    top = sphere("s1", radius=radius, segments=verts, rings=rings, loc=(0, 0, length / 2))
    bot = sphere("s2", radius=radius, segments=verts, rings=rings, loc=(0, 0, -length / 2))
    o = join([cyl, top, bot], name)
    M = Matrix.Translation(mid) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    o.data.transform(M)
    if material:
        o.data.materials.append(material)
    shade_smooth(o)
    return o


def subsurf(obj, levels=2):
    """Apply a subdivision-surface modifier — turns blocky cages into soft organic
    cartoon forms. Apply per-part BEFORE joining so creases don't appear."""
    mod = obj.modifiers.new("ss", "SUBSURF")
    mod.levels = levels
    mod.render_levels = levels
    _select_only([obj])
    bpy.ops.object.modifier_apply(modifier=mod.name)
    shade_smooth(obj)
    return obj


# --------------------------------------------------------------------------- #
# Materials
# --------------------------------------------------------------------------- #

def mat(name, color, rough=0.8, metal=0.0, emit=None, emit_strength=2.0):
    """Get-or-create a Principled material. color = (r,g,b) 0..1."""
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit is not None:
        b.inputs["Emission Color"].default_value = (emit[0], emit[1], emit[2], 1.0)
        b.inputs["Emission Strength"].default_value = emit_strength
    # Also drive the viewport display colour so Workbench previews look right.
    m.diffuse_color = (color[0], color[1], color[2], 1.0)
    return m


# --------------------------------------------------------------------------- #
# Mesh ops
# --------------------------------------------------------------------------- #

def shade_smooth(obj):
    for p in obj.data.polygons:
        p.use_smooth = True


def shade_flat(obj):
    for p in obj.data.polygons:
        p.use_smooth = False


def _select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def join(objs, name):
    """Join meshes into one object (material slots merged). Origin left at world origin."""
    objs = [o for o in objs if o is not None]
    _select_only(objs)
    bpy.ops.object.join()
    res = bpy.context.active_object
    res.name = name
    return res


def set_origin(obj, point):
    """Move the object's origin to a world-space point (geometry unchanged in world)."""
    obj.data.transform(Matrix.Translation(-Vector(point)))
    obj.location = Vector(point)


def origin_to_bottom(obj):
    """Origin at (centre-x, centre-y, min-z) — base sits on the floor at the origin."""
    xs = [(obj.matrix_world @ v.co) for v in obj.data.vertices]
    minx = min(v.x for v in xs); maxx = max(v.x for v in xs)
    miny = min(v.y for v in xs); maxy = max(v.y for v in xs)
    minz = min(v.z for v in xs)
    set_origin(obj, ((minx + maxx) / 2, (miny + maxy) / 2, minz))


def uv_unwrap(obj):
    """Smart-UV-project a single object (headless-safe)."""
    _select_only([obj])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")


def poly_count(obj):
    return len(obj.data.polygons)


# --------------------------------------------------------------------------- #
# Hierarchy (characters)
# --------------------------------------------------------------------------- #

def empty(name, loc=(0, 0, 0)):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.2
    e.location = loc
    bpy.context.scene.collection.objects.link(e)
    return e


def parent_keep(child, parent):
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


# --------------------------------------------------------------------------- #
# Export
# --------------------------------------------------------------------------- #

def export_fbx(root_objs, filepath):
    """Export the given objects (and their children) to FBX with Unity-friendly axes."""
    def collect(o, acc):
        acc.add(o)
        for c in o.children:
            collect(c, acc)
    acc = set()
    for r in root_objs:
        collect(r, acc)
    _select_only(list(acc))
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        apply_unit_scale=True,
        global_scale=1.0,
        apply_scale_options="FBX_SCALE_NONE",
        bake_space_transform=True,       # bakes +Z-up -> +Y-up so Unity import is 1:1
        object_types={"EMPTY", "MESH", "ARMATURE"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
    )


def export_glb(root_objs, filepath):
    def collect(o, acc):
        acc.add(o)
        for c in o.children:
            collect(c, acc)
    acc = set()
    for r in root_objs:
        collect(r, acc)
    _select_only(list(acc))
    bpy.ops.export_scene.gltf(
        filepath=filepath,
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
    )


# --------------------------------------------------------------------------- #
# Preview rendering
# --------------------------------------------------------------------------- #

def _look_at(cam, target):
    d = Vector(target) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def render_preview(filepath, cam_loc, target=(0, 0, 1), res=640, ortho=None,
                   sun_energy=3.0, bg=(0.05, 0.04, 0.03)):
    scene = bpy.context.scene
    # World background
    scene.world = scene.world or bpy.data.worlds.new("W")
    scene.world.use_nodes = True
    bgnode = scene.world.node_tree.nodes.get("Background")
    if bgnode:
        bgnode.inputs["Color"].default_value = (bg[0], bg[1], bg[2], 1.0)
        bgnode.inputs["Strength"].default_value = 1.0

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = Vector(cam_loc)
    if ortho:
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = ortho
    _look_at(cam, target)
    scene.camera = cam

    key = bpy.data.objects.new("Key", bpy.data.lights.new("Key", "SUN"))
    key.data.energy = sun_energy
    key.rotation_euler = (0.9, 0.1, 0.6)
    bpy.context.scene.collection.objects.link(key)

    fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", "SUN"))
    fill.data.energy = sun_energy * 0.4
    fill.rotation_euler = (1.1, 0.0, -2.2)
    bpy.context.scene.collection.objects.link(fill)

    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except Exception:
        scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = res
    scene.render.resolution_y = res
    scene.render.film_transparent = False
    scene.render.filepath = filepath
    bpy.ops.render.render(write_still=True)

    # tidy up preview-only objects
    for o in (cam, key, fill):
        bpy.data.objects.remove(o, do_unlink=True)
