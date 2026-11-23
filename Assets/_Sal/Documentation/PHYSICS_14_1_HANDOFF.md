# Physics 14.1 · Ohm’s Law

Open the Practical Launcher scene in Unity 6000.3.24f1, enter Play Mode, and choose Physics 14.1. The scene also opens directly from Assets → Scenes → Physics_14_1_Ohms_Law. In the Unity menu, run Naphthalene → Build Android APK when ready to build. Physics 14.1 is registered in both the editor scene list and the build script.

## Unity checks

- Practicals → Physics 14.1 → Test bench in Play Mode checks the launcher, walking entrance, bench shortcut, inspection views, camera lock and target framing at 16:9 and 20:9.
- Practicals → Physics 14.1 → Test complete student flow runs fixed circuit checkpoints and the two student modes. Both tests use a separate progress file. See the current results in Validation/Physics141BenchSmoke.txt and Validation/Physics141FlowSmoke.txt.

## Guided walkthrough

The practical opens with a procedure and equipment guide. In Guided, tap the circled equipment to learn what each piece does. A small corner badge tracks the eight steps. A floating prompt near the equipment gives the current action and count, and **Why?** opens extra explanations when available. Gold circles identify where to start and mint circles identify the target. Tap a meter to zoom in, then drag its rotary dial to the marked range. Press **POWER** on the supply and drag around its voltage knob until the supply display reads 6.0 V. Guided explains the effect of each rheostat move inline and keeps the close-up until the wiper reaches the target. Wiring tools appear once there is a wire to edit, and the workbook becomes available after switching the circuit off.

A new run starts with an unwired bench. If there is saved work, the opening page offers **Resume Guided** or **Resume Assessment** to restore it. **New Guided run** starts from Step 1.

Guided has nine short learning checks. The first six cover the ammeter, plug key, clean contacts, DC settings, voltmeter placement and zero readings. After the first and third reading pairs, students check how to vary current and calculate resistance. The last check covers the graph axes. A wrong answer gives a hint and another try. A correct answer explains the idea. Students can skip each check, and Assessment runs without these prompts.

In the circuit diagram, the **Equipment key** matches the six schematic symbols to the bench equipment. Tap each symbol on the paper to place it, then drag a wire from one terminal to the other or tap the two terminals. Guided shows the next pair and explains its purpose as you draw. **Show wiring hint** traces that wire as a dashed line for five seconds. The hint disappears when you connect the wire. Green wires form the main loop and blue wires connect the voltmeter across the sample.

The opening procedure states Ohm’s law and defines V, I and R. After preparing the lead, mount the loose nichrome sample on the rear posts. Tap a screw cap to loosen it, drag that end onto the exposed metal, then tap the cap again to tighten the wrap. On the main bench, the key, rheostat and nichrome board use screw posts. The supply and meters accept plugs. A lead counts toward the circuit after all its screw caps are tightened. Saved runs retain the installed sample and secured leads.

The workbook uses the same pinned paper as the circuit diagram. On **Observations**, choose a saved meter pair or tap a table field to highlight its matching row. Use the **Graph** tab to plot the readings on graph paper, adjust the points and trend line, and write the conclusion.

## Phase fixes verified in Unity

- **Bench.** Adjusted the camera framing for the test aspect ratios, moved supply controls out of the reading close-up, and replaced the flat rheostat winding with a baked coil mesh.
- **Wiring and measurements.** Corrected resume so the saved selector settings are applied before the displays refresh. Added visible seated plugs and clip grips, and kept the numeric model independent of the rendered cable geometry.
- **Workbook and launch.** Verified calculator assignment, retakes and graph submission. The launcher test now waits for each scene to finish loading before checking it.

## Android checklist

1. **Room and camera.** Try both walk and shortcut. Check that the view transition feels comfortable. Look for any overlap with the notch, system gestures, sockets, selectors or LCDs.
2. **Wiring.** Mount both ends of the sample. Try a drop on a closed cap and then loosen it, wrap the lead and tighten it. Check the smallest posts and jacks, whether your finger obscures the target, and how easily you can undo a mistake. Listen for the selector, connector and key sounds.
3. **Measurements.** Press the physical POWER switch and turn the supply knob both ways to reach 6.0 V. Drag the rheostat slowly and quickly. In Guided, release early and confirm the close-up stays on the wiper, then release on the target to proceed. Watch both LCDs and capture five pairs. In Assessment, try a duplicate and verify the reading cannot be captured again.
4. **Workbook.** Open the Android keyboard in the first and last rows, try the calculator and assign a result, and plot and nudge a point near each axis. Return to the launcher during wiring and again after editing the graph, then reopen the scene and verify progress and power state.
5. **Full run.** Finish Guided and Assessment. Close and reopen the app mid-session. Check audio levels and frame rate during drag and camera movement, and open Chemistry and Physics 8.1 from the launcher.

## Measurement choices

The Punjab practical notebook, printed pages 30–31, governs the circuit, procedure, rheostat, number of observations and graph. Handheld digital multimeters follow the supplied photo. The 6.0 V setup reading is on the supply display, with the key open. The voltmeter measures across the nichrome. The pure C# solver uses ideal meter loading, a 15 Ω sample and constant temperature. The workbook grades calculations from the rounded numbers that the student actually saw.

The Guided target sequence is 25, 50, 35, 15 and 5 Ω. These are teaching choices for this simulation. The midpoint is the first reference. The 50 Ω end gives a clearly lower current, 35 Ω fills the gap, and 15 Ω and 5 Ω produce two higher, distinct readings. The supply stays at 6.0 V across the sequence. After the first reading, Assessment accepts any valid setting that gives a distinct current.

