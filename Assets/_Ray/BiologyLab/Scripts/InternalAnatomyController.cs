using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace BiologyLab
{
    // Owns the internal view's children; imported meshes/materials are never modified.
    internal sealed class InternalAnatomyController : IDisposable
    {
        private readonly VisualElement root;
        private readonly FrogPreviewController preview;
        private readonly List<Action> cleanup = new List<Action>();
        private readonly List<LayerMaterials> layers = new List<LayerMaterials>();
        private readonly List<Button> menu = new List<Button>();
        private OrganLabelingController labeling;
        private StudyOrgansController studyOrgans;
        private FemaleAnatomyModel femaleModel;
        private OrganSystemAnnotationsController digestive, respiratory, circulatory, nervous, urogenital;
        private LayerMaterials respiratoryHead;
        private GameObject urogenitalCloaca;
        private Material urogenitalCloacaMaterial;
        private bool isolated;
        private float fittedSize;
        private Vector3 fittedCenter;
        public bool IsSystems => visible && (selection == "Systems" || selection == "Digestive" || selection == "Respiratory" || selection == "Circulatory" || selection == "Nervous" || selection == "Urogenital");
        private VisualElement ui, top, bank, actions, menuPanel;
        private Label note;
        private Button gender, home;
        private bool visible, female;
        private string selection = "Muscles";
        private Bounds fullBounds, organBounds, systemsOverviewBounds;
        private Vector3 initialPosition, pivot;
        private float entryTime, zoom = 1f, targetZoom = 1f, yaw, targetYaw, pitch, targetPitch;
        private Vector2 pan, targetPan;
        private GameObject spleen;
        private Material spleenMaterial;
        public bool IsLabeling => visible && selection == "Labeling";
        public bool IsStudyingOrgans => visible && selection == "StudyOrgans";

        public InternalAnatomyController(VisualElement root, FrogPreviewController preview)
        { this.root = root; this.preview = preview; }
        public void Init()
        {
            ui = root.Q("internal-ui"); top = root.Q("internal-top"); bank = root.Q("label-bank");
            actions = root.Q("study-actions"); menuPanel = root.Q("layer-menu");
            note = root.Q<Label>("internal-note"); gender = root.Q<Button>("gender"); home = root.Q<Button>("anatomy-home");
            foreach (string name in new[] { "muscles", "skeleton", "organs", "labeling" }) menu.Add(root.Q<Button>("layer-" + name));
            labeling = new OrganLabelingController(root, preview); labeling.Init();
            studyOrgans = new StudyOrgansController(root, preview); studyOrgans.Init();
            digestive = new OrganSystemAnnotationsController(root, preview); digestive.Init();
            respiratory = new OrganSystemAnnotationsController(root, preview, true); respiratory.Init();
            circulatory = new OrganSystemAnnotationsController(root, preview, false, true); circulatory.Init();
            nervous = new OrganSystemAnnotationsController(root, preview, false, false, true); nervous.Init();
            urogenital = new OrganSystemAnnotationsController(root, preview, false, false, false, true); urogenital.Init();
        }
        public void Start()
        {
            initialPosition = preview.Anatomy.transform.position;
            // No separate spleen is supplied. This small, documented schematic makes its label target visible.
            spleen = GameObject.CreatePrimitive(PrimitiveType.Sphere); spleen.name = "Spleen (Schematic Supplement)";
            spleen.transform.SetParent(preview.Anatomy.transform.Find("Internal Organs"), false); spleen.layer = 31;
            spleen.transform.position = preview.Anatomy.transform.TransformPoint(new Vector3(.56f,5.2f,5.65f));
            spleen.transform.localScale = Vector3.one * .38f; UnityEngine.Object.Destroy(spleen.GetComponent<Collider>());
            spleenMaterial = new Material(preview.Anatomy.transform.Find("Internal Organs").GetComponentInChildren<Renderer>().sharedMaterial.shader);
            spleenMaterial.name = "Schematic Spleen"; spleenMaterial.SetColor("_BaseColor", new Color(.47f,.08f,.11f));
            spleen.GetComponent<Renderer>().sharedMaterial = spleenMaterial;
            foreach (string name in new[] { "Muscles", "Skeleton", "Internal Organs" })
                layers.Add(new LayerMaterials(preview.Anatomy.transform.Find(name)));
            fullBounds = BoundsOf(preview.Anatomy.transform.Find("Muscles"));
            organBounds = BoundsOf(preview.Anatomy.transform.Find("Internal Organs"));
            systemsOverviewBounds = organBounds;
            systemsOverviewBounds.Encapsulate(BoundsOf(preview.Anatomy.transform.Find("Respiratory Head")));
            pivot = fullBounds.center - initialPosition;
            femaleModel = new FemaleAnatomyModel(preview.Anatomy.transform); femaleModel.Init(); femaleModel.Start();
            urogenitalCloaca = GameObject.CreatePrimitive(PrimitiveType.Capsule); urogenitalCloaca.name = "Urogenital Cloaca (Schematic Segment)";
            urogenitalCloaca.transform.SetParent(preview.Anatomy.transform, false); urogenitalCloaca.transform.localPosition = new Vector3(0f,4.22f,2.40f);
            urogenitalCloaca.transform.localRotation = Quaternion.Euler(90f,0f,0f); urogenitalCloaca.transform.localScale = new Vector3(.29f,.49f,.29f); urogenitalCloaca.layer = 31;
            UnityEngine.Object.Destroy(urogenitalCloaca.GetComponent<Collider>());
            Shader organShader = preview.Anatomy.transform.Find("Internal Organs/Urinary Bladder").GetComponent<Renderer>().sharedMaterial.shader;
            urogenitalCloacaMaterial = new Material(organShader) { name = "Schematic Cloaca" };
            urogenitalCloacaMaterial.SetColor("_BaseColor", new Color(.49f,.31f,.25f));
            urogenitalCloacaMaterial.SetFloat("_Smoothness", .28f);
            urogenitalCloaca.GetComponent<Renderer>().sharedMaterial = urogenitalCloacaMaterial;
            urogenitalCloaca.SetActive(false);
            labeling.Start();
            digestive.Start();
            respiratory.Start();
            circulatory.Start();
            nervous.Start();
            urogenital.Start();
            studyOrgans.Start();
            Bind(root.Q<Button>("system-urogenital"), () => Select("Urogenital"));
            Bind(root.Q<Button>("system-nervous"), () => Select("Nervous"));
            Bind(root.Q<Button>("system-circulatory"), () => Select("Circulatory"));
            respiratoryHead = new LayerMaterials(preview.Anatomy.transform.Find("Respiratory Head"));
            Bind(root.Q<Button>("system-respiratory"), () => Select("Respiratory"));
            Bind(root.Q<Button>("study-systems"), () => Select("Systems"));
            Bind(root.Q<Button>("study-organs"), () => Select("StudyOrgans"));
            Bind(root.Q<Button>("system-digestive"), () => Select("Digestive"));
            Bind(root.Q<Button>("system-isolate"), () => { isolated = !isolated; ApplySystem(); });
            EventCallback<GeometryChangedEvent> layoutChanged = evt => { if (visible) { preview.Tick(0f); Tick(0f); } };
            preview.ViewImage.RegisterCallback(layoutChanged);
            cleanup.Add(() => preview.ViewImage.UnregisterCallback(layoutChanged));
            Bind(menu[0], SelectMuscles); Bind(menu[1], () => Select("Skeleton"));
            Bind(menu[2], () => Select("Organs")); Bind(menu[3], () => Select("Labeling"));
            Bind(gender, ToggleGender);
            Bind(root.Q<Button>("retry-exam"), () => { labeling.Reset(); ResetCamera(); });
            Bind(root.Q<Button>("zoom-in"), () => targetZoom = Mathf.Clamp(targetZoom * .85f, .25f, 2.2f));
            Bind(root.Q<Button>("zoom-out"), () => targetZoom = Mathf.Clamp(targetZoom / .85f, .25f, 2.2f));
            Bind(root.Q<Button>("rotate-left"), () => Rotate(-12f, 0f));
            Bind(root.Q<Button>("rotate-right"), () => Rotate(12f, 0f));
            Bind(root.Q<Button>("camera-up"), () => Move(0f, 1f)); Bind(root.Q<Button>("camera-down"), () => Move(0f, -1f));
            Bind(root.Q<Button>("camera-left"), () => Move(-1f, 0f)); Bind(root.Q<Button>("camera-right"), () => Move(1f, 0f));
            Bind(root.Q<Button>("camera-reset"), ResetCamera); Bind(root.Q<Button>("camera-center"), ResetCamera);
            SetVisible(false);
        }
        private void Bind(Button button, Action action)
        { button.clicked += action; cleanup.Add(() => button.clicked -= action); }
        private static Bounds BoundsOf(Transform model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        public void SetVisible(bool value)
        {
            visible = value; ui.EnableInClassList("hidden", !value); top.EnableInClassList("hidden", !value);
            root.Q("anatomy-screen").EnableInClassList("internal-view", value);
            if (value)
            {
                female = false; entryTime = 0f; ResetCamera(); SelectMuscles(); UpdateGender();
                foreach (Button button in menu) { button.style.opacity = 0f; button.style.translate = new Translate(-40f, 0f); }
            }
            else
            {
                labeling.SetVisible(false);
                studyOrgans.SetVisible(false);
                digestive.SetVisible(false); respiratory.SetVisible(false); circulatory.SetVisible(false); nervous.SetVisible(false); urogenital.SetVisible(false); respiratoryHead.Apply(false, true); urogenitalCloaca.SetActive(false);
                if (preview.Anatomy != null) { preview.Anatomy.transform.rotation = Quaternion.identity; preview.Anatomy.transform.position = initialPosition; }
                if (femaleModel != null) femaleModel.SetFemale(false);
                if (preview.ViewImage != null) ClearImageInsets();
                home.text = "⌂ Home";
            }
        }
        public void SelectMuscles() { Select("Muscles"); }
        private void Select(string value)
        {
            selection = value;
            for (int i = 0; i < menu.Count; i++) menu[i].EnableInClassList("selected", new[] { "Muscles", "Skeleton", "Organs", "Labeling" }[i] == value);
            bool quiz = value == "Labeling";
            bool organStudy = value == "StudyOrgans";
            isolated = false;
            root.Q("systems-menu").EnableInClassList("hidden", !IsSystems);
            root.Q("systems-menu-right").EnableInClassList("hidden", !IsSystems);
            root.Q("system-isolate").EnableInClassList("hidden", !IsSystems);
            root.Q<Label>("anatomy-title").text = value == "Digestive" ? "Digestive system" : value == "Respiratory" ? "Respiratory system" : value == "Circulatory" ? "Circulatory system" : value == "Nervous" ? "Nervous system" : value == "Urogenital" ? (female ? "Female urogenital system" : "Male urogenital system") : organStudy ? "Study organs" : IsSystems ? "Study systems" : "Internal anatomy";
            ui.EnableInClassList("labeling-mode", quiz);
            ui.EnableInClassList("systems-mode", IsSystems);
            ui.EnableInClassList("organ-study-mode", organStudy);
            bank.EnableInClassList("hidden", !quiz); actions.EnableInClassList("hidden", quiz); menuPanel.EnableInClassList("hidden", quiz);
            root.Q("retry-exam").EnableInClassList("hidden", !quiz);
            home.text = quiz || organStudy ? "← Back" : "⌂ Home";
            if (IsSystems || organStudy) { home.text = "← Back"; actions.AddToClassList("hidden"); menuPanel.AddToClassList("hidden"); }
            foreach (LayerMaterials layer in layers)
            {
                bool organs = layer.Name == "Internal Organs";
                layer.Apply(quiz || IsSystems || organStudy ? organs : layer.Name == value || value == "Organs" && organs, (quiz || IsSystems || organStudy) && !organs);
            }
            femaleModel.SetFemale(female);
            femaleModel.SetGhost(!quiz && value != "Organs");
            labeling.SetVisible(quiz);
            studyOrgans.SetVisible(organStudy);
            if (organStudy) studyOrgans.SetGender(female);
            digestive.SetVisible(value == "Digestive");
            respiratory.SetVisible(value == "Respiratory");
            circulatory.SetVisible(value == "Circulatory");
            nervous.SetVisible(value == "Nervous");
            urogenital.SetVisible(value == "Urogenital");
            urogenitalCloaca.SetActive(value == "Urogenital");
            if (!IsSystems) respiratoryHead.Apply(false, true);
            if (IsSystems) ApplySystem();
            ResetCamera();
            // Keep both directions continuous; attachments use the same interpolated model pose.
            Tick(0f);
            UpdateNote();
        }
        private void ToggleGender() { female = !female; UpdateGender(); }
        private void ApplySystem()
        {
            foreach (LayerMaterials layer in layers) if (layer.Name == "Internal Organs") layer.ApplySystemParts(selection == "Digestive", selection == "Respiratory", selection == "Circulatory", selection == "Nervous", selection == "Urogenital", isolated);
            foreach (LayerMaterials layer in layers) if (layer.Name == "Muscles") layer.Apply(false, true);
            femaleModel.SetFemale(female && (!isolated || selection == "Urogenital"));
            femaleModel.SetGhost(selection != "Systems" && selection != "Urogenital");
            respiratory.SetFemale(female);
            urogenital.SetFemale(female);
            respiratoryHead.Apply(selection == "Systems" || selection == "Respiratory", selection == "Nervous" ? isolated : isolated && selection != "Respiratory");
            root.Q<Button>("system-isolate").text = isolated ? "Context" : "Isolate";
            root.Q<Button>("system-isolate").EnableInClassList("selected", isolated);
            root.Q<Button>("system-circulatory").EnableInClassList("selected", selection == "Circulatory");
            root.Q<Button>("system-nervous").EnableInClassList("selected", selection == "Nervous");
            root.Q<Button>("system-urogenital").EnableInClassList("selected", selection == "Urogenital");
            root.Q<Button>("system-digestive").EnableInClassList("selected", selection == "Digestive");
            root.Q<Button>("system-respiratory").EnableInClassList("selected", selection == "Respiratory");
        }
        private void UpdateGender()
        {
            gender.text = female ? "♀ Female" : "♂ Male";
            gender.tooltip = female ? "Female: schematic ovaries, oviducts and ovisacs" : "Male: original testes and ducts";
            femaleModel.SetFemale(female); femaleModel.SetGhost(selection != "Organs" && selection != "Labeling"); UpdateNote();
            if (IsSystems) ApplySystem();
            if (IsStudyingOrgans) studyOrgans.SetGender(female);
            if (selection == "Urogenital") root.Q<Label>("anatomy-title").text = female ? "Female urogenital system" : "Male urogenital system";
        }
        private void UpdateNote() { note.text = selection + " • " + (female ? "Female (schematic reproductive parts)" : "Male") + (IsLabeling ? " • Drag labels into boxes" : ""); }
        private void Rotate(float horizontal, float vertical)
        { targetYaw += horizontal; targetPitch += vertical; }
        private void Move(float horizontal, float vertical)
        {
            if (IsLabeling || IsSystems || IsStudyingOrgans) Rotate(horizontal * 12f, vertical * 10f);
            else targetPan += new Vector2(horizontal, vertical) * .08f;
        }
        private void ResetCamera()
        {
            targetZoom = 1f; targetPan = Vector2.zero;
            float defaultYaw = IsLabeling ? 180f : 0f;
            targetYaw = yaw + Mathf.DeltaAngle(yaw, defaultYaw); targetPitch = pitch + Mathf.DeltaAngle(pitch, 0f);
        }
        private void ClearImageInsets()
        {
            Image image = preview.ViewImage;
            image.style.left = 0f; image.style.right = 0f; image.style.top = 0f; image.style.bottom = 0f;
        }
        public void Tick(float delta)
        {
            if (!visible) return;
            entryTime += delta;
            for (int i = 0; i < menu.Count; i++)
            {
                float t = Mathf.Clamp01((entryTime - .65f - i * .16f) / .45f);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                menu[i].style.opacity = eased; menu[i].style.translate = new Translate(-40f * (1f - eased), 0f);
            }
            bool portrait = Screen.width < Screen.height;
            bool compact = (float)Screen.width / Mathf.Max(1, Screen.height) > 1.9f;
            Image image = preview.ViewImage;
            image.style.left = IsSystems || IsStudyingOrgans ? 0f : IsLabeling ? 0f : portrait ? 0f : 210f;
            image.style.right = IsSystems || IsStudyingOrgans ? 0f : IsLabeling ? portrait ? 0f : 240f : portrait ? 0f : 210f;
            image.style.top = (IsSystems || IsStudyingOrgans) && portrait ? 120f : !IsLabeling && portrait ? 160f : 0f;
            image.style.bottom = IsLabeling ? portrait ? 345f : compact ? 0f : 145f : portrait ? 180f : compact ? 80f : 110f;
            if (IsSystems || IsStudyingOrgans) image.style.bottom = portrait ? 180f : 0f;
            if (IsStudyingOrgans && studyOrgans.HasSelection)
            {
                image.style.left = portrait ? 0f : 255f;
                image.style.right = portrait ? 0f : 90f;
                image.style.top = portrait ? 206f : 0f;
            }
            float easing = 1f - Mathf.Exp(-9f * delta);
            zoom = Mathf.Lerp(zoom, targetZoom, easing); yaw = Mathf.Lerp(yaw, targetYaw, easing);
            pitch = Mathf.Lerp(pitch, targetPitch, easing);
            Transform model = preview.Anatomy.transform;
            // Horizontal rotation flips the frog around its own head-to-tail axis.
            model.rotation = Quaternion.AngleAxis(yaw, Vector3.forward) * Quaternion.AngleAxis(pitch, Vector3.right);
            Bounds bounds = IsStudyingOrgans ? (studyOrgans.HasSelection ? studyOrgans.FocusBounds : organBounds) : selection == "Urogenital" ? urogenital.FocusBounds : selection == "Nervous" ? nervous.FocusBounds : selection == "Circulatory" ? circulatory.FocusBounds : selection == "Respiratory" ? respiratory.FocusBounds : selection == "Digestive" ? digestive.FocusBounds : selection == "Systems" ? systemsOverviewBounds : IsLabeling ? organBounds : fullBounds;
            Vector3 rotationPivot = IsLabeling || IsSystems || IsStudyingOrgans ? organBounds.center - initialPosition : pivot;
            model.position = initialPosition + rotationPivot - model.rotation * rotationPivot;
            Camera camera = preview.ViewCamera;
            camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            Vector3 center = bounds.center;
            camera.transform.position = center + Vector3.up * 60f;
            float aspect = Mathf.Max(.25f, image.contentRect.width / Mathf.Max(1f, image.contentRect.height));
            Vector3 extents = bounds.extents;
            if (IsLabeling) extents.x = Mathf.Max(7.4f, extents.x);
            Vector3 xAxis = model.rotation * Vector3.right, yAxis = model.rotation * Vector3.up, zAxis = model.rotation * Vector3.forward;
            float halfWidth = Mathf.Abs(xAxis.x) * extents.x + Mathf.Abs(yAxis.x) * extents.y + Mathf.Abs(zAxis.x) * extents.z;
            float halfHeight = Mathf.Abs(xAxis.z) * extents.x + Mathf.Abs(yAxis.z) * extents.y + Mathf.Abs(zAxis.z) * extents.z;
            // Rotation changes the view, never the user's magnification. Fit only the initial orientation.
            float size = Mathf.Max(extents.z, extents.x / aspect) * (IsLabeling ? 1.15f : 1.08f);
            if (IsSystems) size = Mathf.Max(extents.z, 3.4f / aspect) * (selection == "Nervous" ? 1.36f : selection == "Systems" ? 1.32f : 1.24f);
            if (IsStudyingOrgans) size = studyOrgans.HasSelection ? Mathf.Max(2.2f, extents.z * 1.55f, extents.x / aspect * 1.45f) : Mathf.Max(extents.z, 3.4f / aspect) * (portrait ? 1.18f : 1.1f);
            if (fittedSize <= 0f) { fittedSize = size; fittedCenter = center; }
            fittedSize = Mathf.Lerp(fittedSize, size, easing);
            Vector3 actualCenter = model.TransformPoint(center - initialPosition);
            fittedCenter = Vector3.Lerp(fittedCenter, actualCenter, easing);
            if (IsSystems || IsStudyingOrgans) { size = fittedSize; camera.transform.position = fittedCenter + Vector3.up * 60f; }
            // At close zoom, allow the camera to reach the extremities, including the head.
            float limitX = Mathf.Max(0f, halfWidth / size - targetZoom * aspect) + .12f;
            float limitY = Mathf.Max(0f, halfHeight / size - targetZoom) + .12f;
            targetPan = new Vector2(Mathf.Clamp(targetPan.x, -limitX, limitX), Mathf.Clamp(targetPan.y, -limitY, limitY));
            pan = Vector2.Lerp(pan, targetPan, easing);
            // Start closer, then pull out as the transition reveals the specimen.
            float intro = Mathf.Lerp(.78f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((entryTime - .55f) / 1.1f)));
            camera.orthographicSize = size * zoom * intro;
            camera.transform.position += (camera.transform.right * pan.x + camera.transform.up * pan.y) * size;
            labeling.SetFrame(organBounds.center, size);
            labeling.Tick(delta);
            studyOrgans.Tick(delta);
            digestive.Tick(); respiratory.Tick(); circulatory.Tick(); nervous.Tick(); urogenital.Tick();
        }
        public void Dispose()
        {
            foreach (Action action in cleanup) action(); cleanup.Clear();
            labeling.Dispose(); studyOrgans.Dispose(); femaleModel.Dispose();
            digestive.Dispose(); respiratory.Dispose(); circulatory.Dispose(); nervous.Dispose(); urogenital.Dispose(); respiratoryHead.Dispose();
            foreach (LayerMaterials layer in layers) layer.Dispose();
            UnityEngine.Object.Destroy(spleen); UnityEngine.Object.Destroy(spleenMaterial);
            UnityEngine.Object.Destroy(urogenitalCloaca); UnityEngine.Object.Destroy(urogenitalCloacaMaterial);
        }
    }

    internal sealed class LayerMaterials : IDisposable
    {
        public string Name { get; }
        private readonly Renderer[] renderers;
        private readonly Material[][] original, ghost;
        private readonly List<Material> owned = new List<Material>();
        public LayerMaterials(Transform layer)
        {
            Name = layer.name; renderers = layer.GetComponentsInChildren<Renderer>(true);
            original = new Material[renderers.Length][]; ghost = new Material[renderers.Length][];
            var cache = new Dictionary<Material, Material>();
            for (int i = 0; i < renderers.Length; i++)
            {
                original[i] = renderers[i].sharedMaterials; ghost[i] = new Material[original[i].Length];
                for (int j = 0; j < original[i].Length; j++)
                {
                    Material source = original[i][j];
                    if (!cache.TryGetValue(source, out Material material))
                    {
                        material = MakeGhost(source); cache.Add(source, material); owned.Add(material);
                    }
                    ghost[i][j] = material;
                }
            }
        }
        internal static Material MakeGhost(Material source)
        {
            var material = new Material(source) { name = source.name + " Study Ghost (5% opacity)", renderQueue = 3000 };
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f); material.SetFloat("_SpecularHighlights", 0f); material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON"); material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("DepthOnly", false); material.SetShaderPassEnabled("ShadowCaster", false);
            Color color = material.GetColor("_BaseColor"); color.a = .05f; material.SetColor("_BaseColor", color);
            return material;
        }
        public void Apply(bool selected, bool hidden)
        {
            for (int i = 0; i < renderers.Length; i++)
            { renderers[i].enabled = !hidden && renderers[i].name != "Sclera"; renderers[i].sharedMaterials = selected ? original[i] : ghost[i]; }
        }
        public void ApplySystemParts(bool digestive, bool respiratory, bool circulatory, bool nervous, bool urogenital, bool isolated)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                string name = renderers[i].name;
                bool focused = digestive || respiratory || circulatory || nervous || urogenital;
                bool included = digestive && (name.StartsWith("Liver") || name == "Gall Bladder" || name == "Pancreas" || name == "Stomach and Intestines") || respiratory && (name == "Lung" || name == "Lung 1") || circulatory && (name == "Heart" || name == "Vein" || name == "Vein 1" || name == "Pulmonary Vein" || name == "Abdominal Vein") || nervous && (name == "Cerebrum" || name == "Cerebellum" || name == "Diencephalon" || name == "Hypothalamus" || name.StartsWith("Olfactory Lobe") || name.StartsWith("Optic Lobe") || name == "Spinal Cord" || name == "Infundibular Duct (Review)") || urogenital && (name.StartsWith("Fat Body") || name.StartsWith("Kidney") || name.StartsWith("Wolffian Duct") || name == "Urinary Bladder" || name.StartsWith("Testis") || name.StartsWith("Tubule (Review)"));
                renderers[i].enabled = (!focused || name != "Sclera") && (!isolated || included);
                renderers[i].sharedMaterials = !focused || included ? original[i] : ghost[i];
            }
        }
        public void ApplyHeadContext(bool ghosted, bool hidden)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                string name = renderers[i].name;
                bool head = name == "Mylohyoid" || name.StartsWith("Masseter") || name.StartsWith("Temporalis") || name.StartsWith("Depressor Mandibulae");
                renderers[i].enabled = head && !hidden;
                renderers[i].sharedMaterials = ghosted ? ghost[i] : original[i];
            }
        }
        public void Dispose()
        {
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].sharedMaterials = original[i];
            foreach (Material material in owned) UnityEngine.Object.Destroy(material);
        }
    }

}
