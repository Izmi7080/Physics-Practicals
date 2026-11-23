# Task: Screw Gauge — Thickness of a Metal Strip (Matric Physics Exp 1.3)

Read `CLAUDE.md` first; all of its rules apply (`_Azm` naming and namespace, singleton rules,
three-layer separation, screen-space UI, documentation, never delete without asking).
Reference pages: `Docs/Exp1.3_ScrewGauge/` (textbook pages 7, 8, 9 and 11).
**Status: BUILT (2026-10-21).** Stage 1 (model) first, then Stages 0, 2, 3 and 4 in one pass as
the user asked, with the recommended answers to the open questions. No post-lab.
Scene `ScrewGauge_Azm` (catalog card "Screw Gauge: Thickness of a Metal Strip", replacing
"Physics Practical 3"). Model notes: `Assets/_Azm/Art/Models/ScrewGauge/ScrewGauge_Azm_NOTES.md`.

## Decisions taken (recommendations)
1. Metal strip, true thickness 0.73 mm, ±1 LC per place (ASSUMPTIONs in
   `ScrewGaugePracticalConfig_Azm`); a wire is available through `objectKind`.
2. Zero error random each session, ±1–5 LC, never zero.
3. Thimble drag + hold-to-turn Ratchet (and Open). A reading is accepted only after the ratchet
   clicks; closing with the thimble alone gives the "use the ratchet" warning.
4. Lock: tappable; it blocks turning and says so.
5. Shared keypad: `CaliperReadingPadUI_Azm` now works through `IReadingInstrument_Azm` (caliper and
   screw gauge). The pendulum and cylinder scenes were rebuilt and re-tested.

## Deviations
- The least count is a 4-choice question (after the one-turn pitch demonstration), not typed entry.
- Calculation: the worked lines are revealed (Learn) or shown at once (Perform); the student does not
  type the mean and there is no calculator.
- Briefing apparatus cards do not highlight 3D objects; precautions show their reason inline.
- Ratchet over-tightening is warned about, but not simulated as a smaller reading.
**Out of scope: the post-lab activities** (folded paper, coin, comparisons, similarity/difference
with the caliper, brain teaser) — optional, not built yet.

## What the textbook asks for
- **Instrument:** U-shaped frame, anvil, spindle, lock, sleeve with the main scale and the datum
  line, thimble with the circular scale, ratchet. Range 0–25 mm.
- **Pitch** = distance the spindle moves for one turn of the thimble = **0.5 mm**.
  **Least count** = pitch ÷ divisions on the circular scale = 0.5 mm ÷ 50 = **0.01 mm**.
- **Zero error** (anvil and spindle closed with the ratchet):
  - 0 of the circular scale ON the datum line: no zero error.
  - 0 BELOW the datum line: **positive**. Zero error = +n × LC (textbook: 2nd division on the
    datum line → +0.02 mm).
  - 0 ABOVE the datum line: **negative**. Zero error = −(50 − n) × LC (textbook: 46th division →
    −(50 − 46) × 0.01 = −0.04 mm).
  - **Zero correction** = −(zero error): negative error → positive correction, and vice versa.
- **Procedure:** find LC → close anvil and spindle with the ratchet until it clicks → find the zero
  error and correction → hold the strip between anvil and spindle → tighten gently with the ratchet
  until it clicks → main-scale reading M (the mark just under or before the edge of the thimble) →
  circular-scale division n on the datum line → X = n × LC → Y = M + X → t = Y + ZC → two more
  readings at different places → mean thickness.
- **Table:** No. | M (mm) | n | X = n × LC (mm) | Y = M + X (mm) | t = Y ± ZC (mm), three rows,
  with LC, zero error and zero correction above it; mean t = (t₁ + t₂ + t₃)/3. Result in mm.
- **Cautions:** apply the zero correction; take readings at different places; do not press the
  jaws very hard; do not turn the thimble back and forth unnecessarily; turn the screw gently using
  the ratchet. **Think boxes:** what is the ratchet for (stops you over-tightening, so every reading
  uses the same light pressure); why must the strip be flat / the wire kink-free.

## Reuse from the cylinder practical
| Piece | Reuse |
|---|---|
| Step-as-data controller pattern, HUD layout, drawer (Data / Calc / Guide), toast, pause, confirm, results | Copy and adapt (`VernierCylinder_Azm` pattern) |
| `Core/PracticalGuide_Azm`, `UI/GuideViewUI_Azm`, `UI/BriefingUI_Azm`, `UI/UISound_Azm`, `UI/PracticalCameraRig_Azm` | As they are |
| `Instruments/CaliperReadingPadUI_Azm` (M, n keypad; sign + n for zero error) | Generalise into a shared reading pad (M may have .5 here; n goes to 49) |
| Code-drawn magnifier | New one for the sleeve + thimble (`ScrewGaugeMagnifierUI_Azm`) |

---

# STAGE 0 — Decisions (proposed, need approval)
1. **Object measured:** the metal strip (the experiment's title object), true thickness
   **0.73 mm** (ASSUMPTION: gives M = 0.5, n = 23, so both scales are used), ±1 LC per position.
   The wire is offered as a config switch, not a second practical.
2. **Zero error:** random each session, ±1–5 least counts, never zero (like the cylinder).
3. **Turning the gauge on a phone:**
   - **Thimble:** drag up/down on the thimble to turn it (fast travel, like spinning it with the
     fingers). Shows the spindle moving; it cannot pass the strip.
   - **Ratchet:** a hold-to-turn **Ratchet** button turns it slowly; at contact it "clicks" (sound +
     tiny ratchet wobble) and stops. Readings are only accepted after the ratchet has clicked.
   - Closing onto the strip with the thimble alone is allowed but warned: "Use the ratchet - turning
     the thimble hard presses the strip and gives a smaller reading." (teaches the caution).
4. **Lock:** shown and tappable (locks the spindle after a reading); optional for the flow.
5. **Shared pad:** generalise the caliper keypad rather than copying it (both practicals keep
   working; regression-test the pendulum and cylinder steps).

## STOP GATE 0

---

# STAGE 1 — 3D model (Blender, through MCP) — DONE (2,422 triangles, 3 materials)
Same standard as the vernier caliper (`Art/Models/VernierCaliper/VernierCaliper_Azm_NOTES.md`):
real size, exact scale geometry, procedural build script saved in the .blend, FBX for Unity.
- Hierarchy: `ScrewGauge_Azm` (root, origin = centre of the anvil's measuring face, axis +X)
  → `Frame_Azm` (frame + anvil + sleeve with main scale and datum line), `Lock_Azm`,
  `Spindle_Azm` (gap = local X; rotation about X = gap / 0.5 mm × 360°) → `Thimble_Azm` (circular
  scale; separate so Unity can rotate it to simulate the zero error), `Ratchet_Azm` (spins on its
  own for the click).
- Graduations as one texture with exact, linear UV mapping documented in the NOTES.
- Files: `ArtSource/ScrewGauge_Azm.blend`, `ArtSource/build_screwgauge_azm.py`,
  `Assets/_Azm/Art/Models/ScrewGauge/` (FBX, PNG, NOTES, screenshots).

## STOP GATE 1

---

# STAGE 2 — Instrument logic (`Scripts/Instruments`, shared)
1. `ScrewGaugeConfig_Azm`: pitch 0.5 mm, 50 divisions, range 0–25 mm, ratchet speed, tolerance.
2. `ScrewGaugeMeasurement_Azm` (logic): gap from the thimble angle (gap = angle / 360° × pitch),
   stops at the object; ratchet vs thimble closing; "clicked" state; zero error (+ = reads high);
   reading check (M with 0.5 mm steps, n 0–49) and zero-error check (sign + n, textbook method).
3. `ScrewGaugeRigView_Azm`: drives Spindle_Azm (position + rotation), Thimble_Azm (zero-error
   offset), Ratchet_Azm (click wobble); Unity axis mapping from the model NOTES.
4. `ScrewGaugeDragInteraction_Azm`: vertical drag on the thimble → angle (IDragOwner_Azm, so the
   camera does not orbit).
5. `ScrewGaugeMagnifierUI_Azm`: code-drawn sleeve (mm marks above the datum line, half-mm below,
   numbers 0, 5, 10…) with the thimble edge, and a vertical strip of the circular scale moving past
   the datum line; "Show me" highlights M and n.
6. Shared reading pad (from the caliper pad): M field accepts x.0 / x.5; n 0–49; zero-error mode
   with sign. Regression: pendulum + cylinder still pass.

## STOP GATE 2

---

# STAGE 3 — The practical: `ScrewGauge_Azm` scene and core loop
- Scene: `LabEnvironment_Azm`, a tray with the screw gauge in a small stand (axis horizontal,
  scales facing the camera) and the metal strip. Views: Overview, Gauge.
- Config `ScrewGaugePracticalConfig_Azm`: true thickness, variation, zero-error setting,
  least-count choices, readings (3), step list.
- Steps (data):
  1. **Pitch and least count:** turn the thimble one full turn and watch the spindle move 0.5 mm
     on the main scale; then choose the least count (0.5 mm, 0.05 mm, 0.01 mm, 0.1 mm).
  2. **Zero error:** close with the ratchet until it clicks; read the zero error (sign + n);
     ZC shown in a chip.
  3. **Thickness × 3:** the strip is placed between anvil and spindle (a different place each time);
     close with the ratchet; enter M and n; the row fills in (M, n, X, Y, t).
  4. **Calculate:** mean t = (t₁ + t₂ + t₃)/3, worked line by line (Learn) or shown at once (Perform).
  5. **Result:** thickness of the strip in mm, Retry / Main menu.
- Data tab: the textbook table and constants line (LC, zero error, ZC) in mm, 2 decimal places.
- Catalog: replace "Physics Practical 3" with this practical (Learn on, Perform locked).

## STOP GATE 3

---

# STAGE 4 — Learn mode and polish
Briefing (Aim, Apparatus, What you will measure), "Show me", hints (pitch, ratchet purpose, positive
error is subtracted, flat strip, different places), Guide tab from a `PracticalGuide_ScrewGauge_Azm`
asset (cautions with their reasons, the two Think questions answered), worked calculation, pause
menu, sounds (ratchet click). Test at 16:9 and 20:9.

## STOP GATE 4

---

## Open questions
1. Strip thickness 0.73 mm OK, or do you have a value from your lab?
2. Zero error: random each session (recommended) or fixed?
3. Thimble drag + hold-to-turn ratchet (recommended), or ratchet only?
4. Metal strip only (recommended), or let the student choose strip or wire?
