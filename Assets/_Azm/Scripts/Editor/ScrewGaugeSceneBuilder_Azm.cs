using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.Instruments;
using VirtualLab.Azm.ScrewGaugePractical;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.EditorTools
{
    /*
     * Layer: Editor tooling (not part of the shipped app).
     * Purpose: "VirtualLab_Azm > Build Screw Gauge Scene" builds ScrewGauge_Azm - Matric Physics
     * Exp 1.3 "Screw Gauge: Thickness of a Metal Strip":
     *   - the lab room, an instrument tray with the screw gauge (the project's Blender model) on a
     *     small stand, axis horizontal, scales facing the camera, and the metal strip (or a wire,
     *     by config) with its two poses (on the tray / between anvil and spindle);
     *   - the screw gauge logic, view and thimble-drag interaction (shared Scripts/Instruments), the
     *     practical's controller and config, the camera rig (Overview, Gauge);
     *   - the screen UI from the SHARED SHELL (PracticalShellBuilder_Azm, HCI restructure). This
     *     builder only adds the practical's own parts: the gauge controls in the bottom row (Open /
     *     Ratchet hold buttons, Lock), the "turn one full turn" button, the screw gauge magnifier
     *     (sleeve + circular scale), the Data table, the Calc text and the result value.
     * It also creates the data assets if missing (ScrewGaugeConfig_Azm, ScrewGaugePracticalConfig_Azm,
     * PracticalGuide_ScrewGauge_Azm), refreshes the step wording (ApplyDefaultTexts), remaps the
     * model's materials to URP Lit, adds the scene to Build Settings and turns the catalog's
     * "Physics Practical 3" placeholder into this practical.
     * The menu item never overwrites an existing scene; BuildForced (code only) does.
     * Talks to: PracticalShellBuilder_Azm, UIFactory_Azm, UIBuilder_Azm, the runtime scripts.
     */
    public static class ScrewGaugeSceneBuilder_Azm
    {
        private const string Root = "Assets/_Azm";
        private const string SceneName = "ScrewGauge_Azm";
        private const string ScenePath = Root + "/Scenes/" + SceneName + ".unity";
        private const string InstrumentConfigPath = Root + "/Data/ScrewGaugeConfig_Azm.asset";
        private const string PracticalConfigPath = Root + "/Data/ScrewGaugePracticalConfig_Azm.asset";
        private const string GuidePath = Root + "/Data/PracticalGuide_ScrewGauge_Azm.asset";
        private const string CatalogPath = Root + "/Data/PracticalCatalog_Azm.asset";
        private const string PlaceholderInfoPath = Root + "/Data/Practicals/PracticalInfo_Physics3_Azm.asset";
        private const string InfoName = "PracticalInfo_ScrewGauge_Azm";
        private const string LabPrefabPath = Root + "/Prefabs/LabEnvironment_Azm.prefab";
        private const string MaterialFolder = Root + "/Art/Materials";
        private const string ModelPath = Root + "/Art/Models/ScrewGauge/ScrewGauge_Azm.fbx";
        private const string TexturePath = Root + "/Art/Models/ScrewGauge/ScrewGaugeMarkings_Azm.png";

        private const float BenchTopY = 0.988f;
        private static readonly Vector3 TrayPosition = new Vector3(1.25f, BenchTopY, -0.75f);

        // Gauge view: the camera looks at the middle of the gauge and the rig fits this much of it
        // (metres, width x height: frame, anvil, spindle, sleeve and thimble) into the free scene
        // window on every screen shape (HCI restructure; it used to be a fixed 16:9/20:9 framing).
        private static readonly Vector3 GaugeFrameCentreLocal = new Vector3(-0.035f, -0.010f, 0f);
        private static readonly Vector2 GaugeFrameSize = new Vector2(0.13f, 0.055f);
        private const float GaugeViewDistance = 0.31f;
        private const float GaugeViewHeight = 0.05f;

        private static UITheme_Azm theme;

        private struct Station
        {
            public Transform tray, station, root, spindle, thimble, ratchet, lockLever;
            public Collider grab;
            public Transform item, restPose, gaugePose;
        }

        [MenuItem("VirtualLab_Azm/Build Screw Gauge Scene")]
        public static void Build() => Run(false);

        public static void BuildForced() => Run(true);

        private static void Run(bool force)
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[ScrewGaugeSceneBuilder_Azm] Stop Play mode first."); return; }
            if (!force && File.Exists(ScenePath)) { Debug.LogError("[ScrewGaugeSceneBuilder_Azm] " + ScenePath + " already exists. Not overwriting it."); return; }

            // Re-importing the model reloads assets, so it happens before anything is referenced.
            PrepareModelImport();
            // A new scene unloads unreferenced assets, so every asset is loaded AFTER this.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PracticalShellBuilder_Azm.LoadTheme();
            theme = PracticalShellBuilder_Azm.Theme;
            var catalog = AssetDatabase.LoadAssetAtPath<PracticalCatalog_Azm>(CatalogPath);
            var labPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabPrefabPath);
            var gaugeConfig = PracticalShellBuilder_Azm.LoadOrCreate<ScrewGaugeConfig_Azm>(InstrumentConfigPath, null);
            var config = PracticalShellBuilder_Azm.LoadOrCreate<ScrewGaugePracticalConfig_Azm>(PracticalConfigPath, c => c.steps = ScrewGaugePracticalConfig_Azm.CreateDefaultSteps());
            config.ApplyDefaultTexts();
            EditorUtility.SetDirty(config);
            var guide = PracticalShellBuilder_Azm.LoadOrCreate<PracticalGuide_Azm>(GuidePath, FillGuide);

            Camera camera = PracticalShellBuilder_Azm.CreateCamera();
            camera.nearClipPlane = 0.01f;   // the gauge view is close to the instrument
            PracticalShellBuilder_Azm.CreateLight();
            var backInput = PracticalShellBuilder_Azm.CreateBootstrap(catalog);
            PracticalShellBuilder_Azm.CreateEventSystem();
            var lab = (GameObject)PrefabUtility.InstantiatePrefab(labPrefab);
            lab.transform.position = Vector3.zero;
            var input = new GameObject("Input_Azm").AddComponent<TouchMouseInput_Azm>();

            var st = BuildStation(config);

            // ----- instrument: logic, presentation, interaction -----
            var gaugeGO = new GameObject("ScrewGaugeMeasurement_Azm");
            var gauge = gaugeGO.AddComponent<ScrewGaugeMeasurement_Azm>();
            gauge.config = gaugeConfig;
            gauge.objectName = config.ObjectName;
            var rigView = gaugeGO.AddComponent<ScrewGaugeRigView_Azm>();
            rigView.measurement = gauge;
            rigView.spindle = st.spindle;
            rigView.thimble = st.thimble;
            rigView.ratchet = st.ratchet;
            rigView.lockLever = st.lockLever;
            var drag = gaugeGO.AddComponent<ScrewGaugeDragInteraction_Azm>();
            drag.inputSource = input;
            drag.measurement = gauge;
            drag.thimble = st.thimble;
            drag.thimbleHit = st.grab;
            drag.viewCamera = camera;

            // ----- the practical -----
            var controllerGO = new GameObject("ScrewGaugeController_Azm");
            var controller = controllerGO.AddComponent<ScrewGaugeController_Azm>();
            controller.config = config;
            controller.gauge = gauge;
            var itemView = controllerGO.AddComponent<ScrewGaugeObjectView_Azm>();
            itemView.controller = controller;
            itemView.item = st.item;
            itemView.restPose = st.restPose;
            itemView.gaugePose = st.gaugePose;

            // ----- camera -----
            var rig = camera.gameObject.AddComponent<PracticalCameraRig_Azm>();
            rig.viewCamera = camera;
            rig.inputSource = input;
            rig.dragOwners = new MonoBehaviour[] { drag };
            // Feel (Stage 6): a tick every 5 divisions while turning; squash + red flash when the
            // object is closed on with the thimble instead of the ratchet.
            var gaugeJuice = gaugeGO.AddComponent<ScrewGaugeJuice_Azm>();
            gaugeJuice.theme = PracticalShellBuilder_Azm.Theme;
            gaugeJuice.measurement = gauge;
            gaugeJuice.drag = drag;
            gaugeJuice.squeezedObject = st.item;
            // Overview: the gauge on its stand and the strip's place on the tray, fitted into the
            // free area (the tray seen from the front and above: ~0.32 m of it across, ~0.14 m up).
            Vector3 trayTarget = st.tray.TransformPoint(new Vector3(-0.02f, 0.04f, 0.0f));
            rig.views.Add(new CameraViewEntry_Azm { name = "Overview", position = trayTarget + new Vector3(0f, 0.17f, -0.32f), target = trayTarget, fieldOfView = 40f, frameSize = new Vector2(0.32f, 0.14f) });
            Vector3 target = st.station.TransformPoint(GaugeFrameCentreLocal);
            rig.views.Add(new CameraViewEntry_Azm
            {
                name = "Gauge",
                position = target + new Vector3(0f, GaugeViewHeight, -GaugeViewDistance),
                target = target,
                fieldOfView = 38f,
                frameSize = GaugeFrameSize
            });
            rig.startView = (int)SGView_Azm.Gauge;

            BuildScreenUI(controller, gauge, rig, backInput, guide);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            PracticalShellBuilder_Azm.AddToBuildSettings(ScenePath);
            UpdateCatalogEntry();
            AssetDatabase.SaveAssets();
            Debug.Log("[ScrewGaugeSceneBuilder_Azm] Screw gauge scene built.");
        }

        // ---------- assets ----------

        // From the Matric textbook pages for Exp 1.3 (Docs/Exp1.3_ScrewGauge), paraphrased.
        private static void FillGuide(PracticalGuide_Azm g)
        {
            g.title = "Screw Gauge: Thickness of a Metal Strip (Exp 1.3)";
            g.aim = "Measure the thickness of a metal strip with a screw gauge.";
            g.description = "A screw gauge measures lengths as small as a hundredth of a millimetre. It has two scales: the main scale on the sleeve and the circular scale on the thimble. Turning the thimble one full turn moves the spindle by one pitch.";
            g.apparatus = new[] { "Screw gauge", "Flat metal strip (or a kink-free wire)" };
            g.whatYouMeasure = "The pitch and least count of the screw gauge, its zero error, then the thickness of the strip three times at different places. The result is the mean of the three corrected readings.";
            g.procedure = new[]
            {
                "Find the least count: LC = pitch ÷ number of divisions on the circular scale.",
                "Close the anvil and spindle by turning the ratchet until it clicks.",
                "Check the zero error and work out the zero correction.",
                "Hold the metal strip between the anvil and the spindle.",
                "Tighten gently with the ratchet until it clicks.",
                "Note the main-scale reading M: the mark just under or before the edge of the thimble.",
                "Note the division n of the circular scale that lies on the datum line.",
                "Fraction X = n × LC; observed thickness Y = M + X.",
                "Apply the zero correction: t = Y ± ZC.",
                "Take two more readings at different places and find the mean thickness."
            };
            g.formulas = new[]
            {
                "Pitch = distance moved on the main scale for one full turn = 0.5 mm",
                "Least count LC = pitch ÷ number of circular divisions = 0.5 mm ÷ 50 = 0.01 mm",
                "Zero error: circular 0 below the datum line = +n × LC; above it = −(50 − n) × LC",
                "Zero correction ZC = −(zero error)",
                "X = n × LC;  Y = M + X;  t = Y + ZC;  mean t = (t₁ + t₂ + t₃) / 3"
            };
            g.precautions = new[]
            {
                new GuidePrecaution_Azm { text = "Add or subtract the zero correction as required.", why = "A zero error shifts every reading by the same amount." },
                new GuidePrecaution_Azm { text = "Take readings at different places.", why = "A strip or wire is not exactly the same thickness everywhere; the mean is more reliable." },
                new GuidePrecaution_Azm { text = "Do not press the jaws very hard.", why = "Squeezing bends the strip and the frame, so the reading comes out too small." },
                new GuidePrecaution_Azm { text = "Do not turn the thimble back and forth unnecessarily.", why = "It wears the screw, which spoils the pitch." },
                new GuidePrecaution_Azm { text = "Turn the screw gently using the ratchet.", why = "The ratchet slips once the spindle touches, so every reading uses the same light pressure. (Think: this is what the ratchet is for.)" },
                new GuidePrecaution_Azm { text = "Use a flat strip (or a kink-free wire).", why = "A bend or kink holds the spindle away from the metal, so the reading is too large. (Think: why it must be flat.)" }
            };
            g.observationTable = "For readings 1–3: main-scale reading M (mm), circular division n on the datum line, fraction X = n × LC (mm), observed thickness Y = M + X (mm), corrected thickness t = Y ± ZC (mm). LC, zero error and zero correction are written above the table; mean t = (t₁ + t₂ + t₃)/3.";
        }

        private static void PrepareModelImport()
        {
            var tex = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (tex != null && tex.maxTextureSize != 1024)
            {
                tex.maxTextureSize = 1024;
                tex.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Material steel = GetMaterial("ScrewGaugeSteel_Azm", new Color(0.78f, 0.78f, 0.80f), 0.75f, 0.6f, null);
            Material frame = GetMaterial("ScrewGaugeFrame_Azm", new Color(0.05f, 0.07f, 0.11f), 0f, 0.5f, null);
            Material marks = GetMaterial("ScrewGaugeMarkings_Azm", Color.white, 0.6f, 0.6f, texture);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "ScrewGaugeSteel_Azm"), steel);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "ScrewGaugeFrame_Azm"), frame);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "ScrewGaugeMarkings_Azm"), marks);
            importer.SaveAndReimport();
        }


        // Turns the "Physics Practical 3" placeholder into this practical (renamed, never deleted).
        private static void UpdateCatalogEntry()
        {
            string path = PlaceholderInfoPath;
            var info = AssetDatabase.LoadAssetAtPath<PracticalInfo_Azm>(path);
            if (info == null)
            {
                path = Root + "/Data/Practicals/" + InfoName + ".asset";
                info = AssetDatabase.LoadAssetAtPath<PracticalInfo_Azm>(path);
            }
            if (info == null) { Debug.LogWarning("[ScrewGaugeSceneBuilder_Azm] Catalog entry not found - add the practical to the catalog by hand."); return; }
            info.id = "physics.screw_gauge";
            info.displayName = "Screw Gauge: Thickness of a Metal Strip";
            info.subject = SubjectType_Azm.Physics;
            info.shortDescription = "Find the pitch, least count and zero error, then measure a metal strip to a hundredth of a millimetre.";
            info.estimatedMinutes = 15;
            info.apparatus = new[] { "Screw gauge", "Flat metal strip" };
            info.sceneName = SceneName;
            info.isLocked = false;
            info.learnModeAvailable = true;
            info.performModeAvailable = false;
            EditorUtility.SetDirty(info);
            if (path == PlaceholderInfoPath) AssetDatabase.RenameAsset(path, InfoName);
        }


        // ---------- apparatus helpers ----------

        private static Material GetMaterial(string name, Color color, float metallic, float smoothness, Texture2D baseMap)
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

        private static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Transform Pose(Transform parent, string name, Vector3 localPos, Vector3 localEuler)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(localEuler);
            return t;
        }

        // ---------- tray, gauge station and the strip ----------
        // Gauge frame (the model after FBX import): origin = anvil measuring face; local -X = from the
        // anvil towards the ratchet; +Y = up; +Z = the scale side (front). Frame bottom at y = -34 mm.
        private static Station BuildStation(ScrewGaugePracticalConfig_Azm config)
        {
            var s = new Station();
            Material trayMat = GetMaterial("Tray_Azm", new Color(0.10f, 0.11f, 0.13f), 0.3f, 0.4f, null);
            Material stripMat = GetMaterial("MetalStrip_Azm", new Color(0.72f, 0.70f, 0.66f), 0.85f, 0.55f, null);

            var tray = new GameObject("InstrumentTray").transform;
            tray.position = TrayPosition;
            s.tray = tray;
            Prim(tray, "Base", PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(0.58f, 0.02f, 0.32f), trayMat);
            Prim(tray, "RimFront", PrimitiveType.Cube, new Vector3(0f, 0.03f, -0.155f), new Vector3(0.58f, 0.03f, 0.01f), trayMat);
            Prim(tray, "RimBack", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.155f), new Vector3(0.58f, 0.03f, 0.01f), trayMat);
            Prim(tray, "RimLeft", PrimitiveType.Cube, new Vector3(-0.285f, 0.03f, 0f), new Vector3(0.01f, 0.03f, 0.32f), trayMat);
            Prim(tray, "RimRight", PrimitiveType.Cube, new Vector3(0.285f, 0.03f, 0f), new Vector3(0.01f, 0.03f, 0.32f), trayMat);

            const float trayTop = 0.02f;
            const float frameBottom = 0.034f;    // below the axis
            const float standHeight = 0.012f;
            var station = new GameObject("GaugeStation").transform;
            station.SetParent(tray, false);
            station.localPosition = new Vector3(-0.12f, trayTop + standHeight + frameBottom, 0.06f);
            station.localRotation = Quaternion.Euler(0f, 180f, 0f);   // scales face the camera, ratchet to the right
            s.station = station;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(station, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            s.root = instance.transform;
            s.spindle = instance.transform.Find("Spindle_Azm");
            s.thimble = s.spindle.Find("Thimble_Azm");
            s.ratchet = s.spindle.Find("Ratchet_Azm");
            s.lockLever = instance.transform.Find("Lock_Azm");

            // Grab area round the thimble and ratchet (turns with the thimble; nearly round in YZ).
            var grab = new GameObject("GrabArea_Azm").transform;
            grab.SetParent(s.thimble, false);
            var box = grab.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(-0.021f, 0f, 0f);
            box.size = new Vector3(0.046f, 0.022f, 0.022f);
            s.grab = box;

            // Stand under the frame's bow.
            Prim(station, "Stand", PrimitiveType.Cube, new Vector3(-0.015f, -frameBottom - standHeight * 0.5f, 0f), new Vector3(0.028f, standHeight, 0.022f), trayMat);

            // The strip (thickness along the gauge axis) or a wire (axis front-to-back).
            float t = config.trueThicknessMm / 1000f;
            Transform item;
            if (config.objectKind == SGObjectKind_Azm.Wire)
            {
                item = Prim(tray, "Wire", PrimitiveType.Cylinder, Vector3.zero, new Vector3(t, 0.02f, t), stripMat).transform;
                s.gaugePose = Pose(station, "Pose_InGauge", new Vector3(-t * 0.5f, 0f, 0f), new Vector3(90f, 0f, 0f));
                s.restPose = Pose(tray, "Pose_Rest", new Vector3(0.10f, trayTop + t * 0.5f, -0.05f), new Vector3(0f, 90f, 90f));
            }
            else
            {
                item = Prim(tray, "MetalStrip", PrimitiveType.Cube, Vector3.zero, new Vector3(t, 0.026f, 0.008f), stripMat).transform;
                // Between the faces, mostly above the axis (as if held by its top end).
                s.gaugePose = Pose(station, "Pose_InGauge", new Vector3(-t * 0.5f, 0.004f, 0f), Vector3.zero);
                s.restPose = Pose(tray, "Pose_Rest", new Vector3(0.10f, trayTop + t * 0.5f, -0.05f), new Vector3(0f, 0f, 90f));
            }
            item.SetPositionAndRotation(s.restPose.position, s.restPose.rotation);
            s.item = item;
            return s;
        }


        // ---------- screen-space UI (shared shell + this practical's parts) ----------

        private static void BuildScreenUI(ScrewGaugeController_Azm controller, ScrewGaugeMeasurement_Azm gauge,
            PracticalCameraRig_Azm rig, BackButtonInput_Azm backInput, PracticalGuide_Azm guide)
        {
            var spec = new ShellSpec_Azm
            {
                viewNames = new[] { "Overview", "Gauge" },
                viewIcons = new[] { ThemeIcon_Azm.Overview, ThemeIcon_Azm.Gauge },
                magnifierTitle = "Magnifier: sleeve and thimble edge (left), circular scale (right)",
                padDecimalKey = true,
                padMainLabel = ScrewGaugeTexts_Azm.MainLabel,
                padDivisionLabel = ScrewGaugeTexts_Azm.DivisionLabel,
                leastCountTitle = "Pitch and least count",
                turnButtonLabel = ScrewGaugePracticalTexts_Azm.TurnOneTurn,
                nextLineLabel = ScrewGaugePracticalTexts_Azm.NextLine,
                seeResultLabel = ScrewGaugePracticalTexts_Azm.SeeResult
            };
            var shell = PracticalShellBuilder_Azm.Build(spec, gauge, rig, backInput, guide, out ScrewGaugeHUD_Azm hud);
            hud.controller = controller;
            hud.turnButton = shell.turnButton;

            BuildGaugeControls(shell, hud);
            BuildMagnifier(shell, gauge);
            BuildDataPane(shell.dataPane, controller);

            var calc = shell.calcPane.gameObject.AddComponent<ScrewGaugeCalcUI_Azm>();
            calc.controller = controller;
            calc.body = shell.calcBody;

            hud.results = BuildResults(shell, controller);
            PracticalShellBuilder_Azm.Finish(shell);
        }

        // Open (hold) / Ratchet (hold) / Lock - in the bottom row, after the view buttons.
        private static void BuildGaugeControls(Shell_Azm shell, ScrewGaugeHUD_Azm hud)
        {
            var bar = UIFactory_Azm.NewImage(shell.bottomRow, "GaugeControls", theme, ThemeColor_Azm.PanelStrong);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = theme.spaceS;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var open = UIFactory_Azm.NewButton(bar.transform, "OpenHold", theme, "◀ Open", ThemeColor_Azm.Secondary, new Vector2(200f, 132f), theme.fontBody + 2f);
            hud.openHold = open.gameObject.AddComponent<HoldButton_Azm>();
            var ratchet = UIFactory_Azm.NewButton(bar.transform, "RatchetHold", theme, "Ratchet ▶", ThemeColor_Azm.Primary, new Vector2(250f, 132f), theme.fontTitle - 2f);
            hud.ratchetHold = ratchet.gameObject.AddComponent<HoldButton_Azm>();
            hud.lockButton = UIFactory_Azm.NewButton(bar.transform, "LockButton", theme, "Lock", ThemeColor_Azm.Secondary, new Vector2(170f, 132f), theme.fontBody + 2f);
            hud.lockLabel = hud.lockButton.GetComponentInChildren<TMP_Text>();
            hud.gaugeControls = bar.gameObject;
        }

        // The sleeve takes the width the screen allows; the circular-scale strip keeps a fixed width
        // at the right (anchored, so the magnifier works at every aspect ratio).
        private static void BuildMagnifier(Shell_Azm shell, ScrewGaugeMeasurement_Azm gauge)
        {
            const float thimbleWidth = 330f;
            var sleeve = UIFactory_Azm.NewImage(shell.magnifierContent, "SleeveArea", theme, ThemeColor_Azm.Secondary, false);
            sleeve.raycastTarget = false;
            var sleeveRect = sleeve.rectTransform;
            sleeveRect.anchorMin = new Vector2(0f, 0f);
            sleeveRect.anchorMax = new Vector2(1f, 1f);
            sleeveRect.offsetMin = Vector2.zero;
            sleeveRect.offsetMax = new Vector2(-(thimbleWidth + 4f), 0f);
            sleeve.gameObject.AddComponent<RectMask2D>();

            var thimble = UIFactory_Azm.NewImage(shell.magnifierContent, "ThimbleArea", theme, ThemeColor_Azm.Panel, false);
            thimble.raycastTarget = false;
            var thimbleRect = thimble.rectTransform;
            thimbleRect.anchorMin = new Vector2(1f, 0f);
            thimbleRect.anchorMax = new Vector2(1f, 1f);
            thimbleRect.pivot = new Vector2(1f, 0.5f);
            thimbleRect.sizeDelta = new Vector2(thimbleWidth, 0f);
            thimbleRect.anchoredPosition = Vector2.zero;
            thimble.gameObject.AddComponent<RectMask2D>();

            var magnifier = shell.magnifier.gameObject.AddComponent<ScrewGaugeMagnifierUI_Azm>();
            magnifier.theme = theme;
            magnifier.measurement = gauge;
            magnifier.sleeveArea = sleeveRect;
            magnifier.thimbleArea = thimbleRect;
        }

        private static void BuildDataPane(RectTransform dataPane, ScrewGaugeController_Azm controller)
        {
            var table = dataPane.gameObject.AddComponent<ScrewGaugeTableUI_Azm>();
            table.theme = theme;
            table.controller = controller;
            table.headerText = UIFactory_Azm.NewText(dataPane, "Constants", theme, "", theme.fontCaption - 2f, ThemeColor_Azm.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory_Azm.Pin(table.headerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(700f, 70f), Vector2.zero);
            var headerRow = UIFactory_Azm.NewRect("HeaderRow", dataPane);
            headerRow.anchorMin = new Vector2(0f, 1f);
            headerRow.anchorMax = new Vector2(1f, 1f);
            headerRow.pivot = new Vector2(0.5f, 1f);
            headerRow.sizeDelta = new Vector2(0f, 76f);
            headerRow.anchoredPosition = new Vector2(0f, -76f);
            var headerLayout = headerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 2f;
            headerLayout.padding = new RectOffset(4, 4, 0, 0);
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            headerLayout.childControlWidth = headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = headerLayout.childForceExpandHeight = false;
            table.headerRow = headerRow;
            var rows = UIBuilder_Azm.NewVerticalScroll(dataPane, "Rows", out ScrollRect scroll);
            var scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(0f, 110f);
            scrollRect.offsetMax = new Vector2(0f, -158f);
            var vertical = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 6f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            table.rowContainer = rows;
            table.emptyLabel = UIFactory_Azm.NewText(dataPane, "Empty", theme, "Your readings will appear here", theme.fontBody - 2f, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.Center);
            UIFactory_Azm.Stretch(table.emptyLabel.rectTransform, 0f, 110f, 0f, 160f);
            table.meanText = UIFactory_Azm.NewText(dataPane, "Mean", theme, "", theme.fontLabel, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory_Azm.Pin(table.meanText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(700f, 100f), Vector2.zero);
        }

        // The result value inside the shared results frame.
        private static ScrewGaugeResultsUI_Azm BuildResults(Shell_Azm shell, ScrewGaugeController_Azm controller)
        {
            var panel = shell.resultsPanel;
            var caption = UIFactory_Azm.NewText(panel.transform, "Caption", theme, "", theme.fontBody, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.TopLeft);
            UIFactory_Azm.Pin(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1000f, 50f), new Vector2(50f, -140f));
            var value = UIFactory_Azm.NewText(panel.transform, "Value", theme, "", theme.fontDisplay * 1.7f, ThemeColor_Azm.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            value.overflowMode = TextOverflowModes.Overflow;
            UIFactory_Azm.Pin(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1200f, 120f), new Vector2(50f, -190f));
            var detail = UIFactory_Azm.NewText(panel.transform, "Detail", theme, "", theme.fontLabel, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.TopLeft);
            UIFactory_Azm.Pin(detail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1400f, 200f), new Vector2(0f, -330f));
            var results = panel.gameObject.AddComponent<ScrewGaugeResultsUI_Azm>();
            results.controller = controller;
            results.panel = panel;
            results.valueText = value;
            results.captionText = caption;
            results.detailText = detail;
            results.retryButton = shell.retryButton;
            results.menuButton = shell.menuButton;
            return results;
        }
    }
}
