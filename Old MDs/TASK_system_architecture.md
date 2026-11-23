# Task: Full System Architecture — Staged Implementation

## How to use this file
This file describes the FULL plan across six stages. **Do not implement all of it at once.**
Each stage ends with a STOP GATE — complete that stage, report what was built, and wait for
explicit approval before starting the next. The reason for the gating is that earlier stages
produce something demonstrable on their own; later stages are worthless if the earlier ones
are not solid. Do not build menus and polish before the practical itself is correct.

Read CLAUDE.md first. Everything in it still applies (three-layer separation, no PhysX for
experiment-critical motion, summary comment at the top of every script, adequate inline
comments, flag architectural decisions rather than making them silently).

## Current state of the project
- v1 pendulum demo works: drag length → confirm → drag angle → confirm → Start/Stop timing →
  data table row → repeat → graph → computed g.
- v2 improvements are specified in TASK_pendulum_demo_v2.md; some may be partially implemented.
  Check before rebuilding anything.
- A lab environment scene exists (room, wall cabinets, counter, sink, central bench).
- A character controller and some interactable scripts exist, written by the user.
- There is **no VR SDK in the project**. Input is mobile touch / mouse only for now.

---

## Global constraints — these apply to every stage

### Input
- Touch / mouse only. No gaze, no VR SDK, no controller, in any stage of this file.
- All input still routes through the abstracted interaction layer (`IPracticalInput` or
  equivalent) so a gaze or controller implementation can be added later without touching the
  logic or UI layers.
- Do not add an XR plugin, gaze raycaster, or stereo camera rig as part of this task.

### Movement — station-based only
- Station-to-station movement **only**, in both the desktop build and the eventual VR build.
  This is a deliberate decision by the user: one movement system for both targets, no free-roam.
- The student moves by selecting a station. A station is a named anchor with a position and a
  look direction. The transition is a short fade or a snap — never a smooth glide, because
  smooth locomotion under 3DoF head tracking is the most reliable way to make a student
  motion-sick on a phone-based headset.
- Put this behind an interface (e.g. `IStationNavigator`) even though there will be only one
  implementation today.
- The existing free-roam character controller is superseded by this. Do not delete it — disable
  it and leave it in the project until the user confirms removal.

### UI space — there is no screen-space HUD in this project
Screen Space Overlay canvases do not work in stereo VR rendering: they render at the eye plane,
cannot be focused on, and cause discomfort. **Every canvas in this project is World Space**,
including the ones that follow the camera. Two categories:

- **Camera-anchored** — a world-space canvas parented to a follow target roughly 1.5–2 m in front
  of the camera, with slight positional lag. Contains ONLY: the current step instruction (one
  line), a trial progress chip, and the interaction cursor/reticle. Nothing else.
- **World-anchored** — fixed in the lab; the student turns or travels to look at them. Contains
  everything else: instructions panel, observation table, graph, calculator, apparatus controls,
  settings, menus.

Build it this way from Stage 2 onward even though the current build is flat-screen. Building
screen-space now means rebuilding it later, not extending it.

### Asset budget (mobile VR target — the scene renders twice per frame)
- Total scene: ~50k–100k triangles per eye.
- Draw calls: aim for under 50–80.
- Hero / interactive prop (caliper, stopwatch, bob, rig): 1k–5k triangles, one material where
  possible.
- Background furniture: 200–1500 triangles each.
- Textures: 512–1024 max for props.
- Draw calls and overdraw hurt mobile more than raw triangle count. Prefer few materials and
  shared atlases over "low-poly but many materials".
- If an imported model exceeds these budgets, flag it rather than silently accepting it. Note
  any asset's license attribution requirement where one applies.

### Fault isolation
The system must be built so that **a broken practical cannot break the app or any other
practical**. Four other developers will be writing practicals independently and handing them to
the user to integrate. Full requirements are in Stage 5, but nothing written in earlier stages
may violate them: no static references to specific practicals, no core code that names the
pendulum, no shared mutable global state that survives between practical sessions.

---

# STAGE 0 — Foundation audit and movement system

## 0.1 Audit the existing code
Read every script currently in the project, including the character controller and the
interactable scripts the user added. Do not assume file names — find them. Produce a written
report covering, for each script:
- Which layer it belongs to (interaction / logic / UI), or whether it mixes layers.
- Whether input handling is coupled into logic (`Input.*`, `Camera.main`, raycasts inside
  physics or state-machine code).
- Whether it uses `GameObject.Find`, `FindObjectOfType`, or singletons that would break fault
  isolation later.
- Verdict: **KEEP AS IS** / **REFACTOR** (describe how) / **REPLACE** (explain why).

Do not refactor or delete anything during the audit. Report first and wait for direction on
anything marked REPLACE.

## 0.2 Station navigation system
- A `Station` component: position anchor, look direction, display name, and optional gating
  (e.g. "unavailable until the length is confirmed").
- An `IStationNavigator` interface plus one implementation that moves the camera rig between
  stations with a short fade.
- Station selection goes through the existing interaction abstraction — touching a station
  marker in the world, or a station button on a world-space panel. Not a hardcoded input check.

## 0.3 Pendulum station layout
Define three stations in the physics lab scene. (The user may collapse these to two if the
movement feels excessive; treat three as the default.)
1. **Apparatus bench** — the pendulum rig, setup controls, angle overlay, oscillation tally.
2. **Instruments** — vernier calipers, metre scale, stopwatch. Use the counter / back area of
   the existing room.
3. **Data & analysis wall** — observation table, graph, calculator, live g and % error,
   instructions panel.

## STOP GATE 0
Report the audit findings and the station system. Wait for approval.

---

# STAGE 1 — Make the practical correct and complete

Do not build menus, app shell, or the guidance system in this stage. The single goal is one
syllabus-accurate, fully working pendulum practical.

## 1.1 Adopt the syllabus procedure
The user supplied a Matric syllabus document for this practical. The current build diverges from
it in three ways that all matter, and all three must be implemented.

**Effective length.** The quantity that drives the physics is `L = ℓ + r`, where `ℓ` is the
string length measured from the pivot to the **top** of the bob, and `r` is the bob's radius.
The student sets `ℓ` and measures the bob diameter `d` with vernier calipers; the app computes
`r = d/2` and `L = ℓ + r`. The simulated motion must use `L`, not `ℓ`.

**Two timing runs per length, averaged.** For each length the student times 20 oscillations
twice (`t₁`, `t₂`); the app averages them to mean `t`, and `T = t / 20`. Averaging repeated runs
is a core lab skill and it is what the observation table expects.

**Start and stop at the mean position.** The student starts and stops timing as the bob passes
through the equilibrium (mean) position, not at an extreme — the bob moves fastest there, so the
moment is easier to judge. Detect when the tap happens relative to the bob's phase, and in
teaching mode (Stage 3) give feedback when the student consistently taps at the extremes.

Also from the syllabus: the bob is released **from rest**, not pushed. Treat a "pushed" release
as an error state the app catches, since the extra energy invalidates the simple-pendulum
assumption.

## 1.2 Observation table — match the syllabus columns
`Obs. No. | ℓ (cm) | r (cm) | L = ℓ+r (cm) | n | t₁ (s) | t₂ (s) | mean t (s) | T = t/n (s) | T² (s²)`
- `n` is fixed at 20.
- Derived columns (`r`, `L`, mean `t`, `T`, `T²`) are computed by the system, not entered.
- Target is at least 5 different lengths.
- Rows animate in rather than popping.
- Per-row discard / redo control. A discarded row does not count toward the trial target and
  does not feed the graph or the g calculation.

**Units:** the table displays centimetres. Verify the logic layer converts to metres before
computing `g` from the slope. Getting this wrong produces a g that is wrong by a factor of 100
and is an easy mistake to miss.

## 1.3 Vernier caliper interaction
**Do not use a high-detail caliper model with geometry-engraved graduations.** Fine scale
markings are unreadable at mobile-VR resolution through cheap lenses, which defeats the entire
point of the exercise. Implement it in two parts:

- **3D prop** — low-poly, 1–3k triangles, one material. It only needs to read as "a caliper".
  The student selects it, then selects the bob; the jaws animate closed around the bob.
- **Reading view** — this is where the actual skill happens. A large world-space panel showing
  the main scale and the vernier scale as crisp 2D vector/sprite UI, zoomed and high contrast.
  Not a camera pointed at the 3D model. The student reads the main-scale value and identifies
  the aligning vernier division, then confirms the reading.
- The app knows the true bob diameter. Validate the student's reading against it with a
  tolerance; in teaching mode (Stage 3), explain misreadings.

The same principle applies to the metre scale: the syllabus lists parallax error as a precaution,
so the scale reading should be legible and, in teaching mode, read at the correct eye level.

## 1.4 Physics
- Keep deterministic scripted motion — no Rigidbody, no PhysX, no hinge joints.
- Driven by a fixed true `g = 9.8 m/s²`. The student's job is to recover that value
  experimentally; the simulation must not shortcut to their intended answer.
- Motion uses effective length `L`.
- Small-angle SHM is acceptable given the ≤15° enforcement. Note the choice and the reasoning
  in the script's summary comment, as CLAUDE.md requires.

## 1.5 v2 features
If not already implemented, implement them now, adapted to the station layout: live `ℓ` and `θ`
readouts while adjusting; protractor overlay with green/red zones; trial progress indicator;
tap-based oscillation counting with a visible tally; discard/redo; live-updating g and % error
once at least 2 trials exist; predict-observe-explain prompt; reflection screen after results.

## STOP GATE 1
A student can complete a syllabus-accurate pendulum practical end to end. Report and wait.

---

# STAGE 2 — World-space UI migration

## 2.1 Move everything out of screen space
Every existing canvas becomes World Space, per the rules in Global constraints.
- Panels sit 1.5–2.5 m from the station anchor, angled toward the student, with the main content
  within roughly ±30° of comfortable gaze.
- Text sized for legibility on a low-resolution phone display viewed through lenses — err large.
  Test at the target device resolution, not on the desktop editor window.

## 2.2 Camera-anchored layer
Only the current step instruction (one line), the trial progress chip, and the cursor/reticle.
A world-space canvas on a follow target with slight lag, never Screen Space Overlay.

## 2.3 Station panel assignments
- **Apparatus bench** — setup panel (live `ℓ` and `θ` readouts, confirm buttons), protractor
  overlay on the rig, oscillation tally positioned so it is visible while watching the swing.
- **Instruments** — caliper, metre scale, a diegetic stopwatch that shows running time, and a
  per-instrument help button.
- **Data wall** — observation table, graph (axes, gridlines, plotted points, best-fit line,
  slope readout), live g and % error displayed large and prominent, calculator, instructions
  panel.

## 2.4 Calculator — two separate things
- **Worked-calculation panel** — shows the formula with the student's own values substituted
  (`T = t/n = 31.4 / 20 = 1.57 s`), revealed step by step on tap. This is the part that teaches.
- **Simple arithmetic pad** — basic four-function calculator for the student's own checking.
  Do not build a full scientific calculator: large UI surface, little pedagogical return, and
  painful to operate by gaze once VR arrives.

## 2.5 Interaction feedback
Every interactable gets a hover/selection highlight, and every selection gets an audible and
visual confirmation. Silent selection reads as broken, especially in VR.

## STOP GATE 2
The same practical, now structurally VR-correct. Report and wait.

---

# STAGE 3 — Guidance layer (teaching mode)

Teaching mode is **the base practical plus a guidance layer** — never a separate flow. Perform
mode is the same scene with the guidance systems disabled by a config flag. Build it this way so
that adding Perform mode later is a flag flip, not a second implementation.

## 3.1 Guidance content as data
Create a `PracticalGuidanceContent` ScriptableObject holding: aim/description, hypothesis,
apparatus list (name + blurb + reference to the 3D object), quantities-measured table, formulas,
numbered procedure steps, precautions (each with a one-line reason), and the observation table
schema.

**Do not hardcode pendulum text into panel scripts.** The panel renders whatever content asset it
is handed. This is what makes the other 14 practicals cheap to add.

## 3.2 Reference panel (always available, on the data wall)
Tabbed: `Aim & Hypothesis | Apparatus | Procedure | Formulas | Precautions | Observation Table`.
- The Procedure tab highlights the current step and dims completed and future ones, so the
  reference panel doubles as a progress tracker.
- The Apparatus tab highlights the corresponding 3D object in the scene when an item is selected.
- Each precaution expands to show why it matters.

## 3.3 Pre-practical briefing
On first entry: aim → hypothesis → apparatus walkthrough (with 3D highlighting) → what will be
measured. Skippable after the first viewing, replayable from the reference panel.

## 3.4 Just-in-time contextual hints
Fired by what the student actually does. Each fires once per session by default, with a setting
to re-enable them.

| Trigger | Hint |
|---|---|
| First length setting | Measure from the pivot to the **top** of the bob — the radius is added separately |
| First caliper use | How to read a vernier scale |
| Exceeds 15° | Beyond this the pendulum no longer behaves as simple harmonic motion |
| Taps start/stop at an extreme | Start at the mean position — the bob moves fastest there, so the moment is easier to judge |
| Releases with a push rather than from rest | The extra energy invalidates the simple-pendulum assumption |
| Attempts to time 1 oscillation | Timing 20 spreads reaction-time error instead of letting it dominate one measurement |
| Tries to move on after a single run at a length | Run it twice and average — single runs hide mistiming |
| Before plotting | A straight line through the origin confirms T² ∝ L |

## 3.5 Oscillation demonstration animation — high priority
Before the student times anything for the first time, play a short animated demonstration of ONE
full oscillation: the bob swinging through a complete cycle (mean → one extreme → mean → other
extreme → back to mean), with the path traced, a counter ticking 0 → 1, and a marker flashing at
the mean position at each moment where the stopwatch should start or stop.

Beginners reliably misjudge both what counts as one oscillation and where timing starts. Written
instructions do not fix this misconception; the animation does. Make it replayable from the
reference panel, and follow it with ONE guided trial — the full loop with step-by-step prompting
and no scoring — before the student's real trials begin.

## 3.6 Mode configuration
A `PracticalModeConfig` (Teaching / Perform) gates: briefing, contextual hints, worked
calculations, prediction prompt, reflection screen, and the guided first trial. Perform mode
keeps the reference panel but collapsed, and disables the rest.

## STOP GATE 3
Teaching mode complete for the pendulum. Report and wait.

---

# STAGE 4 — Application shell

Only now, with a finished practical to wrap.

## 4.1 Scenes
- `Boot` — init, settings load, persistent managers, auto-advance to the main menu.
- `MainMenu` — title, Start/Continue, Settings, Credits, Quit.
- `PracticalSelect` — subject → practical → mode.
- `Lab_Physics` — the existing lab, acting as a **shell** that loads a practical's apparatus
  prefab and config.
- `Lab_Chemistry`, `Lab_Biology` — later, same pattern.

**One lab scene per subject, not one scene per practical.** Otherwise the project ends up with 15
near-duplicate scenes to maintain, and any change to the room has to be made 15 times.

Menus are world-space panels in a simple 3D space, consistent with the no-screen-space rule.

## 4.2 App flow state machine
`AppFlowManager` with states `Boot, MainMenu, SubjectSelect, PracticalSelect, ModeSelect,
Loading, InPractical, Results`. This is separate from the practical's own step machine —
practical logic must not know menus exist.

```
Boot → MainMenu
        ├─ Start/Continue → SubjectSelect (Physics / Chemistry [locked] / Biology [locked])
        │     └─ PracticalSelect (cards generated from config)
        │          └─ ModeSelect (Learn / Perform [locked])
        │               └─ Loading → Lab scene → practical session
        │                    └─ Results → Reflection → [Retry / PracticalSelect / MainMenu]
        ├─ Settings
        └─ Quit
```

Locked items (Chemistry, Biology, Perform mode) are visible but disabled with a "coming soon"
state. Do not hide them — the user wants them shown as locked so the links can be enabled later.

## 4.3 Config-driven menus
`SubjectDefinition` and `PracticalDefinition` ScriptableObjects drive the menus. Adding a
practical must never require editing menu code. The full asset contract is in Stage 5.

## 4.4 Settings
World-space, reachable from the main menu and the in-practical pause menu:
- Comfort: transition style, fade strength.
- Interaction: cursor size. (Gaze dwell duration later, when gaze exists.)
- Guidance: hints on/off, replay tutorials, reset first-time flags.
- Audio: master / SFX.
- Display: **text size** — important, since small text is unreadable through cheap lenses.
- Language: English only. But route **all** user-facing strings through a text-lookup layer
  rather than hardcoding them in scripts and prefabs, so Urdu can be added later without
  touching every file. Urdu itself is backlog — do not implement it.

## 4.5 Pause menu and quality-of-life
- Pause: resume, restart trial, restart practical, settings, quit to menu.
- **Recenter / reset view** — required for 3DoF later, where drift is guaranteed. Add the
  plumbing now even though there is no headset yet.
- Restart the current trial without losing the session.
- Session auto-save, local only — no backend, no Firebase, no SQLite — so an interruption does
  not wipe four completed trials.
- Skip buttons on all animations and briefings after the first viewing.

## STOP GATE 4
Complete app flow with one practical inside it. Report and wait.

---

# STAGE 5 — Generalisation, fault isolation, and the team integration framework

This is the most important stage for the project's success. Four other developers will each build
practicals independently and hand them to the user for integration. Their work must plug in
without modifying core code, and **a broken practical must not break anything else**.

## 5.1 The practical package
A practical is delivered as a self-contained package:
1. `PracticalDefinition` (SO) — metadata, prefab reference, scene reference, step config,
   supported modes.
2. `PracticalGuidanceContent` (SO) — all instructional content.
3. Apparatus prefab — 3D content plus a root component implementing `IPracticalApparatus`.
4. Step sequence config — data-driven, using the standard step types below.
5. A small practical-specific behaviour script implementing `IPracticalBehaviour` — physics,
   validation, and derived quantities unique to that experiment. Nothing else.

Items 1, 2 and 4 are inspector work. Only item 5 is code, and it is narrow by design.

## 5.2 Interfaces to define and document
- **`IPracticalApparatus`** — `Initialize(PracticalContext ctx)`, `GetStations()`,
  `ResetForNewTrial()`, `GetInteractables()`, `Teardown()`. Receives all services through the
  context object; never locates them itself.
- **`IPracticalStep`** — `OnEnter()`, `OnExit()`, `Tick()`, `IsComplete`, `CanAdvance`,
  `GetInstructionText()`.
- **`IPracticalBehaviour`** — per-practical physics and validation hooks.
- **`IPracticalDataSchema`** — observation table columns, derived/computed columns, what is
  plotted on each axis, what is computed from the fit, the expected value and its tolerance. The
  table, graph and error display all work off this without the author touching any UI code.
- **`PracticalContext`** — the injected service bundle: input, station navigator, data recorder,
  feedback/hint service, guidance service, audio. This is the **only** way a practical reaches
  the rest of the system.

## 5.3 Standard step types
Provide these so authors write as little code as possible: `SetParameterStep`,
`MeasureWithInstrumentStep`, `TimingStep`, `ConfirmStep`, `ObserveStep`, `RecordDataStep`,
`RepeatUntilStep`, `AnalyseStep`.

Most practicals should be expressible as a sequence of these with zero custom step classes. If a
practical needs a custom step, that is a signal the standard set is missing something — note it.

## 5.4 Fault isolation — hard requirements
Implement all of the following:

- **No static or hardcoded references to any specific practical anywhere in core code.**
  Practicals are discovered from a registry of `PracticalDefinition` assets and loaded by
  reference.
- **Validate before load.** Before a practical loads, run a validation check: required assets
  present, interfaces implemented, prefab resolvable, schema valid. On failure, the practical is
  marked unavailable in the menu with a diagnostic message, and the app continues normally.
- **Sandbox the lifecycle.** Every call into practical-authored code — `Initialize`, step
  `OnEnter`/`Tick`/`OnExit`, `IPracticalBehaviour` hooks, `Teardown` — is wrapped in try/catch.
  An exception is logged with the practical's name and the step it occurred in, the session ends
  gracefully with an error message, and the student is returned to the practical select menu. It
  must never leave the app in a broken state or take down the shell.
- **No shared mutable global state between practicals.** Each session gets a fresh context. Any
  service holding per-session state is reset on teardown.
- **Complete teardown.** Unloading a practical destroys its objects, unsubscribes its event
  handlers, and clears its data. A previously-run broken practical must not affect the next one.
- **Additive scene loading** for practical content where it aids isolation, with unload on exit.
- **Per-practical failure is visible, not silent.** Log clearly which practical failed and at
  which step, so the user can hand the diagnostic straight back to its author.

## 5.5 Author tooling — build all three
1. **Template practical** — a working, trivial dummy practical ("set a value, measure it, log it,
   repeat 3 times, plot it") that authors copy as their starting point. This is more effective
   than documentation alone, because they begin from something that already conforms.
2. **Validation editor tool** — a menu item that checks a practical package against the contract:
   interfaces implemented; no forbidden API usage (`Input.*`, `Camera.main`, `FindObjectOfType`,
   `GameObject.Find`, `Resources.Load` of core assets, Rigidbody on experiment-critical motion);
   prefab self-contained with no cross-scene references; triangle and material counts within
   budget; all required assets assigned; guidance content non-empty. Output a pass/fail report
   naming each failure and the offending object or file.
3. **Test harness scene** — a bare scene that loads ONE practical in isolation with a minimal
   shell, so an author can test without the full app, and the user can verify a submission
   without merging it into the main project first.

## 5.6 Prove the framework
Build a second practical entirely as config + prefab + minimal behaviour script, writing no core
code. If that is not possible, the framework is not finished — report exactly what had to change
in core code and why.

Prefer a practical whose loop shape differs from the pendulum's — something that is not "vary a
length, time something" — so the framework is genuinely tested rather than shaped around a single
case. Ask the user which practical to use for this.

## STOP GATE 5
Report the framework, the tooling, and the second practical. Wait.

---

## Documentation requirements — all stages
Per CLAUDE.md: every new or modified script starts with a summary comment stating what it does,
which layer it belongs to, and what it talks to. Inline comments throughout, especially for state
transitions, physics and maths, and domain assumptions (angle limits, tolerances, formulas).

Additionally for this task:
- Every interface in the practical contract (Stage 5) gets thorough documentation comments, since
  four external developers will implement against them.
- Anything encoding pedagogical intent — hint triggers, the 15° threshold, the oscillation
  demonstration, prediction and reflection logic — gets a comment explaining **why**, not just
  what.
- Flag every assumption made about correct experimental procedure so it can be checked against
  the syllabus later.

## Out of scope for this entire file
- No VR SDK, XR plugin, stereo rendering, or gaze interaction.
- No backend, no cloud sync, no Firebase, no SQLite. Local save only.
- No Urdu localisation (route strings through a lookup layer, but do not translate).
- No voice-over. Structure guidance content so an audio clip reference could be added per item
  later, but do not record or wire up audio narration.
- No grading or scoring of prediction and reflection answers.
- No practicals beyond the pendulum and the one framework-validation practical in Stage 5.
