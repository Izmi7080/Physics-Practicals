# Pendulum Practical — Session Handout

A working record of what was built, why, and what's left to check by hand. Covers the v1 vertical
slice, the v2 UX/learning pass, and two unrelated Input System conversions done at the end.

## 1. v1 — Working Pendulum Practical (per TASK_pendulum_demo.md)

Built the first complete practical end-to-end: drag length → confirm → drag angle (rejected with
feedback past 15°) → confirm → manual timer → trial logged → repeat ×5 → T² vs L graph with
computed g. Followed CLAUDE.md's three-layer architecture strictly.

**Architecture delivered:**
- **Interaction layer** (`Assets/Scripts/Interaction/`) — `IPracticalInput` interface,
  `MouseTouchInput` (mouse/touch today via the new Input System), `DraggableBobHandle` (forwards
  raw drag axes, doesn't interpret them).
- **Practical logic layer** (`Assets/Scripts/Practicals/Pendulum/`) — `PendulumExperimentController`
  (state machine), `PendulumPhysicsModel` (pure-math small-angle SHM, no Rigidbody/PhysX),
  `PendulumTrialData`, `PendulumVisualPresenter`.
- **Reusable trial-loop layer** (`Assets/Scripts/TrialLoop/`) — `ITrialData`, `TrialLoopController`
  — explicitly generic, meant to be reused unchanged by future practicals.
- **UI/presentation layer** (`Assets/Scripts/UI/`) — instructions, feedback, data table, graph
  (`LinearRegression` utility in `Assets/Scripts/Common/`).
- Greybox 3D rig (table, stand, pivot/thread/bob) built from primitives, then colored (wood/steel/
  brass) since the default grey/white look wasn't presentable.

**Bugs found and fixed during verification (not just assumed working):**
- `ResultsGraphUI` was attached to the very panel it toggled — its own `OnEnable` re-deactivated
  the panel the instant it tried to show it. Fixed by moving the watcher script onto the
  always-active `PendulumSystem` object.
- `UIGraphPlotter` cloned inactive template prefabs without re-activating the clones.
- `VerticalLayoutGroup`s on the data table had `childControlHeight = false`, so every row (built
  from a full-stretch "Panel") rendered on top of every other row instead of stacking.
- The Predict/Tally/Reflection UI scripts only reacted to *future* `StepChanged` events and never
  synced to the *current* step on enable, so panels could show incorrectly at scene start.

Verified via automated Play-mode runs (driving the public API + `Button.onClick.Invoke()`
directly) end to end: correct g≈9.8 m/s² recovery, angle-limit rejection/feedback, all five trials
logging correctly.

## 2. v2 — UX/Learning Layer (per TASK_pendulum_demo_v2.md)

Added on top of v1 without touching the core physics/trial-loop shape:

| # | Feature | Where |
|---|---|---|
| 1 | Live L/θ readout while dragging | `LiveReadoutUI` |
| 2 | Green/red angle-limit arc (3D, live) | `AngleLimitArcPresenter` |
| 3 | "Trial X of Y" + button relabels to "Finish & Analyze" | `TrialProgressUI` |
| 4 | Tap-based oscillation counting (replaces free Start/Stop) | `TapOscillation()` + `TapTallyUI` |
| 5 | Trial # column + discard/redo per row | `TrialDataRowUI`, `TrialDataTableUI`, `TrialLoopController.TrialEntry` |
| 6 | Live-updating g estimate (≥2 trials) | `LiveGEstimateUI` |
| 7 | Predict → observe → reveal | `PredictionPromptUI`, `PendulumStep.Predicting` |
| 8 | One-time "why the 15° limit" explanation | `FeedbackMessageUI` + `AngleLimitExceeded` event |
| 9 | Closing reflection screen (restated g/error, cause picker) | `ReflectionScreenUI`, `PendulumStep.Reflecting` |

**Logic-layer changes** (flagged before implementing, per the task's own instruction):
`PendulumExperimentController` gained `Predicting`/`Reflecting` steps and swapped manual
`StopSwing()` for tap-driven `TapOscillation()` (no damping in the SHM model, so nothing is lost
by requiring the full tap count). `TrialLoopController` was rewritten around a `TrialEntry`
(numbered, discardable) so `IsComplete` is now a live, non-monotonic check instead of a one-shot
event.

**Bugs found and fixed:**
- Same "watcher on its own toggled panel" mistake recurred for `ResultsGraphUI` during Reflecting
  — a stray `&& step != Reflecting` kept the old results panel (and its now-unreachable "Continue"
  button) visible underneath the reflection screen. Removed the exception.
- **The Predict Up/Down/Same buttons were never actually wired** — `PredictionPromptUI` and all
  its fields were correctly set up, but the buttons' `OnClick` was never connected to
  `PredictUp()`/`PredictDown()`/`PredictSame()`. Caught by the user testing manually; my own
  verification had called the controller methods directly instead of clicking through the
  buttons, so it never exercised the actual click path. Fixed and re-verified with
  `Button.onClick.Invoke()`.
- Both the prediction buttons and the reflection cause buttons *did* change state when clicked,
  but gave no immediate visible acknowledgment, which read as "nothing happens." Added instant
  feedback: the top instruction line now confirms the prediction the moment it's tapped, and the
  reflection screen's question line flips to a thank-you line once a cause is picked.

## 3. Input System conversions (unrelated scripts, not part of the pendulum practical)

Converted two pre-existing lab scripts from the legacy `UnityEngine.Input` API to the Input
System package, matching the polling style already used in `MouseTouchInput.cs`:

- **`LabFirstPersonMovement.cs`** — touch joystick + swipe-look, editor WASD + right-mouse-look.
  Note: mouse-look sensitivity default dropped from `2.0` to `0.1` because `Mouse.current.delta`
  reports raw per-frame pixel movement, not the Input Manager's pre-scaled axis value — worth
  re-tuning by feel once testable.
- **`LabInteractionRaycaster.cs`** — click/tap-and-drag pickup of the platinum wire tool.

Neither could be compiled or Play-tested before submission because the Unity MCP connection
dropped mid-session (see below) — **please check the Console and do a manual pass on both.**

## 4. Known follow-ups / things to check by hand

- **Unity MCP connection**: dropped mid-session; Unity's own "MCP for Unity" window shows the
  local server running and session active, so the fix needed was restarting this Claude Code
  session (project-scoped MCP servers are only dialed at session start, not hot-reloaded).
- **Drag feel & tap timing**: never testable from this side — needs a real manual pass with mouse
  and/or touch.
- **g estimate can look wild with poorly-varied trial lengths**: if consecutive trials use nearly
  identical L, the regression slope becomes numerically unstable (seen once during testing,
  g ≈ 32.76). Not a bug — just a reminder to vary length meaningfully between trials, now easier
  to verify since the data table is actually readable.
- **Minor cosmetic nit**: the best-fit line on the results graph slightly overruns the plot area's
  top-right corner by a few pixels. Harmless, not fixed (not worth the churn for a demo).
- Both converted Input System scripts need a Console check + manual test once Unity/MCP is back.

## 5. File manifest

**New (this session):**
```
Assets/Scripts/Interaction/IPracticalInput.cs
Assets/Scripts/Interaction/MouseTouchInput.cs
Assets/Scripts/Interaction/DraggableBobHandle.cs
Assets/Scripts/TrialLoop/ITrialData.cs
Assets/Scripts/TrialLoop/TrialLoopController.cs
Assets/Scripts/Common/LinearRegression.cs
Assets/Scripts/Practicals/Pendulum/PendulumPhysicsModel.cs
Assets/Scripts/Practicals/Pendulum/PendulumTrialData.cs
Assets/Scripts/Practicals/Pendulum/PendulumExperimentController.cs
Assets/Scripts/Practicals/Pendulum/PendulumVisualPresenter.cs
Assets/Scripts/UI/StepInstructionUI.cs
Assets/Scripts/UI/FeedbackMessageUI.cs
Assets/Scripts/UI/TrialDataRowUI.cs
Assets/Scripts/UI/TrialDataTableUI.cs
Assets/Scripts/UI/UIGraphPlotter.cs
Assets/Scripts/UI/ResultsGraphUI.cs
Assets/Scripts/UI/LiveReadoutUI.cs
Assets/Scripts/UI/AngleLimitArcPresenter.cs
Assets/Scripts/UI/TrialProgressUI.cs
Assets/Scripts/UI/LiveGEstimateUI.cs
Assets/Scripts/UI/PredictionPromptUI.cs
Assets/Scripts/UI/ReflectionScreenUI.cs
Assets/Scripts/UI/TapTallyUI.cs
```

**Modified (this session, pre-existing files):**
```
Assets/Scripts/LabFirstPersonMovement.cs   (legacy Input -> Input System)
Assets/Scripts/LabInteractionRaycaster.cs  (legacy Input -> Input System)
```

**Removed (v1, superseded prototype):**
```
Assets/Scripts/PendulumController.cs
Assets/Scripts/Stopwatch.cs
```

**Scene:** `Assets/Scenes/SampleScene.unity` — rebuilt pendulum rig + full UI (Canvas), all wired
and saved.
