# Tajarbagah — Integration guide (the practical contract)

Tajarbagah is ONE Unity project and ONE app. Every member's practicals live inside it, each in the
member's own folder, and are opened from the shared menu. This page is the contract a practical
must meet. Questions / changes to the shared parts: Azzam (owns the app shell and project settings).

## 0. Toolchain (everyone the same)
- Unity **6000.3.24f1**, URP 17.3, **new Input System only** (no `UnityEngine.Input.*` calls).
- Android, **landscape only**, min API 25, target API 36.
- Git with Unity Smart Merge (see §7).

## 1. Your folder, your names
| Member | Folder | Suffix | Namespace | Assembly |
|---|---|---|---|---|
| Azzam | `Assets/_Azm` | `_Azm` | `VirtualLab.Azm.*` | `VirtualLab.Azm` |
| Rayyan | `Assets/_Ray` | `_Ray` | `VirtualLab.Ray.*` | `VirtualLab.Ray` |
| Saleha | `Assets/_Sal` | `_Sal` | `VirtualLab.Sal.*` | `VirtualLab.Sal` |
| Bareera | `Assets/_Bar` | `_Bar` | `VirtualLab.Bar.*` | `VirtualLab.Bar` |
| Mujtaba | `Assets/_Muj` | `_Muj` | `VirtualLab.Muj.*` | `VirtualLab.Muj` |

(Suffixes are proposals — each member confirms theirs.)
- EVERYTHING you make goes in your folder: scripts, scenes, prefabs, materials, ScriptableObjects,
  UI, audio. Your scenes end with your suffix (`FrogAnatomy_Ray.unity`).
- Every class, enum, interface, scene, prefab and asset carries your suffix and lives in your
  namespace, so nothing can clash with someone else's.
- Your folder has an **assembly definition** (`.asmdef`). Your code compiles on its own; if you need
  something from another assembly (Input System, TextMesh Pro, the app shell) add it as a reference.
  You may NOT reference another member's assembly.

## 2. Shared assets (`Assets/_Shared`)
- Assets used by more than one practical live ONCE in `Assets/_Shared` (lab room / lab environment
  pack, common apparatus, fonts). Do not keep your own copy of anything in `_Shared`, TextMesh Pro,
  `Assets/Settings` (URP) or `Assets/Plugins` (DOTween).
- Adding to `_Shared` or changing it goes through Azzam.
- Third-party packages from the Package Manager / Asset Store: ask first (they are project-wide).

## 3. A practical plugs into the app like this
1. **One entry scene** per practical, in your folder.
2. Put the shell's **`AppBootstrap`** prefab (`Assets/_Shared/Prefabs`) in that scene. It makes the
   scene playable on its own in the Editor and connects it to the app (settings, scene loading).
3. **Leaving the practical:** call `AppManager_Azm.Instance.GoToMainMenu()`. Never
   `Application.Quit()`, never load another member's scene directly.
4. **Android back button** (arrives as Escape): back / pause inside your practical; it must never
   quit the app.
5. **Registration** (done by Azzam): a `PracticalInfo` asset (id, title, subject, short description,
   thumbnail, minutes, apparatus, scene name, Learn/Perform) is added to the catalog and your scene
   to Build Settings. Send these details with your practical.
6. Your own start/launcher/subject-select menus are NOT used in the app (the shared menu replaces
   them) — keep them only if your practical has its own internal steps menu.

## 4. Code rules (they prevent integration bugs)
- No `DontDestroyOnLoad` in practical code (only the two app managers use it).
- Scene-local singletons: set `Instance = null` in `OnDestroy`; warn (never silently destroy) on a
  duplicate.
- No `static` fields holding practical state (they survive scene loads and leak into the next run).
- Every event subscription is removed in `OnDisable` / `OnDestroy`.
- Avoid `Camera.main` / `GameObject.Find` / `FindObjectOfType` in logic; pass references in.
- Physics/measurement motion is scripted from equations, not from Rigidbody (see CLAUDE.md).

## 5. UI rules
- Canvas Scaler: Scale With Screen Size, 1920×1080, Screen Match Mode **Expand**.
- All interactive UI inside a safe-area container; touch targets ≥ 120 px at 1920×1080.
- TextMesh Pro for all text. Test at 16:9, 20:9 (2400×1080) and 4:3 (tablets).
- Recommended (not required at first): the shared theme and practical shell for a consistent look.

## 6. Mobile budgets
- 60 fps target on a mid-range phone, never below 30.
- Interactive props ≤ 5k triangles, background props ≤ 1.5k; a practical that is a single
  close-up model (e.g. the frog) agrees its own budget with Azzam (~50–120k on screen).
- Textures ≤ 1024 for props (2048 for a hero model), PNG/JPG — no 4K/8K source textures in the repo.
- No build folders, APKs or `Library/` in git (the `.gitignore` handles it).

## 7. Git workflow
- `main` always opens and builds. Work on a branch: `<suffix>/<practical>` (e.g. `ray/frog`).
- Merge through a pull request; Azzam reviews anything outside your folder.
- One owner per scene (scenes do not merge well).
- Enable Unity Smart Merge once per PC (adjust the Unity path):
  ```
  git config --global merge.unityyamlmerge.name "Unity SmartMerge"
  git config --global merge.unityyamlmerge.driver "'F:/Softwares/Unity Hub/Editors/6000.3.24f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
  ```

## 8. How your current project gets in (first integration, done with Azzam)
Your folder is copied in WITH its `.meta` files (asset IDs stay the same, so your scenes keep their
links). Files that already exist in Tajarbagah (template files, TextMesh Pro, the lab environment
pack) are skipped — your scenes then use the shared copy automatically. After that you work only in
Tajarbagah; your old repo stays as an archive.
