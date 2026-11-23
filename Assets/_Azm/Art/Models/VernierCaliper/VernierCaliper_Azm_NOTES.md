# VernierCaliper_Azm — model notes

Source: `ArtSource/VernierCaliper_Azm.blend` (outside `Assets/`). Everything is rebuilt by the
text block / file `build_vernier_azm.py` (also saved as `ArtSource/build_vernier_azm.py`).
Real size, 1 unit = 1 m. Metric scale only, 0–15 cm.

## 1. Objects, origins, slide axis

| Object | Parent | Origin (Blender, mm) | What it is |
|---|---|---|---|
| `VernierCaliper_Azm` | – | (0, 0, 0) = **MainScaleZero** | Empty, root |
| `MainBody_Azm` | root | (0, 0, 0) | Beam + main scale + fixed outside jaw + fixed inside jaw + depth-rod groove |
| `Slider_Azm` | root | (0, 0, 0) when closed | Sliding outside jaw, sliding inside jaw, slider body with window, thumb rest |
| `VernierPlate_Azm` | Slider | (0, −2.55, 0) | Plate in the slider window carrying the vernier scale |
| `LockScrew_Azm` | Slider | (21, 0, 9.7) | Knurled lock screw. Origin on its axis at its base; the axis is local Z (Unity: local Y) |
| `DepthRod_Azm` | Slider | (190, 1.0, 0.5) | Depth rod. Origin at the centre of its **tip** face |

All rotations are 0 and all scales are 1, in Blender and as imported into Unity (checked in this project, Unity 6000.3).

**MainScaleZero** is the point on the fixed outside jaw's measuring face (plane x = 0) at the
height of the main-scale tick baseline (z = 0), at mid-thickness of the beam (y = 0).

**Slide axis.** Slider movement is a pure translation along one axis:

| | Blender | Unity (after import) |
|---|---|---|
| Opening of `d` metres | `Slider_Azm.location.x = +d` | `Slider_Azm.localPosition.x = −d` |
| Jaws closed | 0 | 0 |
| Maximum travel | 0.150 | −0.150 |

1 m of slider translation = 1 m of jaw opening, exactly. The two outside measuring faces are flat
and vertical, and lie exactly on the planes x = 0 (fixed) and x = d (sliding). Closed, they touch
with no gap and no overlap. At 0.150 m the slider's right end is flush with the beam's right end.

**Axis mapping on import (verified in Unity):** Blender +X → Unity **−X** (slide direction),
Blender +Z (up) → Unity +Y, Blender −Y (the front, scale side) → Unity **+Z**. To view the scale
face-on in Unity, put the camera on the +Z side looking toward −Z.

**Inside jaws.** Their measuring (knife) edges are at Blender x = −7.0 mm (fixed jaw, faces −X)
and x = −7.0 + d (sliding jaw, faces +X). The inside reading therefore equals the outside reading
`d` with no correction. When closed, the two edges coincide. The jaws cross: the fixed one is in
the back half of the thickness and the sliding one in the front half, as on a real caliper.

**Depth rod.** Closed, its tip is flush with the beam's right end (Blender x = 190 mm). It
protrudes exactly `d` past that end, so depth = `d`.

**Zero error.** `VernierPlate_Azm` is separate so Unity can shift it. Its rest local position is
Blender (0, −2.55, 0) mm = Unity (0, 0, +2.55) mm. Shifting the plate by +e along Blender X
(Unity local x = −e) gives a **positive** zero error of e: with the jaws closed the vernier 0
reads +e. Use the opposite sign for a negative zero error.

## 2. Graduations and UV-to-mm mapping

One texture, `VernierMarkings_Azm.png`, 1024 × 256 RGBA. Marks are black (alpha 1). Elsewhere
alpha is 0 and RGB is a steel grey of 0.78, so the file works both as an opaque base map and as an
alpha mask. The ticks were rendered with anti-aliasing off on exact pixel columns; the numbers were
rendered anti-aliased.

**Main scale strip** (faces of `MainBody_Azm`'s front face, material `VernierMarkings_Azm`, atlas
top half). The mapping is linear and exact:

- `U = (6·x + 47.5) / 1024`, where x is in mm along the beam from MainScaleZero (Blender X).
  The strip runs U 0 → 1 = x −7.916667 → 162.75 mm, at 6 px per mm.
- Tick k mm (k = 0…150) is centred at `U = (47.5 + 6k)/1024`, which is pixel column 47 + 6k.
  Each tick is 1 px wide (0.167 mm).
- `V = (134 + 16·z) / 256`, where z is in mm above the baseline, at 16 px per mm.
  The baseline z = 0 is at V = 0.5234375 (row 134). The strip mesh runs z −0.3125 → 7.625 mm
  (V 129/256 → 1). It starts one texel above the band edge so filtering never picks up the
  vernier artwork stored below it.
- Tick lengths above the baseline: 2.5 mm (each mm), 3.5 mm (each 5 mm), 5.0 mm (each 10 mm).
  The numbers 0–15 sit above the long ticks, with "cm" just right of the 0.
- In Unity local coordinates (x_u = −x), `U = (47.5 − 6000·x_u) / 1024`.

**Vernier strip** (front face of `VernierPlate_Azm`, atlas U 0–0.25, V 0–0.5):

- `U = (20·x + 37.5) / 1024`, where x is in mm in plate-local space (origin on the 0 line).
  The plate runs U 0 → 0.25 = x −1.875 → 10.925 mm, at 20 px per mm.
- Line n (n = 0…10) is at x = 0.9·n mm, centred at `U = (37.5 + 18n)/1024`, which is pixel columns
  36–38 + 18n. Each line is 3 px wide (0.15 mm).
- `V = (128 + 20·z) / 256`, where z is in mm, from 0 at the plate's top edge (touching the main
  baseline) down to −6.4 mm at its bottom. So V 0.5 → 0 runs top → bottom.
- Line lengths: 2.0 mm, or 3.0 mm for lines 0, 5 and 10, labelled "0", "5", "10".
- 10 vernier divisions = 9 mm, so the least count is 1 mm / 10 = 0.1 mm = 0.01 cm.

**Checks done in Blender:**
- All 151 main ticks and all 11 vernier lines were found at exactly their expected pixel
  columns.
- Closed: vernier 0 is on main 0, and vernier 10 is on main 9 mm.
- At x = 0.0234 the main reading is 2.3 cm, and vernier line 4 is at 27.000 mm, exactly on main
  tick 27. All other lines are at least 0.1 mm off a tick.
- Screenshots 1 and 2 show these two checks.

## 3. Triangles and materials

| Object | Triangles | Materials |
|---|---|---|
| MainBody_Azm | 192 | VernierSteel_Azm, VernierMarkings_Azm (scale strip) |
| Slider_Azm | 412 | VernierSteel_Azm |
| VernierPlate_Azm | 12 | VernierSteel_Azm, VernierMarkings_Azm (front face) |
| LockScrew_Azm | 116 | VernierSteel_Azm |
| DepthRod_Azm | 36 | VernierSteel_Azm |
| **Total** | **768** | 2 |

That is well under the 5k interactive-prop guideline in CLAUDE.md and the 10k task budget.

The meshes are triangulated and manifold, with no n-gons and no loose vertices, and normals point
out. Each has one UV map (`UVMap`) with no overlaps. Visible long edges have 0.3 mm bevels. Normals
are smooth with weighted normals (sharp above 60°, or 25° on the knurled screw) and are exported as
custom normals; leave Unity's Normals setting on "Import".

- **VernierSteel_Azm**: Principled BSDF, base 0.57 grey, metallic 1, roughness 0.35, no texture.
- **VernierMarkings_Azm**: Base Color = the texture (linked directly, so the FBX references it).
  In Blender, metallic is 1 − alpha and roughness is 0.35 + 0.3·alpha; the FBX does not carry
  those. Suggested URP setup: URP/Lit, Base Map = `VernierMarkings_Azm.png` (sRGB), Metallic about
  0.6, Smoothness about 0.6. On `MainBody_Azm` Unity lists this material first (sub-mesh 0).
- Texture import: keep it at 1024. Linear or Trilinear filtering, Aniso about 4. Mip maps will fade
  the 1 px main ticks when the scale is small on screen. If you draw the scale procedurally
  instead, use the formulas in section 2.

## 4. Assumptions and deviations (please check)

1. **Slider length is 40 mm, not about 50.** The beam is 200 mm and the fixed jaw takes the first
   10 mm left of zero. A 50 mm slider would run off the beam at a 150 mm opening.
2. **The main scale has ticks 0–150 mm only, as specified.** Readings above 141.0 mm cannot be fully
   resolved, because the matching vernier line would fall past the last tick. Limit app readings
   to 141 mm, or extend the ticks to 160 mm.
3. **The slider's front frame extends 3.5 mm left of its jaw face**, in front of the beam. The spec
   puts the vernier 0 line on the jaw face, so without this the 0 line and the "0" label would be
   cut off.
4. **The inside jaws are crossed** (back half / front half), so their measuring edges coincide when
   closed. The smallest internal diameter they can measure is about 1.5 mm.
5. Main ticks are 1 px = 0.167 mm wide (spec: about 0.15). That keeps them pixel-exact.
6. The scale strips are faces of the surface itself, not decals, so there is no z-fighting.
7. The depth-rod groove runs the full length of the beam's back face and is open at both ends.
8. **FBX exporter bug.** Blender 5.1's "Apply Transform" put objects two levels deep (the slider's
   children) at a wrong 90° rotation and offset. `export_fbx()` in the build script corrects this
   during the export only. The FBX was re-imported to confirm every object matches the .blend.
9. The .blend also has a `MarkingSetup_Azm` scene (the orthographic camera and artwork used to
   render the texture) and the default Light and Camera. None of these are exported.

## 5. Rebuilding

Open `ArtSource/VernierCaliper_Azm.blend`, open the text block `build_vernier_azm.py`, and click
**Run Script**. This rebuilds the geometry, re-renders the texture and rebuilds the materials and
hierarchy.

To export, run this in Blender's Python console:

```python
g = {}
exec(bpy.data.texts["build_vernier_azm.py"].as_string(), g)
g["export_fbx"]()
```

## 6. Screenshots (in this folder)

1. `VernierCaliper_Azm_Shot1_Closed_ZeroOnZero.png`: closed, vernier 0 on main 0.
2. `VernierCaliper_Azm_Shot2_Open_2p34cm.png`: open to 2.34 cm (main 2.3 cm, line 4 coincides).
3. `VernierCaliper_Azm_Shot3_InsideJaws.png`: inside jaws open to 15 mm.
4. `VernierCaliper_Azm_Shot4_DepthRod.png`: depth rod out 40 mm past the beam end.
