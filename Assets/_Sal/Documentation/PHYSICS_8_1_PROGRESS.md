# Physics 8.1 progress

## Phase 1: walking scene complete — 8 November 2026

The first checkpoint in the [implementation plan](../PHYSICS_8_1_IMPLEMENTATION_PLAN.md) established the walking route. The same scene now opens the setup described below.

- Desktop: **WASD** or arrows to walk; hold the **right mouse button** and drag to look.
- Touch: use the **left joystick** to walk and swipe an empty area on the right to look.
- Follow the distance and direction cue from the centre table to the **original main sink**. When close enough, **Inspect tap** moves the same camera into a close-up and temporarily locks walking. **Back to room** restores the eye pose; walk back to the centre table to complete the preview.

The scene uses the shared lab room without editing its prefab or adding a second tap. The scene owns its `CharacterController`, camera and Input System UI. The movement adaptation is in `Assets/Scripts/Practicals/Physics81/`; `Assets/Editor/Physics81SceneBuilder.cs` creates the scene and won't overwrite an existing copy. The new scene is not in the Android build list yet.

Unity verified a clear 2.72 m corridor for the player capsule, one enabled camera and no chemistry session. The [Play Mode report](Validation/Physics81WalkSmoke.txt) verifies the round trip, walking lock in inspection, restoration of the camera and the final HUD state with scripted movement. On-phone touch remains untested. The chemistry scene, shared lab prefab, project settings, build settings and package manifest have unchanged hashes. No APK was built.

## Phase 2: water and apparatus setup — 8 November 2026

Open `Assets/Scenes/Physics_8_1_Specific_Heat.unity` and press **Play**. Follow the short action cue and use the local placement buttons:

1. Place the empty cup on the left pan. Tap individual weights in the weight box. **Record mass** becomes available after the pointer settles at zero.
2. Pick up the jug, walk to the original sink, and set it under the tap. Fill toward the marked **200 mL** line, turn off the tap, then pick it up and walk back. The filled jug stays on the centre slab.
3. Use **Hold Pour** until the water reaches the cylinder's **30 mL** mark. The flow slows near a mark; **Return water** lets you retry without losing water. Pour into the cup, weigh it again, then return it to the mixing stand.
4. Fit the lid and cup thermometer. Use the height slider to position the bulb about **10 mm** above the bottom; confirm only when it is immersed and clear. Fit the stirrer and weigh the cold brass bob.
5. Use the same cylinder to measure about **150 mL** for the bath. Put the filled beaker on the gauze, fit the second probe, and confirm its depth. The Phase 2 checkpoint ended here with the lamp off.

**Inspect** is available at the current station. Use **Back to room** to resume walking. Placements in this phase use local snap controls. The setup components are `LabSetupSession`, `SetupLedger`, `LabBeamBalance` and `LabSetupHud`. `Physics81SetupBuilder` installed the setup only in the Physics 8.1 scene. It keeps a pre-setup scene copy under `Validation` and refuses to install a second time. Reused models and scene-specific materials are under `Assets/PracticalAssets/Physics81`.

The [isolated Unity Play Mode walkthrough](Validation/Physics81SetupPlayMode.txt) passed. It tested the real walking controller and setup actions, including underfill, remeasuring, volume conservation, beam balance records, both probe placements, and framing of the tap outlet and fill line together. The original chemistry scene, shared lab prefab, package manifest and project/build settings have unchanged hashes. No APK was built; phone touch and visual usability remain to be checked.

### Bench layout

The centre slab has separate areas for weighing on the left, measuring behind the cup, mixing in the middle and heating on the right. Both loose thermometers and the stirrer start flat on the front rack. The smaller 200, 30 and 150 mL labels appear one at a time at the station where that measurement is made. The [layout walkthrough](Validation/Physics81BenchLayoutPlayMode.txt) checks these states and the complete setup sequence.

In the editor, **Practicals → Physics 8.1 → Tidy bench layout** reapplies the arrangement. When the scene already contains unsaved changes, the command applies the layout with Undo support and leaves the scene pending save.

## Phase 3: procedure, guidance and recording rehearsal

Open `Assets/Scenes/Physics_8_1_Specific_Heat.unity` in Unity and press **Play**. **Study the method** opens the aim, apparatus diagram, all 13 steps, controls and precautions. Choose **Guided** for automatic close-ups and readiness cues, or **Assessment** for manual inspection and independent recording.

- Start with the existing balance, sink and water-bath setup. The procedure now pauses for the initial cup mark before weighing the bob.
- Light the spirit lamp, wait for the boiling cue, lower the bob and mark the hot bath reading. Guided asks you to finish the rehearsal soak. The real notebook calls for ten minutes in boiling water.
- Lift the lid and instruments. Drag the thread control from right to left to lift, move and lower the bob. A partial drag stays where you left it. Turn off the lamp and replace the lid.
- Hold **Stir** with one finger. Use the other to inspect, zoom or tap **Record temperature**. Opening **Study**, **Tasks** or **Readings** pauses the experiment and releases held controls. Closing the page keeps the previous camera view and any manual pause.
- In the Phase 3 checkpoint, **Readings** put three recorded masses beside three timed rehearsal marks. Phase 4 replaces those marks with live scale captures.

`SetupLedger` owns the sequence and timestamps. `LabSetupSession` connects it to the existing fixtures and camera. `LabSetupHud` and `LabSetupPages` build the controls and pages under the scene’s existing Setup HUD canvas.

Editor walkthroughs cover both [Guided](Validation/Physics81ProcedureGuided.txt) and [Assessment](Validation/Physics81ProcedureAssessment.txt), with synthetic touch input for scrolling, transfer, and simultaneous stirring and inspection. On-device touch and performance testing remain pending.

## Phase 4: heat model and thermometer captures

The same scene now uses live temperatures. During the warming and soaking waits, the labelled clock can run at **32×**. Guided waits for ten simulated minutes with the bob immersed in boiling water and then returns to **1×** before transfer. Assessment allows early readings after placement. Transfer and mixing run at **1×** in both modes.

- Watch the half-degree scale and press **Record temperature** at the three measurement points. The Readings page keeps each displayed value and its lab time fixed for the run.
- Move the bob smoothly between the vessels. Leaving it in air cools it, and leaving the lid off cools the cup faster. A sharp downward tug while the bob reaches the cup can splash water and reduce its mass. Stirring reduces temperature differences inside the cup.
- Boiling changes the actual bath volume. The lamp stops supplying heat when the bath reaches its low-water reserve. If that happens during heating, Readings shows the observations collected so far.

`Physics81ThermalModel` is a plain C# model with 0.05-second simulation steps and an energy ledger. `Physics81InstrumentRig` derives immersion from the apparatus and updates the columns, bubbles and splashes. The nominal brass value is 380 J/(kg·K), water uses the notebook’s 4200 J/(kg·K), and the bath uses the 100 °C atmospheric approximation. The lamp’s effective power, heat-loss rates and probe lag are teaching-model parameters. They have not been fitted to measurements from a physical lab. The equations and sources are in the [implementation plan](../PHYSICS_8_1_IMPLEMENTATION_PLAN.md#6-heat-model-and-source-of-each-equation).

The [Guided](Validation/Physics81ThermalGuided.txt) and [Assessment](Validation/Physics81ThermalAssessment.txt) Unity walkthroughs verify live scale captures, the transfer and stir controls, and the mode-specific soak rules. The numerical checks cover the ideal **34.83 °C** mixture, energy accounting, frame-rate independence for the same inputs, and the effects of stirring, transfer delays, an open lid and spilling. Phone testing remains pending.

## Phase 5: workbook, assessment and local recovery

Open **Workbook** from the bench HUD or the **Readings** page. Copy the six saved readings into the blank entry fields. **Calculation** uses your entries to show the water mass, water temperature rise, bob temperature drop, and the result in J/(g·K) and J/(kg·K). The original calculator opens beside the current page. **Aim and method** opens Study and returns to the workbook when closed. The setup diagram and full method are available in Guided and after finishing Assessment.

- **Write-up** collects observations, a conclusion and an optional heat-loss reflection. Guided has prompts and a separate example. The app checks whether the required writing is present. It leaves the interpretation in the student’s words.
- After the run, **Feedback** separates transcription from technique. It compares the entries with the saved readings and explains early or late marks, short soaking, delays, limited stirring, and spills when observed. The technique checks use 5 seconds for transfer and 3 seconds for brief stirring. **Submit workbook** validates masses and temperature order before saving the result.
- **Resume saved trial** reopens the latest workbook after a finished run. During heating or transfer, it restores the prepared apparatus before ignition, refills the bath to the measured level, and retains the original masses, initial temperature, trial conditions and draft. An interruption earlier in preparation restarts the setup and keeps the draft.
- **Retry heating** keeps the prepared setup and conditions, and clears the hot and mixture entries and write-up for a fresh heating attempt. **Repeat trial** starts with fresh apparatus and an empty workbook. One latest trial is stored locally under `Application.persistentDataPath/physics.8.1.json`.
- **Assessment variation** is off by default. When enabled, a stored seed selects small changes in room temperature and the bob’s cooling in air. Guided uses the fixed baseline. Resume and heating retry reuse the same conditions.

The scene still uses `LabSetupSession` and `SetupLedger`. `LabPhysicsWorkbook` builds the workbook, `Physics81Trial` holds the entries and calculation rules, and `Physics81TrialStore` writes the local save with a recovery copy. The calculator comes from the existing shared `Practicals/Calculator/Calculator` resource.

The [Guided](Validation/Physics81WorkbookGuided.txt) and [Assessment](Validation/Physics81WorkbookAssessment.txt) Editor walkthroughs cover submission, calculator use, rejected entries, the separated feedback, and scene reloads during heating and after submission. The guided example of 5 g, 35 g, 50 g, 23 °C, 100 °C and 33 °C gives about **376 J/(kg·K)**. Phone testing remains pending.

## Placement and instrument refinements

- Drag the actual cup, jug, lid, probes, stirrer, beaker and brass bob to their seats. Guided tints the current item and shows its destination in mint. Incorrect drops glide back. The balance uses one button per mass. Each tap adds another copy and **Remove last weight** undoes one addition.
- Use the straight graduations on the jug, cylinder and beaker. Guided highlights the 200, 30 and 150 mL targets at the corresponding fill. The water mesh fits the measured interior of each model, and the stream follows the tap or the vessel spout. The cylinder gets a closer view during measurement.
- Fit the thermometer through the lid on the open holder. Adjust the exposed bulb against the millimetre scale, then move the lid and thermometer onto the cup together. The stirrer goes through the second hole.
- Record masses before removing the cup or bob by hand. Watch the bath warm at 16×, then drag the bob into the bath when it boils. Guided shows the warm-up, the submerged bob and the reading view in order. The instrument columns move on fixed printed scales, with a half-degree reading panel for recording.

The [Guided](Validation/Physics81PlacementGuided.txt) and [Assessment](Validation/Physics81PlacementAssessment.txt) Editor runs verified direct placement with simulated touch, repeated weights and undo, the outside-cup alignment, the water route, and completion through the workbook.

## Next: Phase 6

Test the scene on a phone and prepare the group handoff. The scene can be opened independently in Unity for the current workflow checks.
