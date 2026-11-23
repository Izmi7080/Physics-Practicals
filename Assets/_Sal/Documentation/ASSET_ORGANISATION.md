# Project folders

Visual assets are collected under `Assets/PracticalAssets`:

- `Art/Environment`: laboratory models, textures and their original supporting files.
- `Art/BunsenBurner`: imported burner model and textures.
- `Art/MortarAndPestle`: imported mortar/pestle model and textures.
- `Materials/Environment` and `Materials/Shared`: existing material collections.
- `Imported`: reused apparatus, particle effects, fonts and shaders from donor projects.
- `Generated/Naphthalene`: generated apparatus meshes, materials and layout revision markers shared by the practical profiles.
- `Prefabs`: environment prefabs.

Code, scenes and configuration remain directly under `Assets`:

- `Scripts`: runtime scripts, including the reused calculator script in `Scripts/Calculator`.
- `Scenes`: open `Melting_Point_Naphthalene.unity` to run the practical.
- `Editor`: scene tools, layout migrations, build/export tools and validation.
- `Settings`: render pipeline and volume settings.
- `Resources`: experiment definitions, calculator prefab/UI assets and other assets loaded by resource key. This special folder name preserves Unity runtime loading.
- `InputSystem_Actions.inputactions` and `link.xml`: input and code-preservation configuration.

Moves used Unity's asset database. Existing metadata GUIDs were retained, and explicit editor-tool/build/documentation paths were updated. Original team projects were not changed. No APK was rebuilt.

The pre-organisation backup is in `Validation/BeforeAssetOrganisation`. Unity's reference checks are recorded in `Validation/ArtAssetOrganisationValidation.txt`. The Naphthalene menu includes **Validate organised assets** to repeat those checks while the backup remains available.
