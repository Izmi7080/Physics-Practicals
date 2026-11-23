using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.Cylinder;
using VirtualLab.Azm.Instruments;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.EditorTools
{
    /*
     * Layer: Editor tooling (not part of the shipped app).
     * Purpose: "VirtualLab_Azm > Build Vernier Cylinder Scene" builds VernierCylinder_Azm - Matric
     * Physics Exp 1.1 + 1.2 as one practical ("Vernier Calipers: Solid Cylinder"):
     *   - the lab room (LabEnvironment_Azm), an instrument tray on the bench with the vernier caliper
     *     (the project's Blender model) upright in a small stand, and a solid metal cylinder with
     *     its three poses (resting / across the jaws / lengthwise) on two thin rails that stay clear
     *     of the jaws;
     *   - the caliper logic, view and drag interaction (shared Scripts/Instruments), the cylinder
     *     controller and its config, the camera rig with Overview and Caliper views;
     *   - the screen UI from the SHARED SHELL (PracticalShellBuilder_Azm, HCI restructure): anchored
     *     regions, instruction card, keypad with in-panel feedback, least-count panel, drawer with
     *     labelled strip, toast, results frame, pause, confirm, briefing. This builder only adds the
     *     practical's own parts: the caliper magnifier, the Data table, the Calc text and the result
     *     values.
     * It also creates the data assets if missing (CylinderConfig_Azm, PracticalGuide_Cylinder_Azm),
     * refreshes the step wording from the defaults (ApplyDefaultTexts), adds the scene to Build
     * Settings and turns the catalog's "Physics Practical 2" placeholder into this practical. The menu
     * item never overwrites an existing scene; BuildForced (code only) does, for development rebuilds.
     * Talks to: PracticalShellBuilder_Azm, UIFactory_Azm, UIBuilder_Azm, the runtime scripts.
     */
    public static class CylinderSceneBuilder_Azm
    {
        private const string Root = "Assets/_Azm";
        private const string SceneName = "VernierCylinder_Azm";
        private const string ScenePath = Root + "/Scenes/" + SceneName + ".unity";
        private const string ConfigPath = Root + "/Data/CylinderConfig_Azm.asset";
        private const string GuidePath = Root + "/Data/PracticalGuide_Cylinder_Azm.asset";
        private const string CaliperConfigPath = Root + "/Data/CaliperConfig_Azm.asset";
        private const string CatalogPath = Root + "/Data/PracticalCatalog_Azm.asset";
        private const string PlaceholderInfoPath = Root + "/Data/Practicals/PracticalInfo_Physics2_Azm.asset";
        private const string InfoName = "PracticalInfo_VernierCylinder_Azm";
        private const string LabPrefabPath = Root + "/Prefabs/LabEnvironment_Azm.prefab";
        private const string MaterialFolder = Root + "/Art/Materials";
        private const string CaliperModelPath = Root + "/Art/Models/VernierCaliper/VernierCaliper_Azm.fbx";

        // Island bench top (measured in the pendulum stage); the tray sits where the pendulum's does.
        private const float BenchTopY = 0.988f;
        private static readonly Vector3 TrayPosition = new Vector3(1.25f, BenchTopY, -0.75f);

        // Caliper view: the camera looks at the middle of the caliper and the rig fits this much of
        // it (metres, width x height: the beam, the jaws and the cylinder between them) into the
        // free scene window - on every screen shape (HCI restructure; it used to be a fixed framing
        // tuned for 16:9 and 20:9).
        private static readonly Vector3 CaliperFrameCentreLocal = new Vector3(-0.085f, -0.012f, 0f);
        private static readonly Vector2 CaliperFrameSize = new Vector2(0.20f, 0.085f);
        private const float CaliperViewDistance = 0.50f;
        private const float CaliperViewHeight = 0.08f;

        private static UITheme_Azm theme;

        private struct Station
        {
            public Transform tray, station, root, slider, plate;
            public Collider grab;
            public Transform cylinder, restPose, acrossPose, lengthPose;
        }

        [MenuItem("VirtualLab_Azm/Build Vernier Cylinder Scene")]
        public static void Build() => Run(false);

        // Development use from code only: REPLACES the scene file even if it exists.
        public static void BuildForced() => Run(true);

        private static void Run(bool force)
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[CylinderSceneBuilder_Azm] Stop Play mode first."); return; }
            if (!force && File.Exists(ScenePath))
            {
                Debug.LogError("[CylinderSceneBuilder_Azm] " + ScenePath + " already exists. Not overwriting it.");
                return;
            }

            // A new scene unloads unreferenced assets, so every asset is loaded AFTER this.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PracticalShellBuilder_Azm.LoadTheme();
            theme = PracticalShellBuilder_Azm.Theme;
            var catalog = AssetDatabase.LoadAssetAtPath<PracticalCatalog_Azm>(CatalogPath);
            var labPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabPrefabPath);
            var config = PracticalShellBuilder_Azm.LoadOrCreate<CylinderConfig_Azm>(ConfigPath, c => c.steps = CylinderConfig_Azm.CreateDefaultSteps());
            config.ApplyDefaultTexts();
            EditorUtility.SetDirty(config);
            var caliperConfig = PracticalShellBuilder_Azm.LoadOrCreate<CaliperConfig_Azm>(CaliperConfigPath, null);
            var guide = PracticalShellBuilder_Azm.LoadOrCreate<PracticalGuide_Azm>(GuidePath, FillGuide);

            Camera camera = PracticalShellBuilder_Azm.CreateCamera();
            PracticalShellBuilder_Azm.CreateLight();
            var backInput = PracticalShellBuilder_Azm.CreateBootstrap(catalog);
            PracticalShellBuilder_Azm.CreateEventSystem();
            var lab = (GameObject)PrefabUtility.InstantiatePrefab(labPrefab);
            lab.transform.position = Vector3.zero;

            var input = new GameObject("Input_Azm").AddComponent<TouchMouseInput_Azm>();

            // ----- apparatus -----
            var st = BuildStation(config);

            // ----- caliper: logic, presentation, interaction (shared Scripts/Instruments) -----
            var caliperGO = new GameObject("CaliperMeasurement_Azm");
            var measurement = caliperGO.AddComponent<CaliperMeasurement_Azm>();
            measurement.config = caliperConfig;
            measurement.objectName = "cylinder";
            measurement.quantitySymbol = "Y";
            var caliperView = caliperGO.AddComponent<CaliperRigView_Azm>();
            caliperView.measurement = measurement;
            caliperView.caliperRoot = st.root;
            caliperView.slider = st.slider;
            caliperView.vernierPlate = st.plate;
            var caliperDrag = caliperGO.AddComponent<CaliperDragInteraction_Azm>();
            caliperDrag.inputSource = input;
            caliperDrag.measurement = measurement;
            caliperDrag.rig = caliperView;
            caliperDrag.sliderHit = st.grab;
            caliperDrag.viewCamera = camera;

            // ----- the practical -----
            var controllerGO = new GameObject("CylinderController_Azm");
            var controller = controllerGO.AddComponent<CylinderController_Azm>();
            controller.config = config;
            controller.caliper = measurement;
            var cylinderView = controllerGO.AddComponent<CylinderRigView_Azm>();
            cylinderView.controller = controller;
            cylinderView.cylinder = st.cylinder;
            cylinderView.restPose = st.restPose;
            cylinderView.acrossPose = st.acrossPose;
            cylinderView.lengthPose = st.lengthPose;

            // ----- camera -----
            var rig = camera.gameObject.AddComponent<PracticalCameraRig_Azm>();
            rig.viewCamera = camera;
            rig.inputSource = input;
            rig.dragOwners = new MonoBehaviour[] { caliperDrag };
            // Feel (Stage 6): a tick per mm of jaw travel, tock + camera nudge on contact.
            var caliperJuice = caliperGO.AddComponent<CaliperJuice_Azm>();
            caliperJuice.measurement = measurement;
            caliperJuice.drag = caliperDrag;
            caliperJuice.cameraRig = rig;
            // Overview: the caliper and the cylinder's resting place on the tray, from the front, above,
            // fitted into the free area (~0.42 m of the tray across, ~0.16 m up).
            Vector3 trayTarget = st.tray.TransformPoint(new Vector3(-0.07f, 0.04f, 0.02f));
            rig.views.Add(new CameraViewEntry_Azm { name = "Overview", position = trayTarget + new Vector3(0f, 0.24f, -0.44f), target = trayTarget, fieldOfView = 40f, frameSize = new Vector2(0.42f, 0.16f) });
            Vector3 caliperTarget = st.station.TransformPoint(CaliperFrameCentreLocal);
            rig.views.Add(new CameraViewEntry_Azm
            {
                name = "Caliper",
                position = caliperTarget + new Vector3(0f, CaliperViewHeight, -CaliperViewDistance),
                target = caliperTarget,
                fieldOfView = 38f,
                frameSize = CaliperFrameSize
            });
            rig.startView = (int)CylinderView_Azm.Caliper;

            // ----- screen UI -----
            BuildScreenUI(controller, measurement, rig, backInput, guide);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            PracticalShellBuilder_Azm.AddToBuildSettings(ScenePath);
            UpdateCatalogEntry(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[CylinderSceneBuilder_Azm] Vernier cylinder scene built.");
        }

        // ---------- assets ----------

        // The written guide, from the Matric textbook pages for Exp 1.1 and 1.2 (paraphrased).
        private static void FillGuide(PracticalGuide_Azm g)
        {
            g.title = "Vernier Calipers: Solid Cylinder (Exp 1.1 and 1.2)";
            g.aim = "Exp 1.1: find the area of cross-section of a solid cylinder.\nExp 1.2: find its volume.\nBoth by measuring its diameter and length with vernier calipers.";
            g.description = "Vernier calipers measure lengths to a hundredth of a centimetre. The sliding vernier scale next to the main scale lets you read the fraction of a millimetre between two main-scale marks.";
            g.apparatus = new[] { "Vernier calipers", "Solid metal cylinder" };
            g.whatYouMeasure = "The least count and the zero error of the calipers, then the diameter D of the cylinder three times and its length L three times. From their means: r = D/2, the area of cross-section A = πr² and the volume V = πr²L.";
            g.procedure = new[]
            {
                "Find the least count: LC = smallest main-scale division ÷ number of vernier divisions.",
                "Close the jaws gently and find the zero error and the zero correction.",
                "Grip the cylinder across between the outside jaws. Read the main scale M and the vernier division n that coincides: Y = M + n × LC, then apply the zero correction. Repeat at three places.",
                "Grip the cylinder lengthwise and measure its length L the same way, three times.",
                "Find the mean D and the mean L, then r = D/2, A = πr² and V = πr²L.",
                "Record your results."
            };
            g.formulas = new[]
            {
                "Least count LC = smallest main-scale division ÷ number of vernier divisions",
                "Fraction X = n × LC;  observed value Y = M + X",
                "Zero correction ZC = −(zero error);  corrected value = Y + ZC",
                "Radius r = D / 2;  area of cross-section A = πr²",
                "Volume V = πr²L"
            };
            g.precautions = new[]
            {
                new GuidePrecaution_Azm { text = "Close the jaws gently.", why = "Pressing hard can bend the jaws and change every reading." },
                new GuidePrecaution_Azm { text = "Apply the zero correction.", why = "If the vernier 0 is not on the main-scale 0 with the jaws closed, every reading is off by the same amount." },
                new GuidePrecaution_Azm { text = "Both jaws should touch the cylinder.", why = "A gap makes the reading too large; squeezing tilts the jaws." },
                new GuidePrecaution_Azm { text = "Take readings at different places.", why = "A real cylinder is not perfectly even, so the mean of several readings is more reliable." }
            };
            g.observationTable = "For L₁–L₃ and D₁–D₃: main-scale reading M, vernier division n, fraction X = n × LC, observed value Y = M + X, corrected value Y + ZC (all in cm). The least count, zero error and zero correction are written above the table.";
        }

        // Turns the "Physics Practical 2" placeholder into this practical (same asset, so the catalog
        // keeps its reference and order). Renamed, never deleted.
        private static void UpdateCatalogEntry(PracticalCatalog_Azm catalog)
        {
            string path = PlaceholderInfoPath;
            var info = AssetDatabase.LoadAssetAtPath<PracticalInfo_Azm>(path);
            if (info == null)
            {
                path = Root + "/Data/Practicals/" + InfoName + ".asset";
                info = AssetDatabase.LoadAssetAtPath<PracticalInfo_Azm>(path);
            }
            if (info == null) { Debug.LogWarning("[CylinderSceneBuilder_Azm] Catalog entry not found - add the practical to the catalog by hand."); return; }

            info.id = "physics.vernier_cylinder";
            info.displayName = "Vernier Calipers: Solid Cylinder";
            info.subject = SubjectType_Azm.Physics;
            info.shortDescription = "Find the least count and zero error, then measure a cylinder to work out its area of cross-section and volume.";
            info.estimatedMinutes = 15;
            info.apparatus = new[] { "Vernier calipers", "Solid metal cylinder" };
            info.sceneName = SceneName;
            info.isLocked = false;
            info.learnModeAvailable = true;
            info.performModeAvailable = false;
            EditorUtility.SetDirty(info);
            if (path == PlaceholderInfoPath) AssetDatabase.RenameAsset(path, InfoName);
        }

        // ---------- apparatus helpers ----------

        private static Material GetMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
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

        // ---------- tray, caliper station and cylinder ----------
        // Caliper frame (the model after FBX import): origin = main-scale zero on the fixed jaw's
        // measuring face; local -X = opening direction; +Y = up; +Z = the scale side (front).
        private static Station BuildStation(CylinderConfig_Azm config)
        {
            var s = new Station();
            Material trayMat = GetMaterial("Tray_Azm", new Color(0.10f, 0.11f, 0.13f), 0.3f, 0.4f);
            Material brass = GetMaterial("CylinderBrass_Azm", new Color(0.80f, 0.62f, 0.32f), 0.85f, 0.6f);

            var tray = new GameObject("InstrumentTray").transform;
            tray.position = TrayPosition;
            s.tray = tray;
            Prim(tray, "Base", PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(0.58f, 0.02f, 0.32f), trayMat);
            Prim(tray, "RimFront", PrimitiveType.Cube, new Vector3(0f, 0.03f, -0.155f), new Vector3(0.58f, 0.03f, 0.01f), trayMat);
            Prim(tray, "RimBack", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.155f), new Vector3(0.58f, 0.03f, 0.01f), trayMat);
            Prim(tray, "RimLeft", PrimitiveType.Cube, new Vector3(-0.285f, 0.03f, 0f), new Vector3(0.01f, 0.03f, 0.32f), trayMat);
            Prim(tray, "RimRight", PrimitiveType.Cube, new Vector3(0.285f, 0.03f, 0f), new Vector3(0.01f, 0.03f, 0.32f), trayMat);

            const float trayTop = 0.02f;
            const float jawTipBelowZero = 0.0475f;
            const float tipClearance = 0.004f;
            var station = new GameObject("CaliperStation").transform;
            station.SetParent(tray, false);
            station.localPosition = new Vector3(-0.24f, trayTop + jawTipBelowZero + tipClearance, 0.09f);
            station.localRotation = Quaternion.Euler(0f, 180f, 0f);   // scales face the camera, jaw opens to the right
            s.station = station;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CaliperModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(station, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            s.root = instance.transform;
            s.slider = instance.transform.Find("Slider_Azm");
            s.plate = s.slider.Find("VernierPlate_Azm");

            var grab = new GameObject("GrabArea_Azm").transform;
            grab.SetParent(s.slider, false);
            var box = grab.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(-0.016f, -0.012f, 0f);
            box.size = new Vector3(0.06f, 0.075f, 0.03f);
            s.grab = box;

            float trayY = -(jawTipBelowZero + tipClearance);   // tray top in the caliper frame
            Prim(station, "StandPost", PrimitiveType.Cube, new Vector3(0.005f, (0.006f + trayY) * 0.5f, -0.0045f), new Vector3(0.006f, 0.006f - trayY, 0.004f), trayMat);
            Prim(station, "StandBase", PrimitiveType.Cube, new Vector3(0.006f, trayY + 0.002f, -0.012f), new Vector3(0.026f, 0.004f, 0.018f), trayMat);

            // The solid cylinder. Unity's cylinder is 2 units tall along Y and 1 unit across.
            float d = config.trueDiameterCm / 100f, l = config.trueLengthCm / 100f;
            const float centreY = -0.0275f;   // mid-height of the 40 mm outside jaws
            var cylinder = Prim(tray, "SolidCylinder", PrimitiveType.Cylinder, Vector3.zero, new Vector3(d, l * 0.5f, d), brass).transform;
            s.cylinder = cylinder;
            // Two thin rails hold it at mid-jaw height. They are 2.5-5.5 mm in front of / behind the
            // jaw plane (the jaws are 3.5 mm thick), so the sliding jaw never passes through them.
            float railTop = centreY - d * 0.5f + 0.0004f;
            foreach (float z in new[] { -0.004f, 0.004f })
                Prim(station, "CylinderRail", PrimitiveType.Cube, new Vector3(-0.027f, (railTop + trayY) * 0.5f, z), new Vector3(0.046f, railTop - trayY, 0.003f), trayMat);

            // Poses: across = axis towards the camera (end-on, the measured circle faces the student);
            // lengthwise = axis along the beam; rest = lying on the tray beside the caliper.
            s.acrossPose = Pose(station, "Pose_Across", new Vector3(-d * 0.5f, centreY, 0f), new Vector3(90f, 0f, 0f));
            s.lengthPose = Pose(station, "Pose_Lengthwise", new Vector3(-l * 0.5f, centreY, 0f), new Vector3(0f, 0f, 90f));
            s.restPose = Pose(tray, "Pose_Rest", new Vector3(0.10f, trayTop + d * 0.5f, -0.05f), new Vector3(0f, 0f, 90f));
            cylinder.SetPositionAndRotation(s.restPose.position, s.restPose.rotation);
            return s;
        }

        // ---------- screen-space UI (shared shell + this practical's parts) ----------

        private static void BuildScreenUI(CylinderController_Azm controller, CaliperMeasurement_Azm measurement,
            PracticalCameraRig_Azm rig, BackButtonInput_Azm backInput, PracticalGuide_Azm guide)
        {
            var spec = new ShellSpec_Azm
            {
                viewNames = new[] { "Overview", "Caliper" },
                viewIcons = new[] { ThemeIcon_Azm.Overview, ThemeIcon_Azm.Caliper },
                magnifierTitle = "Magnifier: main scale (top), vernier (bottom)",
                padDecimalKey = false,
                padMainLabel = CaliperTexts_Azm.MainScaleLabel,
                padDivisionLabel = CaliperTexts_Azm.VernierLabel,
                leastCountTitle = "Least count",
                nextLineLabel = CylinderTexts_Azm.NextLine,
                seeResultLabel = CylinderTexts_Azm.SeeResult
            };
            var shell = PracticalShellBuilder_Azm.Build(spec, measurement, rig, backInput, guide, out CylinderHUD_Azm hud);
            hud.controller = controller;

            // Magnifier: the code-drawn main scale and vernier.
            var scaleArea = UIFactory_Azm.NewRect("ScaleArea", shell.magnifierContent);
            UIFactory_Azm.Stretch(scaleArea);
            scaleArea.gameObject.AddComponent<RectMask2D>();
            var magnifierUI = shell.magnifier.gameObject.AddComponent<CaliperMagnifierUI_Azm>();
            magnifierUI.theme = theme;
            magnifierUI.measurement = measurement;
            magnifierUI.scaleArea = scaleArea;

            BuildDataPane(shell.dataPane, controller);

            var calc = shell.calcPane.gameObject.AddComponent<CylinderCalcUI_Azm>();
            calc.controller = controller;
            calc.body = shell.calcBody;

            hud.results = BuildResults(shell, controller);
            PracticalShellBuilder_Azm.Finish(shell);
        }

        // Data: constants line, table header, rows (scroll), means.
        private static void BuildDataPane(RectTransform dataPane, CylinderController_Azm controller)
        {
            var table = dataPane.gameObject.AddComponent<CylinderTableUI_Azm>();
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
            table.meansText = UIFactory_Azm.NewText(dataPane, "Means", theme, "", theme.fontLabel, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory_Azm.Pin(table.meansText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(700f, 100f), Vector2.zero);
        }

        // The result values inside the shared results frame.
        private static CylinderResultsUI_Azm BuildResults(Shell_Azm shell, CylinderController_Azm controller)
        {
            var panel = shell.resultsPanel;
            var areaCaption = UIFactory_Azm.NewText(panel.transform, "AreaCaption", theme, "Area of cross-section (Exp 1.1)", theme.fontBody, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.TopLeft);
            UIFactory_Azm.Pin(areaCaption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(680f, 50f), new Vector2(50f, -140f));
            var area = UIFactory_Azm.NewText(panel.transform, "Area", theme, "", theme.fontDisplay * 1.5f, ThemeColor_Azm.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            area.overflowMode = TextOverflowModes.Overflow;
            UIFactory_Azm.Pin(area.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(680f, 110f), new Vector2(50f, -190f));
            var volumeCaption = UIFactory_Azm.NewText(panel.transform, "VolumeCaption", theme, "Volume (Exp 1.2)", theme.fontBody, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.TopLeft);
            UIFactory_Azm.Pin(volumeCaption.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(680f, 50f), new Vector2(-50f, -140f));
            var volume = UIFactory_Azm.NewText(panel.transform, "Volume", theme, "", theme.fontDisplay * 1.5f, ThemeColor_Azm.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            volume.overflowMode = TextOverflowModes.Overflow;
            UIFactory_Azm.Pin(volume.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(680f, 110f), new Vector2(-50f, -190f));
            var detail = UIFactory_Azm.NewText(panel.transform, "Detail", theme, "", theme.fontLabel, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.TopLeft);
            UIFactory_Azm.Pin(detail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1400f, 200f), new Vector2(0f, -330f));

            var results = panel.gameObject.AddComponent<CylinderResultsUI_Azm>();
            results.controller = controller;
            results.panel = panel;
            results.areaText = area;
            results.volumeText = volume;
            results.detailText = detail;
            results.retryButton = shell.retryButton;
            results.menuButton = shell.menuButton;
            return results;
        }
    }
}
