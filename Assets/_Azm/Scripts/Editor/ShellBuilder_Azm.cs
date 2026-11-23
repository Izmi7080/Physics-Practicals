using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.EditorTools
{
    /*
     * Layer: Editor tooling (not part of the shipped app).
     * Purpose: One menu command - "VirtualLab_Azm > Build Stage 1 Shell" - that creates everything
     * the app shell needs, so it is repeatable and reviewable instead of hand-clicked:
     *   - data assets: UITheme_Azm, PracticalCatalog_Azm + a PracticalInfo_Azm per practical,
     *     AssetCredits_Azm
     *   - the LabEnvironment_Azm prefab, COPIED from the room objects in the old Lab scene (the old
     *     scene and its objects are only read, never changed or saved)
     *   - the PracticalCard_Azm prefab used by the practical-select grid
     *   - three scenes (Splash_Azm, MainMenu_Azm, SimplePendulum_Azm) added to Build Settings
     *   - landscape-only orientation in Player Settings
     * It refuses to run if any of the three scenes already exists, so it can never overwrite hand
     * edits. Existing data assets/prefabs are reused, never overwritten.
     * The scene-building half lives in ShellBuilder_Azm.Scenes.cs.
     * Talks to: AssetDatabase, EditorSceneManager, PlayerSettings, EditorBuildSettings.
     */
    public static partial class ShellBuilder_Azm
    {
        private const string Root = "Assets/_Azm";
        private const string DataFolder = Root + "/Data";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string SceneFolder = Root + "/Scenes";
        private const string MaterialFolder = Root + "/Art/Materials";

        private const string OldLabScenePath = "Assets/Scenes/Lab.unity";
        private const string SplashScenePath = SceneFolder + "/Splash_Azm.unity";
        private const string MainMenuScenePath = SceneFolder + "/MainMenu_Azm.unity";
        private const string PendulumScenePath = SceneFolder + "/SimplePendulum_Azm.unity";

        [MenuItem("VirtualLab_Azm/Build Stage 1 Shell")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[ShellBuilder_Azm] Stop Play mode first.");
                return;
            }

            foreach (string scenePath in new[] { SplashScenePath, MainMenuScenePath, PendulumScenePath })
            {
                if (File.Exists(scenePath))
                {
                    Debug.LogError($"[ShellBuilder_Azm] '{scenePath}' already exists - refusing to overwrite. Delete it yourself first if you really want a rebuild.");
                    return;
                }
            }

            // --- Data assets ---
            UITheme_Azm theme = CreateOrLoad<UITheme_Azm>(DataFolder + "/UITheme_Azm.asset");
            AssetCredits_Azm credits = BuildCredits();
            PracticalCatalog_Azm catalog = BuildCatalog();

            // --- LabEnvironment_Azm: copy of the old room, made while the old Lab scene is open ---
            GameObject labPrefab = BuildLabPrefab();
            if (labPrefab == null) return;

            AssetDatabase.SaveAssets();

            // --- Scenes (each is built in its own fresh scene) ---
            BuildSplashScene(theme, catalog);
            BuildMainMenuScene(theme, catalog, credits, labPrefab);
            BuildPendulumScene(theme, catalog, labPrefab);

            ApplyBuildSettings();
            ApplyPlayerSettings();

            // Leave the main menu open for inspection.
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShellBuilder_Azm] Stage 1 shell built: 3 scenes, catalog, theme, LabEnvironment_Azm prefab.");
        }

        // Code-only (HCI restructure): REBUILDS Splash_Azm and MainMenu_Azm with the current builder
        // code (Expand canvas, new settings, button feel). Reuses the existing theme, catalog,
        // credits and LabEnvironment_Azm prefab; never touches SimplePendulum_Azm or the old Lab scene.
        public static void RebuildMenuScenesForced()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[ShellBuilder_Azm] Stop Play mode first."); return; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var theme = AssetDatabase.LoadAssetAtPath<UITheme_Azm>(DataFolder + "/UITheme_Azm.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<PracticalCatalog_Azm>(DataFolder + "/PracticalCatalog_Azm.asset");
            var credits = AssetDatabase.LoadAssetAtPath<AssetCredits_Azm>(DataFolder + "/AssetCredits_Azm.asset");
            var labPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/LabEnvironment_Azm.prefab");
            if (theme == null || catalog == null || labPrefab == null)
            {
                Debug.LogError("[ShellBuilder_Azm] Theme, catalog or LabEnvironment_Azm prefab missing - run Build Stage 1 Shell first.");
                return;
            }
            BuildSplashScene(theme, catalog);
            BuildMainMenuScene(theme, catalog, credits, labPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShellBuilder_Azm] Splash_Azm and MainMenu_Azm rebuilt.");
        }

        // --- Asset helpers ---

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static AssetCredits_Azm BuildCredits()
        {
            string path = DataFolder + "/AssetCredits_Azm.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<AssetCredits_Azm>(path) == null;
            var credits = CreateOrLoad<AssetCredits_Azm>(path);
            if (!isNew) return credits;

            credits.entries.Add(new AssetCreditEntry_Azm
            {
                assetName = "Liberation Sans (default TextMeshPro font)",
                author = "Red Hat, Inc.",
                license = "SIL OFL 1.1",
                source = "Shipped with Unity's TextMeshPro essentials",
                note = "Replace with the project's rounded UI font (Poppins or Inter, SIL OFL) once it is added."
            });
            credits.entries.Add(new AssetCreditEntry_Azm
            {
                assetName = "Lab room props (tap, water shelf)",
                author = "",
                license = "credit to be confirmed",
                source = "Assets/Flame Test (supplied with the Flame Test practical)",
                note = "Author and licence were not found in the project - confirm before release."
            });
            EditorUtility.SetDirty(credits);
            return credits;
        }

        private static PracticalCatalog_Azm BuildCatalog()
        {
            string path = DataFolder + "/PracticalCatalog_Azm.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<PracticalCatalog_Azm>(path) == null;
            var catalog = CreateOrLoad<PracticalCatalog_Azm>(path);
            if (!isNew) return catalog;

            var list = new List<PracticalInfo_Azm>();

            list.Add(MakeInfo("PracticalInfo_SimplePendulum_Azm", "physics.simple_pendulum", "Simple Pendulum",
                SubjectType_Azm.Physics,
                "Time a swinging pendulum at different lengths, then find g from your own graph.",
                20, "SimplePendulum_Azm", false, true, false,
                new[] { "Retort stand with clamp", "Thread and metal bob", "Metre scale", "Vernier calipers", "Stopwatch" }));

            // Locked placeholders - shown on the menu as "Coming soon", never hidden.
            for (int i = 2; i <= 5; i++)
                list.Add(Placeholder("Physics" + i, "physics.placeholder_" + i, "Physics Practical " + i, SubjectType_Azm.Physics));
            for (int i = 1; i <= 5; i++)
                list.Add(Placeholder("Chemistry" + i, "chemistry.placeholder_" + i, "Chemistry Practical " + i, SubjectType_Azm.Chemistry));
            for (int i = 1; i <= 5; i++)
                list.Add(Placeholder("Biology" + i, "biology.placeholder_" + i, "Biology Practical " + i, SubjectType_Azm.Biology));

            catalog.practicals = list;
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static PracticalInfo_Azm Placeholder(string fileKey, string id, string displayName, SubjectType_Azm subject)
        {
            return MakeInfo("PracticalInfo_" + fileKey + "_Azm", id, displayName, subject,
                "Coming soon.", 0, "", true, true, false, new string[0]);
        }

        private static PracticalInfo_Azm MakeInfo(string fileName, string id, string displayName, SubjectType_Azm subject,
            string description, int minutes, string sceneName, bool locked, bool learn, bool perform, string[] apparatus)
        {
            string path = DataFolder + "/Practicals/" + fileName + ".asset";
            var info = CreateOrLoad<PracticalInfo_Azm>(path);
            info.id = id;
            info.displayName = displayName;
            info.subject = subject;
            info.shortDescription = description;
            info.estimatedMinutes = minutes;
            info.sceneName = sceneName;
            info.isLocked = locked;
            info.learnModeAvailable = learn;
            info.performModeAvailable = perform;
            info.apparatus = apparatus;
            EditorUtility.SetDirty(info);
            return info;
        }

        // Copies (does not move) the old room into a prefab. SaveAsPrefabAsset writes a new asset
        // from the scene object and leaves the scene object exactly as it was.
        private static GameObject BuildLabPrefab()
        {
            string path = PrefabFolder + "/LabEnvironment_Azm.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            // Open the old scene only to read it. It is not modified and never saved.
            if (EditorSceneManager.GetActiveScene().path != OldLabScenePath)
                EditorSceneManager.OpenScene(OldLabScenePath, OpenSceneMode.Single);

            GameObject room = GameObject.Find("Environment_LabRoom");
            if (room == null)
            {
                Debug.LogError("[ShellBuilder_Azm] 'Environment_LabRoom' not found in the old Lab scene - cannot build LabEnvironment_Azm.");
                return null;
            }

            EnsureFolder(PrefabFolder);
            return PrefabUtility.SaveAsPrefabAsset(room, path);
        }

        private static void ApplyBuildSettings()
        {
            // The three shell scenes first, in the required order. The old Lab scene is kept but
            // disabled so it can never be the scene the app starts in (nothing is removed).
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(SplashScenePath, true),
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(PendulumScenePath, true),
                new EditorBuildSettingsScene(OldLabScenePath, false)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // Landscape only (left + right), no portrait - CLAUDE.md UI rules.
        private static void ApplyPlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }
    }
}
