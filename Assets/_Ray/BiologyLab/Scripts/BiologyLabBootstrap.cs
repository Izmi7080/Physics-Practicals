using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BiologyLab
{
    // The scene's single bootstrap owns all module lifetimes.
    [RequireComponent(typeof(UIDocument))]
    public sealed class BiologyLabBootstrap : MonoBehaviour
    {
        public GameObject skinAsset;
        public GameObject anatomyPrefab;
        public AnimationClip idleClip;
        private LabController controller;
        private PanelSettings originalSettings, runtimeSettings;

        private void OnEnable()
        {
            var document = GetComponent<UIDocument>();
            originalSettings = document.panelSettings;
            runtimeSettings = Instantiate(originalSettings);
            document.panelSettings = runtimeSettings;
            controller = new LabController(document.rootVisualElement, runtimeSettings, skinAsset, anatomyPrefab, idleClip);
            controller.Init();
            controller.Start();
        }
        private void Update()
        {
            // INTEGRATION: Android back (Escape) steps back one screen, or leaves from the welcome screen.
            if (controller != null && TajarbagahLink.BackPressedThisFrame()) controller.Back();
            controller?.Tick(Time.unscaledDeltaTime);
        }
        private void OnDisable()
        {
            controller?.Dispose(); controller = null;
            GetComponent<UIDocument>().panelSettings = originalSettings;
            if (runtimeSettings != null) Destroy(runtimeSettings);
        }
    }

    internal sealed class LabController : IDisposable
    {
        private readonly VisualElement root;
        private readonly PanelSettings panel;
        private readonly FrogPreviewController preview;
        private ExternalAnnotationsController annotations;
        private InternalAnatomyController internalAnatomy;
        private readonly List<ButtonFeedback> feedback = new List<ButtonFeedback>();
        private VisualElement app, safe, welcome, study, anatomyScreen, transition;
        private Image guideImage, anatomyImage;
        private Button anatomyHome;
        private float transitionTime = -1f;
        private bool transitionSwitched;
        private string pendingView;
        private Button start, home, external, internalButton, back, quit;
        private float startDelay = -1f;
        private Vector2Int lastSize;
        private Rect lastSafeArea;

        public LabController(VisualElement root, PanelSettings panel, GameObject skin, GameObject anatomy, AnimationClip clip)
        {
            this.root = root;
            this.panel = panel;
            preview = new FrogPreviewController(skin, anatomy, clip);
        }
        public void Init()
        {
            app = root.Q("app"); safe = root.Q("safe"); welcome = root.Q("welcome"); study = root.Q("study");
            anatomyScreen = root.Q("anatomy-screen"); transition = root.Q("transition");
            anatomyHome = root.Q<Button>("anatomy-home");
            guideImage = root.Q<Image>("frog-image"); anatomyImage = root.Q<Image>("anatomy-image");
            start = root.Q<Button>("start"); home = root.Q<Button>("home");
            quit = root.Q<Button>("quit");
            external = root.Q<Button>("external"); internalButton = root.Q<Button>("internal"); back = root.Q<Button>("back");
            foreach (Button button in root.Query<Button>().ToList()) { var child = new ButtonFeedback(button); child.Init(); feedback.Add(child); }
            preview.Init(guideImage);
            annotations = new ExternalAnnotationsController(root.Q("diagram"), preview);
            annotations.Init();
            internalAnatomy = new InternalAnatomyController(root, preview);
            internalAnatomy.Init();
        }
        public void Start()
        {
            preview.Start();
            annotations.Start();
            internalAnatomy.Start();
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            foreach (var child in feedback) child.Start();
            start.clicked += StartExploring; home.clicked += OpenHome;
            quit.clicked += QuitExperiment;
            external.clicked += OpenExternal; internalButton.clicked += OpenInternal; back.clicked += OpenGuide;
            anatomyHome.clicked += AnatomyHome;
            root.Q<Button>("internal-quit").clicked += QuitExperiment;
            OpenHome();
            ApplyLayout();
        }
        public void Tick(float delta)
        {
            ApplyLayout();
            foreach (var child in feedback) child.Tick(delta);
            if (startDelay >= 0f)
            {
                startDelay += delta;
                if (startDelay >= .32f) { startDelay = -1f; OpenGuide(); }
            }
            preview.Tick(delta);
            annotations.Tick();
            internalAnatomy.Tick(delta);
            TickTransition(delta);
        }
        private void ApplyLayout()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            Rect area = Screen.safeArea;
            if (size == lastSize && area == lastSafeArea) return;
            lastSize = size; lastSafeArea = area;
            bool portrait = size.x < size.y;
            bool wide = (float)size.x / Mathf.Max(1, size.y) > 1.9f;
            panel.referenceResolution = portrait ? new Vector2Int(480,960) : wide ? new Vector2Int(960,540) : new Vector2Int(1280,800);
            app.EnableInClassList("portrait", size.x < size.y);
            app.EnableInClassList("compact", (float)size.x / Mathf.Max(1, size.y) > 1.9f);
            // Convert physical safe-area pixels into the scaled panel's coordinate space.
            float xScale = root.resolvedStyle.width / Mathf.Max(1, size.x);
            float yScale = root.resolvedStyle.height / Mathf.Max(1, size.y);
            if (!float.IsNaN(xScale) && !float.IsNaN(yScale))
            {
                safe.style.marginLeft = area.xMin * xScale;
                safe.style.marginRight = (size.x - area.xMax) * xScale;
                safe.style.marginTop = (size.y - area.yMax) * yScale;
                safe.style.marginBottom = area.yMin * yScale;
            }
        }
        private void OnGeometryChanged(GeometryChangedEvent evt) { lastSize = Vector2Int.zero; ApplyLayout(); }
        private void OpenHome()
        {
            startDelay = -1f;
            internalAnatomy.SetVisible(false);
            CancelTransition(); welcome.RemoveFromClassList("hidden"); study.AddToClassList("hidden");
            anatomyScreen.AddToClassList("hidden"); annotations.SetVisible(false); preview.SetVisible(false);
        }
        private void StartExploring()
        {
            // Keep the welcome screen visible until the click bounce finishes.
            if (startDelay < 0f) startDelay = 0f;
        }
        // INTEGRATION: back button. Any inner screen returns to the welcome screen; the welcome screen exits.
        public void Back()
        {
            if (welcome.ClassListContains("hidden")) OpenHome(); else QuitExperiment();
        }
        private void QuitExperiment()
        {
            startDelay = -1f;
            // INTEGRATION: inside Tajarbagah, return to the app's main menu instead of quitting.
            if (TajarbagahLink.ExitToApp()) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void OpenGuide()
        {
            internalAnatomy.SetVisible(false);
            CancelTransition(); anatomyScreen.AddToClassList("hidden"); annotations.SetVisible(false);
            preview.SetPresentation(guideImage, false); ShowStudy("guide");
        }
        private void AnatomyHome()
        {
            if (internalAnatomy.IsLabeling || internalAnatomy.IsSystems || internalAnatomy.IsStudyingOrgans) internalAnatomy.SelectMuscles();
            else OpenGuide();
        }
        private void OpenExternal() { BeginTransition("external"); }
        private void OpenInternal() { BeginTransition("internal"); }
        private void BeginTransition(string view)
        {
            if (transitionTime >= 0f) return;
            pendingView = view; transitionTime = 0f; transitionSwitched = false;
            root.Q<Label>("transition-label").text = view == "external" ? "External anatomy" : "Internal anatomy";
            root.Q("transition-progress").style.width = Length.Percent(0f);
            transition.style.opacity = 0f; transition.RemoveFromClassList("hidden");
        }
        private void TickTransition(float delta)
        {
            if (transitionTime < 0f) return;
            transitionTime += delta;
            root.Q("transition-progress").style.width = Length.Percent(Mathf.Min(100f, transitionTime * 50f));
            transition.style.opacity = transitionTime < .4f ? transitionTime / .4f : transitionTime < 1f ? 1f : 2f - transitionTime;
            if (!transitionSwitched && transitionTime >= 1f)
            {
                transitionSwitched = true;
                welcome.AddToClassList("hidden"); study.AddToClassList("hidden"); anatomyScreen.RemoveFromClassList("hidden");
                bool externalView = pendingView == "external";
                root.Q<Label>("anatomy-title").text = externalView ? "External anatomy" : "Internal anatomy";
                preview.SetPresentation(anatomyImage, externalView); preview.SetVisible(true); preview.SetInternal(!externalView);
                annotations.SetVisible(externalView);
                internalAnatomy.SetVisible(!externalView);
            }
            if (transitionTime >= 2f) CancelTransition();
        }
        private void CancelTransition() { transitionTime = -1f; transition.AddToClassList("hidden"); transition.style.opacity = 1f; }
        private void ShowStudy(string view)
        {
            welcome.AddToClassList("hidden"); study.RemoveFromClassList("hidden");
            preview.SetVisible(true); preview.SetInternal(view == "internal");
            root.Q<Label>("heading").text = view == "guide" ? "Meet the frog." : view == "external" ? "External anatomy." : "Internal anatomy.";
            root.Q<Label>("guide").text = view == "guide"
                ? "Identification of major organs and organ systems in a dissected frog (dissection by demonstrator / teacher)."
                : view == "external"
                ? "Observe the head, trunk, eyes, nostrils, forelimbs and hindlimbs. Compare their positions and shapes."
                : "Observe the organs, muscles and skeleton. Identify the major structures and the organ systems they belong to.";
            root.Q<Label>("hint").text = view == "guide" ? "Choose a view to explore the frog’s anatomy." : view == "external" ? "The frog is playing its original Idle1 animation." : "The skin is hidden to reveal the static dissection layers.";
            root.Q<Label>("view-label").text = view == "internal" ? "Internal view" : "External view";
            root.Q<Label>("pose-label").text = view == "internal" ? "Organs • Muscles • Skeleton" : "Idle animation • Observe its natural posture";
            back.EnableInClassList("hidden", view == "guide");
        }
        public void Dispose()
        {
            start.clicked -= StartExploring; home.clicked -= OpenHome; external.clicked -= OpenExternal;
            quit.clicked -= QuitExperiment;
            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            internalButton.clicked -= OpenInternal; back.clicked -= OpenGuide;
            anatomyHome.clicked -= AnatomyHome;
            root.Q<Button>("internal-quit").clicked -= QuitExperiment;
            foreach (var child in feedback) child.Dispose();
            annotations.Dispose();
            internalAnatomy.Dispose();
            preview.Dispose();
        }
    }

    internal sealed class ButtonFeedback : IDisposable
    {
        private readonly Button button;
        private float scale = 1f, target = 1f, bounce = -1f;
        private bool hovered;
        private float pulseTime;
        public ButtonFeedback(Button button) { this.button = button; }
        public void Init() { scale = target = 1f; }
        public void Start()
        {
            button.RegisterCallback<PointerEnterEvent>(Enter); button.RegisterCallback<PointerLeaveEvent>(Leave);
            button.RegisterCallback<PointerDownEvent>(Down); button.RegisterCallback<PointerUpEvent>(Up);
            button.RegisterCallback<PointerCancelEvent>(Cancel); button.clicked += Click;
            button.RegisterCallback<MouseEnterEvent>(MouseEnter); button.RegisterCallback<MouseLeaveEvent>(MouseLeave);
        }
        private float HoverScale => button.name == "start" ? 1.07f : 1.035f;
        private void MouseEnter(MouseEnterEvent evt) { hovered = true; target = HoverScale; }
        private void MouseLeave(MouseLeaveEvent evt) { hovered = false; target = 1f; }
        private void Enter(PointerEnterEvent evt) { hovered = evt.pointerType == "mouse"; target = hovered ? HoverScale : 1f; }
        private void Leave(PointerLeaveEvent evt) { hovered = false; target = 1f; }
        private void Down(PointerDownEvent evt) { target = .95f; }
        private void Up(PointerUpEvent evt) { target = hovered ? HoverScale : 1f; }
        private void Cancel(PointerCancelEvent evt) { hovered = false; target = 1f; }
        private void Click() { bounce = 0f; }
        public void Tick(float delta)
        {
            scale = Mathf.Lerp(scale, target, 1f - Mathf.Exp(-18f * delta));
            float value = scale;
            pulseTime = (pulseTime + delta) % 2f;
            if (button.name == "start" && !hovered && target == 1f && bounce < 0f)
                value += .025f * (.5f - .5f * Mathf.Cos(pulseTime * Mathf.PI));
            if (bounce >= 0f) { bounce += delta; float t = bounce / .32f; if (t >= 1f) bounce = -1f; else value += Mathf.Sin(t * Mathf.PI * 2f) * .075f * (1f - t); }
            button.style.scale = new Scale(Vector3.one * value);
        }
        public void Dispose()
        {
            button.UnregisterCallback<PointerEnterEvent>(Enter); button.UnregisterCallback<PointerLeaveEvent>(Leave);
            button.UnregisterCallback<PointerDownEvent>(Down); button.UnregisterCallback<PointerUpEvent>(Up);
            button.UnregisterCallback<PointerCancelEvent>(Cancel); button.clicked -= Click;
            button.UnregisterCallback<MouseEnterEvent>(MouseEnter); button.UnregisterCallback<MouseLeaveEvent>(MouseLeave);
            button.style.scale = new Scale(Vector3.one);
        }
    }

    internal sealed class FrogPreviewController : IDisposable
    {
        private readonly GameObject skinAsset, anatomyAsset;
        private readonly AnimationClip clip;
        private Image image;
        private GameObject stage, skin, anatomy, cameraObject, lightObject;
        private Camera camera;
        private RenderTexture texture;
        private PlayableGraph graph;
        private AnimationClipPlayable playable;
        private bool visible, isInternal;
        private bool externalPresentation;
        public GameObject Skin => skin;
        public GameObject Anatomy => anatomy;
        public Image ViewImage => image;
        public Camera ViewCamera => camera;
        private float time;
        private readonly Vector3 origin = new Vector3(1000f, 1000f, 1000f);

        public FrogPreviewController(GameObject skin, GameObject anatomy, AnimationClip clip)
        { skinAsset = skin; anatomyAsset = anatomy; this.clip = clip; }
        public void Init(Image image) { this.image = image; }
        public void Start()
        {
            stage = new GameObject("Frog Preview Stage"); stage.transform.position = origin;
            skin = UnityEngine.Object.Instantiate(skinAsset, stage.transform);
            anatomy = UnityEngine.Object.Instantiate(anatomyAsset, stage.transform);
            foreach (Transform layer in anatomy.transform) if (layer.name == "Skin") layer.gameObject.SetActive(false);
            SetLayer(stage, 31);
            var animator = skin.GetComponent<Animator>() ?? skin.AddComponent<Animator>();
            animator.enabled = true; animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("BiologyLabIdle"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            var output = AnimationPlayableOutput.Create(graph, "FrogIdle", animator); output.SetSourcePlayable(playable);
            graph.Play(); graph.Evaluate(0f);
            cameraObject = new GameObject("Frog Preview Camera");
            camera = cameraObject.AddComponent<Camera>(); camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(240f/255f,222f/255f,182f/255f,1f);
            camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 250f;
            lightObject = new GameObject("Frog Preview Key Light");
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2f;
            light.cullingMask = 1 << 31; light.shadows = LightShadows.None;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            image.scaleMode = ScaleMode.ScaleToFit;
            SetInternal(false);
        }
        private static void SetLayer(GameObject root, int layer) { foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer; }
        public void SetVisible(bool value) { visible = value; stage.SetActive(value); cameraObject.SetActive(value); lightObject.SetActive(value); }
        public void SetPresentation(Image target, bool externalView)
        {
            if (image != null) image.image = null;
            image = target; image.scaleMode = ScaleMode.ScaleToFit; image.image = texture;
            externalPresentation = externalView;
            camera.backgroundColor = externalView || target.name == "anatomy-image"
                ? new Color(235f/255f,216f/255f,170f/255f,1f) : new Color(240f/255f,222f/255f,182f/255f,1f);
        }
        public void SetInternal(bool value)
        {
            isInternal = value; skin.SetActive(!value); anatomy.SetActive(value); time = 0f;
            if (!value) { playable.SetTime(0); graph.Evaluate(0); }
            FrameModel();
        }
        private Bounds ModelBounds()
        {
            GameObject model = isInternal ? anatomy : skin;
            var bounds = new Bounds(); bool first = true;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                Bounds part = renderer.bounds;
                // Imported animated bounds include the extended feeding tongue. Use the idle pose vertices.
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    var mesh = new Mesh(); skinned.BakeMesh(mesh);
                    bool partFirst = true;
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Vector3 point = skinned.transform.TransformPoint(vertex);
                        if (partFirst) { part = new Bounds(point, Vector3.zero); partFirst = false; } else part.Encapsulate(point);
                    }
                    UnityEngine.Object.Destroy(mesh);
                }
                if (first) { bounds = part; first = false; } else bounds.Encapsulate(part);
            }
            return bounds;
        }
        private void FrameModel()
        {
            Bounds bounds = ModelBounds();
            camera.transform.position = bounds.center + (isInternal ? new Vector3(0f,60f,0f) : externalPresentation ? new Vector3(50f,17f,12f) : new Vector3(28f,32f,38f));
            if (isInternal) camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            else camera.transform.LookAt(bounds.center);
            float width = 0f, height = 0f;
            GameObject model = isInternal ? anatomy : skin;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                Mesh temporary = null;
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skinned) { temporary = new Mesh(); skinned.BakeMesh(temporary); mesh = temporary; }
                else { var filter = renderer.GetComponent<MeshFilter>(); mesh = filter != null ? filter.sharedMesh : null; }
                if (mesh != null) foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 local = camera.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    width = Mathf.Max(width, Mathf.Abs(local.x)); height = Mathf.Max(height, Mathf.Abs(local.y));
                }
                if (temporary != null) UnityEngine.Object.Destroy(temporary);
            }
            float margin = externalPresentation ? (camera.aspect < 1f ? 1.15f : 1.65f) : 1.12f;
            camera.orthographicSize = Mathf.Max(height, width / Mathf.Max(.2f,camera.aspect)) * margin;
            // Short landscape screens need a clear strip beneath the feet for the limb labels.
            if (externalPresentation && camera.aspect > 1.9f)
                camera.transform.position -= camera.transform.up * camera.orthographicSize * .16f;
        }
        public void Tick(float delta)
        {
            if (!visible) return;
            if (!isInternal) { time = (time + delta) % Mathf.Max(.01f,clip.length); playable.SetTime(time); graph.Evaluate(0f); }
            Rect rect = image.contentRect;
            if (rect.width < 10f || rect.height < 10f) return;
            float aspect = rect.width / rect.height;
            int width = Mathf.Clamp(Mathf.RoundToInt(768f * Mathf.Min(1f,aspect)),256,1024);
            int height = Mathf.Clamp(Mathf.RoundToInt(width / aspect),256,1024);
            // Internal layouts resize on mode switches; reuse one target to avoid allocation stalls.
            if (isInternal) { width = 1024; height = 1024; }
            // The camera projects for the UI rectangle; map the entire stable target into that rectangle.
            image.scaleMode = isInternal ? ScaleMode.StretchToFill : ScaleMode.ScaleToFit;
            camera.aspect = aspect;
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null) { camera.targetTexture = null; texture.Release(); UnityEngine.Object.Destroy(texture); }
                texture = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32); texture.Create();
                camera.targetTexture = texture; image.image = texture; camera.aspect = aspect;
                if (!isInternal) FrameModel();
            }
        }
        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (image != null) image.image = null;
            if (camera != null) camera.targetTexture = null;
            if (texture != null) { texture.Release(); UnityEngine.Object.Destroy(texture); }
            if (stage != null) UnityEngine.Object.Destroy(stage);
            if (cameraObject != null) UnityEngine.Object.Destroy(cameraObject);
            if (lightObject != null) UnityEngine.Object.Destroy(lightObject);
        }
    }
}
