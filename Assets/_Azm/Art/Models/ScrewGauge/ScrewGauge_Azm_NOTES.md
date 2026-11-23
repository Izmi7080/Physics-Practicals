# ScrewGauge_Azm — model notes

Source: `ArtSource/ScrewGauge_Azm.blend` (outside `Assets/`). The build script is the text block
`build_screwgauge_azm.py`, also saved as `ArtSource/build_screwgauge_azm.py`. It is real size
(1 unit = 1 m) and models the textbook instrument for Exp 1.3: **pitch 0.5 mm, 50 circular
divisions, least count 0.01 mm, range 0–25 mm.**

## 1. Objects, origins, motion

| Object | Parent | Unity local position (mm) | What it is |
|---|---|---|---|
| `ScrewGauge_Azm` | – | 0 | Empty root. Origin = centre of the **anvil's measuring face** |
| `Frame_Azm` | root | 0 | U-frame (enamel), anvil, spindle boss, sleeve with the main scale and datum line |
| `Lock_Azm` | root | (−33, 8, 0) | Lock knob + lever. Origin on its turning axis (local Y up in Unity) |
| `Spindle_Azm` | root | (−gap, 0, 0) | Spindle. Origin = centre of its measuring face |
| `Thimble_Azm` | Spindle | (−42, 0, 0) | Thimble with the circular scale. Origin on the axis at its left edge |
| `Ratchet_Azm` | Spindle | (−72, 0, 0) | Ratchet. Origin on the axis |

All imported rotations are 0 and all scales 1 (checked in Unity 6000.3).

**The one rule Unity needs** (verified in Unity: line 34 lands exactly on the datum at 7.84 mm):

```
gap (m)   = thickness between anvil and spindle, 0 ... 0.025
Spindle_Azm.localPosition = (-gap, 0, 0)
Spindle_Azm.localRotation = Quaternion.Euler(gap / 0.0005f * 360f, 0, 0)   // one turn per 0.5 mm
```

The thimble and ratchet follow because they are children of the spindle.

- **Axis mapping:**

  | Blender | Unity |
  |---|---|
  | +X (anvil → ratchet) | −X |
  | +Z (up) | +Y |
  | −Y (front, the scale side) | +Z |

  To see the scales face-on, put the camera on the +Z side looking towards −Z.
- **Measuring faces:** the anvil face is the plane x = 0 and the spindle face is x = −gap (Unity).
  Both are flat. At gap 0 they touch, with no overlap.
- **Zero error:** rotate `Thimble_Azm` relative to the spindle:
  `Thimble_Azm.localRotation = Euler(zeroError_mm / 0.01 * 7.2°, 0, 0)` (+ = reads high).
  Checked in Blender:
  - **+0.02 mm:** with the gap closed, line 2 is on the datum and 0 is below it. Textbook: positive error.
  - **−0.04 mm:** line 46 is on the datum and 0 is above it. Textbook: negative error,
    −(50 − 46) × LC.
- **Ratchet click:** spin `Ratchet_Azm` a little on its own (local X) without moving the spindle.

## 2. Scales and UV mapping
One texture, `ScrewGaugeMarkings_Azm.png`, 1024 × 256 RGBA. Marks are black with alpha 1.
Everywhere else alpha is 0 and RGB is steel grey 0.78, so the texture works as a base map and as a mask.
Marks were rendered with anti-aliasing off on exact pixel columns; the digits are anti-aliased.

Angles round the axis, α, are measured from the **front** towards **up**.

**Sleeve: main scale and datum line**
- Atlas region: rows 0–127, V 0–0.5. It covers the front band of the sleeve, α = −40°…+40°,
  which is 8 faces of 10°.
- `U = (s + 3.515625) × 32 / 1024`, where s is mm from the main-scale 0 (at 32 px per mm).
  The main-scale 0 is 42 mm from the anvil face, so with the gap closed the thimble edge sits on it.
- `V = (α + 40°) / 80° × 0.5`. The datum line is at α = 0 (V = 0.25), pixel rows 63–64.
- **mm marks** (k = 0…25) sit **above** the datum line, centred at pixel column 112.5 + 32k. They
  are 5 px wide (0.16 mm), 1.25 mm long, and 1.8 mm long every 5 mm. They are numbered 0, 5 … 25.
- **Half-mm marks** sit **below** the line, at columns 128.5 + 32k (k = 0…24), 1.0 mm long.

**Thimble: circular scale**
- Atlas region: rows 128–255, V 0.5–1, and U 0…1000/1024. Columns 1000–1023 are empty.
- `U = (10.5 + 20 × α / 7.2°) / 1024`. Line k (k = 0…49) is at α = 7.2k, on pixel column
  10.5 + 20k, 3 px wide. The UV seam is at α = −3.78°, between lines 49 and 0.
- `V = 0.5 + (x − 0.4) / 8 × 0.5`, where x is mm from the thimble edge. The marks start at the edge.
  They are 1.8 mm long, and 2.6 mm long every 5 divisions, numbered 0, 5 … 45.
- **Reading direction:** as the gap opens the thimble turns so the front surface moves down. The
  next higher number then comes down to the datum line. With the gap closed, 5 is above the line
  and 45 below it, as in textbook Fig 1.3.2(a).
- The thimble's U runs round the cylinder and V along it, so the digits in the PNG are stored
  mirrored and turned 90°. They read upright on the model; this matters only if you redraw the texture.

**Checks:**
- All 26 mm marks, 25 half-mm marks and 50 circular marks are on their exact pixel columns.
- **Closed:** 0 on the datum line.
- **Gap 7.84 mm:** the last visible sleeve mark is 7.5 (below the line) and line 34 is on the datum,
  so 7.5 + 34 × 0.01 = 7.84 mm. In numbers, line 34 is at 0.000°, with lines 33 and 35 at ∓7.2°.

## 3. Triangles and materials
| Object | Triangles | Materials |
|---|---|---|
| Frame_Azm | 764 | ScrewGaugeSteel_Azm, ScrewGaugeFrame_Azm, ScrewGaugeMarkings_Azm (sleeve band) |
| Thimble_Azm | 1100 | ScrewGaugeSteel_Azm, ScrewGaugeMarkings_Azm (scale band) |
| Ratchet_Azm | 288 | ScrewGaugeSteel_Azm |
| Spindle_Azm | 144 | ScrewGaugeSteel_Azm |
| Lock_Azm | 126 | ScrewGaugeSteel_Azm |
| **Total** | **2422** | 3 |

That is under the 5k interactive-prop guideline. The meshes are triangulated and manifold, with no
n-gons or loose vertices, and normals point out. Each has one UV map. Normals are smooth and
weighted, and are exported as custom normals; leave Unity's Normals setting on "Import".

- **ScrewGaugeSteel_Azm:** satin chrome. Metallic 1, roughness 0.3.
- **ScrewGaugeFrame_Azm:** dark navy enamel. Metallic 0, roughness 0.45.
- **ScrewGaugeMarkings_Azm:** Base Color = the texture. In Blender, metallic is 1 − alpha.
  Suggested Unity setup: URP/Lit with Metallic about 0.6 and Smoothness about 0.6, the same as the
  vernier caliper's markings material.

The caliper had 2 materials. This model has 3, because a screw gauge frame is painted, not bare steel.

## 4. Assumptions
1. **Proportions are typical of a school 0–25 mm micrometer:** anvil and spindle Ø 6.5 mm, sleeve
   Ø 9.5 mm, thimble Ø 16 mm and 30 mm long, frame 8 mm thick. Only the scales and the motion
   are exact.
2. **The circular scale sits on the thimble's cylinder**, starting right at its edge, not on a
   bevel. The datum line meets the thimble edge 3.25 mm lower than the scale surface. The app's
   code-drawn magnifier will be the reading aid, as with the caliper.
3. **The frame has no printed text or brand.** The textbook's "0-25 x0.01mm" label was left off.
4. **The spindle and sleeve pass through the frame and thimble without bored holes.** They are
   hidden inside, which saves triangles.
5. **The same FBX exporter fix as the vernier caliper is applied** for the nested children. I
   re-imported the FBX and every object matches the .blend.

## 5. Rebuild / export
Open `ArtSource/ScrewGauge_Azm.blend`, open the text block `build_screwgauge_azm.py`, and click
**Run Script**. To export, run this in Blender's Python console:

```python
g = {}
exec(bpy.data.texts["build_screwgauge_azm.py"].as_string(), g)
g["export_fbx"]()
```

`set_gap(gap_m, zero_error_mm)` in the script poses the gauge for screenshots.

## 6. Screenshots (in this folder)
1. `ScrewGauge_Azm_Shot1_Closed_ZeroOnDatum.png`
2. `ScrewGauge_Azm_Shot2_Reading_7p84mm.png`: M = 7.5 mm, n = 34.
3. `ScrewGauge_Azm_Shot3_PositiveZeroError_0p02mm.png`: line 2 on the datum, 0 below it.
4. `ScrewGauge_Azm_Shot4_Overview_5mm.png`: 5 mm gap.
