# Practical Authoring Guide — Read This Before You Build Anything

This document is for every team member building a practical for the VR Virtual Lab project.
It is also written to be given to an AI assistant (Claude Code, Copilot, ChatGPT) that you use
while building — if you use one, give it this entire file as context before you ask it for code.

## Read this part first

**Your job is not to build a separate app and hand over a demo.** Your job is to build a
practical that plugs into the shared system and runs inside the same app as everyone else's,
using the same interaction, UI, data, and guidance systems. A practical that works beautifully on
its own but cannot be loaded by the shell is not finished work — it is a rewrite waiting to
happen, and the rewrite will land on whoever is integrating.

**You are expected to understand the system, not just follow the checklist.** You will be asked
to integrate your own practical into the main project, debug it there, and fix it when it breaks.
Read the core interfaces and the template practical properly. If you do not understand why a rule
below exists, ask — do not guess and do not work around it.

**Your practical must not be able to break anyone else's.** The system sandboxes each practical,
but that protection only works if you follow the isolation rules in section 5. A practical that
reaches outside its own boundary defeats the sandbox and can take the whole app down.

---

## 1. What a practical actually is

A practical is **not** a scene, and it is **not** a set of custom manager scripts. It is a
package of five things:

| # | Item | What it is | Code? |
|---|---|---|---|
| 1 | `PracticalDefinition` | ScriptableObject: name, description, thumbnail, prefab reference, step config, supported modes | No — inspector |
| 2 | `PracticalGuidanceContent` | ScriptableObject: aim, hypothesis, apparatus list, formulas, procedure steps, precautions | No — inspector |
| 3 | Apparatus prefab | Your 3D content + a root component implementing `IPracticalApparatus` | Minimal |
| 4 | Step sequence config | The trial loop, built from standard step types | No — inspector |
| 5 | Behaviour script | `IPracticalBehaviour` — the physics, validation, and derived values unique to your experiment | Yes, but narrow |

Only item 5 is real code, and it should stay small. If you find yourself writing a state machine,
input handling, a UI panel, a data table, or a graph — **stop**. Those already exist in the core
system and you are duplicating them. Ask how to use the existing one instead.

---

## 2. What you can start on right now

The core framework (the interfaces, the template practical, the validation tool) is still being
built. Until it lands, do not start writing gameplay scripts — you will write them against
guesses and have to throw them away. Do this instead, in this order:

**2.1 Nail down the real procedure.** Get the actual syllabus and lab manual for your practical.
Write out, in a document:
- Aim and hypothesis
- Full apparatus list
- Every quantity measured, its symbol, its unit, and what instrument measures it
- Which quantities are measured and which are calculated, with the formulas
- The numbered step-by-step procedure
- The precautions, each with a one-line reason why it matters
- The exact observation table, with every column, in the order it appears in the manual

This is the single most valuable thing you can produce right now, and it maps directly onto
`PracticalGuidanceContent` later. Getting a procedure wrong on paper costs an hour; finding out
after you have modelled and scripted it costs a week.

**2.2 Check it with a subject teacher.** Textbook procedures and lab-manual procedures sometimes
differ. Confirm the version your board expects before anyone builds it.

**2.3 Work out the trial loop shape.** Nearly every practical is some version of: set up
parameters → perform an action or take an observation → record a data point → repeat → analyse.
Write out what varies between repetitions in your practical, what stays fixed, what gets recorded
each time, and what the final analysis produces. If your practical does not fit that shape at
all, say so early — the framework needs to know.

**2.4 Gather and budget your 3D assets.** See section 6. Do not import a 200k-triangle model and
assume someone will fix it later.

**2.5 Identify the teaching moments.** For each step, what do students commonly get wrong? Those
misconceptions become the contextual hints and the demonstrations that make teaching mode worth
building. Write them down as you research the procedure — you will not remember them later.

---

## 3. Hard rules — these are not style preferences

Breaking any of these means your practical fails validation and cannot be integrated.

**3.1 No direct input handling.** No `Input.GetMouseButton`, no `Input.touches`, no
`Camera.main`, no raycasts of your own. All input comes through the injected input service. The
project is moving to VR later and the input method will change — code that reads input directly
will break, and it will break in everyone's practical at once.

**3.2 No `GameObject.Find`, `FindObjectOfType`, or singletons.** Everything you need is either
assigned in the inspector on your own prefab, or provided through the `PracticalContext` object
handed to you in `Initialize()`. Reaching out into the scene breaks isolation and makes your
practical impossible to load in the test harness.

**3.3 No direct UI references and no UI of your own.** You do not create the observation table,
the graph, the instruction line, or the feedback messages. You describe your data in
`IPracticalDataSchema` and send feedback through the feedback service; the core system renders
all of it. This is what keeps every practical looking consistent.

**3.4 No screen-space canvases, ever.** Screen Space Overlay does not work in VR. If your
practical needs a panel, it is a world-space canvas placed in your scene or prefab. No
exceptions — this is the most common thing that has to be thrown away and rebuilt.

**3.5 No Rigidbody or PhysX for anything the experiment measures.** Unity's physics introduces
damping and jitter that corrupt measured results. Script the motion deterministically from the
governing equation. Rigidbodies are fine for purely decorative objects that nothing is measured
from.

**3.6 Station-based movement only.** The student does not walk around freely. Your practical
defines stations (a position and a look direction) and the core navigator moves the student
between them. Do not add a character controller.

**3.7 Nothing in your practical runs when it is not loaded.** No `[RuntimeInitializeOnLoad]`, no
scene-persistent objects, no static state that survives teardown. `Teardown()` must leave nothing
behind: destroy your objects, unsubscribe your event handlers, clear your data.

**3.8 Your prefab is self-contained.** No references to objects in a scene you do not own, no
references to another practical's assets, no assumptions about what else is in the room.

---

## 4. Code documentation requirements

These apply to every script you write, without exception. The integrator has to read your code,
and so does the next person who touches it in six months.

- **Every script starts with a summary comment block** stating: what the script does, which layer
  it belongs to (interaction / logic / UI), and what it talks to. Three or four lines.
- **Comment the physics and the maths.** Write the equation you are implementing, state the
  assumptions it relies on, and note the units. `// L must be in metres here, the table displays
  cm` prevents a whole class of bug.
- **Comment every domain assumption.** Angle limits, tolerances, expected values, how many
  repetitions, what counts as "correct". Say where the number came from — "syllabus says ≤15°",
  not just `maxAngle = 15f`.
- **Comment pedagogical intent.** If a hint fires at a particular moment or a step is deliberately
  manual rather than automated, say why. This reasoning is invisible in the code and gets deleted
  by whoever cleans up next.
- **Clear names over clever ones.** This project gets defended in a viva. Code you cannot explain
  out loud is a liability.

---

## 5. Fault isolation — why your practical must not break anything else

The core system sandboxes each practical: it validates before loading, wraps every call into your
code in try/catch, and tears the session down cleanly if your practical throws. If your practical
crashes, the student gets an error message and is returned to the menu. The app survives, and so
does everyone else's work.

**That protection only holds if you stay inside your boundary.** Specifically:

- An exception inside your code is contained. An exception inside your code that has already
  modified shared state, registered a global handler, or left objects in the scene is not.
- If you reach out and modify anything outside your own prefab and context, you have broken the
  sandbox, and the failure will show up in someone else's practical where nobody will think to
  look for it.
- Clean up in `Teardown()`. A leaked event subscription from your practical fires during the next
  practical and produces a bug that looks like it belongs to that practical, not yours.

When your practical does fail, the system logs which practical and which step. Expect to be
handed that log and to fix it yourself.

---

## 6. Asset budget

The app runs on cheap Android phones in a VR viewer, which means **the scene renders twice per
frame**. Budgets are tight and they are not negotiable.

| Item | Budget |
|---|---|
| Your entire practical's added geometry | Keep well under 30k triangles |
| Hero / interactive prop (an instrument the student uses) | 1k–5k triangles, ideally 1 material |
| Background / decorative props | 200–1500 triangles each |
| Textures | 512–1024 max |
| Materials | As few as possible — share them |

Notes:
- **Draw calls and material count hurt more than triangle count.** Ten 2k-triangle objects with
  ten different materials is worse than one 20k-triangle object with one material.
- **Do not use high-detail models with fine engraved detail for anything the student has to
  read.** Scale graduations, dial markings, and small printed numbers are unreadable through
  cheap VR lenses. The 3D prop only has to look like the instrument; the actual reading happens
  on a dedicated world-space panel with crisp 2D graphics. This is how the vernier caliper is
  being done, and it is the pattern to follow for any instrument with a scale.
- **Check licences** on anything downloaded. Note the required attribution and hand it over with
  your practical. Non-commercial-only assets are not usable.
- Decimate oversized models in Blender before importing. Do not import first and hope.

---

## 7. Teaching mode vs Perform mode

Every practical supports two modes, and you build **one** thing, not two:

- **Teaching mode** = the practical plus a guidance layer (briefing, contextual hints, worked
  calculations, demonstrations, reflection prompts).
- **Perform mode** = the same practical with that guidance layer switched off by a config flag.

Do not write two versions of anything. If you find yourself writing `if (teachingMode)` inside
your core logic, that is a sign the guidance belongs in the guidance layer instead — ask.

Teaching mode is the priority. Perform mode is currently locked in the app and will be enabled
later by flipping flags, so long as you have built it as one practical with a switchable layer.

---

## 8. What "done" means for your practical

Before you hand it over, all of these must be true:

- [ ] The procedure matches the syllabus, and a subject teacher has confirmed it
- [ ] It runs correctly in the **test harness scene**, loaded in isolation
- [ ] It passes the **validation tool** with zero failures
- [ ] It loads, runs to completion, and tears down cleanly — then a second practical loads after
      it with no side effects
- [ ] It handles being quit halfway through without leaving anything behind
- [ ] All content is in the `PracticalGuidanceContent` asset, not hardcoded in scripts
- [ ] The data schema drives the table and the graph — you wrote no UI
- [ ] Asset budgets met, licences noted
- [ ] Every script has its summary comment and adequate inline comments
- [ ] You can explain, out loud, how your practical is loaded, how it receives input, how its data
      reaches the table, and what happens when it throws an exception

That last item is the real test. If you cannot explain it, you do not yet understand the system
well enough to integrate or debug your own work.

---

## 9. If you are using an AI assistant

Most of you will. Used properly it is a large speed-up; used carelessly it produces exactly the
code this document forbids, because the generic "how to build a Unity experiment" answer looks
nothing like our architecture.

**Give it this entire file, plus `CLAUDE.md` from the project root, as context before asking for
anything.** Not a summary — the whole file.

**Tell it explicitly what not to do.** AI assistants default to `GameObject.Find`,
`Camera.main`, `Input.GetMouseButtonDown`, Rigidbody-based physics, screen-space canvases, and
singleton managers, because that is what most Unity tutorials use. Every one of those is banned
here. Say so up front, every time.

**Start from the template practical, not from a blank file.** Point the AI at the template and
ask it to follow that structure. A generated-from-scratch practical will not conform.

**Never paste in code you cannot explain.** You will be asked to debug it during integration, and
"the AI wrote it" is not an answer. If you do not understand what it produced, ask it to explain
the code rather than asking it for more code.

**Check every generated script against sections 3 and 4 before committing it.** Run the
validation tool. AI-generated code fails these rules routinely, and it is faster to catch it
yourself than to have it bounced back during integration.

**Do not let it invent the procedure.** The experimental procedure comes from the syllabus and the
lab manual. An AI will happily produce a plausible-sounding pendulum or titration procedure that
does not match what your board actually examines.

---

## 10. Questions to ask before you start building

Answer these for your own practical and check them with the integrator before you write code:

1. What are my stations, and what is at each one?
2. What varies between trials, and how many trials does the student do?
3. Exactly which columns does my observation table need, and which are measured vs calculated?
4. What does the analysis step produce — a graph? a single computed value? a comparison against a
   known value with a tolerance?
5. Which instruments does the student use, and does any of them need a dedicated reading view?
6. Which standard step types cover my loop? Do I need anything the standard set does not have?
7. What are the three or four mistakes students most commonly make, and where should hints fire?
8. Is there a misconception that needs a demonstration animation rather than text?

---

**When in doubt: ask, do not improvise.** A ten-minute question now costs less than a week of
work that has to be rewritten at integration.
