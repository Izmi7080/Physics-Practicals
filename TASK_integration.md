# TASK — Integrate all team practicals into one app: Tajarbagah

Goal: one Android app (one Unity project, one repo) that contains every member's practicals
behind the shared menu, without breaking anyone's work, and with a way for all 5 members to keep
working in it afterwards.

Rule for every stage: **nothing is deleted or overwritten in anyone's original repo.** Their repos
stay as they are (they are the fallback). All integration work happens in a new host repo.

---

## The method (and why it differs a little from "clone everything into one folder")

Cloning every repo into one master folder is right for **looking** (Stage 0). It is not the way to
**combine** them, because each clone is a whole Unity project: its own ProjectSettings, Packages,
render pipeline, input settings, tags/layers and GUIDs. Unity projects cannot be merged folder-by-
folder or with git merges across repos.

So the plan is three folders, kept apart:

```
F:\Integration\
  sources\            read-only clones of all 5 members' repos (never opened in Unity, never edited)
    azzam\ ...
    member2\ ...
  reports\            the inventory and decisions (generated + written here)
F:\Unity Projects\Tajarbagah\    the HOST project (new repo) — the only place things are combined
```

- `sources\` is **outside** any Unity project, so Unity never imports them by accident.
- Each practical is brought into the host **one at a time**, as its own folder WITH its `.meta`
  files (or a `.unitypackage` export from the member's project). Both keep the asset GUIDs, so scene
  and prefab references survive the move.
- After integration, every member works **in the host repo on their own branch** and merges with a
  pull request. Their old repos are frozen.

---

## STAGE 0 — Collect and survey (read-only)
1. Each member gives repo access (private repos: add the integrator as a collaborator, or make a
   read-only invite). List: member, repo URL, which practicals, which scene each practical starts in.
2. Clone all repos into `F:\Integration\sources\<member>\<repo>` (shallow clone is fine).
3. Automated inventory per project (Claude writes a script that reads the files, no Unity needed):
   - Unity version (`ProjectSettings/ProjectVersion.txt`);
   - render pipeline (URP / Built-in) and the pipeline asset;
   - input handling (old Input Manager / new Input System / Both) — `ProjectSettings.asset`;
   - packages and versions (`Packages/manifest.json`), Asset Store / third-party folders;
   - scenes, and which ones are the practical's entry scene;
   - folders and naming: does the work sit in one suffixed folder (`_Azm`-style) or loose in
     `Assets/Scripts`? namespaces? class names that would collide with someone else's?
   - singletons / `DontDestroyOnLoad`, static state, `Camera.main`, `FindObjectOfType` usage;
   - tags, layers, sorting layers, physics layers in use (these are PROJECT-WIDE in Unity);
   - duplicate assets across projects (same GUID or same third-party package imported twice);
   - size: textures > 1024, models over the triangle budget, total asset size;
   - Android player settings (min/target API, orientation, scripting backend, package name).
4. Output: `reports/Inventory.md` — one row per project, plus a list of every conflict found.

## STOP GATE 0 — review the inventory together

**Stage 0 status (2026-11-24):** 3 of 4 teammate repos surveyed (Rayyan, Saleha, Bareera; the 5th
to come). Report: `F:/Integration/reports/Inventory.md`. All on Unity 6000.3.24f1 + URP 17.3 +
new Input System. Conflicts: shared template/lab-environment/TMP GUIDs, Bareera's project is a copy of
Azzam's old project, Rayyan's 1.5 GB of 8K textures.

---

## STAGE 1 — Team decisions (from the inventory)
Decisions the team has to take (Claude proposes, the team agrees):
1. **Unity version** everyone uses from now on (proposal: 6000.3.x LTS, as in this project).
2. **Render pipeline** (proposal: URP) and **input system** (proposal: new Input System; "Both"
   only as a temporary bridge for a practical that still uses the old one).
3. **Host project**: start from this project (it already has the app shell: splash, menu,
   practical catalog, settings, theme, scene loading, back button) — copied into a NEW repo
   `Tajarbagah`, so this repo stays as Azzam's frozen original.
4. **Shared core ownership**: the app shell (AppManager, SettingsManager, catalog, theme, menu)
   becomes shared code. Decide: keep it as-is with `_Azm` names, or move it to a neutral
   `VirtualLab.Core` / `_Shared` folder owned by one person. (Changes CLAUDE.md naming rules.)
5. **Per-member folders and suffixes** for everyone (`Assets/_Azm`, `Assets/_<Name2>` ...), each
   with its own namespace and **assembly definition** (`.asmdef`), so one member's compile error
   or accidental dependency cannot break another's.
6. **Project-wide settings owner**: tags, layers, Player settings, quality, packages change only
   through one person (avoids silent breakage).
7. **Git workflow**: `main` = always builds; each member a branch per practical; pull requests;
   Unity Smart Merge (UnityYAMLMerge) configured; one owner per scene (scenes do not merge well).
8. **Practical contract** (what a practical must provide to plug into the app) — see Stage 2.

## STOP GATE 1 — decisions written in `reports/Decisions.md`

**Stage 1 decisions (Azzam, 2026-11-24):**
1. Host = THIS project (Physics Practicals); the others are integrated into it (no separate new
   repo for now). The GitHub backup (commit 60fff54) is the fallback.
2. Azzam's legacy old-pendulum content may be left out / archived in the host (backup exists).
3. `Assets/_Shared/` for shared assets + one suffix folder, namespace and asmdef per member: agreed.
4. Azzam oversees the app shell and all project-wide settings.
5. Rayyan's textures: explained, decision pending.
6. Bareera's practicals = Reflection and anhydrous CuSO4 (there is no Titration scene; remove the
   stale build entry when importing).

---

## STAGE 2 — Prepare the host
1. Create `Tajarbagah` repo from this project (fresh history or keep it — decide in Stage 1).
2. Remove nothing; move legacy/unused items to `Assets/_Archive` only with approval.
3. Apply the Stage 1 decisions: shared core folder, asmdefs, `ThirdParty/` for shared packages
   (each third-party asset imported ONCE), project settings, `.gitignore` / `.gitattributes`,
   Smart Merge config.
4. Write `INTEGRATION_GUIDE.md` — the **practical contract**:
   - one folder `Assets/_<Name>/`, one namespace, one asmdef, `_<Name>` suffix on classes/scenes;
   - one entry scene per practical, registered in the catalog (`PracticalInfo` asset: id, subject,
     title, scene, thumbnail, Learn/Perform support);
   - leave the practical ONLY through the app manager (back to menu), never `Application.Quit`;
   - Android back button = pause/back, never quit;
   - no `DontDestroyOnLoad`, no static practical state, unsubscribe events in `OnDestroy`;
   - landscape, safe area, Canvas Scaler 1920×1080 Expand, 48 dp touch targets;
   - recommended (not required at first): the shared shell (HUD base, theme) for a consistent look.
5. Test: host builds an APK and runs on a phone before anyone else's work goes in.

**Stage 2 progress (2026-11-24):**
- Branch `integration` created (main = backup 60fff54).
- Frog fix produced in `F:/Integration/processed/rayyan` (models 391k -> 113k tris, textures
  1.5 GB -> 40 MB, same GUIDs, names kept Unity-style) — imported in Stage 3.
- Dependency trace (`F:/Integration/reports/deps.py`): the app needs only the lab environment pack +
  lab-room materials from `Assets/Flame Test`, `InputSystem_Actions`, `Assets/Settings`,
  `Assets/Resources/DOTweenSettings`, Plugins, TextMesh Pro. Legacy (old pendulum `Assets/Scripts`,
  `Assets/Scenes`, `Assets/Prefabs`, `Assets/Models`, Screenshots, TutorialInfo, Readme, the rest of
  `Flame Test`) is unused.
- `INTEGRATION_GUIDE.md` written (draft).
- Done in Unity (2026-11-24):
  - lab environment pack -> `Assets/_Shared/ThirdParty/3D Laboratory Environment with Appratus`,
    lab-room prefab + materials -> `Assets/_Shared/LabRoom`, AngleArcLine.mat -> `_Azm/Art/Materials`
    (all GUIDs kept);
  - legacy content sent to the Recycle Bin (Flame Test rest, Models, Prefabs, Readme, Scenes,
    Screenshots, Scripts, TutorialInfo); `Lab.unity` removed from Build Settings;
  - Player settings: Tajarbagah / Tajarbagah / `com.tajarbagah.app`, landscape left+right only;
  - asmdefs: `VirtualLab.Azm`, `VirtualLab.Azm.Editor`, `DOTween.Modules`;
  - `Assets/_Shared/Prefabs/AppBootstrap.prefab` (SceneBootstrap with catalog + theme);
  - checks: 5 scenes, 0 missing scripts / broken refs; 117 prefabs/materials/SOs clean except 4
    third-party water prefabs (material missing inside `liquids.fbx` — pre-existing in the pack);
    play test Splash -> Menu -> Pendulum -> Cylinder -> Screw gauge -> Menu, no errors.

## STOP GATE 2

---

## STAGE 3 — Pilot: integrate ONE practical end to end
Pick the cleanest project from the inventory.
1. Branch `integrate/<member>-<practical>`.
2. Bring the folder in with `.meta` files (or its `.unitypackage`); add any packages it needs.
3. Fix: compile errors, missing references, collisions (names, tags/layers, input, pipeline
   materials turning pink), singletons.
4. Register it in the catalog; menu → practical → back to menu works; Learn/Perform if supported.
5. Play test in Editor at 16:9 / 20:9 / 4:3, then an APK on a phone; record frame rate and APK size.
6. Merge to `main`. Write down every fix needed → becomes the checklist for the others.

**Stage 3 done (2026-11-24): Mujtaba — Osmosis in potato** (branch `integrate/mujtaba-osmosis`)
- Imported `Assets/OsmosisPotato` -> `Assets/_Muj/OsmosisPotato` with .meta files (GUIDs kept), using
  `F:/Integration/reports/import_plan.py` (+ `deps.py`): 134 files / 12.2 MB. His 141 MB `Reference`
  folder was copied selectively (only the 53 files his scenes use). 11 files already in the host
  (URP settings, TextMesh Pro) were skipped — his scenes use the host copies.
- His code compiled unchanged (own asmdefs `Osmosis`, `Osmosis.Editor`, two test assemblies;
  `DOTween.Modules` referenced by name, so it binds to the host's).
- Integration changes (all marked `INTEGRATION (Tajarbagah)` in his folder):
  `Scripts/Integration/TajarbagahLink.cs` (exit: shut down his persistent Sfx + SceneLoader, then
  AppManager_Azm.GoToMainMenu), `PracticalEntry.cs` (creates his sound bank in Guidance/Lab, starts
  the music in Guidance as his PLAY did), `BackButton.cs` (Android back opens his own exit popup);
  `Sfx.Shutdown/Exists`, `SceneLoader.Shutdown`; `MenuActions.Quit/ToMenu` -> app menu (no
  Application.Quit); `Osmosis.asmdef` references `VirtualLab.Azm`; AppBootstrap in Guidance and Lab;
  canvas scalers -> Expand (4:3 cut off his BACK/NEXT arrows).
- Practical starts at `Osmosis_Guidance` (his title screen `Osmosis_Menu` is replaced by the app menu
  and is not in the build). Card: `_Muj/OsmosisPotato/Data/PracticalInfo_Osmosis.asset` in the Biology
  slot 1. Build: `Osmosis_Guidance`, `Osmosis_Lab`.
- Tested: menu -> Biology card -> Guidance (bank created, music on) -> Lab (music stops as in his
  original) -> back button/popup -> YES -> app menu (bank + loader gone, no stray audio); second
  entry works; 7 build scenes 0 missing scripts / broken refs; no console errors.
- Not changed (team to decide): his classes keep his naming (namespace `Osmosis`, no `_Muj` suffix);
  his `SceneLoader` sets `Application.targetFrameRate = 60` and enables EnhancedTouch at app start
  (harmless app-wide, noted); tests not run yet.

**Pilot checklist for the next imports**
1. `deps.py` on the member project (roots = their scenes/scripts/prefabs) -> `import_plan.py` dry run;
   use `--partial` for big asset dumps; check the "skip" list (host copies win).
2. Copy with `--copy` on a new branch; refresh Unity; check compile + assembly names.
3. Scan their scenes/prefabs for missing scripts / broken refs.
4. Find every `DontDestroyOnLoad`, `Application.Quit`, own menu/launcher scene, scene-name loads and
   global settings (frame rate, input) -> route exits to `AppManager_Azm.GoToMainMenu` and clean up
   their persistent objects on exit.
5. Pick the entry scene (skip their title/launcher), add `AppBootstrap`, Android back button.
6. Canvas scalers -> Expand; capture 16:9 / 20:9 / 4:3.
7. PracticalInfo asset in their folder + catalog slot + Build Settings.
8. Play test menu -> practical -> exit -> menu twice; re-scan all build scenes; merge.

## STOP GATE 3 — the pilot checklist is reviewed

---

## STAGE 4 — Remaining practicals, one at a time
Same steps as Stage 3, one branch and one merge per practical, ideally with its author present
(they know their scenes). After each merge: whole-app smoke test (menu → every practical → back).

**Stage 4a done (2026-11-24): Saleha** (branch `integrate/saleha`)
- Imported with `import_member.py` into `Assets/_Sal/` (same structure, so Resources paths still
  work): Scripts, Resources, the 3 practical scenes, link.xml, only the USED files of PracticalAssets
  (210 files, 37.7 MB); 30 of her lab-environment files resolve to `_Shared`. Her Editor tools and
  launcher scene were left out (her notes: builders can auto-install layouts); her docs are in
  `_Sal/Documentation`.
- New asmdef `VirtualLab.Sal` (her code was in Assembly-CSharp, many classes have no namespace);
  `link.xml` updated to the new assembly name (otherwise IL2CPP would strip her save classes on
  Android only).
- Integration changes (marked `INTEGRATION (Tajarbagah)`): `PracticalLauncher.Available/ReturnToMenu`
  -> AppManager_Azm.GoToMainMenu (her single return point); `OhmBench.ReturnToMenu` internal;
  `Scripts/Integration/TajarbagahBackButton.cs` (Android back -> her own saving exit, Chemistry and
  Ohm); `PracticalSafeArea` clamps an invalid safe area (the Editor reported one larger than the
  screen and her UI stretched to 125 %); AppBootstrap in all 3 scenes.
- Menu: Chemistry "Melting & boiling points" (slot chemistry 1), Physics "Ohm's law" (slot physics 4);
  build: Melting_Point_Naphthalene, Physics_14_1_Ohms_Law. Physics 8.1 (unfinished, not in her
  launcher) imported but NOT in the menu/build.
- Tested: menu -> each card -> her intro screen; one camera / listener / EventSystem; "All practicals"
  and the back-button path return to the app menu (time scale restored); 16:9/20:9/4:3 fit after the
  safe-area fix; 9 build scenes 0 missing / broken; 2 broken refs she documented (old flame material
  texture slot, calculator prefab font) are not used at runtime.

**Saleha scope (Azzam, 2026-11-24):** only Ohm's law and Melting/boiling points are hers in this
integration; Physics 8.1 (specific heat) is NOT part of it. Its code stays (Ohm's law uses its
walking controls `LabWalkRig`/`LabWalkPad` and `Resources/Physics81Audio`); its scene and 11 assets
used only by it (`F:/Integration/reports/sal_unused.txt`) are to be removed — pending Azzam's OK
(the automatic removal was blocked by the safety check). **Done:** removed with Azzam's OK
(commit 053e107); `Physics81Audio/Snap.wav` (loaded by name by Ohm's law) added.

**Stage 4b done (2026-11-24): Bareera** (branch `integrate/bareera`)
- Her project is a copy of Azzam's old project plus her work: only HER 18 scripts were imported (35
  copies of the old project's scripts left out; her scripts do not depend on them), her 2 scenes and
  the assets they use (31 files, 5.2 MB) into `Assets/_Bar`; 33 files resolve to `_Shared`/host
  copies. Her `PlanarMirror.shader` (loaded by name from `Scripts/Resources`) added after the play
  test showed it missing.
- New asmdef `VirtualLab.Bar` (her code had no namespaces) incl. URP references.
- Input: her CuSO4 and Reflection code used the old `UnityEngine.Input` (throws under the Input System
  setting): `Scripts/Integration/BarInput.cs` gives the same calls on the Input System (mouse + touch,
  first finger = left button); 13 call sites switched.
- Exits (marked `INTEGRATION (Tajarbagah)`): `Scripts/Integration/TajarbagahExit.cs` (app menu +
  Android back); `SceneNavigator` back/quit, `CuSO4PracticalSteps.EndPractical` and
  `ReflectionPractical.EndPractical` -> app menu (her menus StartScene / SubjectSelect /
  PracticalSelect are not imported); AppBootstrap in both scenes.
- Menu: Chemistry "Test for water (anhydrous CuSO4)" (slot chemistry 2), Physics "Laws of reflection"
  (slot physics 5); both scenes in the build.
- Tested: card -> intro (16:9/20:9/4:3 fit) -> CuSO4 bench renders, input adapter runs without
  errors -> End -> app menu (time scale restored); Reflection real mirror surface created; 11 build
  scenes 0 missing / broken; no console errors.

**Stage 4c done (2026-11-24): Rayyan** (branch `integrate/rayyan`)
- Imported into `Assets/_Ray` (same structure): `BiologyLab` (scripts, UI Toolkit), scene
  `Scenes/BiologyLab.unity` and only the assets it uses (44 files); 7 resolve to `_Shared`/host.
- Frog anatomy over budget (~600k tris): Muscle/Skeleton/Haslet FBX replaced by decimated versions
  (headless Blender, `F:/Integration/reports/frog_process.py`) with the original `.meta` (same GUIDs,
  prefab links intact): anatomy 112.7k tris (whole frog prefab 142k incl. skin rig). The 16 frog
  textures (TGA, 4096) replaced by PNGs: 2048 for colour/normal, 1024 for occlusion/smoothness, same GUIDs.
- His Editor postprocessor `Editor/FrogAnatomyNames.cs` IS needed (it was first left out as a tool):
  it renames the FBX parts from Chinese to English at import, and his runtime code finds parts by
  those names (`Internal Organs/Urinary Bladder` ...). Without it the internal view threw a
  NullReferenceException. Imported with the path changed to `Assets/_Ray/Models`.
- New asmdef `VirtualLab.Ray` (namespace `BiologyLab`; refs Input System, VirtualLab.Azm).
- Exits (marked `INTEGRATION`): `Scripts/Integration/TajarbagahLink.cs`; Quit buttons (`quit`,
  `internal-quit`) -> app menu instead of `Application.Quit`; Android back: inner screen -> his welcome
  screen, welcome -> app menu. AppBootstrap in the scene.
- Menu: Biology "Frog anatomy (organs and organ systems)" (slot biology 2, learn mode only);
  build scene 12: BiologyLab.
- Tested: menu -> card -> welcome; guide -> internal; all 11 internal views (muscles, skeleton,
  organs, labeling, systems, digestive, respiratory, circulatory, nervous, urogenital, study organs)
  without errors; back -> welcome -> app menu; no console errors. Not yet seen: his UI Toolkit screens
  at 20:9/4:3 (the capture tool does not draw UI Toolkit) - check by eye in the Game view / on a phone.
- Known, from his repo: `Materials/Skeleton.mat` metallic map points to a texture that is not in his
  repo either (renders without it).

## STOP GATE 4 (after each practical)

---

## STAGE 5 — Whole-app pass
1. Consistency: menu cards/thumbnails, theme, back behaviour, sounds, settings apply everywhere.
2. Performance on a mid/low-range phone (60 fps target, never below 30), APK size, load times.
3. Freeze the members' old repos (README: "moved to Tajarbagah"); everyone continues on
   branches in the host repo.

## STOP GATE 5

---

## What Claude can do vs what needs people
- Claude: clone, inventory script and report, conflict list, host setup, guide, importing and
  fixing each practical through Unity MCP, scripted play tests, captures.
- People: repo access, the Stage 1 decisions, explaining their own practicals' intended flow,
  phone testing, and approving anything that deletes or moves files.

## Open questions (answer before Stage 0)
1. The four other members' repo URLs, and are they private (need collaborator access)?
2. Which practicals each member has, and the entry scene of each.
3. Does anyone use a different Unity version, the Built-in pipeline, or Asset Store packages?
4. Has anyone already used the app shell (menu/catalog) from this project, or are theirs standalone?
