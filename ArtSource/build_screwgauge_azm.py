"""build_screwgauge_azm.py - procedural, repeatable build of the game-ready SCREW GAUGE (micrometer).

For Matric Physics Exp 1.3 (thickness of a metal strip / wire). Run it from Blender's Text Editor
(Run Script) or exec it and call build_all(). It deletes the previous gauge (collection
"ScrewGauge_Azm") and rebuilds everything: geometry, UVs, normals, the graduation texture
(rendered in scene "ScrewMarkingSetup_Azm"), materials and the object hierarchy.
export_fbx() writes the FBX for Unity.

Instrument (textbook): pitch 0.5 mm, 50 circular-scale divisions -> least count 0.01 mm, range 0-25 mm.

Frame (Blender, metres, 1 BU = 1 m), origin = centre of the ANVIL's measuring face:
  +X along the spindle axis (from the anvil towards the ratchet), +Z up, -Y is the front (scales).
  Gap g (m) = Spindle_Azm.location.x; Spindle_Azm.rotation_euler.x = g / PITCH * 2*pi.
Angles around the axis (alpha) are measured from the FRONT (-Y) towards +Z (up):
  a point at radius r and angle alpha is (x, -r*cos(alpha), r*sin(alpha)).
All dimensions below are in millimetres and converted with MM.
"""
import math
import os
import tempfile

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Vector, geometry

MM = 0.001

ART_DIR = r"F:\Unity Projects\Physics Practicals\ArtSource"
OUT_DIR = r"F:\Unity Projects\Physics Practicals\Assets\_Azm\Art\Models\ScrewGauge"
BLEND_PATH = os.path.join(ART_DIR, "ScrewGauge_Azm.blend")
FBX_PATH = os.path.join(OUT_DIR, "ScrewGauge_Azm.fbx")
TEX_PATH = os.path.join(OUT_DIR, "ScrewGaugeMarkings_Azm.png")
COLL_NAME = "ScrewGauge_Azm"
MARK_SCENE = "ScrewMarkingSetup_Azm"
TEX_NAME = "ScrewGaugeMarkings_Azm"
MAT_STEEL, MAT_PAINT, MAT_MARKS = "ScrewGaugeSteel_Azm", "ScrewGaugeFrame_Azm", "ScrewGaugeMarkings_Azm"

# ---------------------------------------------------------------- instrument (textbook Exp 1.3)
PITCH_MM = 0.5            # spindle travel for one turn of the thimble
DIVISIONS = 50            # circular-scale divisions  -> LC = 0.5 / 50 = 0.01 mm
RANGE_MM = 25.0
DIV_DEG = 360.0 / DIVISIONS                       # 7.2 degrees per circular division

# ---------------------------------------------------------------- dimensions (mm)
SPINDLE_R = 3.25          # anvil and spindle diameter 6.5 mm
FRAME_HALF_T = 4.0        # frame thickness 8 mm
SLEEVE_R = 4.75
SLEEVE_X0 = 36.0          # sleeve starts inside the frame's spindle boss
SCALE_ZERO_X = 42.0       # main-scale 0 on the sleeve = thimble edge when the gap is 0
THIMBLE_R = 8.0
THIMBLE_LEN = 30.0
RATCHET_LEN = 12.0
LOCK_POS = (33.0, 0.0, 8.0)

# ---------------------------------------------------------------- graduation texture atlas
# One 1024 x 256 RGBA atlas.
# Rows 0-127 (V 0..0.5): SLEEVE band, U 0..1 = s (mm from the main-scale 0) in [S_MIN, S_MIN + 32],
#   32 px per mm; V 0..0.5 = alpha in [-40, +40] degrees (datum line at alpha 0 = V 0.25).
# Rows 128-255 (V 0.5..1): THIMBLE band, U 0..1000/1024 = alpha in [SEAM, SEAM + 360) degrees,
#   20 px per division (mark k at U px 10.5 + 20k); V 0.5..1 = thimble x (mm from its edge) in
#   [BAND_X0, BAND_X0 + 8].
ATLAS_W, ATLAS_H = 1024, 256
SLEEVE_PPM = 32.0
S_MIN = -112.5 / SLEEVE_PPM                       # -3.515625 mm: main mark k at px 112.5 + 32k
SLEEVE_BAND_DEG = 40.0
THIMBLE_PX_PER_DIV = 20.0
THIMBLE_PX0 = 10.5                                # pixel centre of circular mark 0
SEAM_DEG = -THIMBLE_PX0 / THIMBLE_PX_PER_DIV * DIV_DEG   # -3.78 degrees (between marks 49 and 0)
BAND_X0, BAND_LEN = 0.4, 8.0
BG_RGB = 0.78


def sleeve_uv(x_mm, alpha_deg):
    s = x_mm - SCALE_ZERO_X
    return ((s - S_MIN) * SLEEVE_PPM / ATLAS_W,
            (alpha_deg + SLEEVE_BAND_DEG) / (2 * SLEEVE_BAND_DEG) * 0.5)


def thimble_uv(x_mm, alpha_deg):
    return ((THIMBLE_PX0 + alpha_deg / DIV_DEG * THIMBLE_PX_PER_DIV) / ATLAS_W,
            0.5 + (x_mm - BAND_X0) / BAND_LEN * 0.5)


def alpha_of(co):
    """Angle (degrees) of a point around the X axis, from the front (-Y) towards +Z."""
    return math.degrees(math.atan2(co.z, -co.y))


# ---------------------------------------------------------------- helpers
def main_scene():
    win = bpy.context.window or (bpy.context.window_manager.windows[0] if bpy.context.window_manager.windows else None)
    sc = win.scene if win else bpy.context.scene
    if sc.name == MARK_SCENE:
        sc = next(s for s in bpy.data.scenes if s.name != MARK_SCENE)
    return sc


def view3d_ctx():
    for w in bpy.context.window_manager.windows:
        for a in w.screen.areas:
            if a.type == 'VIEW_3D':
                r = next(r for r in a.regions if r.type == 'WINDOW')
                return dict(window=w, screen=w.screen, area=a, region=r)
    raise RuntimeError("No 3D viewport open")


def _ccw(loop):
    area = sum(loop[i][0] * loop[(i + 1) % len(loop)][1] - loop[(i + 1) % len(loop)][0] * loop[i][1] for i in range(len(loop)))
    return list(loop) if area > 0 else list(reversed(loop))


def prism(outer, y0, y1):
    """Extrude an XZ outline (mm) between y0 and y1 (mm)."""
    loop = _ccw(outer)
    pts = [Vector(p) for p in loop]
    vo, _eo, fo, ovs, _oes, _ofs = geometry.delaunay_2d_cdt(pts, [], [list(range(len(pts)))], 1, 1e-6)
    inv = {}
    for i, orig in enumerate(ovs):
        for j in orig:
            inv[j] = i
    bm = bmesh.new()
    v0 = [bm.verts.new((p.x * MM, y0 * MM, p.y * MM)) for p in vo]
    v1 = [bm.verts.new((p.x * MM, y1 * MM, p.y * MM)) for p in vo]
    for poly in fo:
        bm.faces.new([v0[i] for i in poly])
        bm.faces.new([v1[i] for i in reversed(poly)])
    for k in range(len(loop)):
        a, b = inv[k], inv[(k + 1) % len(loop)]
        bm.faces.new([v0[a], v0[b], v1[b], v1[a]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm


def revolve(profile, segments, start_deg=0.0, mat_fn=None):
    """Lathe a CLOSED (x, r[, knurl]) profile (mm) around the X axis.

    r = 0 makes a single vertex on the axis (a cone/fan cap). knurl = amplitude (mm): ring radius
    alternates r - knurl / r + knurl per segment (low-poly knurling).
    mat_fn(i, j) -> material index of the face between profile points i and i+1, segment j.
    Ring vertices sit at alpha = start_deg + 360 j / segments.
    """
    bm = bmesh.new()
    rings = []
    for p in profile:
        x, r = p[0], p[1]
        knurl = p[2] if len(p) > 2 else 0.0
        if r <= 0.0:
            rings.append([bm.verts.new((x * MM, 0.0, 0.0))])
            continue
        ring = []
        for j in range(segments):
            a = math.radians(start_deg + 360.0 * j / segments)
            rr = r + (knurl if j % 2 else -knurl)
            ring.append(bm.verts.new((x * MM, -rr * math.cos(a) * MM, rr * math.sin(a) * MM)))
        rings.append(ring)
    n = len(rings)
    for i in range(n):
        A, B = rings[i], rings[(i + 1) % n]
        if len(A) == 1 and len(B) == 1:
            continue
        for j in range(segments):
            jn = (j + 1) % segments
            if len(A) == 1:
                f = bm.faces.new([A[0], B[jn], B[j]])
            elif len(B) == 1:
                f = bm.faces.new([A[j], A[jn], B[0]])
            else:
                f = bm.faces.new([A[j], A[jn], B[jn], B[j]])
            f.material_index = mat_fn(i, j) if mat_fn else 0
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-9)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm


def cap_edges(bm, axis=Vector((0, 1, 0))):
    bm.normal_update()
    out = []
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        d = [f.normal.dot(axis) for f in e.link_faces]
        if sum(1 for x in d if abs(x) > 0.99) == 1 and sum(1 for x in d if abs(x) < 0.01) == 1:
            out.append(e)
    return out


def bevel(bm, edges, off_mm):
    edges = [e for e in edges if e.is_valid]
    if edges:
        bmesh.ops.bevel(bm, geom=edges, offset=off_mm * MM, offset_type='OFFSET', segments=1,
                        profile=0.5, affect='EDGES', clamp_overlap=True)
    bm.normal_update()


def new_obj(name, bm, coll, mats):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    return ob


def boolean(ob, cutter, op):
    mod = ob.modifiers.new("azm_bool", 'BOOLEAN')
    mod.operation = op
    mod.solver = 'EXACT'
    mod.object = cutter
    dg = bpy.context.evaluated_depsgraph_get()
    dg.update()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    ob.modifiers.remove(mod)
    old = ob.data
    name = old.name
    ob.data = me
    bpy.data.meshes.remove(old)
    me.name = name
    me_cut = cutter.data
    bpy.data.objects.remove(cutter, do_unlink=True)
    if me_cut.users == 0:
        bpy.data.meshes.remove(me_cut)


def clean(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-8)
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    loose_e = [e for e in bm.edges if not e.link_faces]
    if loose_e:
        bmesh.ops.delete(bm, geom=loose_e, context='EDGES')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()


def keep_used_materials(me):
    """Drop material slots no face uses (so Unity gets no empty sub-meshes)."""
    used = sorted({p.material_index for p in me.polygons})
    mats = [me.materials[i] for i in used]
    remap = {old: new for new, old in enumerate(used)}
    idx = [remap[p.material_index] for p in me.polygons]
    me.materials.clear()
    for m in mats:
        me.materials.append(m)
    for p, i in zip(me.polygons, idx):
        p.material_index = i


# ---------------------------------------------------------------- parts
def build_frame(coll, mats):
    # U-shaped frame in the XZ plane: left arm holds the anvil, right arm holds the spindle boss
    # and the sleeve; the axis (z = 0) runs through the tops of both arms.
    outline = [(-12, 8), (-12, -18), (-6, -30), (6, -34), (24, -34), (34, -28), (38, -18), (38, 8),
               (28, 8), (28, -12), (23, -20), (3, -20), (-4, -12), (-4, 8)]
    bm = prism(outline, -FRAME_HALF_T, FRAME_HALF_T)
    for f in bm.faces:
        f.material_index = 1                      # enamel paint
    bevel(bm, cap_edges(bm), 0.4)
    frame = new_obj("Frame_Azm", bm, coll, mats)

    # Anvil (steel): measuring face exactly at x = 0, flat, with a small chamfer on its rim.
    anvil = revolve([(-6, 0), (-6, SPINDLE_R), (-0.3, SPINDLE_R), (0, SPINDLE_R - 0.3), (0, 0)], 24)
    boolean(frame, new_obj("tmp_anvil", anvil, coll, mats), 'UNION')

    # Spindle boss (bearing) on the right arm.
    boss = revolve([(27, 0), (27, 4.5), (27.5, 5.0), (38.5, 5.0), (38.5, 0)], 32)
    boolean(frame, new_obj("tmp_boss", boss, coll, mats), 'UNION')

    # Sleeve with the main scale: the faces of the front band (alpha -40..40 deg) between
    # x = SCALE_ZERO_X + S_MIN and the sleeve end carry the markings material.
    xa = SCALE_ZERO_X + S_MIN
    xb = xa + ATLAS_W / SLEEVE_PPM
    seg = 36                                       # 10 degrees per face; band = 8 faces
    band = int(2 * SLEEVE_BAND_DEG / (360.0 / seg))

    def sleeve_mat(i, j):
        return 2 if (i == 2 and j < band) else 0

    sleeve = revolve([(SLEEVE_X0, 0), (SLEEVE_X0, SLEEVE_R), (xa, SLEEVE_R), (xb, SLEEVE_R), (xb, 0)],
                     seg, start_deg=-SLEEVE_BAND_DEG, mat_fn=sleeve_mat)
    boolean(frame, new_obj("tmp_sleeve", sleeve, coll, mats), 'UNION')
    clean(frame.data)
    return frame


def build_spindle(coll, mats):
    bm = revolve([(0, 0), (0, SPINDLE_R - 0.3), (0.3, SPINDLE_R), (72, SPINDLE_R), (72, 0)], 24)
    return new_obj("Spindle_Azm", bm, coll, mats)


def build_thimble(coll, mats):
    # Local x from the thimble's left EDGE (x = 0); the circular scale is on the cylinder from
    # x = BAND_X0 for BAND_LEN mm. 50 segments: one face per division, mark k at alpha = 7.2 k.
    profile = [
        (0.0, 5.0), (0.0, 7.6), (BAND_X0, THIMBLE_R),          # edge annulus + small chamfer
        (BAND_X0 + BAND_LEN, THIMBLE_R),                      # scale band (index 2 -> 3)
        (13.5, THIMBLE_R), (14.0, 8.25, 0.2), (25.5, 8.25, 0.2), (26.0, THIMBLE_R),   # knurled grip
        (27.0, 6.5), (THIMBLE_LEN, 6.5), (THIMBLE_LEN, 5.0),   # neck and end face
    ]

    def mat(i, j):
        return 2 if i == 2 else 0

    bm = revolve(profile, DIVISIONS, start_deg=SEAM_DEG, mat_fn=mat)
    return new_obj("Thimble_Azm", bm, coll, mats)


def build_ratchet(coll, mats):
    bm = revolve([(-0.2, 0), (-0.2, 5.0), (0.5, 5.5), (1.0, 5.6, 0.25), (10.0, 5.6, 0.25), (10.5, 5.4),
                  (RATCHET_LEN, 4.6), (RATCHET_LEN, 0)], 24)
    return new_obj("Ratchet_Azm", bm, coll, mats)


def build_lock(coll, mats):
    # Knob on the frame's right arm, origin at its base on the rotation axis (local Z).
    knob = revolve([(0, 0), (0, 2.6), (2.2, 2.6), (2.8, 2.0), (2.8, 0)], 16)
    bmesh.ops.rotate(knob, verts=knob.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(-90), 4, 'Y'))
    lock = new_obj("Lock_Azm", knob, coll, mats)
    lever = prism([(-0.9, 1.2), (0.9, 1.2), (0.9, 2.4), (-0.9, 2.4)], -6.5, 0.0)
    bevel(lever, cap_edges(lever), 0.2)
    boolean(lock, new_obj("tmp_lever", lever, coll, mats), 'UNION')
    clean(lock.data)
    return lock


# ---------------------------------------------------------------- UVs, normals, triangulation
def smart_uv(ob, select_fn):
    ctx = view3d_ctx()
    vl = main_scene().view_layers[0]
    with bpy.context.temp_override(**ctx):
        for o in vl.objects:
            o.select_set(False)
        vl.objects.active = ob
        ob.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        bm = bmesh.from_edit_mesh(ob.data)
        bm.select_mode = {'FACE'}
        for f in bm.faces:
            f.select_set(select_fn(f))
        bm.select_flush_mode()
        bmesh.update_edit_mesh(ob.data)
        bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.01,
                                 area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        ob.select_set(False)


def finalize(ob, exact_uv=None, uv_offset=(0.0, 0.0), uv_scale=1.0, sharp_deg=40.0):
    """exact_uv(face) -> list of (u, v) per loop, for faces with the markings material."""
    me = ob.data
    keep_used_materials(me)
    marks = next((i for i, m in enumerate(me.materials) if m.name == MAT_MARKS), -1)
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    smart_uv(ob, lambda f: f.material_index != marks)
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.active
    for f in bm.faces:
        if f.material_index == marks and exact_uv:
            for lp, value in zip(f.loops, exact_uv(f)):
                lp[uv].uv = value
        else:
            for lp in f.loops:
                u, v = lp[uv].uv
                lp[uv].uv = (u * uv_scale + uv_offset[0], v * uv_scale + uv_offset[1])
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
    bm.to_mesh(me)
    bm.free()
    me.shade_smooth()
    me.set_sharp_from_angle(angle=math.radians(sharp_deg))
    mod = ob.modifiers.new("azm_wn", 'WEIGHTED_NORMAL')
    mod.mode = 'FACE_AREA'
    mod.weight = 50
    mod.keep_sharp = True
    dg = bpy.context.evaluated_depsgraph_get()
    dg.update()
    new = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    ob.modifiers.remove(mod)
    name = me.name
    ob.data = new
    bpy.data.meshes.remove(me)
    new.name = name


def sleeve_face_uv(f):
    return [sleeve_uv(lp.vert.co.x / MM, alpha_of(lp.vert.co)) for lp in f.loops]


def thimble_face_uv(f):
    # Unwrap the angle from the seam so a face never straddles the jump at 360 degrees.
    a = [(alpha_of(lp.vert.co) - SEAM_DEG) % 360.0 for lp in f.loops]
    if max(a) - min(a) > 180.0:
        a = [x + 360.0 if x < 180.0 else x for x in a]
    return [thimble_uv(lp.vert.co.x / MM, x + SEAM_DEG) for lp, x in zip(f.loops, a)]


# ---------------------------------------------------------------- graduation texture
def _remove_scene_objects(sc):
    for o in list(sc.collection.all_objects):
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data is not None and data.users == 0:
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            elif isinstance(data, bpy.types.Curve):
                bpy.data.curves.remove(data)
            elif isinstance(data, bpy.types.Camera):
                bpy.data.cameras.remove(data)


def _rects_obj(sc, name, rects):
    bm = bmesh.new()
    for x0, y0, x1, y1 in rects:
        vs = [bm.verts.new((x0, y0, 0)), bm.verts.new((x1, y0, 0)), bm.verts.new((x1, y1, 0)), bm.verts.new((x0, y1, 0))]
        bm.faces.new(vs)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    sc.collection.objects.link(bpy.data.objects.new(name, me))


def _text(sc, name, body, x, y, size, scale=(1.0, 1.0), rot_z=0.0, align_x='CENTER', align_y='BOTTOM'):
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body = body
    cu.size = size
    cu.align_x = align_x
    cu.align_y = align_y
    ob = bpy.data.objects.new(name, cu)
    ob.location = (x, y, 0)
    ob.scale = (scale[0], scale[1], 1)
    ob.rotation_euler = (0, 0, math.radians(rot_z))
    sc.collection.objects.link(ob)


def _render_pass(sc, path, w, h, show_rects, aa):
    for o in sc.collection.all_objects:
        if o.type in {'MESH', 'FONT'}:
            o.hide_render = (o.type == 'MESH') != show_rects
    sc.display.render_aa = aa
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True, scene=sc.name)
    img = bpy.data.images.load(path, check_existing=False)
    arr = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    os.remove(path)
    return arr[..., 3]


def _render_band(sc, cam, cx, w, h, path):
    """Marks (pixel-aligned rectangles) render with AA off - exact columns, full black; text with AA."""
    cam.location = (cx, h / 2, 10)
    cam.data.ortho_scale = w
    sc.render.resolution_x, sc.render.resolution_y = w, h
    return np.maximum(_render_pass(sc, path, w, h, True, 'OFF'), _render_pass(sc, path, w, h, False, '16'))


def render_markings():
    """Render the scale artwork in PIXEL units (1 BU = 1 px) and assemble the atlas.

    Sleeve band (1024 x 128): datum line along alpha = 0 (rows 63-64); mm marks above it at
    x = 112.5 + 32k (k = 0..25, longer every 5 mm, numbered 0, 5 ... 25); half-mm marks below it at
    x = 128.5 + 32k (k = 0..24).
    Thimble band (1000 x 128): marks k = 0..49 at x = 10.5 + 20k starting at the thimble edge
    (row 1), longer every 5, numbered 0, 5 ... 45. U runs round the thimble and V along its axis, so
    the digits are drawn swapped (mirror + 90 deg) to read upright and left-to-right on the thimble.
    """
    sc = bpy.data.scenes.get(MARK_SCENE) or bpy.data.scenes.new(MARK_SCENE)
    _remove_scene_objects(sc)
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'FLAT'
    sh.color_type = 'SINGLE'
    sh.single_color = (0.0, 0.0, 0.0)
    sh.show_cavity = sh.show_shadows = sh.show_object_outline = sh.show_specular_highlight = False
    sh.show_backface_culling = False
    sc.render.film_transparent = True
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    try:
        sc.view_settings.view_transform = 'Standard'
        sc.view_settings.look = 'None'
    except TypeError:
        pass

    # ----- sleeve band: art at x 0..1024, y 0..128
    sleeve_v_ppm = 128.0 / (2 * math.radians(SLEEVE_BAND_DEG) * SLEEVE_R)    # px per mm round the sleeve
    rects = [(0.0, 63.0, 1024.0, 65.0)]                                       # datum line
    for k in range(int(RANGE_MM) + 1):
        cx = 112.5 + SLEEVE_PPM * k
        length = 1.8 if k % 5 == 0 else 1.25                                  # mm
        rects.append((cx - 2.5, 65.0, cx + 2.5, 65.0 + length * sleeve_v_ppm))
    for k in range(int(RANGE_MM)):
        cx = 112.5 + SLEEVE_PPM * (k + 0.5)
        rects.append((cx - 2.5, 63.0 - 1.0 * sleeve_v_ppm, cx + 2.5, 63.0))
    _rects_obj(sc, "MarkSleeve", rects)
    sx = SLEEVE_PPM / sleeve_v_ppm
    for k in range(0, int(RANGE_MM) + 1, 5):
        _text(sc, f"MarkSleeveNum{k}", str(k), 112.5 + SLEEVE_PPM * k, 65.0 + 1.8 * sleeve_v_ppm + 4.0, 26.0, (sx, 1.0))

    # ----- thimble band: art at x 3000..4000, y 0..128 (row 0 = thimble x BAND_X0)
    ox = 3000.0
    thimble_u_ppm = THIMBLE_PX_PER_DIV / (math.radians(DIV_DEG) * THIMBLE_R)  # px per mm round the thimble
    thimble_v_ppm = 128.0 / BAND_LEN                                          # px per mm along the axis
    rects = []
    for k in range(DIVISIONS):
        cx = ox + THIMBLE_PX0 + THIMBLE_PX_PER_DIV * k
        length = 2.6 if k % 5 == 0 else 1.8
        rects.append((cx - 1.5, 1.0, cx + 1.5, 1.0 + length * thimble_v_ppm))
    _rects_obj(sc, "MarkThimble", rects)
    read = thimble_v_ppm / thimble_u_ppm      # squash along the reading direction (image y)
    for k in range(0, DIVISIONS, 5):
        _text(sc, f"MarkThimbleNum{k}", str(k), ox + THIMBLE_PX0 + THIMBLE_PX_PER_DIV * k,
              1.0 + 2.6 * thimble_v_ppm + 22.0, 34.0, (read, -1.0), 90.0, 'CENTER', 'CENTER')

    cam_data = bpy.data.cameras.new("MarkCam")
    cam_data.type = 'ORTHO'
    cam_data.sensor_fit = 'HORIZONTAL'
    cam_data.clip_start, cam_data.clip_end = 0.1, 100.0
    cam = bpy.data.objects.new("MarkCam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam

    tmp = tempfile.gettempdir()
    sleeve = _render_band(sc, cam, 512.0, 1024, 128, os.path.join(tmp, "azm_sg_sleeve.png"))
    thimble = _render_band(sc, cam, ox + 500.0, 1000, 128, os.path.join(tmp, "azm_sg_thimble.png"))

    atlas = np.zeros((ATLAS_H, ATLAS_W, 4), dtype=np.float32)
    atlas[..., :3] = BG_RGB
    for a, r0 in ((sleeve, 0), (thimble, 128)):
        h, w = a.shape[:2]
        atlas[r0:r0 + h, 0:w, 3] = a
        atlas[r0:r0 + h, 0:w, :3] = (BG_RGB * (1.0 - a))[..., None]

    os.makedirs(OUT_DIR, exist_ok=True)
    tmp_img = bpy.data.images.new("azm_sg_atlas_tmp", ATLAS_W, ATLAS_H, alpha=True)
    tmp_img.pixels.foreach_set(atlas.ravel())
    tmp_img.filepath_raw = TEX_PATH
    tmp_img.file_format = 'PNG'
    tmp_img.save()
    bpy.data.images.remove(tmp_img)

    img = bpy.data.images.get(TEX_NAME)
    if img is None:
        img = bpy.data.images.load(TEX_PATH, check_existing=False)
        img.name = TEX_NAME
    else:
        img.filepath = TEX_PATH
        img.reload()
    img.alpha_mode = 'STRAIGHT'
    return img


# ---------------------------------------------------------------- materials
def make_materials(img):
    def fresh(name):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        m.node_tree.nodes.clear()
        out = m.node_tree.nodes.new('ShaderNodeOutputMaterial')
        bsdf = m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
        m.node_tree.links.new(bsdf.outputs[0], out.inputs[0])
        return m, bsdf

    steel_col = (0.57, 0.57, 0.59, 1.0)
    steel, b = fresh(MAT_STEEL)                     # satin chrome: anvil, spindle, sleeve, thimble
    b.inputs['Base Color'].default_value = steel_col
    b.inputs['Metallic'].default_value = 1.0
    b.inputs['Roughness'].default_value = 0.3
    steel.diffuse_color = steel_col

    paint_col = (0.035, 0.05, 0.08, 1.0)
    paint, b = fresh(MAT_PAINT)                     # dark enamel frame
    b.inputs['Base Color'].default_value = paint_col
    b.inputs['Metallic'].default_value = 0.0
    b.inputs['Roughness'].default_value = 0.45
    paint.diffuse_color = paint_col

    marks, b = fresh(MAT_MARKS)
    nt = marks.node_tree
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    tex.interpolation = 'Linear'
    nt.links.new(tex.outputs['Color'], b.inputs['Base Color'])   # FBX picks this up
    inv = nt.nodes.new('ShaderNodeMath')
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    nt.links.new(tex.outputs['Alpha'], inv.inputs[1])
    nt.links.new(inv.outputs[0], b.inputs['Metallic'])
    rough = nt.nodes.new('ShaderNodeMath')
    rough.operation = 'MULTIPLY_ADD'
    rough.inputs[1].default_value = 0.3
    rough.inputs[2].default_value = 0.3
    nt.links.new(tex.outputs['Alpha'], rough.inputs[0])
    nt.links.new(rough.outputs[0], b.inputs['Roughness'])
    marks.diffuse_color = steel_col
    return [steel, paint, marks]


# ---------------------------------------------------------------- hierarchy / build
def clear_previous(scene):
    coll = bpy.data.collections.get(COLL_NAME)
    if coll is None:
        coll = bpy.data.collections.new(COLL_NAME)
        scene.collection.children.link(coll)
    for o in list(coll.all_objects):
        me = o.data if o.type == 'MESH' else None
        bpy.data.objects.remove(o, do_unlink=True)
        if me is not None and me.users == 0:
            bpy.data.meshes.remove(me)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)
    return coll


def _parent(child, parent, loc_mm):
    child.parent = parent
    child.matrix_parent_inverse = Matrix.Identity(4)
    child.location = Vector(loc_mm) * MM
    child.rotation_euler = (0, 0, 0)
    child.scale = (1, 1, 1)


def set_gap(gap_m, zero_error_mm=0.0):
    """Open the gauge to gap_m metres. zero_error_mm (+ = reads high) turns the thimble's scale."""
    sp = bpy.data.objects["Spindle_Azm"]
    sp.location = (gap_m, 0.0, 0.0)
    sp.rotation_euler = (gap_m / (PITCH_MM * MM) * 2.0 * math.pi, 0.0, 0.0)
    th = bpy.data.objects["Thimble_Azm"]
    th.rotation_euler = (math.radians(zero_error_mm / (PITCH_MM / DIVISIONS) * DIV_DEG), 0.0, 0.0)


def build_all(gap=0.0, render_tex=True):
    scene = main_scene()
    us = scene.unit_settings
    us.system, us.scale_length, us.length_unit = 'METRIC', 1.0, 'MILLIMETERS'
    coll = clear_previous(scene)
    img = render_markings() if (render_tex or TEX_NAME not in bpy.data.images) else bpy.data.images[TEX_NAME]
    mats = make_materials(img)

    frame = build_frame(coll, mats)
    spindle = build_spindle(coll, mats)
    thimble = build_thimble(coll, mats)
    ratchet = build_ratchet(coll, mats)
    lock = build_lock(coll, mats)

    finalize(frame, exact_uv=sleeve_face_uv, uv_scale=0.5, uv_offset=(0.0, 0.5), sharp_deg=50.0)
    finalize(spindle)
    finalize(thimble, exact_uv=thimble_face_uv, uv_scale=0.5)
    finalize(ratchet)
    finalize(lock)

    root = bpy.data.objects.new("ScrewGauge_Azm", None)
    root.empty_display_type = 'PLAIN_AXES'
    root.empty_display_size = 0.01
    coll.objects.link(root)
    _parent(frame, root, (0, 0, 0))
    _parent(lock, root, LOCK_POS)
    _parent(spindle, root, (0, 0, 0))
    _parent(thimble, spindle, (SCALE_ZERO_X, 0, 0))
    _parent(ratchet, spindle, (SCALE_ZERO_X + THIMBLE_LEN, 0, 0))
    set_gap(gap)
    return report()


def report():
    out = {}
    for name in ("Frame_Azm", "Spindle_Azm", "Thimble_Azm", "Ratchet_Azm", "Lock_Azm"):
        me = bpy.data.objects[name].data
        bm = bmesh.new()
        bm.from_mesh(me)
        out[name] = dict(tris=len(me.polygons), nonmanifold=sum(1 for e in bm.edges if not e.is_manifold),
                         ngons=sum(1 for f in me.polygons if len(f.vertices) > 4),
                         loose=sum(1 for v in bm.verts if not v.link_faces), mats=[m.name for m in me.materials])
        bm.free()
    out["total_tris"] = sum(v["tris"] for v in out.values() if isinstance(v, dict))
    return out


# ---------------------------------------------------------------- export / screenshots
def export_fbx():
    scene = main_scene()
    vl = scene.view_layers[0]
    root = bpy.data.objects["ScrewGauge_Azm"]
    for o in vl.objects:
        o.select_set(False)
    for o in [root] + list(root.children_recursive):
        o.select_set(True)
    vl.objects.active = root
    os.makedirs(OUT_DIR, exist_ok=True)

    # Workaround for a bug in Blender's FBX exporter (5.1.2, found with the vernier caliper): with
    # "Apply Transform" the axis conversion is cancelled twice for objects two levels deep. When
    # parent and child both bake, the correct FBX local matrix is G @ M_local @ G^-1 at any depth.
    from io_scene_fbx import fbx_utils
    original = fbx_utils.ObjectWrapper.fbx_object_matrix

    def fixed(self, scene_data, rest=False, local_space=False, global_space=False):
        parent = self.parent
        if (parent is not None and not global_space and self._tag == 'OB' and parent._tag == 'OB'
                and not self.parented_to_armature
                and self.use_bake_space_transform(scene_data) and parent.use_bake_space_transform(scene_data)
                and (local_space or self.has_valid_parent(scene_data.objects))):
            m = self.matrix_rest_local if rest else self.matrix_local
            return scene_data.settings.global_matrix @ m @ scene_data.settings.global_matrix_inv
        return original(self, scene_data, rest, local_space, global_space)

    fbx_utils.ObjectWrapper.fbx_object_matrix = fixed
    try:
        with bpy.context.temp_override(**view3d_ctx()):
            bpy.ops.export_scene.fbx(
                filepath=FBX_PATH, use_selection=True, object_types={'EMPTY', 'MESH'},
                apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                use_mesh_modifiers=True, mesh_smooth_type='OFF', use_tspace=False,
                use_custom_props=False, add_leaf_bones=False, bake_anim=False,
                path_mode='AUTO', embed_textures=False)
    finally:
        fbx_utils.ObjectWrapper.fbx_object_matrix = original
    return FBX_PATH


def snapshot(path, center_mm, width_mm, rot_deg=(90, 0, 0), px=(1600, 900), shading='MATERIAL'):
    """Orthographic offscreen viewport capture (rot_deg = view rotation, (90,0,0) = front)."""
    import gpu
    ctx = view3d_ctx()
    space, region = ctx["area"].spaces.active, ctx["region"]
    scene = main_scene()
    rot = Euler([math.radians(a) for a in rot_deg]).to_matrix().to_4x4()
    cam = Matrix.Translation(Vector(center_mm) * MM) @ rot @ Matrix.Translation((0, 0, 1.0))
    vm = cam.inverted()
    W, H = px
    w = width_mm * MM
    h = w * H / W
    n, f = 0.01, 3.0
    pm = Matrix(((2 / w, 0, 0, 0), (0, 2 / h, 0, 0), (0, 0, -2 / (f - n), -(f + n) / (f - n)), (0, 0, 0, 1)))
    old = (space.shading.type, space.overlay.show_overlays)
    space.shading.type = shading
    space.overlay.show_overlays = False
    off = gpu.types.GPUOffScreen(W, H)
    try:
        off.draw_view3d(scene, scene.view_layers[0], space, region, vm, pm, do_color_management=True)
        buf = off.texture_color.read()
    finally:
        off.free()
        space.shading.type, space.overlay.show_overlays = old
    buf.dimensions = W * H * 4
    pix = np.asarray(buf, dtype=np.float32) / 255.0
    img = bpy.data.images.new("azm_snapshot", W, H, alpha=True)
    img.pixels.foreach_set(pix.ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
    return path


if __name__ == "__main__":
    print(build_all())
