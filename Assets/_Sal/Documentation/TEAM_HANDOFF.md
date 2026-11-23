# Team integration guide

## Open the shared project

1. Clone https://github.com/arty542/TajrubaGah.git.
2. In Unity Hub, add the **NaphthalenePractical** folder from the clone.
3. Open it with **Unity 6000.3.24f1** and allow package resolution and asset import to finish.
4. Open **Assets/Scenes/Practical_Launcher.unity** and enter Play Mode.

The launcher currently offers Chemistry and Physics 14.1. The earlier Physics 8.1 scene is included for direct review and apparatus reuse. The actual scene files and model import settings are committed with their existing GUIDs.

## Scene entry points

| Scene path under Assets/Scenes | Use |
| --- | --- |
| Practical_Launcher.unity | Shared menu |
| Melting_Point_Naphthalene.unity | Naphthalene, biphenyl, acetone, benzene and ethanol |
| Physics_14_1_Ohms_Law.unity | Wire preparation, circuit connections, meter readings, calculations and graph |
| Physics_8_1_Specific_Heat.unity | Earlier brass-bob calorimetry practical |

Load a practical using Single scene mode. Each scene owns its player, camera, input and UI. If the team's hub keeps objects alive with DontDestroyOnLoad, suspend its camera, AudioListener, EventSystem and player input while a practical is active. Restore them on returning to the hub.

For a direct Chemistry selection, set PracticalSelection.ExperimentId to naphthalene, biphenyl, acetone, benzene or ethanol before loading Melting_Point_Naphthalene. The Chemistry introduction also exposes these choices.

## Required packages and project settings

The exact package versions and resolved dependencies are in **Packages/manifest.json** and **Packages/packages-lock.json**.

| Requirement | Current setting |
| --- | --- |
| Unity Editor | 6000.3.24f1 |
| Universal Render Pipeline | 17.3.0 |
| Input System | 1.20.0 |
| Unity UI | 2.0.0 |
| Active Input Handling | Input System Package |
| Orientation | Landscape |
| Android minimum | API 26 |
| Android architecture | ARM64 |

The source project's manifest also includes Unity MCP as an editor development tool, resolved from a Git URL. Use a working Git installation for Package Manager to resolve that dependency. The destination team project can import the practical runtime with URP, Input System and Unity UI installed.

For opening this repository directly, use its committed ProjectSettings. The copied Unity Cloud project association may display a membership warning. The practicals save locally and can run with the local project.

## Bring the practicals into another Unity project

1. Commit the destination project's current work and create an integration branch.
2. Open the source project and select **PracticalAssets**, **Resources**, **Scripts**, **Settings**, **InputSystem_Actions.inputactions** and **link.xml** under Assets, together with the required scenes.
3. Use **Assets > Export Package**, enable **Include dependencies**, then import the package into the destination project. Alternatively, copy those asset trees together with every .meta file while the destination Editor is closed.
4. Inspect matching GUIDs during import. Models, thermometer and calculator assets originated in shared donor projects, so the destination may already contain some of them. Retain one compatible copy of each shared script and class.
5. Keep the internal Resources paths. They load the chemical definitions, calculators and sound clips. The Physics141 and Physics81 runtime folders and the common PracticalUiStyle, PracticalLauncher and Chemistry foundation scripts have dependencies on one another. Include the complete Scripts/Practicals tree for an initial integration.
6. Merge the required URP graphics/quality configuration and Input System setting into the destination's settings. Review its existing rendering, input, platform and application identity settings when doing so.
7. Add the required scene paths to the destination Build Profiles scene list. The source scene list is in **ProjectSettings/EditorBuildSettings.asset**.
8. Run the practicals through the destination's actual hub and check input, camera, audio, save and return behaviour.

The Assets/Editor folder contains development tools, smoke checks and layout builders. Several builders install missing layouts when their marker files are absent. Importing the serialized scenes and runtime folders is sufficient for the initial team integration. When bringing the editor tools too, preserve the Generated/Naphthalene revision markers with the assets.

## Connect the group's launcher

The current return target is **PracticalLauncher.MenuScene** in **Assets/Scripts/Practicals/PracticalLauncher.cs**. It names Practical_Launcher.

For the existing launcher, retain this name and include the scene in the build. For the group's own hub, change the central return target to that hub's scene name and register the hub in the build. Return controls call the practical's save method before loading the menu.

Physics 14.1 returns through OhmBench.ReturnToMenu. Chemistry uses NaphthalenePracticalSession.ReturnToLauncher. The earlier Physics 8.1 flow saves through LabSetupHud.ReturnToLauncher.

## Saving and integration checks

Progress and workbook drafts live under Application.persistentDataPath. Chemistry keeps independent progress per chemical. Interrupted Chemistry heating resumes at fresh ignition, and completed observations retain their captured numbers and scales. Ohm's law saves circuit and workbook state through its own store.

Before merging, run Guided and Assessment, record and edit readings, open the workbook, return to the hub and resume. Check a complete Ohm's law graph submission, both Chemistry trials, two-finger stirring, wrong drops and application pause/resume. Test readable controls and frame rate on an Android device.

Existing walkthrough and validation documents record development history. Source review and Editor checks do not establish the destination project's phone performance. Earlier APK and unitypackage snapshots may predate this commit. Use the committed source as the integration baseline.

The upload check found complete local asset metadata with unique GUIDs. It also found legacy serialized references in the retained calculator prefab, imported flame material and URP configuration. The current runtime creates its calculator UI and uses the configured flame material. Review Unity's Console when opening the clone and when merging rendering settings into a different project version.

## Build and source references

Use Unity's Build Profiles when the group is ready to produce an APK. Android Build Support, SDK/NDK and OpenJDK are installed through Unity Hub for this Editor version.

- **PHYSICS_14_1_HANDOFF.md** explains the Ohm's law procedure, model choices and device checks.
- **BEGINNER_WALKTHROUGH.md** describes Chemistry interaction and workbook behaviour.
- **METHOD_AND_SOURCES.md** records scientific adaptations, including assigned benzene versus the Punjab notebook's toluene entry.
- **Assets/PracticalAssets/README_IMPORT.md** describes the reusable apparatus library.
