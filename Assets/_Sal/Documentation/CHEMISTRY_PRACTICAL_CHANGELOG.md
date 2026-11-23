# Chemistry practical update

The changes apply to the Melting Point practical and the chemistry variants that share its bench and interface. Physics 14.1 supplied the visual pattern.

| Area | Change | Where to review |
| --- | --- | --- |
| Water transfer | Tap the beaker to carry it between stations. Drag it locally under the spout or onto the gauze. The transfer uses a short fade and the beaker visibly settles at the new station. | Fill the beaker in Guided or Assessment. |
| Camera | The bath bench, sink placement and filling each have their own framing. Once the beaker is seated, the camera looks horizontally from the height of the 100 mL mark, with the handle alongside the beaker. The view holds during a grab and during filling. | Compare the views before carrying, after carrying and after seating the beaker. |
| Tap and drag input | Tap the handle on the original fixture to start or stop flow. The view joystick and zoom controls are hidden during the water step. Dropping near the beaker’s marked destination is more forgiving on a small screen. | Fill to the physical line. Try an off-target drop and an underfill or overfill. |
| Guided bench | A small segmented step badge, one current instruction and progress for multi-action steps replace the large permanent teaching panel. Gold marks the piece to touch or grab. Mint marks its destination. Why prompts explain the purpose of each action. | Start a new Guided run and place the tripod, gauze and beaker. |
| Full-screen apparatus view | The lab renders edge to edge. Instructions, expanded explanations and status stay at the left edge. Actions and heating controls stay at the right edge. The bench keeps its central working area clear during movement, immersion and heating. | Expand Why during immersion, then open the heat slider in the thermometer close-up. |
| Apparatus teaching | The procedure opens first when starting Guided. The Study panel includes an Apparatus tab explaining the water bath, preparation tools, thermometer assembly and heating equipment. | Start Guided or open Study → Apparatus. |
| Controls | View tools open on demand. Guided hides the task picker, and observation navigation appears when there is something to record or review. Small drag handles remain for thin tools. | Compare assembly and heating. |
| Capillary collection and packing | Drag the real tube into the highlighted powder, then tap it to settle the grains. A wider touch target helps with the thin glass. The tube taps the bench as the visible sample compacts, and a left-side readout shows loose height, packed height and the target. Guided switches between dipping and settling and advances at the target. | Guided, **Fill and pack the capillary**. Assessment retains the choice to accept an imperfect sample. |
| Capillary options and refill | A compact Options control holds the alternate drag handle, Settle powder and Empty tube when applicable. Tapping the filled beaker reveals Refill water before clamping and heating. | Open Options with loose powder, try emptying the tube, then select the beaker to check refill. |
| Attachment thread | The loose thread appears beside the attached capillary. The close-up includes both the thread and the tie point. Tap the highlighted thread itself to lift it and tie it around the assembly. | Guided, **Attach the capillary**. |
| Thermometer placement | Move follows the glass, and the placement camera includes the real clamp jaws. Drag the glass or its handle to the mint ring to snap the assembly upright. After clamping, use the same handle to lower the assembly, then tap Confirm. The clamp was lowered slightly so the fully immersed thermometer remains held above the reading area. | Guided, **Clamp and immerse**. |
| Melting observation | Naphthalene warms at 8×. The clock slows smoothly from 78 °C to 80 °C and runs at 3× during melting. At 1 °C/min this gives about 20 seconds between first and fully liquid. The melting HUD uses Sample and Thermometer views, and the workbook retains the readings captured at each mark. | Guided, **Heat, stir, and observe**. Mark first liquid, check the red column, and return to Sample. |
| Documents | Study, the written workbook and the readings/calculator sheet use the same pinned-paper treatment as the Physics 14.1 circuit. | Open each page and check the reading fields, tabs and calculator. |

## Verification in Unity

- `Naphthalene → Validate full experience` covers naphthalene, biphenyl, acetone, benzene and ethanol. It checks for UI overlap during assembly, water filling, immersion and the heating close-up with Why and the heat slider open. [Full report](Validation/UsabilityValidation.txt)
- `Naphthalene → Validate bench and tap` is the focused check for those same bench interactions. It includes simulated touch ownership, framing at 20:9, a steady camera while using the tap, overfill recovery, beaker return and calculator assignment. [Bench report](Validation/ChemistryBenchValidation.txt)
- Unity captures show the [bench carry](Validation/ChemistryBenchCarry.png), [sink placement](Validation/ChemistrySinkPlacement.png), [filling view](Validation/ChemistryTapFill.png), [readings and calculator](Validation/ChemistryReadingsPaper.png) and [written workbook](Validation/ChemistryWorkbookPaper.png).
- Capillary validation exercises an off-center touch, direct dip, direct settling, recovery options and automatic progression. Unity captures show [collecting](Validation/ChemistryCapillaryCollect.png) and [settling](Validation/ChemistryCapillaryPack.png).
- The focused check verifies [the visible thread](Validation/ChemistryThreadAttachment.png), tapping it directly to tie it, and its attachment to the thermometer when moving on to the clamp.
- The focused check also tests [reading the thermometer during melting](Validation/ChemistryMeltingThermometer.png), switching back to the sample before completion, and saving both observations.
- The overlap check saved the [immersion close-up](Validation/ChemistryClearImmersion.png) and the [heating thermometer close-up](Validation/ChemistryClearThermometer.png).

Physical Android touch, safe-area behavior and the on-screen keyboard still need a device pass.

## Phone checks

1. With a fresh Guided run, drag the tripod, gauze and beaker before starting the water step.
2. Tap the beaker to carry it to the sink. Drag it under the spout. Confirm that the tap handle, spout and filling line remain visible. Starting and stopping the tap should keep the camera still.
3. Try stopping below and above the line. Empty the beaker or reopen the tap as appropriate, fill to the mark, carry it back and drag it onto the gauze.
4. Open View outside the water step. Check that the joystick releases when the panel closes or focus is lost. Confirm the phone’s keyboard leaves the selected workbook field reachable.
5. On the capillary step, grab the glass slightly off-center and drag to the mortar. Tap the highlighted tube and watch the loose column compact. Try Empty tube from Options, then repeat until the next step opens.
6. At gentle heat, mark First liquid, tap Thermometer, and read the top of the red column. Return to Sample and mark Fully liquid when the last solid disappears.
7. At Clamp and immerse, drag the Move handle from the glass to the clamp ring. Try releasing over the bath first, then at the jaws. Lower the clamped assembly with the same handle and confirm depth.
