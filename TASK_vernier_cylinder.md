# Task: Vernier Calipers — Solid Cylinder (Matric Physics Exp 1.1 + 1.2, one practical)

Read `CLAUDE.md` first; all of its rules apply (`_Azm` naming and namespace, singleton rules,
three-layer separation, screen-space UI, documentation, never delete without asking).
**Status: BUILT (2026-10-19)** — Stages 0, 1, 2 and 4 done in one pass, as the user asked.
Stage 3 (post-lab) is NOT built: the user said it is optional and not wanted yet.

## Decisions taken (user: "use your own recommendations")
1. Cylinder: true D = 2.18 cm, L = 4.36 cm (non-zero vernier divisions); each reading varies by
   ±1 LC (ASSUMPTION, in `CylinderConfig_Azm`).
2. Zero error: random each session, ±1–4 least counts, never zero (`zeroErrorSetting` in the config;
   can be set to Fixed or None).
3. Sign: vernier 0 right of MS 0 = positive. ZC = −(zero error); corrected = Y + ZC. A negative zero
   error is read as −(10 − n) × LC (flagged ASSUMPTION in `VernierScale_Azm` for the teacher).
4. Post-lab: not built (user decision). Inside jaws and depth rod were only needed for it, so they
   were not added to the instrument either.
5. Shared shell: the approved pendulum HUD was NOT refactored (regression risk). New shared pieces
   were written instead and the new practical uses them: `Core/PracticalGuide_Azm`,
   `UI/GuideViewUI_Azm`, `UI/BriefingUI_Azm`, `UI/UISound_Azm`, `UI/PracticalCameraRig_Azm`, plus
   `Scripts/Instruments`. The pendulum's Stage 4 can use the same pieces.

## Deviations from the plan below
- Step 1 (least count) is a 4-choice question, not typed entry.
- Step 5 (calculate): the worked lines are revealed one per tap (Learn) or all at once (Perform);
  the student does not type the results and there is no calculator.
- Apparatus briefing card does not highlight 3D objects; precautions show their reason inline
  (no expand/collapse).
- Zero-error readings must be exact; diameter/length readings are accepted within ±1 LC.

## Why one practical
Exp 1.1 (area of cross-section of a solid cylinder) and Exp 1.2 (volume of a solid cylinder) use
the same instrument, the same object and the same reading routine: least count → zero error →
grip → M and n → D = M + n × LC ± zero correction, three times. 1.2 only adds the length L and
V = πr²L. So the app runs them as **one practical, "Vernier Calipers: Solid Cylinder"**, whose
results panel reports both the area (Exp 1.1) and the volume (Exp 1.2).

## What already exists (built for the pendulum, reused here)
| Piece | Where | Status for this practical |
|---|---|---|
| Caliper 3D model, exact scales, separate vernier plate | `Art/Models/VernierCaliper` (+ NOTES) | Reuse as is |
| `CaliperMeasurement_Azm` (grip, contact, reading check) | `Scripts/Instruments` | Extend: zero error, inside jaws, depth rod |
| `CaliperMagnifierUI_Azm` (code-drawn scales) | `Scripts/Instruments` | Extend: draw the zero error |
| `CaliperReadingPadUI_Azm` (M / n keypad) | `Scripts/Instruments` | Extend: signed zero-error entry |
| `CaliperDragInteraction_Azm`, `CaliperRigView_Azm` | `Scripts/Instruments` | Reuse; rig view also shifts the vernier plate |
| Step-as-data pattern, HUD layout, drawer, toast, results, theme | `Scripts/Practicals/Pendulum`, `Scripts/UI` | Pattern reused; see Stage 0 |

## Syllabus content (from the textbook pages)
**Exp 1.1 procedure:** find the least count; check zero error with the jaws closed; grip the
cylinder; read the main scale M and the coinciding vernier division n; D = M + n × LC ± zero
correction; repeat; area A = πD²/4 = πr².
**Exp 1.2 procedure:** measure the length L at least three times as in 1.1 and average; measure the
diameter D at least three times and average; V = πr²L.
**Observation table (1.2, also used for 1.1's diameter):**
Quantity | No. of obs. | Main scale reading M | Vernier division n | Fraction X = n × LC (cm) |
Observed value Y = M + X (cm) | Corrected value = Y ± ZC (cm) — three rows for L, three for D.
Above the table: Least count LC = … mm = … cm; Zero error = … cm; Zero correction ZC = (±) … cm.
**Cautions:** close the jaws gently; apply the zero correction; both jaws must touch the cylinder.
**Post-lab (1.1):** internal diameter of a beaker (inside jaws) and its depth (depth rod).
**Post-lab (1.2):** reading figures with a zero error (Fig 1.1.6/1.1.7); zero-error table (vernier
0 right of MS 0 = positive, left = negative); pencil thickness (metre rule vs caliper); least count
of a clock.
**Sign convention to implement (check with the teacher, see open questions):** zero error is
positive when the vernier 0 lies to the right of the main-scale 0 with the jaws closed. Zero
correction = −(zero error). Corrected value = observed value + zero correction.

---

# STAGE 0 — Decisions (no code)
Report and get approval on:
1. **Shared practical shell.** The pendulum's HUD, drawer, step pill, toast and results panel are
   written inside `VirtualLab.Azm.Pendulum`. Option A: extract the generic parts into
   `Scripts/UI` + `Scripts/Core` (a `PracticalStep_Azm` base, a generic step HUD) and make both
   practicals use them. Option B: copy and adapt for this practical. Recommend A (the step pattern
   was designed to be reused), but it touches the approved pendulum, so it needs a regression test.
2. **Scene:** new `VernierCylinder_Azm` scene using `LabEnvironment_Azm`, built by an editor
   builder like the pendulum's.
3. **Catalog:** replace the "Physics Practical 2" placeholder with this practical (Learn mode on,
   Perform locked).

## STOP GATE 0

---

# STAGE 1 — Instrument upgrades (`Scripts/Instruments`, shared)
1. **Zero error.** `CaliperMeasurement_Azm` gets a zero error e (mm, ±, whole least counts). With
   the jaws closed the scales read e; every reading is offset by e. `CaliperRigView_Azm` shifts
   `VernierPlate_Azm` by e along the slide axis (Unity local x = −e; see the model NOTES). The
   magnifier draws the vernier at opening + e. The value of e comes from the practical's config
   (none / fixed / random per session within ± N least counts).
2. **Zero-error step support:** close the jaws → the student reads the zero error with its sign
   (keypad gets a +/− toggle) → validated → zero correction shown.
3. **Inside jaws** (beaker internal diameter): opening is limited from ABOVE by the bore (the
   jaws stop when they touch the walls); reading = opening.
4. **Depth rod** (beaker depth): the rod extends by the opening; it stops when it touches the
   bottom; reading = opening.
5. **Regression:** the pendulum's caliper step still works (zero error 0, outside jaws).

## STOP GATE 1
Show the three modes and a positive and a negative zero error on the magnifier.

---

# STAGE 2 — The practical: scene and core loop
## 2.1 Scene
Caliper station like the pendulum's (upright caliper, scales facing the camera) with a solid
metal cylinder. The cylinder can be placed two ways between the outside jaws: across (diameter)
and lengthwise (length). A beaker on the bench for the post-lab. Views: Overview, Caliper
(diameter), Caliper (length), Beaker.

## 2.2 Config (`CylinderConfig_Azm`)
True diameter and length (e.g. D = 2.18 cm, L = 4.36 cm — values that give a non-zero n), a small
per-reading variation (a real cylinder is not perfect; ± 1 least count, flagged ASSUMPTION),
the zero-error setting, the reading tolerance (± 1 LC), beaker internal diameter and depth.

## 2.3 Steps (data, like the pendulum's step list)
1. **Least count:** the magnifier shows both scales; the student counts: 10 vernier divisions =
   9 mm, so LC = 1 mm / 10 = 0.1 mm = 0.01 cm, and enters LC in mm and cm.
2. **Zero error:** close the jaws; read the zero error with its sign; the app shows ZC.
3. **Diameter × 3:** grip the cylinder across; read M and n; the row fills in
   (M, n, X = n × LC, Y = M + X, corrected = Y + ZC). Between readings the cylinder turns a little
   (different diameter, slightly different value).
4. **Length × 3:** the cylinder is placed lengthwise; same routine.
5. **Calculate:** mean D, r = D/2, A = πr² (Exp 1.1), mean L, V = πr²L (Exp 1.2). The student enters
   each result (calculator available), with the worked formula shown.
6. **Results:** A in cm² and V in cm³, the table, and Retry / Main Menu.

## 2.4 Data tab
The textbook table exactly (section above): LC, zero error and ZC in a header line; rows L1–L3
and D1–D3; mean L and mean D under it. Precision: cm to 2 decimal places (the LC is 0.01 cm).

## STOP GATE 2
A student can do the whole loop from the menu to the results at 16:9 and 20:9.

---

# STAGE 3 — Post-lab activities (optional extension after the results)
1. **Beaker internal diameter** with the inside jaws.
2. **Beaker depth** with the depth rod.
3. **Zero-error exercise:** the two textbook situations (vernier 0 right of MS 0 with the 2nd
   division coinciding; left of MS 0 with the 3rd coinciding): the student states the zero error
   and corrects a given reading. Drawn with the magnifier, not as images.
(The pencil and clock questions are outside the caliper simulation; leave them out or show them
as text questions — see open questions.)

## STOP GATE 3

---

# STAGE 4 — Learn mode and polish
Same pattern as the pendulum's Stage 4: briefing (Aim, Apparatus, What you will measure),
"Show me" on every reading, hints (e.g. "Both jaws must touch the cylinder", "Close the jaws
gently", "Positive zero error is subtracted"), Guide tab content from a `PracticalGuide_Azm`
asset filled from the textbook pages, worked calculations, pause menu, sounds.

## STOP GATE 4

---

## Open questions (answer before Stage 0)
1. **Cylinder size:** do you want specific true values (D and L), or should I choose values that
   give a non-zero vernier division?
2. **Zero error:** a fixed zero error, a random one each session, or none in Learn mode at first?
3. **Sign convention:** confirm "vernier 0 right of MS 0 = positive zero error, corrected value =
   observed − zero error" matches how your teacher marks it.
4. **Post-lab:** include the beaker (inside jaws + depth rod) in this practical, or leave it for
   later? Include the pencil and clock questions as plain text questions?
5. **Shared shell (Stage 0, item 1):** OK to refactor the pendulum's HUD into shared code?
