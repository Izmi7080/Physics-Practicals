# Build and source handoff

The initial Android APK completed: version 1.1.0/code 2, ARM64, minimum Android 8.0, landscape. Its APK signature was verified. Unity also reported one imported TextMeshPro shader error: missing TMPro_Properties.cginc.

The missing include is now restored from Unity's bundled TMP Essential Resources in the main project. The incremental run passed shader compilation and reached Gradle, then was stopped at the user's explicit request. No completed replacement APK is claimed. The existing APK below predates the shader fix and may show affected imported text incorrectly. The current source and team package include the fix.

No further build is running. Test the project directly in Unity. Physical phone testing remains with the user.

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| PhysicalStatesPracticals.apk | 49873567 | 0354bcc6eb0340f5350cddc73fc61ce052a14d6cce1e053300bd5220afa897a0 |
| PhysicalStatesPracticals.unitypackage | 34671984 | 8bd1781c6e2d7f70f7aad81b0dc53b7e2edb6649253ca56a7a43a02af083dafe |

Package dependency inspection: 252 assets, all five definitions and the restored shader include present. Editor migration scripts are excluded.

Automated workflows: 455 checks passed. The initial Android build report/log remain under Validation. Actual phone behavior and measured 30 FPS remain unverified.

## Source update after this build

The current Unity source now also includes table-based apparatus, Study onboarding, action zoom hints, view joystick, refill recovery and the physics calculator beside the readings table. The APK and earlier exported package above predate these changes. No APK rebuild was performed, following the user's instruction.
