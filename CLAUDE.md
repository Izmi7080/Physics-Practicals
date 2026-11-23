# Project: Virtual Science Lab (FYP) — Android

## What this project is
An Android app where Pakistani Matric/FSc students perform science practicals themselves in a 3D
virtual lab. The problem it solves: in real schools a teacher demonstrates a practical once to a
class of 40, so most students never do the steps themselves before their board practical exam.

- University FYP, team of 5, two semesters.
- Subjects: Physics, Chemistry, Biology. The practical list is not final.
- **VR is on hold.** The target is a normal Android phone app: landscape, touch input. Do not add
  a VR SDK, XR plugin, gaze input, or stereo camera.
- Each team member builds their own practicals separately. They are integrated into one app
  later. This project is owned by one member (suffix `_Azm`, see Naming).
- Engine: Unity 6 LTS (6000.3.x), C#. Check the project for the render pipeline and the input
  system in use (old Input Manager vs new Input System) — do not assume either.

## Naming and folders (mandatory)
Other members' code will be merged into the same project later, so everything written here must
be impossible to confuse or collide with theirs.

- **Namespace:** all scripts live under `VirtualLab.Azm`, with sub-namespaces as needed
  (`VirtualLab.Azm.Core`, `VirtualLab.Azm.Menu`, `VirtualLab.Azm.UI`,
  `VirtualLab.Azm.Pendulum`).
- **Suffix `_Azm`** on: every class name (MonoBehaviours, ScriptableObject types, plain classes),
  every scene, every prefab, and every ScriptableObject asset.
  Examples: `AppManager_Azm`, `PendulumController_Azm`, `MainMenu_Azm.unity`,
  `LabEnvironment_Azm.prefab`, `PendulumConfig_Azm.asset`.
  Enums and interfaces also get the suffix (`SubjectType_Azm`, `IPracticalInput_Azm`).
- **Folders:** everything new goes under `Assets/_Azm/`:
  ```
  Assets/_Azm/
    Scenes/
    Scripts/Core/        app flow, scene loading, settings, input abstraction
    Scripts/Menu/        splash, main menu, practical select
    Scripts/UI/          shared UI components (drawer, tabs, toasts, table, graph)
    Scripts/Practicals/Pendulum/
    Prefabs/
    Data/                ScriptableObject assets (catalog, configs, guide content, theme)
    Art/                 models, materials, textures, sprites, fonts
  ```
- Reference documents (syllabus PDF, UI mockups) live in `Docs/` at the project root, outside
  `Assets/`.

## Singletons (allowed, with rules)
Singletons are allowed. They are the main source of integration bugs, so follow these rules:

- **App-level singletons** (`DontDestroyOnLoad`) are limited to: `AppManager_Azm` (app flow,
  scene loading, current session info) and `SettingsManager_Azm` (saved settings, audio levels).
  Adding another one requires asking first.
- **Everything else is scene-local.** A scene-local singleton must NOT use `DontDestroyOnLoad`,
  and must set `Instance = null` in `OnDestroy`.
- If a duplicate instance is found, log a warning naming both objects before destroying the
  duplicate. Never destroy silently.
- Every subscription to a static event or C# event is removed in `OnDestroy` / `OnDisable`.
- No static fields holding practical state (trial data, counters, flags). Static state survives
  scene loads on a device and leaks into the next session.

## Architecture: keep three layers separate
1. **Interaction layer** — turns touch/mouse into meaning ("bob dragged to 12°", "tap counter
   pressed"). Behind an interface (`IPracticalInput_Azm` or similar), so the editor mouse, phone
   touch, and a future VR input can be swapped without touching logic.
2. **Logic layer** — the experiment: step flow, physics, validation, calculations. Must not read
   input devices, reference `Camera.main`, or touch UI components directly.
3. **UI / presentation layer** — panels, table, graph, text, feedback. Displays what the logic
   layer reports; sends user intent back through events or method calls, not by editing logic
   state directly.

If input-reading code appears inside a physics or step script, stop and restructure.

## UI rules (Android)
- The working UI is **Screen Space** (Overlay or Camera). Only things that belong to the apparatus
  are in the 3D world: the protractor arc on the pendulum, the live readout next to the bob, the
  stopwatch face.
- Canvas Scaler: Scale With Screen Size, reference resolution **1920×1080**, landscape. Test at
  16:9 and at 20:9 (e.g. 2400×1080) — most current phones are 20:9.
- Respect `Screen.safeArea` (notches, punch-hole cameras, rounded corners): all interactive UI sits
  inside a safe-area container.
- Touch targets at least ~48 dp (about 120 px at the 1920×1080 reference).
- TextMeshPro for all text.
- Visual style comes from one `UITheme_Azm` ScriptableObject (colours, font, corner radius), not
  hardcoded per panel. Style: dark navy translucent panels, white text, teal for primary actions,
  amber for hints, green/red for valid/invalid, rounded corners, soft shadows.
- Android back button (arrives as Escape): navigates back in menus, and opens the pause menu
  inside a practical. It must never quit the app without confirmation.
- Orientation: landscape only (left and right), no portrait.

## Physics and correctness
- Do not use Rigidbody/PhysX for any motion that the student measures. Script it
  deterministically from the governing equation, with the true constants in a config asset.
- The student recovers the real value experimentally (e.g. g). The simulation never shortcuts to
  the answer.
- Where the real practical has a valid range (e.g. angle ≤ 15°), enforce it and tell the student
  why. Do not silently clamp.
- Procedures follow the Matric syllabus document in `Docs/`. Any assumption about the "correct"
  procedure that is not in that document must be flagged in a code comment so a teacher can check
  it later.
- Tunable numbers (limits, counts, tolerances, true constants) live in ScriptableObject configs,
  not as literals in code.

## Performance (Android, mid/low-range phones)
- Target 60 fps, never below 30 on a mid-range phone.
- Interactive props: aim for ≤ 5k triangles; background props ≤ 1.5k. Few materials, shared where
  possible. Textures 1024 max for props.
- Flag any imported model that is far above this instead of accepting it silently.

## Code documentation (every script)
- **Every script starts with a summary comment block** stating: what the script does, which layer
  it belongs to (interaction / logic / UI / core), and what it talks to.
- Adequate inline comments, especially for: step/state transitions, physics and maths (write the
  equation, the assumptions and the units), and domain numbers (say where each came from — "syllabus:
  ≤ 15°", not just `15f`).
- Anything encoding teaching intent (why a hint fires here, why a step is manual) gets a comment
  explaining **why**.
- Clear names over clever ones. This code is explained in a viva.

## Working rules for Claude Code
- **Never delete** files, scenes, prefabs, assets or GameObjects without asking first and getting
  approval. Disabling or moving to an `_Archive` folder is allowed only when the task says so.
- **Do not modify the old pendulum scene or its scripts.** New work goes in new `_Azm` scenes and
  scripts. Existing logic may be copied into the new namespace and adapted.
- Task files (`TASK_*.md`) contain stages with **STOP GATES**. Finish one stage, report what was
  built and how to test it, and wait for approval before starting the next.
- Flag architectural decisions (new packages, new singletons, save formats, changes to project
  settings) before making them.
- No backend, no Firebase, no SQLite, no network code. Local `PlayerPrefs` / local files only.
- No localisation system for now (Urdu is backlog). Keep user-facing text in data assets or one
  place per feature, not scattered across scripts.
- Do not add Asset Store or package-manager packages without asking.
