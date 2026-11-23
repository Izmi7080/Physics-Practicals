# Practical Assets

This Unity package includes models, textures, materials, prefabs, particle effects and generated apparatus in PracticalAssets, plus Resources, runtime Scripts, example Scenes and Settings. Existing asset GUIDs are retained. Editor automation and APKs are not included.

Use Unity 6000.3.24f1 with Universal Render Pipeline 17.3.0, Input System 1.20.0 and Unity UI 2.0.0. A unitypackage does not install Package Manager dependencies or replace the destination project's ProjectSettings. For the practical controls, enable the Input System in the destination project's Player settings and use a compatible URP configuration.

Import with Assets > Import Package > Custom Package, then select PracticalAssets.unitypackage. The example is Assets/Scenes/Melting_Point_Naphthalene.unity. Resources includes all five chemical definitions and the calculator prefab/UI; keep its internal paths when using those runtime features. Runtime scripts remain in Assets/Scripts.

To reuse only individual models, select their model/material/texture dependencies in Unity's import list. Keep the existing .meta files when moving assets afterward. If the destination already contains assets from the donor projects, review matching GUIDs in the import list before replacing them.

The separate PracticalAssets_UnityProject.zip is the complete project alternative, with package manifests and project settings, for opening directly through Unity Hub.
