# Beginner walkthrough: Physical States practicals

## Open and start

1. Open **NaphthalenePractical** in Unity Hub with **Unity 6000.3.24f1**.
2. Open **Assets/Scenes/Melting_Point_Naphthalene.unity**. This one scene supports all five chemicals; its historical filename is retained.
3. Stop Play if needed, allow compilation and press Play.
4. Choose the experiment across the introduction: **5.1 Naphthalene**, **5.2 Biphenyl**, **5.3 Acetone**, **5.4 Benzene**, or **5.5 Ethyl alcohol**. Changing the selection reloads the shared scene with that experiment's definition.
5. Choose **Begin guided** or **Begin assessment**. Guided opens Study first: review Concept, Procedure, Controls, Precautions and Observations, then tap **Start at the bench**. Study is also available from the introduction. Resume restores compatible completed progress.

Original team projects have not been edited. The app uses simple English in Android landscape.

## Controls

- **Apparatus on the table:** the next usable real apparatus appears on the bench. Drag it to its matching outline. There is no inventory tray. In assessment, available apparatus is offered according to its prerequisites. Wrong drops return home.
- **Large objects and pestle:** drag the object directly. They do not have Move handles. Thin apparatus keeps a small glassy Move handle that follows the model. Compound apparatus can be moved without its attached capillary intercepting the drag.
- **Inspection:** tap the real apparatus or **Zoom [apparatus]** on the teaching board. The camera also moves in after placement when there is a detail to check. Use **+ / −** for distance and the round **View** joystick to adjust the angle. **Bench** restores the wide view for the current action. For filling, this view includes both the bath and the main sink.
- **Top icons from left to right** are Study, Tasks, Readings, Workbook, All practicals and Pause. Chemistry and Physics 8.1 use the same icon row. The four-tile All practicals icon saves your progress and opens the launcher. Look for the labelled button on document pages. Tap the Chemistry step badge to collapse the teaching board.
- **Chemistry sound** starts with a short piece of music on the introduction. The tap, flame and tools have their own sounds. Opening a page or pausing quiets the lab, and recording a reading plays a short cue.
- **Progression:** valid completed actions advance automatically. There is no Continue at each step. Recording trial 1 and choosing to begin the second trial is an explicit workbook action.
- **Grinding/stirring:** hold the side control. Releasing or leaving it stops the tool. Hands-free stirring is optional; changing modes stops it first. Use one finger for Stir and the other for the right-side view/observation controls.
- **Pages and pause:** Tasks, Study, Readings and Workbook pause the experiment and stop held tools/tap flow. Closing returns to the selected view. A manually paused run remains paused; stirring needs a fresh hold.

The Scene window has its own editor camera. Game/Simulator shows Main Camera. Keep CINEMA_4D_Editor disabled. Changes to scene objects during Play normally disappear afterward; saved writing is stored separately.

## Melting workflow: naphthalene and biphenyl

1. Place the tripod offered on the table. The gauze appears next, followed by the beaker. Place them in order.
2. Drag the beaker from the gauze into the lab's main sink. Once the beaker snaps below the outlet, the view moves in to show the fill line at eye level. Tap **Fill** and then **Stop** when the surface reaches the line. If you overfill, use **Empty** at the sink. Return the beaker to the gauze. **Refill** is available until the thermometer is clamped and returns you to your preparation step afterward.
3. Use the empty capillary offered on the table and drag one end to the sealing flame. It closes, cools and returns to staging.
4. Use the offered sample portion and put it in the mortar. It remains intact before grinding.
5. Place the pestle and hold Grind until powder is ready.
6. Dip the capillary into powder and use Pack. Each dip/settling action adds or settles **2 mm**. Repeat for the book's compact **10 mm (1 cm)** column. Guided mode advances at that target. Assessment waits for **Use sample** and accepts other nonempty quantities.
7. Place the capillary beside the thermometer bulb and secure the **thread**.
8. Clamp and slide the thermometer vertically. Use inspection to check that bulb and sample are underwater, clear of glass, with thread above water and the capillary opening about 1-2 cm above it. Confirm depth.
9. Light the burner. Warm gently and stir, slowing further near the reference region shown in guided instructions.
10. Inspect Sample for first liquid, then Thermometer; mark **First liquid** and **Fully liquid**. Each mark freezes the temperature number, scale, sample condition and simulation time. Guided mode rejects premature marks; assessment records them for later feedback.
11. Switch off. The Trial 1 review opens with your captured readings. Enter the temperatures and describe what you saw. Choose **Save Trial 1 and start Trial 2**. The assembly stays in place and the app supplies a fresh capillary, sample and cool bath.
12. After trial 2, complete the workbook and Submit. The book result averages the **two first-liquid readings from separate runs**. First-to-final melting within a run is an additional interval, not a substitute for two trials.

## Boiling workflow: acetone, benzene and ethyl alcohol

The boiling experiments use a dedicated electric bath heater and fixed support; tripod/gauze are not loose tasks in this adaptation. The separate burner is only for sealing an empty capillary before solvent is dispensed.

1. Place the beaker on the heater. Fill about 100 mL at the tap and return it.
2. Seal the empty capillary and let it cool.
3. Use **Add drops** to put about one third of the fusion tube's volume in it. Four presses reach the guided target. Assessment permits other quantities and an explicit **Use liquid** decision. Replace a sample if necessary; real solvents require approved waste handling.
4. Insert the capillary **closed end upward**, with its open end in the liquid.
5. Attach the fusion tube beside the thermometer bulb using thread. Clamp and immerse the sample and bulb, keeping the fusion-tube mouth and thread above water.
6. Switch on the electric heater, place the stirrer and stir while warming gently.
7. Use **Fusion tube** to watch actual bubbles. Isolated bubbles can be trapped air; mark the start of a **continuous stream**. Use Thermometer to inspect the reading. This method has one observation per trial, not a melting interval.
8. Switch off, record the trial and repeat with fresh sample. Average the two continuous-bubble readings in the workbook.

The assigned list includes benzene. The Punjab notebook's 5.4 page instead names toluene. This app keeps the assigned benzene experiment and states that difference. Read METHOD_AND_SOURCES.md for this and the electric-heating adaptation.

## Readings and calculator

Open **Readings** to see **Observations and calculations**. **Trial 1** and **Trial 2** are side by side. Melting has first-liquid and fully-liquid rows. Boiling has the continuous-bubble row. Each cell shows the frozen captured temperature with your entry below it. Completed-trial entries are read-only.

The calculator uses the same keypad and display as Physics 8.1. Select an editable reading or the optional mean field, calculate, then tap **Assign result**. Your entry and the original capture remain separate. Open **Workbook** for the descriptions and conclusion. Capturing an observation saves its number, scale, condition and time.

## Workbook and assessment

The workbook has Details, Reference, Observations, Calculations and Conclusion pages. Enter each trial's own saved numbers to one decimal place, describe the observations and write a result, conclusion and technique reflection. Post-lab writing is provided without automatic free-text correctness grading. Student name is optional. Worked examples are separate from student entries.

Guided Reference explains the method, expected region, identifying the event, reading the thermometer and interpreting mistakes. Assessment hides live numeric temperature, worked answers, outlines and technique hints. Its Tasks page lets you select an available action. You may prepare a sample before assembling the bath; scientific prerequisites still apply. The app does not penalize finger precision.

Scores derive from preparation, heating, stirring, observation timing and reading accuracy. There are no random temperature errors. Both complete normal modes require two trials; **Retry heating (practice)** is a separate single-trial exercise and does not replace an existing result.

## Saves and recovery

Each experiment uses **Application.persistentDataPath/Practicals/<id>/progress-v2.json**. Captured PNGs use its Scales folder. Drafts and completed first trials survive closing/reopening. Only completed preparation is restored. Interrupted heating restarts at fresh ignition while retaining an earlier completed trial. Backgrounding pauses; it does not continue heating unseen.

Older naphthalene records are preserved. A changed procedure revision disables unsafe partial resume rather than treating an old 2-3 mm, single-run record as the new two-trial method. Legacy first/final observations remain one old trial.

## Understand the Unity setup

- **GameObject:** an item in the Hierarchy. Its Transform stores position, rotation and scale. Components add appearance/behavior.
- **Main Camera / CameraDirector:** one rendering camera, with Overview and optional calibrated apparatus views. Overview uses its CameraMarkers transform. Inspection framing is recalculated from the real apparatus by the session's Focus method.
- **PracticalSystems / GameManager:** owns the session, StepController, manager, UI, dragging and workspace bindings. Historical naphthalene/melting class names remain for GUID compatibility; the definition selects MeltingSimulation or BoilingSimulation.
- **PracticalWorkspace / Workstations:** named Preparation, Heating, Staging and Sink markers. MovementBounds defines editable local dragging limits; they are not fixed numbers inside the touch controller.
- **Draggable / Collider:** selects and constrains a movable model. Home poses are stored in the session's Items list. Change that binding as well as the Scene object if changing a staging pose.
- **SnapZone:** accepts a compatible identifier and stores its final pose. Green ghost children are visual-only; they have no colliders.
- **Boiling apparatus:** fusion tube, dropper and heater are built at runtime from shared meshes/materials. They appear while playing a boiling profile and disappear when Play stops. They are not separate task scenes.
- **PracticalDefinition:** an Inspector-editable resource asset containing identity, workflow, steps, thermal/sample/water settings and required trials. All five definitions are now playable. Future unfinished definitions must keep Playable off. High-boiling substances need an appropriate bath/model, not a renamed water-bath profile.

Existing script GUIDs are retained. Layout migrations are one-time and backed up under Validation. Phase checkpoints keep source, scene and reports under Validation/Checkpoints.

## Build and hand off

See TEAM_HANDOFF.md for SDK/NDK/JDK installation, the generated APK, package import, hub selection and the phone checklist. The phone receives an APK; the SDK is a laptop build tool. The original team projects remain untouched. Physical phone testing is assigned to the user, who chose to test later.
