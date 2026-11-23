# Physical States chemistry practicals

Open **NaphthalenePractical** in Unity Hub with **Unity 6000.3.24f1**, then open **Assets/Scenes/Melting_Point_Naphthalene.unity**. This shared scene now runs all five experiments: naphthalene, biphenyl, acetone, benzene and ethyl alcohol. Original team projects remain unchanged.

## Start here

- **BEGINNER_WALKTHROUGH.md** explains the controls, each preparation action, two trials, workbook, Inspector and Android installation.
- **PHASED_IMPLEMENTATION.md** records the delivered phases and architecture.
- **METHOD_AND_SOURCES.md** records the assigned-list/Punjab-book differences and simulation adaptations.
- **TEAM_HANDOFF.md** explains integration without replacing the team's ProjectSettings.
- **VALIDATION.md** records the automated checks and device-testing limits.

## What is implemented

The front-facing bench has apparatus offered on the table as needed, optional single-camera inspection, bounded zoom and controlled placement. Large apparatus and pestle are directly draggable; thin tools retain small handles. Tap apparatus for inspection, drag to place, and use Bench to return. Actions advance automatically. Inspection does not change the task.

Students fill the actual beaker at the sink tap, watch the marked filling line, stop or empty it, and transport it to the bath. Melting preparation includes virtual empty-capillary sealing, grinding, a book-specific 1 cm packed column and thread attachment. Guided mode teaches the sequence; assessment allows available prerequisite-based actions and student-chosen quantities, with technique feedback.

Melting and boiling use separate simulation models. Melting captures first and complete melting; boiling uses a fusion tube, inverted capillary and continuous-bubble observation. Volatile-liquid baths use an electric heater; the separate flame only seals an empty tube. Each experiment requires **two actual trials with a fresh sample**, then computes a mean from student-entered readings. A melting interval within one trial is additional information, never a substitute for a second trial.

Guided mode starts with a reopenable Study screen. Instructions describe the immediate sub-action and include a labelled Zoom button. A bounded view joystick smoothly changes the viewing angle in bench and inspection views.

The rounded glassy HUD supports independent stirring, camera and observation pointers. Readings shows both trials side by side with the reused physics calculator and explicit assignment into a selected entry. Reference, Tasks and Workbook pause the experiment and preserve inspection. The workbook retains frozen decimal readings and scales, observations, calculations, result, conclusion and reflection. Saves are independent per chemical. Old notebooks remain readable; incompatible preparation resumes restart safely.

## Android and delivery

**Builds/PhysicalStatesPracticals.apk** is the Android 8.0+ ARM64 landscape development build. **Builds/PhysicalStatesPracticals.unitypackage** is the team dependency export. See BUILD_MANIFEST.md for artifact details and checksums after packaging.

Use **Naphthalene > Build Android APK** or **Naphthalene > Export team package** to regenerate them. Android Build Support, SDK/NDK and OpenJDK must be installed for this Unity version. These menu labels retain the historical project name.

**338 workflow/model checks and 18 focused close-up checks passed.** Actual phone installation, finger usability, Android lifecycle, keyboard, safe areas and measured 30 FPS are still for the user to test, as requested. A 30 FPS target is configured; it is not a device measurement.

## Files and assets

Definitions live in **Assets/Resources/Practicals/Definitions**. Runtime scripts live in **Assets/Scripts/Practicals/MeltingPoint**; the historical folder name is retained. Scene migrations are versioned and repeat-safe; backups and validated checkpoints are in Validation. Imported script/asset GUIDs are preserved. The imported CINEMA_4D_Editor camera stays disabled. Only Main Camera renders.

Builds and validation copies are excluded from Git. Keep the delivered APK/package separately if sharing without those folders.

## Latest delivery status

The user requested stopping the incremental APK rebuild. It is stopped. The source now includes the restored imported TextMeshPro shader include. The existing APK predates this shader-only repair; the team package includes it. See BUILD_MANIFEST.md for exact artifact status. Test the main project directly in Unity; no further build is running.
