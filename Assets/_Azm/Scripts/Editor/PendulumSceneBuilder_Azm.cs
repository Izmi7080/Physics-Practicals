using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.Instruments;
using VirtualLab.Azm.Pendulum;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.EditorTools
{
    /*
     * Layer: Editor tooling (not part of the shipped app).
     * Purpose: "VirtualLab_Azm > Build Stage 2 Pendulum Scene" builds the SimplePendulum_Azm scene:
     *   - the pendulum rig in real units on the island bench (base board, rectangular metal frame tall
     *     enough for a 100 cm thread, thread, 2 cm bob, a vertical metre scale with cm markings)
     *   - an instrument tray on the bench: the vernier caliper (the project's Blender model, upright
     *     in a small stand with a spare bob between its jaws, plus the measuring logic, interaction
     *     and view), a stopwatch with a live face, and a metre rule
     *   - the camera rig (shared PracticalCameraRig_Azm: Overview, Rig, Timing, Caliper - the Timing
     *     view follows the bob), the input and interaction objects, the controller and its config;
     *   - the screen UI from the SHARED SHELL (PracticalShellBuilder_Azm, HCI restructure Stage 4)
     *     plus the pendulum's own parts: the context area (confirm length, the big round tap button
     *     with stopwatch, next length / finish, see my result), the Data and Graph tabs with expand
     *     overlays, the Calc tab (worked calculation + calculator), the oscillation demonstration,
     *     the prediction card and the results panel with the reflection.
     * The menu item only replaces the Stage 1 placeholder scene; BuildForced (code only) always
     * rebuilds it. Existing data assets are reused; step wording is refreshed (ApplyDefaultTexts).
     * Talks to: PracticalShellBuilder_Azm, UIFactory_Azm, UIBuilder_Azm, the Pendulum runtime scripts.
     */
    public static class PendulumSceneBuilder_Azm
    {
        private const string Root = "Assets/_Azm";
        private const string ScenePath = Root + "/Scenes/SimplePendulum_Azm.unity";
        private const string ConfigPath = Root + "/Data/PendulumConfig_Azm.asset";
        private const string GuidePath = Root + "/Data/PracticalGuide_Pendulum_Azm.asset";
        private const string CatalogPath = Root + "/Data/PracticalCatalog_Azm.asset";
        private const string LabPrefabPath = Root + "/Prefabs/LabEnvironment_Azm.prefab";
        private const string MaterialFolder = Root + "/Art/Materials";
        private const string TextureFolder = Root + "/Art/Textures";
        private const string CaliperConfigPath = Root + "/Data/CaliperConfig_Azm.asset";
        private const string CreditsPath = Root + "/Data/AssetCredits_Azm.asset";
        private const string CaliperModelPath = Root + "/Art/Models/VernierCaliper/VernierCaliper_Azm.fbx";
        private const string CaliperTexturePath = Root + "/Art/Models/VernierCaliper/VernierMarkings_Azm.png";

        // The caliper parts the logic and interaction need.
        private struct CaliperParts
        {
            public Transform station;   // the stand the caliper sits in (caliper frame: local -X = opening)
            public Transform root;      // model root VernierCaliper_Azm (origin = main-scale zero)
            public Transform slider;    // model Slider_Azm
            public Collider grab;       // generous grab area around the sliding unit
        }

        // Island bench top (measured from the lab room): the rig and tray stand on it.
        private static readonly Vector3 BenchTop = new Vector3(0.23f, 0.988f, -0.15f);
        private const float PivotHeight = 1.30f;      // pivot above the rig's base (board bottom), metres

        // Camera framing (HCI restructure): the rig fits each view's frame (metres, width x height)
        // into the free scene window, on every screen shape.
        // Rig view: the frame posts (0.96 m apart) and the thread from the pivot down to a 100 cm bob.
        private static readonly Vector2 RigFrameSize = new Vector2(1.05f, 1.15f);
        // Caliper view: the beam, the jaws and the spare bob between them (same as the cylinder's).
        private static readonly Vector3 CaliperFrameCentreLocal = new Vector3(-0.085f, -0.012f, 0f);
        private static readonly Vector2 CaliperFrameSize = new Vector2(0.20f, 0.085f);
        private const float CaliperViewDistance = 0.50f;
        // Looking down ~9° so the tray's front rim does not hide the jaw tips and the bob.
        private const float CaliperViewHeight = 0.08f;

        private static UITheme_Azm theme;

        [MenuItem("VirtualLab_Azm/Build Stage 2 Pendulum Scene")]
        public static void Build()
        {
            Run(false);
        }

        // Development use from code only (no menu item): rebuilds the scene even if it was built
        // before. It REPLACES the scene file, so never call it on a scene someone has edited by hand.
        public static void BuildForced()
        {
            Run(true);
        }

        private static void Run(bool force)
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[PendulumSceneBuilder_Azm] Stop Play mode first."); return; }

            // FIRST: re-importing the caliper model can reload assets in memory, so it must happen
            // before any asset (theme, config...) is loaded and referenced by the new scene.
            PrepareCaliperImport();

            // Safety: only replace the Stage 1 placeholder scene (unless forced from code).
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!force && Object.FindFirstObjectByType<PendulumSceneStub_Azm>() == null)
            {
                Debug.LogError("[PendulumSceneBuilder_Azm] The pendulum scene no longer contains the Stage 1 placeholder, so it was already built or edited. Not overwriting it.");
                return;
            }

            // Opening a new scene unloads assets nothing references yet, so every asset the scene
            // will reference is loaded AFTER this line.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PracticalShellBuilder_Azm.LoadTheme();
            theme = PracticalShellBuilder_Azm.Theme;
            var catalog = AssetDatabase.LoadAssetAtPath<PracticalCatalog_Azm>(CatalogPath);
            var labPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabPrefabPath);
            PendulumConfig_Azm config = LoadOrCreateConfig();
            config.ApplyDefaultTexts();   // short instructions + Why? lines (HCI restructure)
            EditorUtility.SetDirty(config);
            var guide = PracticalShellBuilder_Azm.LoadOrCreate<PracticalGuide_Azm>(GuidePath, FillGuide);
            // The guide text is generated from the syllabus content below, so a rebuild refreshes it
            // (like the step wording). The ∝ sign is avoided: the app font has no glyph for it.
            FillGuide(guide);
            EditorUtility.SetDirty(guide);

            Camera camera = PracticalShellBuilder_Azm.CreateCamera();
            camera.nearClipPlane = 0.02f;
            PracticalShellBuilder_Azm.CreateLight();
            var backInput = PracticalShellBuilder_Azm.CreateBootstrap(catalog);
            PracticalShellBuilder_Azm.CreateEventSystem();

            var lab = (GameObject)PrefabUtility.InstantiatePrefab(labPrefab);
            lab.transform.position = Vector3.zero;

            // ----- input, logic, interaction -----
            var inputGO = new GameObject("Input_Azm");
            var input = inputGO.AddComponent<TouchMouseInput_Azm>();

            var controllerGO = new GameObject("PendulumController_Azm");
            var controller = controllerGO.AddComponent<PendulumController_Azm>();
            controller.config = config;

            // ----- apparatus -----
            BuildRig(controller, camera, out PendulumRigView_Azm rigView);
            BuildTray(controller, camera, out StopwatchFace_Azm stopwatchFace, out CaliperParts caliperParts);

            // ----- caliper: logic, presentation, interaction -----
            var caliperGO = new GameObject("CaliperMeasurement_Azm");
            var measurement = caliperGO.AddComponent<CaliperMeasurement_Azm>();
            measurement.config = LoadOrCreateCaliperConfig();
            measurement.objectName = "bob";
            measurement.quantitySymbol = "d";
            controller.caliper = measurement;

            var caliperView = caliperGO.AddComponent<CaliperRigView_Azm>();
            caliperView.measurement = measurement;
            caliperView.caliperRoot = caliperParts.root;
            caliperView.slider = caliperParts.slider;

            var caliperDrag = caliperGO.AddComponent<CaliperDragInteraction_Azm>();
            caliperDrag.inputSource = input;
            caliperDrag.measurement = measurement;
            caliperDrag.rig = caliperView;
            caliperDrag.sliderHit = caliperParts.grab;
            caliperDrag.viewCamera = camera;

            BuildWorldReadout(controller, rigView, camera);

            var interactionGO = new GameObject("BobInteraction_Azm");
            var bobDrag = interactionGO.AddComponent<BobDragInteraction_Azm>();
            bobDrag.inputSource = input;
            bobDrag.controller = controller;
            bobDrag.rig = rigView;
            bobDrag.viewCamera = camera;

            // ----- camera: the shared rig (views in CameraViewId_Azm order) -----
            var rig = camera.gameObject.AddComponent<PracticalCameraRig_Azm>();
            rig.viewCamera = camera;
            rig.inputSource = input;
            rig.dragOwners = new MonoBehaviour[] { bobDrag, caliperDrag };
            // Feel (Stage 6): caliper ticks / contact; the bob's ticks, release snap and centre glow
            // (the glow image is added with the screen UI).
            var caliperJuice = caliperGO.AddComponent<CaliperJuice_Azm>();
            caliperJuice.measurement = measurement;
            caliperJuice.drag = caliperDrag;
            caliperJuice.cameraRig = rig;
            var pendulumJuice = interactionGO.AddComponent<PendulumJuice_Azm>();
            pendulumJuice.controller = controller;
            pendulumJuice.bobDrag = bobDrag;
            pendulumJuice.rigView = rigView;
            pendulumJuice.viewCamera = camera;
            rig.maxYawDegrees = 28f;
            rig.maxPitchDegrees = 14f;
            rig.minZoom = 0.72f;
            rig.maxZoom = 1.25f;
            Vector3 rigCentre = new Vector3(BenchTop.x, BenchTop.y + PivotHeight - 0.57f, BenchTop.z);
            rig.views.Add(new CameraViewEntry_Azm { name = "Overview", position = new Vector3(0.6f, 2.05f, -3.7f), target = new Vector3(0.55f, 1.55f, -0.4f), fieldOfView = 52f });
            rig.views.Add(new CameraViewEntry_Azm { name = "Rig", position = rigCentre + new Vector3(0f, 0.05f, -2.0f), target = rigCentre, fieldOfView = 50f, frameSize = RigFrameSize });
            // Timing: re-aimed at the bob's rest position by the HUD whenever the length changes.
            Vector3 timingTarget = new Vector3(BenchTop.x, 1.80f, BenchTop.z);
            rig.views.Add(new CameraViewEntry_Azm { name = "Timing", position = timingTarget + new Vector3(0f, 0.05f, -1.6f), target = timingTarget, fieldOfView = 46f, frameSize = new Vector2(0.45f, 0.32f) });
            Vector3 caliperTarget = caliperParts.station.TransformPoint(CaliperFrameCentreLocal);
            rig.views.Add(new CameraViewEntry_Azm
            {
                name = "Caliper",
                position = caliperTarget + new Vector3(0f, CaliperViewHeight, -CaliperViewDistance),
                target = caliperTarget,
                fieldOfView = 38f,
                frameSize = CaliperFrameSize
            });
            rig.startView = (int)CameraViewId_Azm.Overview;   // the HUD then moves to each step's view

            // ----- screen UI -----
            BuildScreenUI(controller, rigView, rig, backInput, guide, measurement, pendulumJuice);
            AddCaliperCredit();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[PendulumSceneBuilder_Azm] Pendulum scene built on the shared shell.");
        }

        // From the syllabus document's Simple Pendulum content (TASK_menus_and_pendulum Appendix).
        private static void FillGuide(PracticalGuide_Azm g)
        {
            g.title = "Simple Pendulum: find g";
            g.aim = "Find the acceleration due to gravity g by timing a simple pendulum at different lengths and plotting T² against L.";
            g.description = "A simple pendulum is a small bob hanging from a fixed point on a light, inextensible thread. Pulled aside slightly and released, it swings back and forth under gravity. You measure how long one complete swing (the time period T) takes, and how that changes with the length.";
            g.hypothesis = "The time period depends on the length: T² is directly proportional to L, and does not depend on the bob's mass or the size of a small swing. So T² against L is a straight line through the origin.";
            g.apparatus = new[] { "Retort stand with clamp and split cork", "Light, inextensible thread (about 100–150 cm)", "Metal spherical bob", "Metre scale", "Vernier calipers", "Stopwatch" };
            g.whatYouMeasure = "The bob's diameter d (vernier calipers) → r = d/2. For at least 5 lengths ℓ: the time t for 20 oscillations, twice, and the mean. Then L = ℓ + r, T = t/20 and T². From the slope of T² against L: g = 4π² / slope.";
            g.procedure = new[]
            {
                "Clamp the thread at the pivot; let the bob hang freely.",
                "Measure the bob's diameter with vernier calipers; halve it to get r.",
                "Set a length ℓ, from the pivot to the top of the bob.",
                "Effective length: L = ℓ + r.",
                "Pull the bob aside by a small angle (≤ 15°) and release from rest - no push.",
                "Start the stopwatch as the bob passes the mean (centre) position.",
                "Count 20 full oscillations; stop the stopwatch as it passes the centre again.",
                "Record this time as t.",
                "Time it once more at the same length; average the two times.",
                "T = t / 20.",
                "Change the length and repeat, for at least 5 different lengths.",
                "Tabulate ℓ, r, L, t, T and T² for every length.",
                "Plot T² (y-axis) against L (x-axis).",
                "A straight line through the origin confirms that T² is proportional to L.",
                "g = 4π² / slope."
            };
            g.formulas = new[]
            {
                "Time period: T = t / n  (n = 20)",
                "Effective length: L = ℓ + r",
                "T² = (4π²/g) L, so T² is proportional to L",
                "g = 4π² / slope  (slope = T² / L)"
            };
            g.precautions = new[]
            {
                new GuidePrecaution_Azm { text = "Keep the angle small (≤ 15°).", why = "Beyond that the pendulum no longer behaves like simple harmonic motion." },
                new GuidePrecaution_Azm { text = "Use the effective length L = ℓ + r.", why = "The bob swings about its centre; the radius matters, especially for short pendulums." },
                new GuidePrecaution_Azm { text = "Time 20 oscillations, never one.", why = "It spreads your reaction-time error over many swings." },
                new GuidePrecaution_Azm { text = "Start and stop at the mean position.", why = "The bob moves fastest there, so the moment is easiest to judge." },
                new GuidePrecaution_Azm { text = "Release from rest - no push.", why = "A push adds energy and invalidates the simple-pendulum assumption." },
                new GuidePrecaution_Azm { text = "Avoid draughts.", why = "Air currents make the bob swing in an ellipse instead of a flat plane." },
                new GuidePrecaution_Azm { text = "Use a light, inextensible thread.", why = "A stretchy or heavy thread changes the effective length during the swing." },
                new GuidePrecaution_Azm { text = "Keep the same person timing.", why = "Any reaction-time bias stays the same for every trial." },
                new GuidePrecaution_Azm { text = "Read the scale at eye level.", why = "Avoids parallax error in the length." }
            };
            g.observationTable = "Obs. No. | ℓ (cm) | r (cm) | L = ℓ + r (cm) | n | t₁ (s) | t₂ (s) | mean t (s) | T = t/n (s) | T² (s²) - with n = 20 for every row and at least 5 rows.";
        }

        // ---------- config ----------

        private static PendulumConfig_Azm LoadOrCreateConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PendulumConfig_Azm>(ConfigPath);
            if (existing != null)
            {
                // A step list saved before Stage 3 has no MeasureDiameter step: add it after Intro,
                // keeping every other step (and any wording edited in the asset) as it is.
                if (!existing.steps.Exists(s => s.id == StepId_Azm.MeasureDiameter))
                {
                    int intro = existing.steps.FindIndex(s => s.id == StepId_Azm.Intro);
                    existing.steps.Insert(intro + 1, PendulumConfig_Azm.CreateMeasureDiameterStep());
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            var config = ScriptableObject.CreateInstance<PendulumConfig_Azm>();
            config.steps = PendulumConfig_Azm.CreateDefaultSteps();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static CaliperConfig_Azm LoadOrCreateCaliperConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CaliperConfig_Azm>(CaliperConfigPath);
            if (existing != null) return existing;
            var config = ScriptableObject.CreateInstance<CaliperConfig_Azm>();
            AssetDatabase.CreateAsset(config, CaliperConfigPath);
            return config;
        }

        // The caliper model is the project's own (built in Blender by ArtSource/build_vernier_azm.py).
        private static void AddCaliperCredit()
        {
            var credits = AssetDatabase.LoadAssetAtPath<AssetCredits_Azm>(CreditsPath);
            if (credits == null || credits.entries.Exists(e => e.assetName == "Vernier caliper model")) return;
            credits.entries.Add(new AssetCreditEntry_Azm
            {
                assetName = "Vernier caliper model",
                author = "Made for this project in Blender",
                license = "Project-owned (no third-party licence)",
                source = "ArtSource/build_vernier_azm.py",
                note = "Procedural model and graduation texture; see VernierCaliper_Azm_NOTES.md."
            });
            EditorUtility.SetDirty(credits);
        }

        // Import settings for the caliper model: the FBX's two materials are remapped to URP Lit
        // materials in Art/Materials, and the graduation texture keeps its marks sharp.
        private static void PrepareCaliperImport()
        {
            var texImporter = (TextureImporter)AssetImporter.GetAtPath(CaliperTexturePath);
            if (texImporter != null)
            {
                texImporter.sRGBTexture = true;
                texImporter.alphaIsTransparency = false;
                texImporter.mipmapEnabled = true;
                texImporter.anisoLevel = 4;
                texImporter.wrapMode = TextureWrapMode.Clamp;
                texImporter.maxTextureSize = 1024;
                texImporter.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CaliperTexturePath);

            // Same steel tint and finish on both, so the scale strips blend into the beam.
            Material steel = GetMaterial("VernierSteel_Azm", new Color(0.78f, 0.78f, 0.80f), 0.75f, 0.6f);
            Material markings = GetMaterial("VernierMarkings_Azm", Color.white, 0.75f, 0.6f, texture);

            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(CaliperModelPath);
            modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "VernierSteel_Azm"), steel);
            modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "VernierMarkings_Azm"), markings);
            modelImporter.SaveAndReimport();
        }


        // ---------- materials / primitives ----------

        private static Material GetMaterial(string name, Color color, float metallic, float smoothness, Texture2D baseMap = null)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (baseMap != null) material.SetTexture("_BaseMap", baseMap);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // ---------- the pendulum rig ----------

        private static Transform BuildRig(PendulumController_Azm controller, Camera camera, out PendulumRigView_Azm rigView)
        {
            Material metal = GetMaterial("MenuStandMetal_Azm", new Color(0.12f, 0.13f, 0.15f), 0.7f, 0.5f);
            Material wood = GetMaterial("MenuStandBoard_Azm", new Color(0.72f, 0.52f, 0.33f), 0f, 0.3f);
            Material gold = GetMaterial("MenuBobGold_Azm", new Color(0.95f, 0.75f, 0.2f), 0.9f, 0.75f);
            Material thread = GetMaterial("MenuThread_Azm", new Color(0.92f, 0.92f, 0.86f), 0f, 0.2f);
            Material scaleMat = GetMaterial("MetreScale_Azm", Color.white, 0f, 0.15f, CreateMetreScaleTexture());

            var rig = new GameObject("PendulumRig").transform;
            rig.position = BenchTop;

            // Base board and a rectangular metal frame. Inner width 0.96 m leaves room for a swing
            // dragged past the limit; height leaves the bob clear of the board at a 100 cm thread.
            Prim(rig, "Board", PrimitiveType.Cube, new Vector3(0f, 0.015f, 0f), new Vector3(1.10f, 0.03f, 0.42f), wood);
            Prim(rig, "PostLeft", PrimitiveType.Cube, new Vector3(-0.48f, 0.03f + 0.6575f, 0f), new Vector3(0.03f, 1.315f, 0.03f), metal);
            Prim(rig, "PostRight", PrimitiveType.Cube, new Vector3(0.48f, 0.03f + 0.6575f, 0f), new Vector3(0.03f, 1.315f, 0.03f), metal);
            Prim(rig, "TopBar", PrimitiveType.Cube, new Vector3(0f, PivotHeight + 0.015f, 0f), new Vector3(0.99f, 0.03f, 0.03f), metal);
            Prim(rig, "PivotClamp", PrimitiveType.Cube, new Vector3(0f, PivotHeight + 0.004f, 0f), new Vector3(0.05f, 0.012f, 0.05f), metal);

            var anchor = new GameObject("PivotAnchor").transform;
            anchor.SetParent(rig, false);
            anchor.localPosition = new Vector3(0f, PivotHeight, 0f);

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(anchor, false);

            var threadGO = Prim(pivot, "Thread", PrimitiveType.Cylinder, new Vector3(0f, -0.3f, 0f), new Vector3(0.005f, 0.3f, 0.005f), thread);
            var bobGO = Prim(pivot, "Bob", PrimitiveType.Sphere, new Vector3(0f, -0.61f, 0f), new Vector3(0.02f, 0.02f, 0.02f), gold, true);
            // A generous hit sphere (5 cm radius in world units): the real 2 cm bob is tiny on a phone.
            var hit = bobGO.GetComponent<SphereCollider>();
            hit.radius = 2.5f;

            // World-space helpers (sprites are generated at run time by PendulumRigView_Azm).
            var protractor = new GameObject("Protractor", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            protractor.transform.SetParent(rig, false);
            protractor.sortingOrder = -2;
            var ring = new GameObject("GrabRing", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            ring.transform.SetParent(rig, false);
            ring.sortingOrder = 5;

            // Metre scale: vertical, zero at the pivot, reading down the thread (behind the swing plane).
            var scaleQuad = Prim(rig, "MetreScale", PrimitiveType.Quad, new Vector3(-0.045f, PivotHeight - 0.525f, 0.04f), new Vector3(0.04f, 1.05f, 1f), scaleMat);
            BuildMetreScaleLabels(rig);

            rigView = rig.gameObject.AddComponent<PendulumRigView_Azm>();
            rigView.controller = controller;
            rigView.viewCamera = camera;
            rigView.anchor = anchor;
            rigView.pivot = pivot;
            rigView.thread = threadGO.transform;
            rigView.bob = bobGO.transform;
            rigView.bobHit = hit;
            rigView.protractor = protractor;
            rigView.grabRing = ring;
            return rig;
        }

        // Numbers every 10 cm beside the scale, on a world canvas where 1 canvas px = 1 mm.
        private static void BuildMetreScaleLabels(Transform rig)
        {
            var canvasGO = new GameObject("MetreScaleLabels", typeof(RectTransform), typeof(Canvas));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            canvasGO.transform.SetParent(rig, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvasGO.transform;
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(70f, 1050f);
            rect.localScale = Vector3.one * 0.001f;
            rect.localPosition = new Vector3(-0.069f, PivotHeight, 0.04f);

            for (int cm = 0; cm <= 100; cm += 10)
            {
                var label = UIFactory_Azm.NewText(rect, "cm" + cm, theme, cm.ToString(), 26f, ThemeColor_Azm.TextPrimary,
                    TextAlignmentOptions.Right, FontStyles.Bold);
                label.overflowMode = TextOverflowModes.Overflow;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                UIFactory_Azm.Pin(label.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(70f, 30f), new Vector2(0f, -cm * 10f));
            }
        }

        // The scale's texture: 1050 mm tall at 2 px per mm. Ticks on the right edge (next to the
        // thread): 5 mm short, 1 cm medium, 5 cm long, 10 cm longest.
        private static Texture2D CreateMetreScaleTexture()
        {
            string path = TextureFolder + "/MetreScale_Azm.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int w = 64, h = 2100;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var background = new Color(0.94f, 0.88f, 0.62f, 1f);
            var ink = new Color(0.08f, 0.08f, 0.08f, 1f);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;

            // y = 0 is the BOTTOM of the texture; the scale's zero is at the TOP.
            for (int mm = 0; mm <= 1050; mm += 5)
            {
                int row = h - 1 - Mathf.Min(mm * 2, h - 1);
                int length = mm % 100 == 0 ? 34 : mm % 50 == 0 ? 26 : mm % 10 == 0 ? 18 : 10;
                int thickness = mm % 100 == 0 ? 5 : 3;
                for (int t = -thickness / 2; t <= thickness / 2; t++)
                {
                    int y = Mathf.Clamp(row + t, 0, h - 1);
                    for (int x = w - length; x < w; x++) pixels[y * w + x] = ink;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            Directory.CreateDirectory(TextureFolder);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- instrument tray ----------

        private static void BuildTray(PendulumController_Azm controller, Camera camera, out StopwatchFace_Azm face, out CaliperParts caliper)
        {
            Material trayMat = GetMaterial("Tray_Azm", new Color(0.10f, 0.11f, 0.13f), 0.3f, 0.4f);
            Material silver = GetMaterial("TrayStopwatch_Azm", new Color(0.82f, 0.83f, 0.85f), 0.9f, 0.7f);
            Material rule = GetMaterial("TrayRule_Azm", new Color(0.88f, 0.75f, 0.45f), 0f, 0.3f);

            var tray = new GameObject("InstrumentTray").transform;
            tray.position = new Vector3(1.25f, BenchTop.y, -0.75f);

            Prim(tray, "Base", PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(0.58f, 0.02f, 0.32f), trayMat);
            Prim(tray, "RimFront", PrimitiveType.Cube, new Vector3(0f, 0.03f, -0.155f), new Vector3(0.58f, 0.03f, 0.01f), trayMat);
            Prim(tray, "RimBack", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.155f), new Vector3(0.58f, 0.03f, 0.01f), trayMat);
            Prim(tray, "RimLeft", PrimitiveType.Cube, new Vector3(-0.285f, 0.03f, 0f), new Vector3(0.01f, 0.03f, 0.32f), trayMat);
            Prim(tray, "RimRight", PrimitiveType.Cube, new Vector3(0.285f, 0.03f, 0f), new Vector3(0.01f, 0.03f, 0.32f), trayMat);

            caliper = BuildCaliperStation(tray, trayMat);

            // Stopwatch: a short silver cylinder with a live face on top.
            var watch = new GameObject("Stopwatch").transform;
            watch.SetParent(tray, false);
            watch.localPosition = new Vector3(0.06f, 0.03f, 0f);
            Prim(watch, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.008f, 0f), new Vector3(0.085f, 0.008f, 0.085f), silver);
            Prim(watch, "Button", PrimitiveType.Cylinder, new Vector3(0f, 0.022f, 0.045f), new Vector3(0.014f, 0.008f, 0.014f), silver);

            var canvasGO = new GameObject("FaceCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            canvasGO.transform.SetParent(watch, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvasGO.transform;
            rect.sizeDelta = new Vector2(150f, 70f);
            rect.localScale = Vector3.one * 0.0005f;
            rect.localPosition = new Vector3(0f, 0.0165f, 0f);
            rect.localRotation = Quaternion.Euler(90f, 0f, 0f);   // faces up

            var bg = UIFactory_Azm.NewImage(rect, "Face", theme, ThemeColor_Azm.PanelStrong);
            UIFactory_Azm.Stretch(bg.rectTransform);
            var text = UIFactory_Azm.NewText(rect, "Time", theme, "00:00.00", 36f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            text.overflowMode = TextOverflowModes.Overflow;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory_Azm.Stretch(text.rectTransform);

            face = watch.gameObject.AddComponent<StopwatchFace_Azm>();
            face.controller = controller;
            face.label = text;

            // Metre rule lying on the tray.
            Prim(tray, "MetreRule", PrimitiveType.Cube, new Vector3(0.12f, 0.025f, -0.085f), new Vector3(0.36f, 0.004f, 0.03f), rule);
        }

        // The vernier caliper standing upright at the back of the tray, scales facing the camera, with
        // a spare bob (identical to the hanging one) on a small pedestal between its outside jaws.
        // A spare bob is used rather than the hanging one so the caliper can stay in the tray and the
        // Caliper view can frame the caliper face-on and large; the hanging bob is 2 m away and would
        // need a wide, oblique view where the scales are hard to see.
        // Caliper frame (the model after FBX import): origin = main-scale zero on the fixed jaw's
        // measuring face; local -X = opening direction; +Y = up; +Z = the scale side (front).
        private static CaliperParts BuildCaliperStation(Transform tray, Material standMat)
        {
            var parts = new CaliperParts();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CaliperModelPath);
            Material bobMat = GetMaterial("MenuBobGold_Azm", new Color(0.95f, 0.75f, 0.2f), 0.9f, 0.75f);

            const float trayTop = 0.02f;           // top of the tray's base, tray local
            const float jawTipBelowZero = 0.0475f; // outside jaw tips are 47.5 mm below the scale baseline
            const float tipClearance = 0.004f;     // jaw tips float 4 mm above the tray
            var station = new GameObject("CaliperStation").transform;
            station.SetParent(tray, false);
            station.localPosition = new Vector3(-0.24f, trayTop + jawTipBelowZero + tipClearance, 0.09f);
            // Turned round so the scale side (+Z) faces the camera (world -Z) and the jaw opens
            // towards world +X (to the right on screen).
            station.localRotation = Quaternion.Euler(0f, 180f, 0f);
            parts.station = station;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(station, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            parts.root = instance.transform;
            parts.slider = instance.transform.Find("Slider_Azm");

            // Grab area: a generous box around the sliding unit (it moves with the slider).
            var grab = new GameObject("GrabArea_Azm").transform;
            grab.SetParent(parts.slider, false);
            var box = grab.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(-0.016f, -0.012f, 0f);
            box.size = new Vector3(0.06f, 0.075f, 0.03f);
            parts.grab = box;

            // Spare 2 cm bob resting against the fixed jaw, centred at mid-jaw height (27.5 mm below
            // the baseline), on a pedestal down to the tray.
            float trayY = -(jawTipBelowZero + tipClearance);   // tray top in the caliper frame
            Prim(station, "SpareBob", PrimitiveType.Sphere, new Vector3(-0.010f, -0.0275f, 0f), new Vector3(0.02f, 0.02f, 0.02f), bobMat);
            float pedestalTop = -0.0375f;
            Prim(station, "BobPedestal", PrimitiveType.Cylinder, new Vector3(-0.010f, (pedestalTop + trayY) * 0.5f, 0f),
                new Vector3(0.008f, (pedestalTop - trayY) * 0.5f, 0.008f), standMat);

            // Small stand: a post behind the fixed jaw's head (the slider never goes there) on a base.
            Prim(station, "StandPost", PrimitiveType.Cube, new Vector3(0.005f, (0.006f + trayY) * 0.5f, -0.0045f),
                new Vector3(0.006f, 0.006f - trayY, 0.004f), standMat);
            Prim(station, "StandBase", PrimitiveType.Cube, new Vector3(0.006f, trayY + 0.002f, -0.012f),
                new Vector3(0.026f, 0.004f, 0.018f), standMat);
            return parts;
        }

        // ---------- world readout label beside the bob ----------

        private static WorldReadout_Azm BuildWorldReadout(PendulumController_Azm controller, PendulumRigView_Azm rigView, Camera camera)
        {
            var go = new GameObject("WorldReadout", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(330f, 110f);
            rect.localScale = Vector3.one * 0.0008f;

            var bg = UIFactory_Azm.NewImage(rect, "Background", theme, ThemeColor_Azm.PanelStrong);
            UIFactory_Azm.Stretch(bg.rectTransform);
            var label = UIFactory_Azm.NewText(rect, "Label", theme, "", 62f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            label.overflowMode = TextOverflowModes.Overflow;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory_Azm.Stretch(label.rectTransform);

            var readout = go.AddComponent<WorldReadout_Azm>();
            readout.controller = controller;
            readout.bob = rigView.bob;
            readout.viewCamera = camera;
            readout.label = label;
            readout.labelTheme = label.GetComponent<ThemedText_Azm>();
            readout.group = go.GetComponent<CanvasGroup>();
            return readout;
        }

        // =====================================================================
        // Screen UI: the shared shell + the pendulum's own parts
        // =====================================================================

        private static void BuildScreenUI(PendulumController_Azm controller, PendulumRigView_Azm rigView, PracticalCameraRig_Azm rig,
            BackButtonInput_Azm backInput, PracticalGuide_Azm guide, CaliperMeasurement_Azm measurement, PendulumJuice_Azm juice)
        {
            var spec = new ShellSpec_Azm
            {
                viewNames = new[] { "Overview", "Rig", "Timing", "Caliper" },
                viewIcons = new[] { ThemeIcon_Azm.Overview, ThemeIcon_Azm.Rig, ThemeIcon_Azm.Timing, ThemeIcon_Azm.Caliper },
                magnifierTitle = "Magnifier: main scale (top), vernier (bottom)",
                padDecimalKey = false,
                padMainLabel = CaliperTexts_Azm.MainScaleLabel,
                padDivisionLabel = CaliperTexts_Azm.VernierLabel,
                resultsTitle = "Your result",
                resultsSize = new Vector2(1720f, 860f),
                drawerTabNames = new[] { "Data", "Graph", "Calc", "Guide" },
                drawerTabIcons = new[] { ThemeIcon_Azm.Table, ThemeIcon_Azm.Graph, ThemeIcon_Azm.Calculator, ThemeIcon_Azm.Book },
                calcTabIndex = 2,
                guideTabIndex = 3,
                defaultCalcBody = false,
                secondChip = true,
                pauseExtraButtonLabel = PendulumTexts_Azm.RestartLengthLabel,
                guideReplayDemoLabel = "Replay oscillation demo"
            };
            var shell = PracticalShellBuilder_Azm.Build(spec, measurement, rig, backInput, guide, out PendulumHUD_Azm hud);
            hud.controller = controller;
            hud.rigView = rigView;
            hud.trialChipText = shell.secondChipText;
            shell.secondChipText.text = "Trial 1 of 5";
            hud.restartLengthButton = shell.pauseExtraButton;

            // View buttons: the HUD shrinks them to icons while the drawer is open.
            hud.viewPanel = (RectTransform)hud.viewButtons[0].transform.parent;
            hud.viewButtonLayouts = new LayoutElement[hud.viewButtons.Length];
            hud.viewLabels = new TMP_Text[hud.viewButtons.Length];
            for (int i = 0; i < hud.viewButtons.Length; i++)
            {
                hud.viewButtonLayouts[i] = hud.viewButtons[i].GetComponent<LayoutElement>();
                hud.viewLabels[i] = hud.viewButtons[i].transform.Find("Label").GetComponent<TMP_Text>();
            }

            // Magnifier: the code-drawn main scale and vernier (shared with the cylinder practical).
            var scaleArea = UIFactory_Azm.NewRect("ScaleArea", shell.magnifierContent);
            UIFactory_Azm.Stretch(scaleArea);
            scaleArea.gameObject.AddComponent<RectMask2D>();
            var magnifierUI = shell.magnifier.gameObject.AddComponent<CaliperMagnifierUI_Azm>();
            magnifierUI.theme = theme;
            magnifierUI.measurement = measurement;
            magnifierUI.scaleArea = scaleArea;

            BuildContextArea(shell, hud);
            hud.drawerContent = BuildDataAndGraph(shell, hud, controller);
            hud.calcUI = BuildCalcPane(shell.calcPane, controller);
            hud.prediction = BuildPrediction(shell);
            hud.results = BuildResults(shell, controller);
            hud.demo = BuildDemo(shell);

            // Learn-mode centre glow: in the gesture coach's layer (over the scene, under the panels).
            var glow = UIFactory_Azm.NewRect("CentreGlow", shell.coach.transform);
            UIFactory_Azm.Pin(glow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150f, 150f), Vector2.zero);
            juice.centreGlow = glow.gameObject.AddComponent<Image>();
            juice.centreGlow.color = theme.GetColor(ThemeColor_Azm.Primary);
            glow.SetAsFirstSibling();

            PracticalShellBuilder_Azm.Finish(shell);
        }

        // ----- context area: left column, right of the view buttons (anchored bottom, stretches across) -----
        // The primary action sits at the RIGHT end, under the right thumb; hints stretch across above it.
        private static void BuildContextArea(Shell_Azm shell, PendulumHUD_Azm hud)
        {
            float m = theme.screenMargin;
            var context = UIFactory_Azm.NewRect("ContextArea", shell.leftColumn);
            context.anchorMin = new Vector2(0f, 0f);
            context.anchorMax = new Vector2(1f, 0f);
            context.pivot = new Vector2(0.5f, 0f);
            // Left edge is set at run time to clear the view buttons (PendulumHUD_Azm.Update).
            context.offsetMin = new Vector2(700f, m);
            context.offsetMax = new Vector2(-theme.spaceS, m + 440f);
            hud.contextRegion = context;
            var right = new Vector2(1f, 0f);

            // Intro
            hud.introGroup = PracticalShellBuilder_Azm.Group(context, "IntroGroup").gameObject;
            hud.continueButton = PracticalShellBuilder_Azm.Pinned(hud.introGroup.transform, "ContinueButton", "Continue", ThemeColor_Azm.Primary, right, right, new Vector2(460f, 130f), Vector2.zero, theme.fontTitle + 4f);

            // Set length: the hint above, Confirm length under the thumb.
            hud.lengthGroup = PracticalShellBuilder_Azm.Group(context, "LengthGroup").gameObject;
            hud.confirmLengthButton = PracticalShellBuilder_Azm.Pinned(hud.lengthGroup.transform, "ConfirmLengthButton", "Confirm length", ThemeColor_Azm.Primary, right, right, new Vector2(500f, 130f), Vector2.zero, theme.fontTitle + 4f);
            hud.lengthHintText = HintStrip(hud.lengthGroup.transform, "LengthHint", 150f);

            // Release: only the hint (the student drags the bob in the scene).
            hud.releaseGroup = PracticalShellBuilder_Azm.Group(context, "ReleaseGroup").gameObject;
            hud.releaseHintText = HintStrip(hud.releaseGroup.transform, "ReleaseHint", 0f);

            // Timing: the big round TAP button at the right with the stopwatch above it; Restart run and
            // the demo replay to its left.
            hud.timingGroup = PracticalShellBuilder_Azm.Group(context, "TimingGroup").gameObject;
            var timing = hud.timingGroup.transform;
            const float tap = 290f;
            var tapPosition = new Vector2(-theme.spaceS, 10f);
            // The ripple ring sits behind the tap button (same size and place).
            var ripple = UIFactory_Azm.NewImage(timing, "TapRipple", theme, ThemeColor_Azm.Primary, false);
            var rippleTheme = ripple.GetComponent<ThemedImage_Azm>();
            rippleTheme.circle = true;
            rippleTheme.Apply();
            ripple.raycastTarget = false;
            UIFactory_Azm.Pin(ripple.rectTransform, right, right, new Vector2(tap, tap), tapPosition);
            ripple.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ripple.rectTransform.anchoredPosition = tapPosition + new Vector2(-tap * 0.5f, tap * 0.5f);
            ripple.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            hud.tapRipple = ripple;

            hud.tapButton = UIFactory_Azm.NewButton(timing, "TapButton", theme, null, ThemeColor_Azm.Primary, new Vector2(tap, tap), theme.fontTitle, true);
            UIFactory_Azm.Pin(hud.tapButton.GetComponent<RectTransform>(), right, right, new Vector2(tap, tap), tapPosition);
            hud.tapCountText = UIFactory_Azm.NewText(hud.tapButton.transform, "Count", theme, "0 / 20", 84f, ThemeColor_Azm.OnPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            hud.tapCountText.overflowMode = TextOverflowModes.Overflow;
            UIFactory_Azm.Pin(hud.tapCountText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(270f, 110f), new Vector2(0f, 26f));
            hud.tapCaptionText = UIFactory_Azm.NewText(hud.tapButton.transform, "Caption", theme, PendulumTexts_Azm.TapToStart, theme.fontCaption + 2f, ThemeColor_Azm.OnPrimary, TextAlignmentOptions.Center);
            UIFactory_Azm.Pin(hud.tapCaptionText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230f, 70f), new Vector2(0f, -56f));

            var stopwatch = UIFactory_Azm.NewImage(timing, "StopwatchPill", theme, ThemeColor_Azm.PanelStrong);
            UIFactory_Azm.Pin(stopwatch.rectTransform, right, new Vector2(0.5f, 0f), new Vector2(270f, 84f), new Vector2(-theme.spaceS - tap * 0.5f, 10f + tap + theme.spaceS));
            hud.stopwatchText = UIFactory_Azm.NewText(stopwatch.transform, "Time", theme, "00:00.00", theme.fontDisplay * 0.82f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            hud.stopwatchText.overflowMode = TextOverflowModes.Overflow;
            UIFactory_Azm.Stretch(hud.stopwatchText.rectTransform);

            float sideX = -(theme.spaceS + tap + theme.spaceM);
            hud.restartRunButton = PracticalShellBuilder_Azm.Pinned(timing, "RestartRunButton", "Restart run", ThemeColor_Azm.Secondary, right, right, new Vector2(260f, 110f), new Vector2(sideX, 10f), theme.fontBody);
            hud.demoReplayButton = PracticalShellBuilder_Azm.Pinned(timing, "DemoReplayButton", PendulumTexts_Azm.DemoReplay, ThemeColor_Azm.Secondary, right, right, new Vector2(260f, 110f), new Vector2(sideX, 10f + 110f + theme.spaceS), theme.fontLabel);
            AutoSizeLabel(hud.demoReplayButton, theme.fontCaption - 2f, theme.fontLabel);

            // Record: Next length / Finish & Analyse (bottom-right).
            var record = PracticalShellBuilder_Azm.Group(context, "RecordGroup");
            hud.recordGroup = record.gameObject;
            var recordLayout = record.gameObject.AddComponent<HorizontalLayoutGroup>();
            recordLayout.spacing = theme.spaceS;
            recordLayout.childAlignment = TextAnchor.LowerRight;
            recordLayout.childControlWidth = recordLayout.childControlHeight = true;
            recordLayout.childForceExpandWidth = recordLayout.childForceExpandHeight = false;
            hud.nextLengthButton = UIFactory_Azm.NewButton(record, "NextLengthButton", theme, "Next length", ThemeColor_Azm.Secondary, new Vector2(300f, 130f), theme.fontTitle - 2f);
            hud.finishButton = UIFactory_Azm.NewButton(record, "FinishButton", theme, "Finish & Analyse", ThemeColor_Azm.Primary, new Vector2(370f, 130f), theme.fontTitle - 2f);

            // Analyse
            hud.analyseGroup = PracticalShellBuilder_Azm.Group(context, "AnalyseGroup").gameObject;
            hud.showResultsButton = PracticalShellBuilder_Azm.Pinned(hud.analyseGroup.transform, "ShowResultsButton", "See my result", ThemeColor_Azm.Primary, right, right, new Vector2(480f, 130f), Vector2.zero, theme.fontTitle + 4f);

            foreach (var g in new[] { hud.introGroup, hud.lengthGroup, hud.releaseGroup, hud.timingGroup, hud.recordGroup, hud.analyseGroup })
                g.SetActive(false);
        }

        // A hint strip across the context area (stretches with it, so it fits beside the open drawer).
        private static TMP_Text HintStrip(Transform parent, string name, float y)
        {
            var strip = UIFactory_Azm.NewImage(parent, name, theme, ThemeColor_Azm.PanelStrong);
            var rect = strip.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(0f, y);
            rect.offsetMax = new Vector2(0f, y + 100f);
            var label = UIFactory_Azm.NewText(strip.transform, "Text", theme, "", theme.fontLabel, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = theme.fontCaption - 2f;
            label.fontSizeMax = theme.fontLabel;
            UIFactory_Azm.Stretch(label.rectTransform, theme.spaceM, 6f, theme.spaceM, 6f);
            return label;
        }

        private static void AutoSizeLabel(Button button, float min, float max)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = max;
        }

        // ----- Data and Graph tabs (+ their full-screen expand overlays) -----
        private static DrawerContentUI_Azm BuildDataAndGraph(Shell_Azm shell, PendulumHUD_Azm hud, PendulumController_Azm controller)
        {
            var dataPane = shell.panes[0];
            var graphPane = shell.panes[1];
            var content = hud.drawer.gameObject.AddComponent<DrawerContentUI_Azm>();
            content.theme = theme;
            content.controller = controller;
            content.toast = hud.toast;
            content.confirm = hud.confirm;

            // Data
            var dataHeader = UIFactory_Azm.NewText(dataPane, "Constants", theme, "", theme.fontLabel, ThemeColor_Azm.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            dataHeader.enableAutoSizing = true;
            dataHeader.fontSizeMin = theme.fontCaption - 4f;
            dataHeader.fontSizeMax = theme.fontLabel;
            UIFactory_Azm.Pin(dataHeader.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(700f, 50f), Vector2.zero);
            var compact = BuildTable(dataPane, controller, false, new Vector2(720f, 0f), new Vector2(0f, -56f), 0f, 110f, out TMP_Text _);
            var expandData = PracticalShellBuilder_Azm.Pinned(dataPane, "ExpandData", "Expand table", ThemeColor_Azm.Secondary, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(340f, 96f), Vector2.zero, theme.fontBody);

            // Graph
            var graph = NewGraph(graphPane, "Graph", false);
            var graphRect = graph.rectTransform;
            graphRect.anchorMin = new Vector2(0f, 0f);
            graphRect.anchorMax = new Vector2(1f, 1f);
            graphRect.offsetMin = new Vector2(0f, 330f);
            graphRect.offsetMax = Vector2.zero;
            var graphStats = UIFactory_Azm.NewText(graphPane, "Stats", theme, "", theme.fontCaption + 2f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft);
            graphStats.rectTransform.anchorMin = new Vector2(0f, 0f);
            graphStats.rectTransform.anchorMax = new Vector2(1f, 0f);
            graphStats.rectTransform.pivot = new Vector2(0.5f, 0f);
            graphStats.rectTransform.offsetMin = new Vector2(0f, 110f);
            graphStats.rectTransform.offsetMax = new Vector2(0f, 320f);
            var expandGraph = PracticalShellBuilder_Azm.Pinned(graphPane, "ExpandGraph", "Expand graph", ThemeColor_Azm.Secondary, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(340f, 96f), Vector2.zero, theme.fontBody);

            // Expanded overlays: above the drawer, under the results panel and the dialogs.
            int above = shell.resultsPanel.transform.GetSiblingIndex();
            var dataOverlay = PracticalShellBuilder_Azm.MakeOverlay(shell.safe, "DataExpandOverlay", ThemeColor_Azm.PanelStrong, true, SlideDirection_Azm.FromBottom);
            dataOverlay.transform.SetSiblingIndex(above);
            UIFactory_Azm.Stretch(dataOverlay.GetComponent<RectTransform>(), 60f, 40f, 60f, 40f);
            var dataClose = OverlayClose(dataOverlay.transform);
            var dataTitle = UIFactory_Azm.NewText(dataOverlay.transform, "Title", theme, "Observation table", theme.fontDisplay, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIFactory_Azm.Pin(dataTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(900f, 110f), new Vector2(40f, -30f));
            var fullHeader = UIFactory_Azm.NewText(dataOverlay.transform, "Constants", theme, "", theme.fontBody, ThemeColor_Azm.Accent, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIFactory_Azm.Pin(fullHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1200f, 60f), new Vector2(40f, -140f));
            var full = BuildTable(dataOverlay.transform, controller, true, new Vector2(-80f, 0f), new Vector2(0f, -210f), 40f, 40f, out TMP_Text _);

            var graphOverlay = PracticalShellBuilder_Azm.MakeOverlay(shell.safe, "GraphExpandOverlay", ThemeColor_Azm.PanelStrong, true, SlideDirection_Azm.FromBottom);
            graphOverlay.transform.SetSiblingIndex(above + 1);
            UIFactory_Azm.Stretch(graphOverlay.GetComponent<RectTransform>(), 60f, 40f, 60f, 40f);
            var graphClose = OverlayClose(graphOverlay.transform);
            var graphTitle = UIFactory_Azm.NewText(graphOverlay.transform, "Title", theme, "T² against L", theme.fontDisplay, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIFactory_Azm.Pin(graphTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(900f, 110f), new Vector2(40f, -30f));
            var bigGraph = NewGraph(graphOverlay.transform, "BigGraph", true);
            var bigRect = bigGraph.rectTransform;
            bigRect.anchorMin = Vector2.zero;
            bigRect.anchorMax = Vector2.one;
            bigRect.offsetMin = new Vector2(40f, 40f);
            bigRect.offsetMax = new Vector2(-560f, -150f);
            var bigStats = UIFactory_Azm.NewText(graphOverlay.transform, "Stats", theme, "", theme.fontBody, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft);
            UIFactory_Azm.Pin(bigStats.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(500f, 700f), new Vector2(-30f, -170f));

            content.dataHeaderText = dataHeader;
            content.compactTable = compact;
            content.dataExpandButton = expandData;
            content.dataExpandOverlay = dataOverlay;
            content.fullTable = full;
            content.fullTableHeaderText = fullHeader;
            content.dataExpandCloseButton = dataClose;
            content.graph = graph;
            content.graphStatsText = graphStats;
            content.graphExpandButton = expandGraph;
            content.graphExpandOverlay = graphOverlay;
            content.bigGraph = bigGraph;
            content.bigGraphStatsText = bigStats;
            content.graphExpandCloseButton = graphClose;
            return content;
        }

        private static Button OverlayClose(Transform overlay)
        {
            var close = UIFactory_Azm.NewIconButton(overlay, "Close", theme, ThemeIcon_Azm.Close, ThemeColor_Azm.Secondary, new Vector2(110f, 110f));
            UIFactory_Azm.Pin(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(110f, 110f), new Vector2(-30f, -30f));
            return close;
        }

        // ----- Calc tab: the worked calculation for one row + a simple calculator (4.6) -----
        private static PendulumCalcUI_Azm BuildCalcPane(RectTransform pane, PendulumController_Azm controller)
        {
            var calc = pane.gameObject.AddComponent<PendulumCalcUI_Azm>();
            calc.controller = controller;

            // Row selector: ‹ Row 3 ›
            calc.previousRowButton = UIFactory_Azm.NewIconButton(pane, "PreviousRow", theme, ThemeIcon_Azm.ChevronLeft, ThemeColor_Azm.Secondary, new Vector2(96f, 96f));
            UIFactory_Azm.Pin(calc.previousRowButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, 96f), Vector2.zero);
            calc.nextRowButton = UIFactory_Azm.NewIconButton(pane, "NextRow", theme, ThemeIcon_Azm.ChevronRight, ThemeColor_Azm.Secondary, new Vector2(96f, 96f));
            UIFactory_Azm.Pin(calc.nextRowButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(96f, 96f), Vector2.zero);
            calc.rowLabel = UIFactory_Azm.NewText(pane, "RowLabel", theme, "", theme.fontTitle, ThemeColor_Azm.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory_Azm.Pin(calc.rowLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(420f, 96f), Vector2.zero);

            calc.body = UIFactory_Azm.NewText(pane, "Body", theme, "", theme.fontBody - 2f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft);
            calc.body.overflowMode = TextOverflowModes.Overflow;
            calc.body.gameObject.AddComponent<TypeInText_Azm>();   // a revealed line types in (Stage 6)
            var bodyTheme = calc.body.GetComponent<ThemedText_Azm>();
            bodyTheme.scalesWithLargeText = true;
            bodyTheme.baseFontSize = theme.fontBody - 2f;
            UIFactory_Azm.Stretch(calc.body.rectTransform, 10f, 120f, 10f, 120f);

            calc.revealButton = PracticalShellBuilder_Azm.Pinned(pane, "RevealButton", PendulumTexts_Azm.CalcNextLine, ThemeColor_Azm.Primary, Vector2.zero, Vector2.zero, new Vector2(340f, 104f), Vector2.zero, theme.fontBody);
            calc.calculatorButton = PracticalShellBuilder_Azm.Pinned(pane, "CalculatorButton", PendulumTexts_Azm.CalculatorButton, ThemeColor_Azm.Secondary, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(300f, 104f), Vector2.zero, theme.fontBody);

            calc.calculatorPanel = BuildCalculator(pane, out calc.calculatorCloseButton);
            return calc;
        }

        // A plain immediate-execution calculator over the Calc pane (C ⌫ √ x² / 7 8 9 ÷ / 4 5 6 × / 1 2 3 − / 0 . π + / =).
        private static PanelTransition_Azm BuildCalculator(RectTransform pane, out Button close)
        {
            var panel = PracticalShellBuilder_Azm.MakeOverlay(pane, "Calculator", ThemeColor_Azm.PanelStrong, true, SlideDirection_Azm.FromBottom);
            var rect = (RectTransform)panel.transform;
            UIFactory_Azm.Stretch(rect);
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // taps stay on the calculator
            var ui = panel.gameObject.AddComponent<CalculatorUI_Azm>();

            close = UIFactory_Azm.NewIconButton(rect, "Close", theme, ThemeIcon_Azm.Close, ThemeColor_Azm.Secondary, new Vector2(96f, 96f));
            UIFactory_Azm.Pin(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(96f, 96f), new Vector2(-12f, -12f));

            var screen = UIFactory_Azm.NewImage(rect, "Screen", theme, ThemeColor_Azm.Secondary);
            var screenRect = screen.rectTransform;
            screenRect.anchorMin = new Vector2(0f, 1f);
            screenRect.anchorMax = new Vector2(1f, 1f);
            screenRect.pivot = new Vector2(0.5f, 1f);
            screenRect.offsetMin = new Vector2(16f, -136f);
            screenRect.offsetMax = new Vector2(-124f, -12f);
            ui.expression = UIFactory_Azm.NewText(screenRect, "Expression", theme, "", theme.fontCaption, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.TopRight);
            UIFactory_Azm.Stretch(ui.expression.rectTransform, 16f, 70f, 16f, 6f);
            ui.display = UIFactory_Azm.NewText(screenRect, "Display", theme, "0", theme.fontDisplay, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.BottomRight, FontStyles.Bold);
            ui.display.enableAutoSizing = true;
            ui.display.fontSizeMin = theme.fontBody;
            ui.display.fontSizeMax = theme.fontDisplay;
            UIFactory_Azm.Stretch(ui.display.rectTransform, 16f, 6f, 16f, 40f);

            var keys = UIFactory_Azm.NewRect("Keys", rect);
            UIFactory_Azm.Pin(keys, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(636f, 516f), new Vector2(0f, 16f));
            var grid = keys.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150f, 76f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            var size = new Vector2(150f, 76f);
            float font = theme.fontTitle;
            ui.clearButton = UIFactory_Azm.NewButton(keys, "KeyClear", theme, "C", ThemeColor_Azm.Accent, size, font);
            ui.backspaceButton = UIFactory_Azm.NewButton(keys, "KeyBack", theme, "⌫", ThemeColor_Azm.Secondary, size, font);
            ui.rootButton = UIFactory_Azm.NewButton(keys, "KeyRoot", theme, "√", ThemeColor_Azm.Secondary, size, font);
            ui.squareButton = UIFactory_Azm.NewButton(keys, "KeySquare", theme, "x²", ThemeColor_Azm.Secondary, size, font);
            ui.digitButtons = new Button[10];
            ui.digitButtons[7] = UIFactory_Azm.NewButton(keys, "Key7", theme, "7", ThemeColor_Azm.Panel, size, font);
            ui.digitButtons[8] = UIFactory_Azm.NewButton(keys, "Key8", theme, "8", ThemeColor_Azm.Panel, size, font);
            ui.digitButtons[9] = UIFactory_Azm.NewButton(keys, "Key9", theme, "9", ThemeColor_Azm.Panel, size, font);
            ui.divideButton = UIFactory_Azm.NewButton(keys, "KeyDivide", theme, "÷", ThemeColor_Azm.Secondary, size, font);
            ui.digitButtons[4] = UIFactory_Azm.NewButton(keys, "Key4", theme, "4", ThemeColor_Azm.Panel, size, font);
            ui.digitButtons[5] = UIFactory_Azm.NewButton(keys, "Key5", theme, "5", ThemeColor_Azm.Panel, size, font);
            ui.digitButtons[6] = UIFactory_Azm.NewButton(keys, "Key6", theme, "6", ThemeColor_Azm.Panel, size, font);
            ui.multiplyButton = UIFactory_Azm.NewButton(keys, "KeyMultiply", theme, "×", ThemeColor_Azm.Secondary, size, font);
            ui.digitButtons[1] = UIFactory_Azm.NewButton(keys, "Key1", theme, "1", ThemeColor_Azm.Panel, size, font);
            ui.digitButtons[2] = UIFactory_Azm.NewButton(keys, "Key2", theme, "2", ThemeColor_Azm.Panel, size, font);
            ui.digitButtons[3] = UIFactory_Azm.NewButton(keys, "Key3", theme, "3", ThemeColor_Azm.Panel, size, font);
            ui.subtractButton = UIFactory_Azm.NewButton(keys, "KeySubtract", theme, "−", ThemeColor_Azm.Secondary, size, font);
            ui.digitButtons[0] = UIFactory_Azm.NewButton(keys, "Key0", theme, "0", ThemeColor_Azm.Panel, size, font);
            ui.pointButton = UIFactory_Azm.NewButton(keys, "KeyPoint", theme, ".", ThemeColor_Azm.Panel, size, font);
            ui.piButton = UIFactory_Azm.NewButton(keys, "KeyPi", theme, "π", ThemeColor_Azm.Secondary, size, font);
            ui.addButton = UIFactory_Azm.NewButton(keys, "KeyAdd", theme, "+", ThemeColor_Azm.Secondary, size, font);
            // '=' spans the last row (outside the grid).
            ui.equalsButton = PracticalShellBuilder_Azm.Pinned(rect, "KeyEquals", "=", ThemeColor_Azm.Primary, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(636f, 76f), new Vector2(0f, 16f), font);
            // The grid sits above '='.
            keys.anchoredPosition = new Vector2(0f, 16f + 76f + 12f);
            keys.sizeDelta = new Vector2(636f, 5 * 76f + 4 * 12f);
            return panel;
        }

        // ----- Learn mode: the prediction card (4.4), top of the left column -----
        private static PredictionUI_Azm BuildPrediction(Shell_Azm shell)
        {
            float m = theme.screenMargin;
            var card = PracticalShellBuilder_Azm.MakeOverlay(shell.leftColumn, "PredictionCard", ThemeColor_Azm.PanelStrong, true, SlideDirection_Azm.FromTop);
            var rect = (RectTransform)card.transform;
            UIFactory_Azm.Pin(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1040f, 320f), new Vector2(0f, -(m + 120f + theme.spaceL)));
            var clamp = card.gameObject.AddComponent<ClampWidth_Azm>();
            clamp.maxWidth = 1040f;
            clamp.sideMargin = theme.spaceS;
            var ui = card.gameObject.AddComponent<PredictionUI_Azm>();
            var tag = UIFactory_Azm.NewText(rect, "Tag", theme, "PREDICT", theme.fontCaption, ThemeColor_Azm.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            tag.characterSpacing = 4f;
            UIFactory_Azm.Pin(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(400f, 36f), new Vector2(theme.spaceL, -theme.spaceS));
            ui.questionText = UIFactory_Azm.NewText(rect, "Question", theme, "", theme.fontTitle - 4f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            ui.questionText.enableAutoSizing = true;
            ui.questionText.fontSizeMin = theme.fontLabel;
            ui.questionText.fontSizeMax = theme.fontTitle - 4f;
            UIFactory_Azm.Stretch(ui.questionText.rectTransform, theme.spaceL, 150f, theme.spaceL, 56f);

            var row = UIFactory_Azm.NewRect("Choices", rect);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.offsetMin = new Vector2(theme.spaceL, theme.spaceM);
            row.offsetMax = new Vector2(-theme.spaceL, theme.spaceM + 112f);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = theme.spaceS;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            ui.largerButton = UIFactory_Azm.NewButton(row, "Larger", theme, PendulumTexts_Azm.PredictLarger, ThemeColor_Azm.Secondary, new Vector2(280f, 112f), theme.fontBody);
            ui.smallerButton = UIFactory_Azm.NewButton(row, "Smaller", theme, PendulumTexts_Azm.PredictSmaller, ThemeColor_Azm.Secondary, new Vector2(280f, 112f), theme.fontBody);
            ui.sameButton = UIFactory_Azm.NewButton(row, "Same", theme, PendulumTexts_Azm.PredictSame, ThemeColor_Azm.Secondary, new Vector2(280f, 112f), theme.fontBody);
            card.gameObject.SetActive(false);   // shown by PendulumHUD_Azm (Learn mode, from the 2nd length)
            return ui;
        }

        // ----- Learn mode: "What is ONE oscillation?" demonstration (4.3), a centred card -----
        private static OscillationDemoUI_Azm BuildDemo(Shell_Azm shell)
        {
            var scrim = PracticalShellBuilder_Azm.MakeScrim(shell.safe, "OscillationDemo");
            // Under the pause menu and the dialogs.
            scrim.transform.SetSiblingIndex(shell.canvas.transform.Find("SafeArea/PausePanel").GetSiblingIndex());
            scrim.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // nothing behind is tappable
            var box = PracticalShellBuilder_Azm.AbsorbingBox(scrim.transform, new Vector2(1400f, 800f));
            var demo = scrim.gameObject.AddComponent<OscillationDemoUI_Azm>();

            var title = UIFactory_Azm.NewText(box.transform, "Title", theme, PendulumTexts_Azm.DemoTitle, theme.fontDisplay, ThemeColor_Azm.Primary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIFactory_Azm.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1200f, 100f), new Vector2(50f, -30f));

            // Stage (left): pivot at its top centre; children are anchored at its centre (see Place()).
            var stageBg = UIFactory_Azm.NewImage(box.transform, "Stage", theme, ThemeColor_Azm.Panel);
            demo.stage = stageBg.rectTransform;
            UIFactory_Azm.Pin(demo.stage, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(620f, 620f), new Vector2(50f, 40f));
            var centre = new Vector2(0.5f, 0.5f);
            demo.centreMarker = UIFactory_Azm.NewImage(demo.stage, "CentreLine", theme, ThemeColor_Azm.TextSecondary, false);
            UIFactory_Azm.Pin(demo.centreMarker.rectTransform, centre, new Vector2(0.5f, 1f), new Vector2(4f, 620f * 0.72f + 60f), new Vector2(0f, 300f));
            demo.centreMarker.raycastTarget = false;
            var dot = UIFactory_Azm.NewImage(demo.stage, "TraceDot", theme, ThemeColor_Azm.Accent, false);
            var dotTheme = dot.GetComponent<ThemedImage_Azm>();
            dotTheme.circle = true;
            dotTheme.Apply();
            dot.raycastTarget = false;
            UIFactory_Azm.Pin(dot.rectTransform, centre, centre, new Vector2(12f, 12f), Vector2.zero);
            demo.traceDotTemplate = dot;
            var thread = UIFactory_Azm.NewImage(demo.stage, "Thread", theme, ThemeColor_Azm.TextPrimary, false);
            thread.raycastTarget = false;
            UIFactory_Azm.Pin(thread.rectTransform, centre, new Vector2(0.5f, 1f), new Vector2(4f, 400f), new Vector2(0f, 300f));
            demo.thread = thread.rectTransform;
            var bob = UIFactory_Azm.NewImage(demo.stage, "Bob", theme, ThemeColor_Azm.Primary, false);
            var bobTheme = bob.GetComponent<ThemedImage_Azm>();
            bobTheme.circle = true;
            bobTheme.Apply();
            bob.raycastTarget = false;
            UIFactory_Azm.Pin(bob.rectTransform, centre, centre, new Vector2(64f, 64f), Vector2.zero);
            demo.bob = bob.rectTransform;
            var pivotBar = UIFactory_Azm.NewImage(demo.stage, "Support", theme, ThemeColor_Azm.TextSecondary, false);
            UIFactory_Azm.Pin(pivotBar.rectTransform, centre, new Vector2(0.5f, 0f), new Vector2(160f, 10f), new Vector2(0f, 300f));

            // Right: the oscillation counter and the caption for the current part of the swing.
            var counterCaption = UIFactory_Azm.NewText(box.transform, "CounterCaption", theme, "Oscillations counted", theme.fontLabel, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.TopLeft);
            UIFactory_Azm.Pin(counterCaption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(620f, 44f), new Vector2(720f, -150f));
            demo.counterText = UIFactory_Azm.NewText(box.transform, "Counter", theme, "0", theme.fontDisplay * 2f, ThemeColor_Azm.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            demo.counterText.overflowMode = TextOverflowModes.Overflow;
            UIFactory_Azm.Pin(demo.counterText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, 130f), new Vector2(720f, -196f));
            demo.captionText = UIFactory_Azm.NewText(box.transform, "Caption", theme, "", theme.fontTitle - 2f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var captionTheme = demo.captionText.GetComponent<ThemedText_Azm>();
            captionTheme.scalesWithLargeText = true;
            captionTheme.baseFontSize = theme.fontTitle - 2f;
            demo.captionText.enableAutoSizing = true;
            demo.captionText.fontSizeMin = theme.fontBody - 4f;
            demo.captionText.fontSizeMax = theme.fontTitle - 2f;
            UIFactory_Azm.Pin(demo.captionText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(620f, 300f), new Vector2(720f, -350f));
            demo.gotItButton = PracticalShellBuilder_Azm.Pinned(box.transform, "GotItButton", PendulumTexts_Azm.DemoGotIt, ThemeColor_Azm.Primary, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(360f, 120f), new Vector2(-50f, 40f), theme.fontTitle + 4f);

            scrim.gameObject.SetActive(false);   // opened before the first timing run (Learn) or on demand
            return demo;
        }

        // ----- results: values left, graph + reflection right, Retry / Main menu bottom-left -----
        private static ResultsPanelUI_Azm BuildResults(Shell_Azm shell, PendulumController_Azm controller)
        {
            var panel = shell.resultsPanel;
            var rect = (RectTransform)panel.transform;
            rect.anchoredPosition = new Vector2(0f, -60f);
            var topLeft = new Vector2(0f, 1f);

            var g = UIFactory_Azm.NewText(rect, "G", theme, "g = –", theme.fontDisplay * 1.9f, ThemeColor_Azm.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            g.overflowMode = TextOverflowModes.Overflow;
            UIFactory_Azm.Pin(g.rectTransform, topLeft, topLeft, new Vector2(740f, 140f), new Vector2(50f, -140f));
            var error = UIFactory_Azm.NewText(rect, "Error", theme, "", theme.fontTitle, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            error.enableAutoSizing = true;
            error.fontSizeMin = theme.fontLabel;
            error.fontSizeMax = theme.fontTitle;
            UIFactory_Azm.Pin(error.rectTransform, topLeft, topLeft, new Vector2(740f, 64f), new Vector2(50f, -290f));
            var detail = UIFactory_Azm.NewText(rect, "Detail", theme, "", theme.fontBody, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.TopLeft);
            var detailTheme = detail.GetComponent<ThemedText_Azm>();
            detailTheme.scalesWithLargeText = true;
            detailTheme.baseFontSize = theme.fontBody;
            detail.enableAutoSizing = true;
            detail.fontSizeMin = theme.fontCaption;
            detail.fontSizeMax = theme.fontBody;
            UIFactory_Azm.Pin(detail.rectTransform, topLeft, topLeft, new Vector2(740f, 250f), new Vector2(50f, -370f));

            // Retry / Main menu: bottom-left, side by side (the right column holds the reflection).
            var retry = (RectTransform)shell.retryButton.transform;
            UIFactory_Azm.Pin(retry, Vector2.zero, Vector2.zero, new Vector2(350f, 120f), new Vector2(50f, 40f));
            var menu = (RectTransform)shell.menuButton.transform;
            UIFactory_Azm.Pin(menu, Vector2.zero, Vector2.zero, new Vector2(350f, 120f), new Vector2(50f + 350f + theme.spaceM, 40f));

            // Right column: graph thumbnail, then the Learn-mode reflection under it.
            var thumb = NewGraph(rect, "GraphThumbnail", true);
            UIFactory_Azm.Pin(thumb.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(860f, 340f), new Vector2(-50f, -30f));
            thumb.labelFontSize = theme.fontCaption;
            thumb.marginLeft = 120f;
            thumb.marginBottom = 80f;

            var reflectionRect = UIFactory_Azm.NewRect("Reflection", rect);
            UIFactory_Azm.Pin(reflectionRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(860f, 450f), new Vector2(-50f, -385f));
            var reflection = reflectionRect.gameObject.AddComponent<ReflectionUI_Azm>();
            reflection.questionText = UIFactory_Azm.NewText(reflectionRect, "Question", theme, "", theme.fontBody, ThemeColor_Azm.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory_Azm.Pin(reflection.questionText.rectTransform, topLeft, topLeft, new Vector2(860f, 50f), Vector2.zero);
            var options = UIFactory_Azm.NewRect("Options", reflectionRect);
            UIFactory_Azm.Pin(options, topLeft, topLeft, new Vector2(860f, 280f), new Vector2(0f, -58f));
            var grid = options.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(422f, 84f);
            grid.spacing = new Vector2(16f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            reflection.optionButtons = new Button[PendulumTexts_Azm.ReflectionOptions.Length];
            for (int i = 0; i < reflection.optionButtons.Length; i++)
            {
                reflection.optionButtons[i] = UIFactory_Azm.NewButton(options, "Option" + i, theme, PendulumTexts_Azm.ReflectionOptions[i], ThemeColor_Azm.Secondary, new Vector2(422f, 84f), theme.fontLabel);
                AutoSizeLabel(reflection.optionButtons[i], theme.fontCaption - 4f, theme.fontLabel);
            }
            reflection.summaryText = UIFactory_Azm.NewText(reflectionRect, "Summary", theme, "", theme.fontLabel, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft);
            reflection.summaryText.enableAutoSizing = true;
            reflection.summaryText.fontSizeMin = theme.fontCaption - 2f;
            reflection.summaryText.fontSizeMax = theme.fontLabel;
            UIFactory_Azm.Pin(reflection.summaryText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(860f, 100f), Vector2.zero);

            // Confetti bursts from behind the g value (Stage 6: a result within 2 % of the true g).
            var confettiRect = UIFactory_Azm.NewRect("Confetti", rect);
            UIFactory_Azm.Pin(confettiRect, topLeft, new Vector2(0.5f, 0.5f), new Vector2(10f, 10f), new Vector2(50f + 300f, -210f));
            var confetti = confettiRect.gameObject.AddComponent<ConfettiBurst_Azm>();
            confetti.theme = theme;

            var results = panel.gameObject.AddComponent<ResultsPanelUI_Azm>();
            results.controller = controller;
            results.panel = panel;
            results.gText = g;
            results.errorText = error;
            results.detailText = detail;
            results.thumbnail = thumb;
            results.retryButton = shell.retryButton;
            results.menuButton = shell.menuButton;
            results.reflection = reflection;
            results.confetti = confetti;
            return results;
        }

        // ---------- graph and table ----------

        private static GraphView_Azm NewGraph(Transform parent, string name, bool large)
        {
            var rect = UIFactory_Azm.NewRect(name, parent);
            var graph = rect.gameObject.AddComponent<GraphView_Azm>();
            graph.theme = theme;
            graph.labelFontSize = large ? 32f : 24f;
            graph.marginLeft = large ? 150f : 110f;
            graph.marginBottom = large ? 100f : 84f;
            graph.pointSize = large ? 30f : 22f;
            graph.lineWidth = large ? 8f : 6f;
            return graph;
        }

        // The table's header row and a scrolling list of rows.
        private static TableView_Azm BuildTable(Transform parent, PendulumController_Azm controller, bool full,
            Vector2 size, Vector2 position, float sideMargin, float bottomMargin, out TMP_Text emptyLabel)
        {
            var holder = UIFactory_Azm.NewRect(full ? "FullTable" : "CompactTable", parent);
            holder.anchorMin = new Vector2(0f, 0f);
            holder.anchorMax = new Vector2(1f, 1f);
            holder.offsetMin = new Vector2(sideMargin, bottomMargin);
            holder.offsetMax = new Vector2(-sideMargin, full ? -210f : -56f);

            var header = UIFactory_Azm.NewRect("Header", holder);
            UIFactory_Azm.Pin(header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 84f), Vector2.zero);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.sizeDelta = new Vector2(0f, 84f);
            var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 2f;
            headerLayout.padding = new RectOffset(4, 4, 0, 0);
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            ScrollRect scroll;
            var content = UIBuilder_Azm.NewVerticalScroll(holder, "Rows", out scroll);
            var scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = new Vector2(0f, -90f);
            var vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 6f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            emptyLabel = UIFactory_Azm.NewText(holder, "Empty", theme, "Your timed lengths will appear here", 30f, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.Center);
            UIFactory_Azm.Stretch(emptyLabel.rectTransform, 0f, 0f, 0f, 120f);

            var table = holder.gameObject.AddComponent<TableView_Azm>();
            table.theme = theme;
            table.controller = controller;
            table.fullColumns = full;
            table.headerContainer = header;
            table.rowContainer = content;
            table.emptyLabel = emptyLabel;
            table.fontSize = full ? 32f : 22f;
            table.rowHeight = full ? 84f : 76f;
            return table;
        }
    }
}
