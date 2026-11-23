"""build_vernier_azm.py - procedural, repeatable build of the game-ready vernier caliper.

Run it from Blender's Text Editor (Run Script) or exec it and call build_all().
It deletes the previous caliper (collection "VernierCaliper_Azm") and rebuilds everything:
geometry, UVs, normals, the graduation texture (rendered in scene "MarkingSetup_Azm"),
materials and the object hierarchy. export_fbx() writes the FBX for Unity.

Frame (Blender, metres, 1 BU = 1 m), origin = MainScaleZero:
  +X along the beam (slide axis), +Z up, -Y is the front face (the side with the scales).
  x = 0  : fixed outside jaw measuring face (= main scale 0 mm)
  z = 0  : main-scale tick baseline (= top edge of the vernier plate)
  y = 0  : mid-thickness of the beam / outside jaws
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
OUT_DIR = r"F:\Unity Projects\Physics Practicals\Assets\_Azm\Art\Models\VernierCaliper"
BLEND_PATH = os.path.join(ART_DIR, "VernierCaliper_Azm.blend")
FBX_PATH = os.path.join(OUT_DIR, "VernierCaliper_Azm.fbx")
TEX_PATH = os.path.join(OUT_DIR, "VernierMarkings_Azm.png")
COLL_NAME = "VernierCaliper_Azm"
MARK_SCENE = "MarkingSetup_Azm"
TEX_NAME = "VernierMarkings_Azm"

# ---------------------------------------------------------------- dimensions (mm)
BEAM_X0, BEAM_X1 = -10.0, 190.0      # 200 mm beam; left 10 mm is the fixed-jaw head
BB, BT = -7.5, 8.5                    # beam bottom / top (16 mm high)
HT = 1.75                             # half thickness (3.5 mm)
JAW_TIP_Z = BB - 40.0                 # outside jaws reach 40 mm below the beam
CLEAR = 0.2                           # slider clearance to the beam
BEVEL = 0.3                           # visible-edge bevel
KNIFE = 0.6                           # chamfer that turns the inside-jaw edge into a knife
SLIDER_LEN = 40.0                     # keeps the slider on the beam at 150 mm opening
FP_Y0, FP_Y1 = -(HT + CLEAR + 1.2), -(HT + CLEAR)   # slider front plate (-3.15 .. -1.95)
BP_Y1 = HT + CLEAR + 1.2                            # slider back plate outer face (3.15)
LIP_B0, LIP_B1 = BB - CLEAR - 1.0, BB - CLEAR       # bottom lip z (-8.7 .. -7.7)
LIP_T0, LIP_T1 = BT + CLEAR, BT + CLEAR + 1.0       # top lip z (8.7 .. 9.7)
INSIDE_EDGE_X = -7.0                  # inside-jaw measuring edges when closed (both jaws)
GROOVE = dict(z0=-0.35, z1=1.35, y0=0.05)            # depth-rod groove in the beam back
ROD = dict(len=200.0, half=0.75, y=1.0, z=0.5)       # depth rod 1.5 x 1.5, centre y/z
SCREW_X = 21.0                        # lock screw axis (slider local x)

# ---------------------------------------------------------------- graduation texture atlas
# One 1024 x 256 RGBA atlas. Top half (V 0.5..1) = main scale band, 1024 x 128 px.
# Bottom-left (U 0..0.25, V 0..0.5) = vernier band, 256 x 128 px.
ATLAS_W, ATLAS_H = 1024, 256
MAIN_PPM_X, MAIN_PPM_Z = 6.0, 16.0    # px per mm on the main strip (non-square texels)
MAIN_PX0 = 47.5                       # px column centre of the 0 mm tick (tick k: 47.5 + 6k)
MAIN_ROW0 = 6.0                       # baseline row inside the 128 px main band
MAIN_X0 = -MAIN_PX0 / MAIN_PPM_X                  # strip left edge   (-7.916667 mm)
MAIN_X1 = (ATLAS_W - MAIN_PX0) / MAIN_PPM_X       # strip right edge  (162.75 mm)
# Strip bottom one texel above the band edge (-0.3125 mm, V = 129/256) so bilinear filtering
# never reads the vernier artwork stored in the rows directly below the main band.
MAIN_Z0 = -(MAIN_ROW0 - 1.0) / MAIN_PPM_Z
MAIN_Z1 = (128 - MAIN_ROW0) / MAIN_PPM_Z          # strip top         (7.625 mm)
VER_PPM = 20.0                        # px per mm on the vernier plate (square texels)
VER_PX0 = 37.5                        # px column centre of vernier line 0 (line n: 37.5 + 18n)
VER_X0 = -VER_PX0 / VER_PPM                       # plate left edge  (-1.875 mm, plate local)
VER_X1 = (256 - VER_PX0) / VER_PPM                # plate right edge (10.925 mm)
VER_Z0 = -128 / VER_PPM                           # plate bottom     (-6.4 mm), top = 0
VER_SPACING = 0.9                     # 10 vernier divisions = 9 mm  ->  LC = 0.1 mm
PLATE_T = 0.6                         # vernier plate thickness
BG_RGB = 0.78                         # steel-grey RGB stored where alpha = 0


def main_uv(x_mm, z_mm):
    """Main scale strip: U linear in x (mm), V linear in z (mm)."""
    return ((x_mm * MAIN_PPM_X + MAIN_PX0) / ATLAS_W,
            (128.0 + MAIN_ROW0 + z_mm * MAIN_PPM_Z) / ATLAS_H)


def vernier_uv(x_mm, z_mm):
    """Vernier plate front face (plate-local mm, origin on the 0 line, top edge z = 0)."""
    return ((x_mm * VER_PPM + VER_PX0) / ATLAS_W,
            (128.0 + z_mm * VER_PPM) / ATLAS_H)


# ---------------------------------------------------------------- small helpers
def main_scene():
    win = bpy.context.window or (bpy.context.window_manager.windows[0]
                                 if bpy.context.window_manager.windows else None)
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


def _area(loop):
    return sum(loop[i][0] * loop[(i + 1) % len(loop)][1] - loop[(i + 1) % len(loop)][0] * loop[i][1]
               for i in range(len(loop))) * 0.5


def _ccw(loop):
    return list(loop) if _area(loop) > 0 else list(reversed(loop))


def prism(outer, a0, a1, axis='Y', holes=(), regions=(), region_cap=0):
    """Extrude a 2D outline (mm) between a0 and a1 (mm).

    axis 'Y': 2D (u, v) -> (x=u, y=a, z=v).  axis 'X': 2D (u, v) -> (x=a, y=u, z=v).
    holes: loops cut through.  regions: loops kept as separate cap faces with material 1
    on the cap at a0 (region_cap=0) or a1 (region_cap=1) - used for the scale strips.
    """
    loops = [_ccw(outer)] + [_ccw(h) for h in holes] + [_ccw(r) for r in regions]
    pts, faces = [], []
    for loop in loops:
        faces.append(list(range(len(pts), len(pts) + len(loop))))
        pts += [Vector(p) for p in loop]
    # output type 1: triangles inside the constraint loops; orig face ids tell which loop(s)
    # each triangle lies in (outer only -> steel cap, region -> scale strip, hole -> dropped).
    vo, _eo, fo, ovs, _oes, ofs = geometry.delaunay_2d_cdt(pts, [], faces, 1, 1e-6)
    inv = {}
    for i, orig in enumerate(ovs):
        for j in orig:
            inv[j] = i
    hole_ids = set(range(1, 1 + len(holes)))
    region_ids = set(range(1 + len(holes), len(loops)))

    def to3(p, a):
        return Vector((p.x, a, p.y)) * MM if axis == 'Y' else Vector((a, p.x, p.y)) * MM

    bm = bmesh.new()
    v0 = [bm.verts.new(to3(p, a0)) for p in vo]
    v1 = [bm.verts.new(to3(p, a1)) for p in vo]
    for poly, orig in zip(fo, ofs):
        if hole_ids & set(orig):
            continue
        in_region = bool(region_ids & set(orig))
        f0 = bm.faces.new([v0[i] for i in poly])
        f1 = bm.faces.new([v1[i] for i in reversed(poly)])
        f0.material_index = 1 if (in_region and region_cap == 0) else 0
        f1.material_index = 1 if (in_region and region_cap == 1) else 0
    for li in range(1 + len(holes)):
        idx = faces[li]
        for k in range(len(idx)):
            a, b = inv[idx[k]], inv[idx[(k + 1) % len(idx)]]
            bm.faces.new([v0[a], v0[b], v1[b], v1[a]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm


def cap_edges(bm, axis=Vector((0, 1, 0)), side=None):
    """Edges between a cap face (normal along axis) and a side wall (normal perpendicular)."""
    bm.normal_update()
    out = []
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        d = [f.normal.dot(axis) for f in e.link_faces]
        caps = [x for x in d if abs(x) > 0.99]
        sides = [x for x in d if abs(x) < 0.01]
        if len(caps) == 1 and len(sides) == 1 and (side is None or caps[0] * side > 0):
            out.append(e)
    return out


def bevel(bm, edges, off_mm, segments=1):
    edges = [e for e in edges if e.is_valid]
    if edges:
        bmesh.ops.bevel(bm, geom=edges, offset=off_mm * MM, offset_type='OFFSET',
                        segments=segments, profile=0.5, affect='EDGES', clamp_overlap=True)
    bm.normal_update()


def knife_edges(bm, x_mm):
    """The two long edges of an inside-jaw measuring edge (vertical, at x = x_mm)."""
    out = []
    for e in cap_edges(bm):
        a, b = e.verts
        if abs(a.co.x - x_mm * MM) < 1e-7 and abs(b.co.x - x_mm * MM) < 1e-7 and abs(a.co.z - b.co.z) > 1e-4:
            out.append(e)
    return out


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


# ---------------------------------------------------------------- parts
def build_main_body(coll, steel, marks):
    outline = [(BEAM_X1, BB), (BEAM_X1, BT), (BEAM_X0, BT), (BEAM_X0, BB),
               (-9.5, -20.0), (-7.0, -38.0), (-4.0, JAW_TIP_Z), (0.0, JAW_TIP_Z), (0.0, BB)]
    strip = [(MAIN_X0, MAIN_Z0), (MAIN_X1, MAIN_Z0), (MAIN_X1, MAIN_Z1), (MAIN_X0, MAIN_Z1)]
    bm = prism(outline, -HT, HT, regions=[strip], region_cap=0)
    bevel(bm, cap_edges(bm), BEVEL)
    body = new_obj("MainBody_Azm", bm, coll, [steel, marks])

    # Fixed inside jaw: back half (y 0.1..1.6) so the sliding inside jaw can pass in front of it.
    jaw = [(INSIDE_EDGE_X, BT - 1.0), (-1.0, BT - 1.0), (-1.0, BT), (-5.5, BT + 8.0),
           (-5.5, BT + 15.0), (INSIDE_EDGE_X, BT + 16.0)]
    bm = prism(jaw, 0.1, 1.6)
    bevel(bm, knife_edges(bm, INSIDE_EDGE_X), KNIFE)
    bevel(bm, cap_edges(bm), BEVEL)
    boolean(body, new_obj("tmp_fixed_inside_jaw", bm, coll, [steel, marks]), 'UNION')

    # Groove for the depth rod along the back face.
    groove = [(BEAM_X0 - 1, GROOVE["z0"]), (BEAM_X1 + 1, GROOVE["z0"]),
              (BEAM_X1 + 1, GROOVE["z1"]), (BEAM_X0 - 1, GROOVE["z1"])]
    bm = prism(groove, GROOVE["y0"], HT + 1.0)
    boolean(body, new_obj("tmp_groove", bm, coll, [steel, marks]), 'DIFFERENCE')
    clean(body.data)
    return body


def build_slider(coll, steel, marks):
    # Front plate with a 3.5 mm overhang to the left (so the vernier 0 line, which sits on the
    # jaw face x = 0, is inside the window) and the window that shows the vernier plate.
    front = [(0, LIP_B0), (SLIDER_LEN, LIP_B0), (SLIDER_LEN, LIP_T1), (0, LIP_T1),
             (0, BT), (-3.5, BT), (-3.5, BB), (0, BB)]
    window = [(-1.95, VER_Z0 - 0.05), (11.0, VER_Z0 - 0.05), (11.0, 8.0), (-1.95, 8.0)]
    bm = prism(front, FP_Y0, FP_Y1, holes=[window])
    bevel(bm, cap_edges(bm, side=-1), BEVEL)
    slider = new_obj("Slider_Azm", bm, coll, [steel, marks])

    # Top lip + back plate + bottom lip (C profile in YZ, extruded along X).
    c = [(-2.4, LIP_B0), (BP_Y1, LIP_B0), (BP_Y1, LIP_T1), (-2.4, LIP_T1),
         (-2.4, LIP_T0), (HT + CLEAR, LIP_T0), (HT + CLEAR, LIP_B1), (-2.4, LIP_B1)]
    bm = prism(c, 0.0, SLIDER_LEN, axis='X')
    def _outer_back_corner(v):
        return abs(v.co.y - BP_Y1 * MM) < 1e-7 and min(abs(v.co.z - LIP_T1 * MM), abs(v.co.z - LIP_B0 * MM)) < 1e-7
    long_edges = [e for e in bm.edges
                  if all(_outer_back_corner(v) for v in e.verts)
                  and abs(e.verts[0].co.x - e.verts[1].co.x) > 1e-4]
    bevel(bm, long_edges, BEVEL)
    boolean(slider, new_obj("tmp_c_channel", bm, coll, [steel, marks]), 'UNION')

    # Sliding outside jaw (measuring face at x = 0, in the beam plane).
    jaw = [(0.0, JAW_TIP_Z), (4.0, JAW_TIP_Z), (7.0, -38.0), (9.5, -20.0), (10.0, LIP_B0 + 0.5), (0.0, LIP_B0 + 0.5)]
    bm = prism(jaw, -HT, HT)
    bevel(bm, cap_edges(bm), BEVEL)
    boolean(slider, new_obj("tmp_sliding_outside_jaw", bm, coll, [steel, marks]), 'UNION')

    # Sliding inside jaw: front half, crosses in front of the fixed one; measuring edge faces +X.
    ijaw = [(-8.5, BT + 0.4), (7.5, BT + 0.4), (7.5, LIP_T1 + 0.4), (INSIDE_EDGE_X, BT + 8.0),
            (INSIDE_EDGE_X, BT + 16.0), (-8.5, BT + 15.0)]
    bm = prism(ijaw, -1.6, -0.1)
    bevel(bm, knife_edges(bm, INSIDE_EDGE_X), KNIFE)
    bevel(bm, cap_edges(bm), BEVEL)
    boolean(slider, new_obj("tmp_sliding_inside_jaw", bm, coll, [steel, marks]), 'UNION')

    # Thumb rest: ridged pad under the slider on the right.
    pad = [(27.0, LIP_B0 + 0.3), (27.0, LIP_B0 - 1.9)]
    for i, x in enumerate(np.arange(28.0, 38.01, 1.0)):
        pad.append((float(x), LIP_B0 - (3.6 if i % 2 == 0 else 3.0)))
    pad += [(39.0, LIP_B0 - 1.9), (39.0, LIP_B0 + 0.3)]
    bm = prism(pad, -2.2, 2.2)
    boolean(slider, new_obj("tmp_thumb_rest", bm, coll, [steel, marks]), 'UNION')
    clean(slider.data)
    # Slider is steel only.
    for p in slider.data.polygons:
        p.material_index = 0
    slider.data.materials.pop(index=1)
    return slider


def build_vernier_plate(coll, steel, marks):
    rect = [(VER_X0, VER_Z0), (VER_X1, VER_Z0), (VER_X1, 0.0), (VER_X0, 0.0)]
    bm = prism(rect, 0.0, PLATE_T, regions=[rect], region_cap=0)  # front face (y = 0) = scale
    return new_obj("VernierPlate_Azm", bm, coll, [steel, marks])


def build_lock_screw(coll, steel):
    n = 12
    rings = [(2.0, 0.0, False), (2.0, 1.2, False), (3.5, 1.2, True), (3.5, 4.2, True), (3.05, 4.6, False)]
    bm = bmesh.new()
    loops = []
    for r, z, knurl in rings:
        ring = []
        for i in range(n):
            a = 2 * math.pi * i / n
            rr = (r - 0.3 if i % 2 else r) if knurl else r
            ring.append(bm.verts.new((rr * math.cos(a) * MM, rr * math.sin(a) * MM, z * MM)))
        loops.append(ring)
    for ra, rb in zip(loops, loops[1:]):
        for i in range(n):
            bm.faces.new([ra[i], ra[(i + 1) % n], rb[(i + 1) % n], rb[i]])
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_obj("LockScrew_Azm", bm, coll, [steel])


def build_depth_rod(coll, steel):
    h = ROD["half"]
    rect = [(-h, -h), (h, -h), (h, h), (-h, h)]
    bm = prism(rect, -ROD["len"], 0.0, axis='X')       # origin at the tip (x = 0)
    long_edges = [e for e in bm.edges if abs(e.verts[0].co.x - e.verts[1].co.x) > 1e-4]
    bevel(bm, long_edges, 0.15)
    return new_obj("DepthRod_Azm", bm, coll, [steel])


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
            f.select_set(False)
        for f in bm.faces:
            if select_fn(f):
                f.select_set(True)
        bm.select_flush_mode()
        bmesh.update_edit_mesh(ob.data)
        bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.01,
                                 area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        ob.select_set(False)


def finalize(ob, exact_uv=None, uv_offset=(0.0, 0.0), uv_scale=1.0, sharp_deg=60.0):
    me = ob.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    smart_uv(ob, lambda f: f.material_index == 0)
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.active
    for f in bm.faces:
        for lp in f.loops:
            if f.material_index == 1 and exact_uv:
                lp[uv].uv = exact_uv(lp.vert.co.x / MM, lp.vert.co.z / MM)
            else:
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
        vs = [bm.verts.new((x0, y0, 0)), bm.verts.new((x1, y0, 0)),
              bm.verts.new((x1, y1, 0)), bm.verts.new((x0, y1, 0))]
        bm.faces.new(vs)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    sc.collection.objects.link(ob)


def _text(sc, name, body, x, y, size, sx=1.0, align='CENTER'):
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body = body
    cu.size = size
    cu.align_x = align
    ob = bpy.data.objects.new(name, cu)
    ob.location = (x, y, 0)
    ob.scale = (sx, 1, 1)
    sc.collection.objects.link(ob)


def _render_pass(sc, path, w, h, show_ticks, aa):
    for o in sc.collection.all_objects:
        if o.type in {'MESH', 'FONT'}:
            o.hide_render = (o.type == 'MESH') != show_ticks
    sc.display.render_aa = aa
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True, scene=sc.name)
    img = bpy.data.images.load(path, check_existing=False)
    arr = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    os.remove(path)
    return arr[..., 3]


def _render_band(sc, cam, cx, w, h, path):
    """Alpha of one band. Ticks are pixel-aligned rectangles, so they are rendered with
    anti-aliasing OFF (exactly one/three pixel columns, full black); numbers with AA on."""
    cam.location = (cx, h / 2, 10)
    cam.data.ortho_scale = w
    sc.render.resolution_x, sc.render.resolution_y = w, h
    ticks = _render_pass(sc, path, w, h, True, 'OFF')
    text = _render_pass(sc, path, w, h, False, '16')
    return np.maximum(ticks, text)


def render_markings():
    """Render the tick / number artwork orthographically (Workbench) and build the atlas.

    The artwork is laid out directly in pixel units (1 BU = 1 px) so every tick lands on a
    known pixel column: main tick k at x = 47.5 + 6k px, vernier line n at x = 37.5 + 18n px.
    """
    sc = bpy.data.scenes.get(MARK_SCENE) or bpy.data.scenes.new(MARK_SCENE)
    _remove_scene_objects(sc)
    try:
        sc.render.engine = 'BLENDER_WORKBENCH'
    except TypeError as e:
        raise RuntimeError(f"Workbench not available: {e}")
    sh = sc.display.shading
    sh.light = 'FLAT'
    sh.color_type = 'SINGLE'
    sh.single_color = (0.0, 0.0, 0.0)
    sh.show_cavity = sh.show_shadows = sh.show_object_outline = sh.show_specular_highlight = False
    sc.display.render_aa = '16'
    sc.render.film_transparent = True
    sc.render.resolution_percentage = 100
    sc.render.pixel_aspect_x = sc.render.pixel_aspect_y = 1.0
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.image_settings.color_depth = '8'
    try:
        sc.view_settings.view_transform = 'Standard'
        sc.view_settings.look = 'None'
    except TypeError:
        pass
    sc.view_settings.exposure = 0.0
    sc.view_settings.gamma = 1.0

    # ---- main scale, band 1024 x 128 px, art at x 0..1024, y 0..128
    ticks = []
    for k in range(0, 151):
        length = 5.0 if k % 10 == 0 else (3.5 if k % 5 == 0 else 2.5)     # mm
        cx = MAIN_PX0 + MAIN_PPM_X * k
        ticks.append((cx - 0.5, MAIN_ROW0, cx + 0.5, MAIN_ROW0 + length * MAIN_PPM_Z))
    _rects_obj(sc, "MarkMainTicks", ticks)
    sx = MAIN_PPM_X / MAIN_PPM_Z            # squash text so it looks right on the 6x16 px/mm strip
    num_base = MAIN_ROW0 + 5.0 * MAIN_PPM_Z + 0.6 * MAIN_PPM_Z
    for cm in range(0, 16):
        _text(sc, f"MarkNum{cm}", str(cm), MAIN_PX0 + MAIN_PPM_X * 10 * cm, num_base, 40.0, sx)
    _text(sc, "MarkCm", "cm", MAIN_PX0 + MAIN_PPM_X * 1.0, num_base, 30.0, sx, align='LEFT')

    # ---- vernier, band 256 x 128 px, art offset to x 3000..3256
    ox = 3000.0
    lines = []
    for n in range(0, 11):
        length = 3.0 if n in (0, 5, 10) else 2.0                            # mm
        cx = ox + VER_PX0 + VER_PPM * VER_SPACING * n
        lines.append((cx - 1.5, 128.0 - length * VER_PPM, cx + 1.5, 131.0))
    _rects_obj(sc, "MarkVernierLines", lines)
    for n in (0, 5, 10):
        _text(sc, f"MarkVer{n}", str(n), ox + VER_PX0 + VER_PPM * VER_SPACING * n, 28.0, 42.0)

    cam_data = bpy.data.cameras.new("MarkCam")
    cam_data.type = 'ORTHO'
    cam_data.sensor_fit = 'HORIZONTAL'
    cam_data.clip_start, cam_data.clip_end = 0.1, 100.0
    cam = bpy.data.objects.new("MarkCam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam

    tmp = tempfile.gettempdir()
    main = _render_band(sc, cam, 512.0, 1024, 128, os.path.join(tmp, "azm_mark_main.png"))
    ver = _render_band(sc, cam, ox + 128.0, 256, 128, os.path.join(tmp, "azm_mark_vernier.png"))

    atlas = np.zeros((ATLAS_H, ATLAS_W, 4), dtype=np.float32)
    atlas[..., :3] = BG_RGB
    for a, r0, c0 in ((main, 128, 0), (ver, 0, 0)):
        h, w = a.shape[:2]
        atlas[r0:r0 + h, c0:c0 + w, 3] = a
        atlas[r0:r0 + h, c0:c0 + w, :3] = (BG_RGB * (1.0 - a))[..., None]

    os.makedirs(OUT_DIR, exist_ok=True)
    tmp_img = bpy.data.images.new("azm_atlas_tmp", ATLAS_W, ATLAS_H, alpha=True)
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
        try:
            m.use_nodes = True
        except Exception:
            pass
        m.node_tree.nodes.clear()
        out = m.node_tree.nodes.new('ShaderNodeOutputMaterial')
        bsdf = m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
        bsdf.location = (-300, 0)
        m.node_tree.links.new(bsdf.outputs[0], out.inputs[0])
        return m, bsdf

    steel_col = (0.57, 0.57, 0.59, 1.0)
    steel, b = fresh("VernierSteel_Azm")
    b.inputs['Base Color'].default_value = steel_col
    b.inputs['Metallic'].default_value = 1.0
    b.inputs['Roughness'].default_value = 0.35
    steel.diffuse_color, steel.metallic, steel.roughness = steel_col, 1.0, 0.35

    marks, b = fresh("VernierMarkings_Azm")
    nt = marks.node_tree
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    tex.interpolation = 'Linear'
    tex.location = (-800, 0)
    nt.links.new(tex.outputs['Color'], b.inputs['Base Color'])         # FBX picks this up
    inv = nt.nodes.new('ShaderNodeMath')
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    nt.links.new(tex.outputs['Alpha'], inv.inputs[1])                  # metal where no mark
    nt.links.new(inv.outputs[0], b.inputs['Metallic'])
    rough = nt.nodes.new('ShaderNodeMath')
    rough.operation = 'MULTIPLY_ADD'
    rough.inputs[1].default_value = 0.3
    rough.inputs[2].default_value = 0.35
    nt.links.new(tex.outputs['Alpha'], rough.inputs[0])
    nt.links.new(rough.outputs[0], b.inputs['Roughness'])
    marks.diffuse_color, marks.metallic, marks.roughness = steel_col, 1.0, 0.35
    return steel, marks


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
        if me.users == 0 and (me.name.startswith("tmp_") or me.name.endswith("_Azm")):
            bpy.data.meshes.remove(me)
    return coll


def _parent(child, parent, loc_mm):
    child.parent = parent
    child.matrix_parent_inverse = Matrix.Identity(4)
    child.location = Vector(loc_mm) * MM
    child.rotation_euler = (0, 0, 0)
    child.scale = (1, 1, 1)


def set_opening(d_m):
    """Slide the slider: jaw opening = d_m metres (local +X)."""
    bpy.data.objects["Slider_Azm"].location = (d_m, 0.0, 0.0)


def build_all(opening=0.0, render_tex=True):
    scene = main_scene()
    us = scene.unit_settings
    us.system, us.scale_length, us.length_unit = 'METRIC', 1.0, 'METERS'
    coll = clear_previous(scene)
    img = render_markings() if (render_tex or TEX_NAME not in bpy.data.images) else bpy.data.images[TEX_NAME]
    steel, marks = make_materials(img)

    body = build_main_body(coll, steel, marks)
    slider = build_slider(coll, steel, marks)
    plate = build_vernier_plate(coll, steel, marks)
    screw = build_lock_screw(coll, steel)
    rod = build_depth_rod(coll, steel)

    finalize(body, exact_uv=main_uv, uv_scale=0.5)                      # steel UVs in [0,.5]^2
    finalize(slider)
    finalize(plate, exact_uv=vernier_uv, uv_scale=0.5, uv_offset=(0.5, 0.0))
    finalize(screw, sharp_deg=25.0)                                     # faceted knurl
    finalize(rod)

    root = bpy.data.objects.new("VernierCaliper_Azm", None)
    root.empty_display_type = 'PLAIN_AXES'
    root.empty_display_size = 0.01
    coll.objects.link(root)
    _parent(body, root, (0, 0, 0))
    _parent(slider, root, (0, 0, 0))
    _parent(plate, slider, (0, FP_Y1 - PLATE_T, 0))        # front face of the plate, on the 0 line
    _parent(screw, slider, (SCREW_X, 0, LIP_T1))
    _parent(rod, slider, (BEAM_X1, ROD["y"], ROD["z"]))    # tip flush with the beam end when closed
    set_opening(opening)
    return report()


def report():
    out = {}
    for name in ("MainBody_Azm", "Slider_Azm", "VernierPlate_Azm", "LockScrew_Azm", "DepthRod_Azm"):
        ob = bpy.data.objects[name]
        me = ob.data
        bm = bmesh.new()
        bm.from_mesh(me)
        nonman = sum(1 for e in bm.edges if not e.is_manifold)
        ngons = sum(1 for f in me.polygons if len(f.vertices) > 4)
        loose = sum(1 for v in bm.verts if not v.link_faces)
        bm.free()
        out[name] = dict(tris=len(me.polygons), nonmanifold=nonman, ngons=ngons, loose=loose,
                         mats=[m.name for m in me.materials])
    out["total_tris"] = sum(v["tris"] for v in out.values() if isinstance(v, dict))
    return out


# ---------------------------------------------------------------- export / screenshots
def export_fbx():
    scene = main_scene()
    vl = scene.view_layers[0]
    root = bpy.data.objects["VernierCaliper_Azm"]
    for o in vl.objects:
        o.select_set(False)
    for o in [root] + list(root.children_recursive):
        o.select_set(True)
    vl.objects.active = root
    os.makedirs(OUT_DIR, exist_ok=True)

    # Workaround for a bug in Blender's FBX exporter (checked in 5.1.2): with "Apply Transform"
    # (bake_space_transform) the axis conversion is cancelled twice for objects two levels deep,
    # so VernierPlate/LockScrew/DepthRod (children of Slider_Azm) came out rotated 90 deg with
    # unconverted offsets. When parent and child both bake, the correct FBX local matrix is
    # G @ M_local @ G^-1 at any depth (what the exporter already produces one level down).
    # The exporter function is patched only for the duration of this export.
    from io_scene_fbx import fbx_utils
    original = fbx_utils.ObjectWrapper.fbx_object_matrix

    def fixed(self, scene_data, rest=False, local_space=False, global_space=False):
        parent = self.parent
        if (parent is not None and not global_space and self._tag == 'OB' and parent._tag == 'OB'
                and not self.parented_to_armature
                and self.use_bake_space_transform(scene_data) and parent.use_bake_space_transform(scene_data)
                and (local_space or self.has_valid_parent(scene_data.objects))):
            m = self.matrix_rest_local if rest else self.matrix_local
            g = scene_data.settings.global_matrix
            return g @ m @ scene_data.settings.global_matrix_inv
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
