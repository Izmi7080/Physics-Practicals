# Project: Mobile VR Virtual Science Lab (FYP)

## What this project is
A mobile VR virtual laboratory app for Pakistani Matric/FSc (secondary/intermediate) science
students. Students independently perform curriculum-aligned lab practicals in VR — instead of
only watching a teacher demonstrate once to a class of 40 — as repeatable practice before real
labs and board practical exams.

- University FYP, team of 5, two-semester timeline, MVP due after semester 1.
- Three subjects: Physics, Chemistry, Biology. Exact practical list is NOT finalized — expect
  practicals to be added/dropped during development. Don't assume a fixed list of 15 exists yet.
- Target hardware: mobile VR (Google Cardboard-style viewer), trying to keep cost minimal.
  A cheap Bluetooth VR controller is being evaluated as an option. Meta Quest / higher-end VR
  is a fallback ONLY if mobile VR interaction proves genuinely infeasible — do not casually
  suggest "just use a Quest" as the fix for interaction problems; the brief is to exhaust
  mobile VR options first.
- Interaction is being prototyped as 3DoF gaze-based (look-to-select, tap/click to confirm).
  This is not finalized either — a controller may be added later.
- Engine: Unity 3D, C#.

## Current strategy: engine-first, not content-first
We are NOT building 15 practicals in parallel. The plan is:
1. Build ONE practical (simple pendulum — measuring 'g') completely, first WITHOUT VR
   (mouse/touch input standing in for headset input), to prove out a reusable system.
2. Once the underlying system works end-to-end for one practical, generalize it so future
   practicals are mostly configuration + new 3D content, not new logic from scratch.
3. VR input gets swapped in later. This is why architecture (below) matters more than usual
   for a "quick demo" — a demo built without this separation will need to be rebuilt, not
   extended, when VR interaction arrives.

## Mandatory architecture: separate these three layers

Every practical must be built as three decoupled layers. Do not let them bleed into each other.

1. **Interaction layer** — abstracts "how the student expresses input" behind an interface
   (e.g. something like `IPracticalInput` with select/drag/release/confirm style events).
   Today's implementation is mouse/touch. Later this gets a gaze+tap implementation and/or a
   Bluetooth controller implementation, WITHOUT changing layer 2 or 3.
2. **Practical logic layer** — the actual experiment: state machine, physics/math, correctness
   checks, feedback triggers. Must not reference input devices, camera, or UI directly — it
   should only know "a trial happened with these parameters" or "the student confirmed this
   step," fed to it by the interaction layer.
3. **UI / data / presentation layer** — data tables, graphs, feedback text, results display.

If you (Claude Code) find yourself writing input-handling code (mouse clicks, touch, gaze
raycasts) directly inside a physics or state-machine script, stop and restructure — that
coupling is exactly what we're trying to avoid.

### Reusable "trial loop" pattern
Most practicals will share a shape: set up parameters → perform an action/observation →
record a data point → repeat for several trials → analyze/aggregate results. Where sensible,
build this as a generic, data-driven controller (steps/config-driven) rather than hardcoding
each practical's step sequence in its own monolithic script. This generic controller is one of
the most important deliverables of the whole project — treat it with more care than any single
practical's content.

## Physics/simulation rules
- Do NOT rely on Unity's built-in Rigidbody/PhysX/hinge-joint physics for experiment-critical
  motion (e.g. pendulum swing). It can introduce unwanted damping/jitter that corrupts the
  measured result. Script deterministic motion directly (numerical integration of the governing
  equation, or an appropriate closed-form formula), so results are controllable and correct.
- Where a practical has a real-world tolerance or valid range (e.g. small-angle approximation
  under ~15° for the pendulum), enforce it and give the student explicit feedback when they
  exceed it — don't silently allow physically invalid setups. This is a core product
  requirement (corrective feedback), not an edge case to skip.
- Practical correctness (procedure, expected values, tolerances) should be treated as needing
  verification against a real syllabus/lab manual eventually — flag any assumption made about
  "the right way to do this experiment" clearly in code comments so it can be checked later.

## Code documentation requirements (follow for every script)
- **Every script file must start with a short summary comment block** at the top stating what
  the script does, what layer it belongs to (interaction / logic / UI), and what it talks to
  (e.g. "reads trial data from X, writes results to Y"). A few lines is enough — this is so a
  human coming back to the project later can understand the system without re-reading all the
  code.
- Use adequate inline comments and explanations throughout — not excessive noise-commenting of
  obvious lines, but enough that someone unfamiliar with a given script can follow the logic,
  especially for: state machine transitions, physics/math, and anything encoding a domain
  assumption (angle limits, formulas, tolerances).
- Prefer clear naming over cleverness. This is a student project that needs to be explainable
  in a viva/demo, not a production codebase optimized for brevity.

## General working instructions for Claude Code
- Do not silently make major architectural decisions (e.g. picking a specific VR SDK, adding a
  backend, choosing a data format) — flag them and explain the tradeoff briefly before
  proceeding, unless explicitly told to just decide.
- Do not add a backend (Firebase/SQLite/API) unless explicitly asked — current plan is local
  save data only.
- Do not assume the practical list, subject count, or interaction method beyond what's stated
  above — these are still in flux.
- When a task file (e.g. TASK_*.md) is provided for a specific week/milestone, that task file's
  scope is the current focus — don't expand scope into future practicals or VR integration
  unless the task file says to.
