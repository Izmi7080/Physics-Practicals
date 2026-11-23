using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BiologyLab
{
    // Handles mesh picking and the per-organ study state; the parent owns the camera and lifecycle.
    internal sealed class StudyOrgansController : IDisposable
    {
        private sealed class OrganEntry
        {
            public string Name;
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public Bounds Bounds;
            public bool FemaleOnly, MaleOnly;
        }

        private readonly VisualElement root;
        private readonly FrogPreviewController preview;
        private readonly List<OrganEntry> entries = new List<OrganEntry>();
        private readonly Dictionary<Collider, OrganEntry> hitTargets = new Dictionary<Collider, OrganEntry>();
        private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<Renderer, Material[]> ghosts = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<Renderer, Material[]> highlights = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<Material, Material> ghostCache = new Dictionary<Material, Material>();
        private readonly Dictionary<Material, Material> highlightCache = new Dictionary<Material, Material>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<Collider> ownedColliders = new List<Collider>();
        private readonly List<ButtonFeedback> indexFeedback = new List<ButtonFeedback>();
        private readonly List<Action> indexCleanup = new List<Action>();
        private readonly Dictionary<OrganEntry, Button> indexButtons = new Dictionary<OrganEntry, Button>();
        private readonly HashSet<OrganEntry> hidden = new HashSet<OrganEntry>();
        private Image image;
        private VisualElement panel, tip, tools, hand, diagram;
        private VisualElement indexPanel, cameraControls;
        private ScrollView indexList;
        private Button indexToggle;
        private Renderer heartRenderer;
        private Label nameLabel, locationLabel, functionLabel;
        private Button isolateButton, hideButton, unhideButton, restartButton;
        private OrganEntry selected;
        private bool visible, female, isolated, browseOpen;
        public bool HasSelection => selected != null;
        public Bounds FocusBounds => selected != null ? selected.Bounds : default(Bounds);

        public StudyOrgansController(VisualElement root, FrogPreviewController preview)
        { this.root = root; this.preview = preview; }

        public void Init()
        {
            image = root.Q<Image>("anatomy-image");
            panel = root.Q("organ-study-panel"); tip = root.Q("organ-study-tip"); tools = root.Q("organ-study-tools");
            hand = root.Q("organ-study-hand"); diagram = root.Q("diagram");
            indexPanel = root.Q("organ-index-panel"); indexList = root.Q<ScrollView>("organ-index-list");
            indexToggle = root.Q<Button>("organ-index-toggle"); cameraControls = root.Q("camera-controls");
            nameLabel = root.Q<Label>("organ-study-name");
            locationLabel = root.Q<Label>("organ-study-location");
            functionLabel = root.Q<Label>("organ-study-function");
            isolateButton = root.Q<Button>("organ-study-isolate");
            hideButton = root.Q<Button>("organ-study-hide");
            unhideButton = root.Q<Button>("organ-study-unhide");
            restartButton = root.Q<Button>("organ-study-restart");
        }

        public void Start()
        {
            Transform organs = preview.Anatomy.transform.Find("Internal Organs");
            heartRenderer = organs.Find("Heart").GetComponent<Renderer>();
            foreach (Renderer renderer in organs.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "Sclera") continue;
                var entry = new OrganEntry { Name = DisplayName(renderer.name),
                    MaleOnly = renderer.name.StartsWith("Testis") || renderer.name.StartsWith("Tubule (Review)") };
                AddRenderer(entry, renderer); entries.Add(entry);
            }
            Transform femaleParts = preview.Anatomy.transform.Find(FemaleAnatomyModel.RootName);
            foreach (Renderer renderer in femaleParts.GetComponentsInChildren<Renderer>(true))
            {
                var entry = new OrganEntry { Name = renderer.name, FemaleOnly = true };
                AddRenderer(entry, renderer);
                entries.Add(entry);
            }
            foreach (OrganEntry entry in entries)
            {
                var button = new Button { text = entry.Name };
                button.AddToClassList("action"); button.AddToClassList("organ-index-item");
                Action select = () => SelectEntry(entry);
                button.clicked += select; indexCleanup.Add(() => button.clicked -= select);
                indexList.Add(button); indexButtons.Add(entry, button);
                var feedback = new ButtonFeedback(button); feedback.Init(); feedback.Start(); indexFeedback.Add(feedback);
            }
            image.RegisterCallback<PointerDownEvent>(OnPointerDown);
            indexToggle.clicked += ToggleBrowse;
            isolateButton.clicked += ToggleIsolate; hideButton.clicked += Hide; unhideButton.clicked += Unhide; restartButton.clicked += Restart;
            SetVisible(false);
        }

        private static string DisplayName(string source)
        {
            switch (source)
            {
                case "Liver": return "Liver lobe 1"; case "Liver 1": return "Liver lobe 2"; case "Liver 2": return "Liver lobe 3";
                case "Lung": return "Right lung"; case "Lung 1": return "Left lung";
                case "Kidney": return "Left kidney"; case "Kidney 1": return "Right kidney";
                case "Fat Body": return "Left fat body"; case "Fat Body 1": return "Right fat body";
                case "Wolffian Duct": return "Left urogenital duct"; case "Wolffian Duct 1": return "Right urogenital duct";
                case "Testis": return "Left testis"; case "Testis 1": return "Right testis";
                case "Tubule (Review)": return "Left testicular tubules"; case "Tubule (Review) 1": return "Right testicular tubules";
                case "Urinary Bladder": return "Bladder";
                case "Stomach and Intestines": return "Stomach and intestines";
                case "Spleen (Schematic Supplement)": return "Spleen";
                default: return source.Replace("(Review)", "").Trim();
            }
        }

        private void AddRenderer(OrganEntry entry, Renderer renderer)
        {
            entry.Renderers.Add(renderer);
            if (entry.Renderers.Count == 1) entry.Bounds = renderer.bounds;
            else { Bounds bounds = entry.Bounds; bounds.Encapsulate(renderer.bounds); entry.Bounds = bounds; }
            Material[] source = renderer.sharedMaterials;
            originals.Add(renderer, source);
            var faded = new Material[source.Length]; var lit = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (!ghostCache.TryGetValue(source[i], out Material ghost))
                {
                    ghost = LayerMaterials.MakeGhost(source[i]); ghostCache.Add(source[i], ghost); ownedMaterials.Add(ghost);
                }
                if (!highlightCache.TryGetValue(source[i], out Material highlight))
                {
                    highlight = new Material(source[i]) { name = source[i].name + " Organ Highlight" };
                    if (highlight.HasProperty("_BaseColor"))
                    {
                        Color color = highlight.GetColor("_BaseColor");
                        highlight.SetColor("_BaseColor", Color.Lerp(color, new Color(1f,.85f,.42f,color.a), .12f));
                    }
                    if (highlight.HasProperty("_EmissionColor"))
                    {
                        highlight.EnableKeyword("_EMISSION"); highlight.SetColor("_EmissionColor", new Color(.06f,.04f,.006f));
                    }
                    highlightCache.Add(source[i], highlight); ownedMaterials.Add(highlight);
                }
                faded[i] = ghost; lit[i] = highlight;
            }
            ghosts.Add(renderer, faded); highlights.Add(renderer, lit);
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            MeshCollider collider = renderer.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh; collider.enabled = false;
            ownedColliders.Add(collider); hitTargets.Add(collider, entry);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!visible || evt.button != 0 || image.contentRect.width < 1f || image.contentRect.height < 1f) return;
            Vector2 point = evt.localPosition;
            if (point.x < 0f || point.y < 0f || point.x > image.contentRect.width || point.y > image.contentRect.height) return;
            Ray ray = preview.ViewCamera.ViewportPointToRay(new Vector3(point.x / image.contentRect.width, 1f - point.y / image.contentRect.height, 0f));
            Physics.SyncTransforms();
            OrganEntry nearest = null; float distance = float.PositiveInfinity;
            bool backfaces = Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces = true;
                foreach (KeyValuePair<Collider, OrganEntry> pair in hitTargets)
                {
                    if (!Eligible(pair.Value) || hidden.Contains(pair.Value) || !pair.Key.gameObject.activeInHierarchy) continue;
                    RaycastHit hit;
                    if (pair.Key.Raycast(ray, out hit, 250f) && hit.distance < distance) { nearest = pair.Value; distance = hit.distance; }
                }
            }
            finally { Physics.queriesHitBackfaces = backfaces; }
            if (nearest == null) return;
            SelectEntry(nearest); evt.StopPropagation();
        }

        private void SelectEntry(OrganEntry entry)
        {
            if (!visible || !Eligible(entry)) return;
            selected = entry; isolated = false; browseOpen = false;
            Apply(); UpdatePanel();
        }
        private void ToggleBrowse() { browseOpen = !browseOpen; UpdatePanel(); }

        private bool Eligible(OrganEntry entry) => (!entry.FemaleOnly || female) && (!entry.MaleOnly || !female);
        private string Caption(OrganEntry entry) => female ? entry.Name.Replace("urogenital duct", "ureter") : entry.Name;
        public void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            if (value) Restart();
            else { selected = null; hidden.Clear(); isolated = false; browseOpen = false; }
            foreach (Collider collider in ownedColliders) if (collider != null) collider.enabled = value;
            UpdatePanel();
        }
        public void SetGender(bool value)
        {
            female = value;
            if (!visible) return;
            if (selected != null && !Eligible(selected)) { selected = null; isolated = false; }
            foreach (KeyValuePair<OrganEntry, Button> pair in indexButtons)
            {
                pair.Value.EnableInClassList("hidden", !Eligible(pair.Key));
                pair.Value.text = Caption(pair.Key);
            }
            Apply(); UpdatePanel();
        }
        public void Restart()
        { selected = null; hidden.Clear(); isolated = false; browseOpen = false; if (visible) Apply(); UpdatePanel(); }
        private void ToggleIsolate() { if (selected == null) return; isolated = !isolated; Apply(); UpdatePanel(); }
        private void Hide() { if (selected == null) return; hidden.Add(selected); Apply(); UpdatePanel(); }
        private void Unhide() { if (selected == null) return; hidden.Remove(selected); Apply(); UpdatePanel(); }

        private void Apply()
        {
            if (!visible) return;
            foreach (OrganEntry entry in entries)
                foreach (Renderer renderer in entry.Renderers)
                {
                    renderer.enabled = Eligible(entry) && !hidden.Contains(entry);
                    renderer.sharedMaterials = isolated && entry != selected ? ghosts[renderer]
                        : entry == selected && !hidden.Contains(entry) ? highlights[renderer] : originals[renderer];
                }
        }
        private void RestoreMaterials()
        { foreach (KeyValuePair<Renderer, Material[]> pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value; }
        private void UpdatePanel()
        {
            if (panel == null) return;
            bool focused = visible && selected != null;
            panel.EnableInClassList("hidden", !focused);
            tip.EnableInClassList("hidden", !visible || focused);
            hand.EnableInClassList("hidden", !visible || focused);
            indexToggle.EnableInClassList("hidden", !visible || focused);
            indexPanel.EnableInClassList("hidden", !visible || focused || !browseOpen);
            cameraControls.EnableInClassList("hidden", visible && browseOpen && !focused);
            tools.EnableInClassList("hidden", !focused);
            restartButton.EnableInClassList("hidden", !visible);
            if (!focused) return;
            nameLabel.text = Caption(selected);
            Describe(nameLabel.text, out string location, out string function);
            locationLabel.text = location; functionLabel.text = function;
            isolateButton.text = isolated ? "Context" : "Isolate";
            isolateButton.EnableInClassList("selected", isolated);
            hideButton.SetEnabled(!hidden.Contains(selected));
            unhideButton.SetEnabled(hidden.Contains(selected));
        }

        public void Tick(float delta)
        {
            if (visible && browseOpen) foreach (ButtonFeedback feedback in indexFeedback) feedback.Tick(delta);
            if (!visible || selected != null || heartRenderer == null) return;
            Vector3 point = preview.ViewCamera.WorldToViewportPoint(heartRenderer.bounds.center);
            if (point.z <= 0f || image.contentRect.width <= 0f) return;
            hand.style.left = image.worldBound.xMin - diagram.worldBound.xMin + point.x * image.contentRect.width - 25f;
            hand.style.top = image.worldBound.yMin - diagram.worldBound.yMin + (1f - point.y) * image.contentRect.height - 3f;
        }

        private static void Describe(string name, out string location, out string function)
        {
            if (name.StartsWith("Liver")) { location = "Below the heart, across the upper abdomen."; function = "Produces bile, which helps digest fats."; }
            else if (name.EndsWith("lung")) { location = "Paired organs beside the heart in the upper body cavity."; function = "Exchanges oxygen and carbon dioxide."; }
            else if (name == "Heart") { location = "Between the lungs, above the liver."; function = "Pumps blood through the body and lungs."; }
            else if (name.Contains("kidney")) { location = "Along the rear wall of the body cavity."; function = "Filters blood and helps form urine."; }
            else if (name.Contains("fat body")) { location = "Beside the kidneys and reproductive structures."; function = "Stores energy for reproduction and hibernation."; }
            else if (name.Contains("ovary")) { location = "Near and on the belly side of the kidney."; function = "Produces eggs and releases them into the body cavity. Shown schematically."; }
            else if (name.Contains("oviduct funnel")) { location = "Anterior opening of the oviduct, near the lung."; function = "Receives eggs from the body cavity. Shown schematically."; }
            else if (name.Contains("oviduct")) { location = "Coiled tube outside the ovary along the body wall."; function = "Carries eggs from the funnel toward the ovisac. Shown schematically."; }
            else if (name.Contains("ovisac")) { location = "Widened posterior end of the oviduct, near the cloaca."; function = "Temporarily holds eggs. Each ovisac opens separately into the cloaca. Shown schematically."; }
            else if (name.Contains("testis") || name.Contains("testicular")) { location = "Near the kidneys in the male body cavity."; function = "Produces or conveys sperm."; }
            else if (name.Contains("ureter")) { location = "Runs from the kidney to the cloaca."; function = "Carries urine through a route separate from the oviduct."; }
            else if (name.Contains("urogenital duct")) { location = "Runs from the kidney toward the cloaca."; function = "Carries urine and sperm in the male frog."; }
            else if (name == "Bladder") { location = "Near the posterior end of the body cavity."; function = "Temporarily stores urine."; }
            else if (name == "Stomach and intestines") { location = "Coiled below the liver in the abdomen."; function = "Digests food and absorbs nutrients."; }
            else if (name == "Gall Bladder") { location = "Under the liver."; function = "Stores bile made by the liver."; }
            else if (name == "Pancreas") { location = "Beside the stomach and small intestine."; function = "Releases digestive enzymes and hormones."; }
            else if (name == "Spleen") { location = "Near the digestive tract."; function = "Helps filter blood. This small mesh is schematic."; }
            else if (name == "Pulmonary Vein") { location = "Between the lungs and the heart."; function = "Carries oxygenated blood from the lungs to the heart."; }
            else if (name == "Abdominal Vein") { location = "Along the abdomen below the heart."; function = "Returns blood from the abdominal region."; }
            else if (name.StartsWith("Vein")) { location = "In the internal blood-vessel network."; function = "Returns blood toward the heart."; }
            else if (name.Contains("Infundibular Duct")) { location = "At the base of the brain."; function = "Links the hypothalamic region with the pituitary region."; }
            else if (name == "Cerebrum") { location = "Forebrain, behind the olfactory lobes."; function = "Helps process sensory input and coordinate responses."; }
            else if (name == "Cerebellum") { location = "Hindbrain, before the spinal cord."; function = "Helps coordinate movement and balance."; }
            else if (name == "Diencephalon") { location = "Forebrain, behind the cerebral hemispheres."; function = "Relays signals and helps regulate internal functions."; }
            else if (name == "Hypothalamus") { location = "At the base of the diencephalon."; function = "Helps regulate body functions and hormone signaling."; }
            else if (name.StartsWith("Olfactory Lobe")) { location = "At the front of the brain."; function = "Receives smell-related sensory information."; }
            else if (name.StartsWith("Optic Lobe")) { location = "In the midbrain."; function = "Processes visual information."; }
            else if (name == "Spinal Cord") { location = "Extends from the brain along the back."; function = "Relays signals between the brain and the body."; }
            else { location = "Within the frog's internal anatomy."; function = "Part of the named organ system; rotate the model to inspect its connections."; }
        }

        public void Dispose()
        {
            image.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            indexToggle.clicked -= ToggleBrowse;
            isolateButton.clicked -= ToggleIsolate; hideButton.clicked -= Hide; unhideButton.clicked -= Unhide; restartButton.clicked -= Restart;
            foreach (Action action in indexCleanup) action();
            foreach (ButtonFeedback feedback in indexFeedback) feedback.Dispose();
            RestoreMaterials();
            foreach (Collider collider in ownedColliders) if (collider != null) UnityEngine.Object.Destroy(collider);
            foreach (Material material in ownedMaterials) UnityEngine.Object.Destroy(material);
        }
    }
}
