# Chemistry practicals: delivered phases

Project: **NaphthalenePractical**. Shared scene: **Assets/Scenes/Melting_Point_Naphthalene.unity**. Original team projects are unchanged.

| Phase | Delivered | Validation checkpoint |
|---|---|---|
| 1 | Configurable experiment definitions, stable procedure IDs, isolated saves, legacy migration | 164 checks |
| 2 | Front-facing bench, apparatus tray, workstations, destination previews and lifted placement | 175 checks |
| 3 | Sink/tap, measured water volume, empty/refill and beaker transport | 184 checks |
| 4 | Empty-capillary sealing, book-specific preparation, thread attachment, prerequisite-based assessment | 199 checks |
| 5 | Two actual trials, fresh sample between trials, board workbook and mean | 227 checks |
| 6 | Biphenyl plus dedicated boiling workflow for acetone, benzene and ethyl alcohol | 455 final checks across all five |
| 7 | Android build configuration, dependency export, beginner and team handoff documentation | Software delivery complete; actual-phone checks deferred to user |

Counts describe successive suites, not independent totals. Final report: **Validation/AllPhasesVerified.txt**. Checkpoints retain their sources and reports under **Validation/Checkpoints**.

## Shared architecture

**PracticalDefinition** assets contain chemical identity, procedure, thermal settings, sample targets and two-trial requirements. **PracticalSelection.ExperimentId** selects a definition before the same scene loads. **StepController** owns the guided sequence; assessment uses **PracticalTaskRules** to offer actions whose real prerequisites are satisfied. Inspecting apparatus never completes or selects a procedure action.

**PracticalWorkspace** names preparation, heating, sink and staging locations. **Draggable**, **DragController** and **SnapZone** constrain movement and settle compatible objects; thin-tool handles and tray entries retain pointer ownership. **CameraDirector** uses one Main Camera and optional inspection targets, with bounded per-view zoom. Views persist when actions advance.

**WaterBathFill** records actual volume and tap state. **ThermalSimulation** shares bath/instrument values and technique metrics; **MeltingSimulation** models melting fraction and two endpoints, while **BoilingSimulation** models liquid heating and one continuous-bubble endpoint. Boiling apparatus is generated from shared assets and uses an electric bath heater. There is no simulated solvent flame.

**PracticalTrialSession** archives completed trials before creating a fresh sample. **PracticalTrialStatistics** validates two distinct completed trials and averages student-entered point readings. **PracticalWorkbookUI** manages drafts and descriptions; numeric scale-reading accuracy and event timing are separate. Heating practice has one trial and is explicitly distinguished from a completed two-trial exercise.

## Save compatibility

Saves use **Application.persistentDataPath/Practicals/<experiment-id>/progress-v2.json**, with Scales alongside them. The experiment ID, procedure revision, stable task ID, preparation state, trial history, workbook draft and notebooks are retained. Legacy naphthalene saves are copied into the new namespace without deleting the original. Two endpoint captures in an old single trial never become two trials. Incompatible procedure revisions retain writing/notebooks but disable unsafe mid-preparation resume. Interrupted heating restarts at fresh ignition; completed trials remain archived.

## Beginner review order

1. Open the shared scene and start each experiment from its introduction.
2. Inspect Definitions to see why the same scene can run different chemicals.
3. Complete one guided run including both trials; inspect the mean and saved decimal readings.
4. Try assessment preparation in a different valid order and choose a different sample quantity.
5. Review BEGINNER_WALKTHROUGH.md and METHOD_AND_SOURCES.md before changing settings.
6. Install the APK on your phone and complete TEAM_HANDOFF.md's device checklist before student delivery.

The user explicitly chose to finish the build and test the phone later. Software checks and APK packaging do not substitute for physical Android acceptance.

## Latest delivery status

The user requested stopping the incremental APK rebuild. It is stopped. The source now includes the restored imported TextMeshPro shader include. The existing APK predates this shader-only repair; the team package includes it. See BUILD_MANIFEST.md for exact artifact status. Test the main project directly in Unity; no further build is running.

## Current bench usability revision

The tray introduced in Phase 2 is superseded by real apparatus offered on the table. Study onboarding, clearer sub-action instructions, action zoom, bounded view joystick, physical beaker filling mark/refill recovery, side-by-side readings and the imported physics calculator are implemented. Current verification: 338 workflow/model checks plus 18 close-up checks passed. No APK rebuild was performed. See CHECKPOINT.md for the latest source status.
