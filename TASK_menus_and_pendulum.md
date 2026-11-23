# Task: App Shell (Splash, Menus, Practical Select) + New Simple Pendulum Scene

Read `CLAUDE.md` first. All of its rules apply here: `_Azm` naming and namespace, singleton rules,
three-layer separation, screen-space UI rules, documentation requirements, never delete without
asking, and do not modify the old pendulum scene.

## How to use this file
The work is split into five stages. **Each stage ends with a STOP GATE**: finish the stage, report
what was built, list anything you assumed, explain how to test it, then wait for approval. Do not
start the next stage on your own.

| Stage | Contents |
|---|---|
| 0 | Audit of the existing project (no changes) |
| 1 | App shell: splash, main menu, practical select, mode select, settings, scene loading |
| 2 | New pendulum scene: cameras, UI layout, the full measurement loop, data table, graph, results |
| 3 | Vernier caliper and effective length |
| 4 | Learn-mode guidance layer, calculator, reflection, pause menu, polish |

## Reference material (in `Docs/` at the project root)
- `Docs/Simple_Pendulum_Experiment_Flow.pdf` — the Matric syllabus procedure. This is the source
  of truth for the experiment. Its content is also transcribed in the Appendix of this file.
- `Docs/Mockups/pendulum_drawer_open.jpg` and `Docs/Mockups/pendulum_drawer_closed.jpg` — target
  layout for the pendulum screen. Match the placement and style; the numbers and text in the
  mockups are illustrative, not exact. **One correction:** the mockup's instruction reads "Tap each
  time the bob passes the centre line". That is wrong: the bob passes the centre twice per
  oscillation. Use the wording defined in Stage 2.6.

---

# STAGE 0 — Audit (read only, change nothing)

Report on:
1. **Existing pendulum code** (v1 and any v2 work). For each script: what it does, which layer it
   belongs to, whether input is mixed into logic, and a verdict of **REUSE** (copy into
   `VirtualLab.Azm.Pendulum` and adapt), **REWRITE**, or **DROP**. Specifically assess the swing
   motion code, the trial/data logic, the g calculation and units handling, and the graph
   drawing.
2. **Character controller and interactable scripts.** The character controller is not used
   in the new scenes. Report whether any interactable script is worth reusing for tap/drag
   interactions on apparatus.
3. **Project setup:** Unity version, render pipeline, input system (old Input Manager, new Input
   System, or both), whether TextMeshPro is set up, current Player Settings for orientation, and
   the scenes currently in Build Settings.
4. **Lab environment:** which GameObjects make up the lab room in the current scene, their
   approximate triangle counts and material counts, and whether they can be turned into one
   reusable prefab (`LabEnvironment_Azm`).
5. **Vernier caliper model:** if a caliper model has been imported, report its triangle count,
   material count, real-world scale, whether the sliding jaw is a separate mesh/object, and any
   licence file or attribution requirement. If none is imported yet, say so.

## STOP GATE 0
Report all five sections. Wait for approval before creating anything.

---

# STAGE 1 — App shell

## 1.1 Scenes
Create three new scenes and add them to Build Settings in this order:
1. `Splash_Azm`
2. `MainMenu_Azm` (contains main menu, practical select, mode select, settings and about as
   panels in one scene)
3. `SimplePendulum_Azm` (empty lab for now; built in Stage 2)

Create the lab room as a prefab, `LabEnvironment_Azm`, by copying (not moving) the room objects
from the current scene. Use it in both `MainMenu_Azm` and `SimplePendulum_Azm` so the room is
defined once.

## 1.2 App-level managers
Only these two persist across scenes (see CLAUDE.md singleton rules):

- **`AppManager_Azm`**
  - Holds the current session: which practical was selected and which mode (`Learn` or
    `Perform`), as a plain `SessionInfo_Azm` class.
  - Loads scenes asynchronously behind a fade-to-black plus a small loading indicator. No
    instant cuts.
  - Provides `GoToMainMenu()`, `StartPractical(PracticalInfo_Azm, PracticalMode_Azm)`,
    `ReloadCurrentPractical()`.
  - Handles app pause/resume events.
- **`SettingsManager_Azm`**
  - Loads and saves settings with `PlayerPrefs`.
  - Settings: master volume, SFX volume, hints on/off, graphics quality (Low / High), and a
    "reset tutorials" action that clears the first-time flags.
  - Raises an event when a setting changes.

Both are created once in `Splash_Azm`. If a scene is played directly in the editor without going
through the splash, a small bootstrap component must create them, so any scene can be tested on
its own.

## 1.3 Practical catalog (data-driven)
- `PracticalInfo_Azm` (ScriptableObject): id, display name, subject (`SubjectType_Azm`: Physics /
  Chemistry / Biology), short description (1–2 lines), thumbnail sprite, estimated minutes, scene
  name, `isLocked`, `learnModeAvailable`, `performModeAvailable`.
- `PracticalCatalog_Azm` (ScriptableObject): the list of all `PracticalInfo_Azm` assets.
- Create the catalog with:
  - **Physics:** Simple Pendulum (unlocked, learn mode available, perform mode locked) plus four
    locked placeholders named "Physics Practical 2" to "Physics Practical 5".
  - **Chemistry:** five locked placeholders.
  - **Biology:** five locked placeholders.
- The menu builds its cards from the catalog at runtime. **Adding a teammate's practical later must
  only require editing the catalog asset and adding their scene to Build Settings — no menu code
  changes.** Write this in the summary comment of the menu script.
- If a card's scene name isn't in Build Settings, the card shows as unavailable and logs a
  warning. It must not crash.

## 1.4 Splash screen (`Splash_Azm`)
- Sequence: university logo (use a placeholder sprite slot the user can fill) → app title
  "Virtual Science Lab" with a one-line tagline → "Final Year Project — Information Technology
  University" credit line. Soft fades, about 2.5–3 s total.
- Loads `MainMenu_Azm` asynchronously during the sequence, so there is no extra wait at the end.
- Tap to skip is allowed after the first launch (tracked in `PlayerPrefs`).
- If Unity's own splash is enabled in Player Settings it plays before this one. Do not change
  that setting; just mention it in the report.

## 1.5 Main menu (`MainMenu_Azm`)
- **Background:** `LabEnvironment_Azm` with the camera doing a slow, gentle drift or orbit around
  the lab bench. Place a simplified pendulum stand on the bench so the scene reads as a physics
  lab. The drift must loop seamlessly.
- **Main panel:** app title, then buttons: **Start**, **Settings**, **About**, **Quit**.
  - Quit asks for confirmation.
- **Practical select panel** (opened by Start):
  - Three subject tabs along the top: Physics / Chemistry / Biology.
  - A grid (or horizontal scroll) of practical cards for the selected subject: thumbnail, name,
    short description, estimated time.
  - Locked cards are greyed out with a lock icon and a "Coming soon" label. Tapping one shows a
    small toast saying so. Locked items are **shown, never hidden**.
- **Practical detail / mode panel** (opened by tapping an unlocked card):
  - Name, description, estimated time, apparatus list (short).
  - Two mode buttons: **Learn** (enabled) and **Perform** (locked, with "Coming soon").
  - A Start button that calls `AppManager_Azm.StartPractical(...)`.
- **Settings panel:** the settings from 1.2 as sliders and toggles. Changes apply immediately and
  are saved.
- **About panel:** app name, team member names (placeholder fields), supervisor name
  (Sarfraz Raza), university, and an "Asset credits" list read from a data asset so model
  attributions can be added later.
- The Android back button goes back one panel. On the main panel it opens the Quit confirmation.
- Panel transitions are short slides or fades (0.2–0.3 s), all using `UITheme_Azm`.

## 1.6 Theme
Create `UITheme_Azm` (ScriptableObject) with the colours and style from the mockups: dark navy
panels at roughly 80–85 % opacity, white text, teal primary buttons, amber hint accents, green/red
valid/invalid, corner radius 12–16 px. Use a free-licensed rounded sans-serif font (e.g. Poppins
or Inter, both under the SIL Open Font License) as a TMP font asset. Every panel reads its colours
from the theme.

## STOP GATE 1
The app launches into the splash, reaches the main menu, and a student can browse subjects, see
locked cards, open Simple Pendulum, choose Learn, and load the (still empty) pendulum scene. Back
button and settings work. Report and wait.

---

# STAGE 2 — Simple Pendulum scene: core practical

Goal: a complete, syllabus-correct measurement loop with the new layout. The bob diameter is a
**fixed known value** in this stage (shown in the UI as "Bob diameter: 2.00 cm (given)"); Stage 3
replaces that with the caliper measurement.

## 2.1 Scene setup
- `SimplePendulum_Azm` uses `LabEnvironment_Azm`.
- Pendulum rig in the centre of the island bench: base board, rectangular metal frame, thread from
  the pivot at the top bar, spherical bob. Scale in real units (1 Unity unit = 1 m). The rig must
  allow thread lengths up to about 100 cm, so the frame must be tall enough. Adjust the rig
  height, and the bench height if needed, and report what you changed.
- A metre scale mounted vertically beside the thread, from the pivot downward, with readable cm
  markings, so the length can be read against it.
- An instrument tray at one side of the bench holding a vernier caliper (placeholder until
  Stage 3), a stopwatch and a metre rule.
- Put all tunables in `PendulumConfig_Azm` (ScriptableObject): true g = 9.8 m/s², n = 20
  oscillations, minimum number of lengths = 5, thread length range (default 30–100 cm), length
  resolution 0.1 cm, maximum release angle 15°, bob diameter 2.00 cm, "push" velocity threshold,
  timing tap tolerance.

## 2.2 Physics
- Scripted motion, no Rigidbody. Small-angle SHM: θ(t) = θ₀ · cos(√(g/L) · t), with **L =
  ℓ + r in metres** (effective length: thread length plus bob radius). This is valid because the
  release angle is capped at 15°. Put that reasoning in the script's summary comment.
- No damping (or negligible), so the bob keeps swinging for both timing runs at one length.
- The simulation clock pauses when the game is paused or the app goes to the background.

## 2.3 Camera views
- `CameraViewController_Azm` with named views, each a position + rotation (+ field of view):
  - **Overview:** the whole bench and rig.
  - **Rig:** closer on the thread, bob and metre scale — used for setting the length and angle.
  - **Timing:** framed so the mean (vertical) position and the full swing are clearly visible.
  - **Caliper:** close-up on the instrument tray (used in Stage 3).
- Transitions are smooth, eased moves of about 0.5–0.8 s.
- Each step can request a view, so the camera moves automatically when a step starts. The
  student can also switch views with the buttons at the bottom left.
- Limited pinch-to-zoom and one-finger drag-to-orbit inside the current view, clamped so the
  apparatus can never go off screen. Double-tap resets to the view's default framing. Touch input
  on UI must not also move the camera.
- **Drawer offset:** when the right-side drawer opens, the framing shifts so the apparatus stays
  centred in the visible area left of the drawer (as in `pendulum_drawer_open.jpg`), and shifts
  back when it closes. Choose the method (camera lens shift, or offsetting the view target) and
  explain the choice in the script comment.

## 2.4 Screen layout (screen-space canvas, inside the safe area)
Match the mockups:
- **Top-left:** pause button (round).
- **Top-centre:** instruction pill, "Step X of Y — <instruction>". Updates on every step change,
  with a short fade.
- **Top-right:** a "Trial X of 5" chip and a "g ≈ …" chip (shown once at least 2 valid rows exist;
  hidden before that).
- **Bottom-left:** camera view buttons with icons (Overview / Rig / Timing / Caliper). The active
  view is highlighted.
- **Bottom-centre:** the **context action area**. Its contents change with the step (e.g. a
  Confirm Length button, the large round tap counter during timing). Only the controls for the
  current step are shown.
- **Right drawer:** tabs **Data / Graph / Calc / Guide**. When closed it collapses to a slim
  vertical icon strip on the right edge (as in `pendulum_drawer_closed.jpg`). It opens and closes
  with a slide animation. It must not cover the context action area. When the drawer is open,
  that area sits in the visible region to its left.
- **Hint toasts:** appear above the bottom row, inside the visible area, auto-dismiss after a few
  seconds, and can be tapped away.
- **World-space elements** (the only ones): the protractor arc behind the bob, the live readout
  label next to the bob ("ℓ = 64.3 cm" while setting the length, "θ = 11°" while setting the
  angle), and the stopwatch face on the tray showing running time.

## 2.5 Step flow
Implement the practical as a step sequence driven by a controller (`PendulumController_Azm`,
scene-local). Each step declares its instruction text, its camera view, which controls appear in
the context action area, and its completion condition. Keep the steps data-like and readable,
since this pattern is reused for other practicals.

**Once per session:**
1. **Intro:** show the given bob diameter and the computed radius r. (Replaced by the caliper
   step in Stage 3.)

**Per length (repeat for at least 5 different lengths):**

2. **Set length ℓ** (Rig view). The student drags the bob up or down to change the thread length.
   The live readout shows ℓ to 0.1 cm and the metre scale matches. Clamped to the configured
   range. Confirm button. On confirm, show "L = ℓ + r = …" briefly.
   - Reject a length within 2 cm of one already recorded, with a message (lengths must differ
     enough to give separate points on the graph).
3. **Displace and release** (Rig view). The student drags the bob sideways; the protractor overlay
   shows green up to 15° and red beyond. The live readout shows θ. **Lifting the finger releases
   the bob.**
   - If released beyond 15°: the bob returns to rest and an error message explains why.
   - If the finger was still moving fast at release (above the push threshold): treat it as a
     **push**. The bob returns to rest and the message says to release from rest.
   - A valid release starts the swing.
4. **Timing run 1** (Timing view) — see 2.6. Records t₁.
5. **Timing run 2**, same length and same swing (no re-release needed). Records t₂.
6. **Record:** the row is added to the table with mean t, T = t/n and T². Row entry is animated.
   The trial counter advances.

**After at least 5 valid rows** (the student can add more; a "Finish & Analyse" button appears
once 5 exist):

7. **Analyse:** the drawer opens on the Graph tab. Plot T² against L, fit a line, compute g
   (see 2.8).
8. **Results:** a results panel with the final g, % error against 9.8 m/s², the graph thumbnail,
   and buttons **Retry** (reload the practical) and **Main Menu**. The reflection prompts are
   added in Stage 4.

## 2.6 Timing interaction (the core skill — implement carefully)
- The large round teal button in the context action area is the student's stopwatch hand.
- **First tap = start**, which should happen as the bob passes the mean position. The on-screen
  stopwatch starts.
- **Each following tap marks one completed oscillation**: the bob back at the mean position,
  moving in the same direction as at the start. The button shows the count "k / 20".
- **The 20th tap stops the timer.** The recorded t is the real elapsed time between the start tap
  and the 20th tap, so the student's own reaction time is part of the measurement, as in a real
  lab.
- Instruction wording during timing: "Tap when the bob passes the centre, then tap after each full
  swing (there and back)."
- Track quality silently, per run, for feedback (Stage 4 turns these into hints):
  - **Off-mean taps:** at each tap, the bob's |θ| relative to the amplitude. Taps far from the mean
    are counted.
  - **Half-swing counting:** if the tap intervals average about T/2, the student is tapping on
    every pass through the centre instead of every full oscillation. Detect this and show an
    immediate message; the run is discarded and restarted.
  - **Missed counts:** if an interval is about 2T, the student missed a count. Flag it; keep the
    run, but mark the row as suspect.
- A "Restart run" button next to the tap button discards the current run.

## 2.7 Data tab (observation table)
- Constants in a header line above the table: "n = 20 · r = 1.00 cm (d = 2.00 cm)".
- Columns: **No. | ℓ (cm) | L (cm) | t₁ (s) | t₂ (s) | mean t (s) | T (s) | T² (s²)**. This is the
  syllabus table with the constant columns (n, r) moved into the header so the table fits the
  drawer.
- Each row has a discard (×) button with a confirmation. A discarded row is removed from the
  graph and the g calculation, and no longer counts toward the 5.
- Suspect rows (from 2.6) show a small warning icon; tapping it explains why.
- An **Expand** button shows the table full-screen with all syllabus columns, including n and r.
- Displayed precision: lengths 1 decimal place, times 2 decimal places, T² 3 decimal places.

## 2.8 Graph tab and g calculation
- Plot T² (y, s²) against L (x). Axes with labels and units, gridlines, one point per valid row,
  and a best-fit line. Built with Unity UI (no charting package).
- Fit y = m·x + c by least squares. Show the slope and the intercept.
- **Units:** the table shows L in cm, but **the fit and g must use L in metres**. Convert
  explicitly, with a comment. Then g = 4π² / m.
- Show g, % error = |g − 9.8| / 9.8 × 100, and a line saying whether the line passes close to the
  origin ("A straight line through the origin confirms T² ∝ L"). Define "close to the origin" in
  the config.
- An **Expand** button shows the graph full-screen.
- The top-right g chip uses the same calculation, updating live from 2 valid rows onward.

## 2.9 Calc and Guide tabs (placeholders in this stage)
Show the tabs with a "Coming in the next stage" message. They are built in Stage 4.

## STOP GATE 2
A student can go from the menu into the pendulum scene, set 5 or more lengths, release correctly
(bad releases rejected), time two runs per length by tapping, see rows in the table, discard a
row, see the graph and g, and reach the results panel. Test at 1920×1080 and 2400×1080. Report
and wait.

---

# STAGE 3 — Vernier caliper and effective length

## 3.1 Decide the model route (based on the Stage 0 audit)
- **Route A — imported model usable:** the sliding jaw is (or can be made) a separate object,
  the triangle count can be kept around 5k or less, and the licence allows use. If the jaw is part
  of one merged mesh, report it: the user will separate it in Blender. Do not attempt mesh surgery
  in code.
- **Route B — simple built caliper:** built from primitives or simple meshes (beam, fixed jaw,
  sliding jaw, depth rod optional) with one material. It only needs to look clearly like a caliper.
- Recommend a route in the report. If the imported model is unusable, go ahead with Route B
  without waiting.
- Record the model's licence and attribution in the About panel's asset credits data.

## 3.2 Measuring interaction (Caliper view)
- A **Measure bob diameter** step replaces the Stage 2 intro step, once per session, before the
  first length. The camera moves to the Caliper view, which shows the caliper and the bob (held
  as a spare identical bob on the tray, or the camera frames the hanging bob — choose whichever
  frames better, and explain).
- The student drags the sliding jaw along the beam. The jaw's movement is constrained to the beam
  axis and **stops at contact** with the bob.
- **Reading magnifier:** a screen-space inset panel showing the main scale (mm) and the vernier
  scale (10 divisions, least count 0.1 mm = 0.01 cm), drawn by code from the jaw's actual
  position. Crisp and large. **Do not rely on the model's texture for the reading**; the
  magnifier is always accurate and legible, whatever the model looks like.
- The student enters the reading with a small numeric keypad as: main scale reading (mm) and
  aligned vernier division (0–9). The app shows d = MSR + VSD × LC.
- Validate against the true diameter (from config) within one least count. If wrong, explain
  what was misread (main scale vs vernier division). In Learn mode, allow a "Show me" button that
  highlights the aligned division.
- On success: r = d/2 is stored and shown in the table header. L = ℓ + r now uses the measured r.
- Zero error is out of scope. Note it in a comment as a possible later addition.

## 3.3 Fallback
If the caliper step cannot be completed in this stage, the Stage 2 "given diameter" intro remains,
behind a config flag. Effective length L = ℓ + r is used either way.

## STOP GATE 3
The student measures the bob with the caliper, the reading is validated, and r feeds into L.
Report the route taken and the caliper's triangle count. Wait.

---

# STAGE 4 — Learn mode, calculator, reflection, pause, polish

## 4.1 Mode switch
- `PracticalModeConfig_Azm` (or a flag in the session) decides which guidance features run.
- **Learn** = everything in this stage enabled. **Perform** = the same scene with the guidance
  off (briefing, hints, "Show me", worked-calculation reveal, predictions, reflection). Perform is
  locked in the menu, but the switch must exist and work, so enabling Perform later is a menu
  change only. **Never build a second version of the practical.**

## 4.2 Briefing (Learn mode, first entry)
A short, skippable card sequence before step 1: **Aim → Hypothesis → Apparatus → What you will
measure.** On the Apparatus card, each item highlights its 3D object (outline or glow) and the
camera frames it. Skip is always available; after the first time, the briefing only runs when
replayed from the Guide tab.

## 4.3 Oscillation demonstration (Learn mode, before the first timing run) — high priority
A short animated demonstration of ONE full oscillation: the bob swings mean → one extreme → mean
→ other extreme → back to mean, with its path traced, a counter ticking 0 → 1, and a marker
flashing at the mean position at the moments where the stopwatch should start and stop. Beginners
often misjudge what one oscillation is and where timing starts; this fixes that. Replayable from
the Guide tab and from a small button next to the tap counter.

## 4.4 Contextual hints (Learn mode)
Each fires once per session (the "hints" setting can turn all of them off; "reset tutorials" makes
them fire again). Show them as hint toasts.

| Trigger | Hint |
|---|---|
| First length setting | Measure from the pivot to the top of the bob. The bob's radius is added separately: L = ℓ + r. |
| Release beyond 15° | Beyond about 15° the pendulum no longer behaves as simple harmonic motion, so T = 2π√(L/g) stops being accurate. |
| Pushed release | Release from rest. A push gives the bob extra energy and invalidates the simple-pendulum assumption. |
| Several off-mean taps in a run | Start and stop at the centre. The bob moves fastest there, so the moment is easier to judge. |
| Half-swing counting detected | One oscillation is a full swing there and back, returning to the centre in the same direction. |
| First completed run 1 | Now time it again. Two runs averaged reduce the effect of a single mistimed run. |
| Before the first graph | A straight line through the origin confirms that T² is proportional to L. |

## 4.5 Prediction prompt (Learn mode, from the second length onward)
Before timing at a new length, ask: "Your new length is longer/shorter than the last one. Will the
time period T be: Larger / Smaller / About the same?" After the row is recorded, briefly show
whether the prediction matched. It is not scored. Keep it to one tap.

## 4.6 Calc tab
- **Worked calculations:** for the selected row, show each formula with the student's numbers,
  revealed one line at a time on tap:
  `r = d/2 = …`, `L = ℓ + r = …`, `mean t = (t₁ + t₂)/2 = …`, `T = t/n = …`, `T² = …`, and for the
  graph `g = 4π² / slope = …`. In Perform mode the lines show immediately without the reveal
  steps.
- **Simple calculator:** a four-function keypad with +, −, ×, ÷, x², √, π, clear and backspace. No
  scientific mode.

## 4.7 Guide tab
Content comes from a `PracticalGuide_Azm` ScriptableObject — **not hardcoded in scripts**. Fill it
from the Appendix. Sections (collapsible): Description, Hypothesis, Apparatus, Quantities,
Formulas, Procedure, Precautions, Observation table.
- **Procedure** highlights the step the student is currently on.
- Each **Precaution** expands to show why it matters.
- Buttons: "Replay briefing" and "Replay oscillation demo".

## 4.8 Reflection (end of the results panel, Learn mode)
Show "Why might your value of g differ from 9.8 m/s²?" with tappable options (multi-select, no
grading, all are real causes):
- Reaction time when tapping the stopwatch
- Miscounting oscillations
- Angle too large in some trials
- Reading the length inaccurately
- Air resistance and friction at the pivot

Then a one-line summary tying their choices to the precautions list.

## 4.9 Pause menu
Opened by the pause button or the Android back button. The simulation clock stops. Options:
**Resume**, **Restart current length** (discards any runs in progress for this length),
**Restart practical**, **Settings** (same settings panel as the main menu), **Guide**,
**Exit to menu** (with confirmation).

## 4.10 Feedback and polish
- Every button press has a short click sound and a visual press state.
- Correct and incorrect actions get distinct subtle sounds.
- Step changes animate the instruction pill; table rows slide in; the drawer slides.
- Check all text for readability on a 6-inch phone at 20:9.
- Keep all animations lightweight.

## STOP GATE 4
Full Learn-mode experience from menu to reflection. Report, including anything cut or simplified.

---

## Out of scope for this task
- VR, gaze, XR plugins.
- Backend, cloud save, analytics.
- Saving a session in progress across app restarts.
- Urdu or any localisation.
- Voice-over.
- Perform mode content (only the switch).
- Zero error on the caliper.
- Any other practical.

---

# Appendix — Simple Pendulum content (from the syllabus document)

Use this to fill `PracticalGuide_Azm` and to drive step instructions.

**Description.** A simple pendulum consists of a small bob suspended from a fixed point by a light,
inextensible thread. When displaced slightly and released, it swings back and forth under
gravity. This experiment measures how long one complete swing (the time period) takes, and how
that changes as the pendulum's length changes.

**Hypothesis.** The time period of a simple pendulum depends on its length. Specifically, T² is
directly proportional to L (T² ∝ L), and does not depend on the mass of the bob or the size of the
swing (for small angles). In other words, a longer pendulum swings more slowly, and that
relationship should show up as a straight line when T² is plotted against L.

**Apparatus.** Retort stand with clamp and split cork · Light, inextensible thread (~100–150 cm) ·
A spherical bob (metal) · Metre scale / measuring tape · Vernier calipers · Stopwatch.

**Quantities measured and recorded.**
| Symbol | Quantity | Measured with |
|---|---|---|
| ℓ | String length (pivot to top of bob) | Metre scale |
| d | Bob diameter | Vernier calipers |
| r | Bob radius = d/2 | Calculated |
| L | Effective length = ℓ + r | Calculated |
| n | Number of oscillations timed (20) | Fixed by experimenter |
| t | Time for 20 oscillations | Stopwatch (2 trials, then averaged) |
| T | Time period = t/n | Calculated |
| T² | Time period squared | Calculated |

**Formulas.**
- Time period from stopwatch data: T = t / n
- Effective length: L = ℓ + r
- Relationship being tested: T² ∝ L, i.e. T² = (4π²/g) L
- g from the graph's slope: g = 4π² / slope (where slope = T²/L)

**Procedure.**
1. Clamp the thread firmly at the pivot using the split cork; let the bob hang freely, clear of the
   stand and floor.
2. Measure the bob's diameter with vernier calipers → halve it to get radius r.
3. Set a string length ℓ; measure it with the metre scale from the pivot to the top of the bob.
4. Calculate effective length: L = ℓ + r.
5. Pull the bob to one side by a small angle (≤ 15°) and release from rest — no push.
6. Start the stopwatch as the bob passes through the mean (equilibrium) position.
7. Count 20 full oscillations, then stop the stopwatch as it passes the mean position again.
8. Record this time as t.
9. Repeat steps 5–8 once more at the same length; average the two times.
10. Calculate T = t / 20.
11. Change the length (ℓ) and repeat steps 3–10, for at least 5 different lengths.
12. Tabulate ℓ, r, L, t, T and T² for every length.
13. Plot T² (y-axis) against L (x-axis).
14. Check the plot: a straight line through the origin confirms T² ∝ L.
15. Calculate g from the slope of the line: g = 4π² / slope.

In the app, step 1 is already done (the rig is set up) and step 2 happens once per session.

**Observation table (syllabus).**
Obs. No. | ℓ (cm) | r (cm) | L = ℓ + r (cm) | n | t₁ (s) | t₂ (s) | Mean t (s) | T = t/n (s) | T² (s²) — with n = 20 for every row and 5 rows.

**Precautions (each with its reason).**
- Keep the displacement angle small (≤ 15°) — beyond this, the pendulum no longer behaves like
  simple harmonic motion.
- Always use effective length (L = ℓ + r), not just the string length — the bob's radius matters,
  especially for shorter pendulums.
- Time 20 oscillations, never one — this spreads out human reaction-time error instead of letting
  it dominate a single measurement.
- Start and stop the stopwatch at the mean position, not at the extremes — the bob moves fastest
  there, making the exact moment easier to judge.
- Release the bob from rest — don't push it, or the extra energy invalidates the simple pendulum
  assumption.
- Avoid draughts or air currents — they make the bob swing in an ellipse instead of a single flat
  plane, throwing off the timing.
- Use a light, inextensible thread — a stretchy or heavy thread shifts the effective length during
  the swing.
- Keep the same observer timing throughout — this keeps any reaction-time bias consistent across
  trials.
- Avoid parallax error when reading the metre scale — read it at eye level, directly at the
  measurement point.
