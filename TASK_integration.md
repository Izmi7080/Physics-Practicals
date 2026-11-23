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

## STOP GATE 3 — the pilot checklist is reviewed

---

## STAGE 4 — Remaining practicals, one at a time
Same steps as Stage 3, one branch and one merge per practical, ideally with its author present
(they know their scenes). After each merge: whole-app smoke test (menu → every practical → back).

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
