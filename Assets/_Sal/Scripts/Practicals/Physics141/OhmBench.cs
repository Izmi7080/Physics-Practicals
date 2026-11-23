using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Physics81;

namespace VirtualLab.Physics141
{
    public sealed class OhmBench : MonoBehaviour
    {
        public LabWalkRig Rig;
        public Transform Table, Overview, CurrentView, VoltageView, RheostatView, ReadingView;
        public TextMesh CurrentDisplay, VoltageDisplay, SupplyDisplay;
        public Transform CurrentSelector, VoltageSelector, Wiper, Key, SupplyKnob;
        public Transform[] Sockets;
        public Material Rubber, Red, Brass;

        RectTransform safe;
        Button useBench, tools, workbookIcon, removeLead, beginGuided, beginAssessment, resumeRun;
        GameObject movePad, opening, procedure, pause, toolsPanel, pageBackdrop;
        GameObject confirmReset;
        Outline toolsHighlight;
        OhmGuideHud hud;
        OhmWiring wiring;
        OhmCircuitPage circuit;
        OhmMeasurements measurements;
        OhmWalkthrough walkthrough;
        OhmWorkbook workbook;
        OhmQuiz quiz;
        OhmProgressStore store;
        OhmProgress progress, savedRun;
        AudioSource audioSource;
        AudioClip click;
        bool atBench, modal, started;
        float ordinaryFov, targetFov;
        float saveAt = float.PositiveInfinity;
        internal Transform View { get; private set; }
        public Transform SupplyPower { get; private set; }
        internal Transform SupplyView { get; private set; }
        internal Transform SampleView { get; private set; }

        void Start()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            click = Resources.Load<AudioClip>("Practicals/ChemistryAudio/Click");
            targetFov = ordinaryFov = Rig.ViewCamera.fieldOfView;
            BuildSupplyControls();
            SampleView = new GameObject("Nichrome close-up").transform;
            SampleView.SetParent(Overview.parent.Find("Nichrome board"), false);
            SampleView.localPosition = new Vector3(.12f, .33f, -.37f);
            SampleView.localRotation = Quaternion.LookRotation(new Vector3(-.12f, -.28f, .37f));
            var screen = new GameObject("Practical HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            screen.transform.SetParent(transform, false);
            screen.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = screen.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = PracticalUiStyle.ReferenceResolution;
            scaler.matchWidthOrHeight = .5f;
            safe = OhmUi.Rect(screen.transform, "Safe area", 0, 0, 1, 1);
            safe.gameObject.AddComponent<PracticalSafeArea>();
            var look = OhmUi.Rect(safe, "Look area", .42f, 0, 1, 1);
            OhmUi.Paint(look, Color.clear);
            look.gameObject.AddComponent<LabWalkPad>().Configure(Rig, true);
            movePad = LabWalkPad.CreateJoystick(safe, Rig, PracticalUIController.CircleSprite()).gameObject;

            var input = OhmUi.Rect(safe, "Bench interaction", 0, 0, 1, 1);
            OhmUi.Paint(input, Color.clear);
            wiring = input.gameObject.AddComponent<OhmWiring>();
            wiring.Configure(this, audioSource);
            input.gameObject.SetActive(false);
            measurements = new OhmMeasurements(this, wiring, audioSource);
            hud = new OhmGuideHud(safe, FramingBounds());
            useBench = OhmUi.Button(safe, "Use bench", .84f, .047f, .976f, .122f, EnterBench);
            circuit = new OhmCircuitPage(safe, this, ClosePage, () => wiring.Guided);
            workbook = new OhmWorkbook(safe, measurements, () => { QueueSave(); walkthrough.Invalidate(); }, () => { workbook.Hide(); ClosePage(); });
            walkthrough = new OhmWalkthrough(this, wiring, measurements, circuit, hud);

            var navigation = PracticalUiStyle.Navigation(safe, PlayClick,
                ("Procedure", "book", () => OpenPage(procedure)),
                ("Workbook", "notebook", OpenWorkbook), ("All practicals", "launcher", ReturnToMenu), ("Pause", "pause", () => OpenPage(pause)));
            workbookIcon = navigation[1];
            navigation[2].gameObject.SetActive(PracticalLauncher.Available);
            tools = OhmUi.Button(safe, "Wiring tools", .027f, .029f, .168f, .088f, ToggleTools);
            toolsHighlight = tools.gameObject.AddComponent<Outline>();
            toolsHighlight.effectColor = PracticalUiStyle.EntryHighlight;
            toolsHighlight.effectDistance = new Vector2(2, 2);
            var backdrop = OhmUi.Rect(safe, "Page backdrop", 0, 0, 1, 1);
            OhmUi.Paint(backdrop, new Color(0, 0, 0, .48f));
            pageBackdrop = backdrop.gameObject;

            opening = BuildProcedure(true);
            procedure = BuildProcedure(false);
            BuildPause();
            BuildWiringTools();
            quiz = new OhmQuiz(CreatePage("Learning check", .235f, .13f, .765f, .87f), CompleteCheck);
            string file = "physics-14-1.json";
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("Physics141.BenchSmoke", false) ||
                UnityEditor.SessionState.GetBool("Physics141.FlowSmoke", false))
                file = "physics-14-1-smoke.json";
#endif
            store = new OhmProgressStore(Path.Combine(Application.persistentDataPath, file));
            var saved = store.Load();
            savedRun = saved.HasWork ? saved : null;
            Restore(new OhmProgress());
            UpdateOpeningChoices();
            wiring.Changed += QueueSave;
            circuit.Changed += QueueSave;
            measurements.Changed += QueueSave;
            ShowTools(false);
            ShowWorkbook(false);
            OpenPage(opening);
        }

        void Update()
        {
            if (!wiring.Busy)
                Rig.ViewCamera.fieldOfView = Mathf.MoveTowards(Rig.ViewCamera.fieldOfView, targetFov, 55 * Time.unscaledDeltaTime);
            if (Time.unscaledTime >= saveAt) Save();
            if (modal) return;
            if (!atBench)
            {
                useBench.gameObject.SetActive(Vector3.Distance(Rig.transform.position, Table.position) < .48f);
                return;
            }
            walkthrough.Update(true);
        }

        void LateUpdate()
        {
            if (walkthrough != null)
                walkthrough.DrawRings(atBench, modal || toolsPanel.activeSelf);
        }

        void BuildSupplyControls()
        {
            var face = SupplyKnob.parent;
            var button = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            button.name = "Supply power switch";
            SupplyPower = button.transform;
            SupplyPower.SetParent(face, false);
            SupplyPower.localPosition = new Vector3(.075f, .073f, -.108f);
            SupplyPower.localRotation = Quaternion.Euler(90, 0, 0);
            SupplyPower.localScale = new Vector3(.021f, .005f, .021f);
            button.GetComponent<MeshRenderer>().sharedMaterial = Rubber;
            Destroy(button.GetComponent<Collider>());

            var label = new GameObject("Power switch label", typeof(TextMesh));
            label.transform.SetParent(face, false);
            label.transform.localPosition = new Vector3(.075f, .093f, -.105f);
            var text = label.GetComponent<TextMesh>();
            text.font = SupplyDisplay.font;
            text.fontSize = SupplyDisplay.fontSize;
            text.characterSize = SupplyDisplay.characterSize * .30f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "POWER";
            label.GetComponent<MeshRenderer>().sharedMaterial = SupplyDisplay.font.material;

            SupplyView = new GameObject("Power supply close-up").transform;
            SupplyView.SetParent(face, false);
            SupplyView.localPosition = new Vector3(0, .24f, -.49f);
            SupplyView.localRotation = Quaternion.LookRotation(new Vector3(.005f, -.125f, .38f));
        }

        void BeginNew(bool guided)
        {
            savedRun = null;
            Restore(new OhmProgress { guided = guided });
            Begin();
        }

        void ResumeRun()
        {
            if (savedRun == null) return;
            Restore(savedRun);
            savedRun = null;
            Begin();
        }

        void Begin()
        {
            started = true;
            QueueSave();
            ClosePage();
            hud.Present(0, "Approach the workbench", "Use Move to walk to the table, or tap Go to bench.",
                0, 0, "", "Go to bench", EnterBench);
        }

        void EnterBench()
        {
            atBench = true;
            movePad.SetActive(false);
            useBench.gameObject.SetActive(false);
            wiring.gameObject.SetActive(true);
            walkthrough.Invalidate();
            PlayClick();
            Focus(Overview);
        }

        internal void Focus(Transform view)
        {
            if (wiring.Busy || modal || View == view) return;
            View = view;
            targetFov = view == Overview ? ordinaryFov - 4 : ordinaryFov;
            Rig.Inspect(view);
        }

        Bounds[] FramingBounds()
        {
            var slab = Overview.parent;
            var items = new[] { SupplyDisplay.transform.parent, CurrentSelector.parent, VoltageSelector.parent,
                Key.parent, Wiper.parent, slab.Find("Nichrome board"), slab.Find("Lead preparation tray") };
            var bounds = new Bounds[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var meshes = items[i].GetComponentsInChildren<Renderer>();
                bounds[i] = meshes[0].bounds;
                for (int j = 1; j < meshes.Length; j++) bounds[i].Encapsulate(meshes[j].bounds);
            }
            return bounds;
        }

        internal void ShowTools(bool visible, bool highlight = false)
        {
            tools.gameObject.SetActive(visible);
            toolsHighlight.enabled = highlight;
            if (!visible) toolsPanel.SetActive(false);
            removeLead.interactable = wiring.HasSelection;
        }

        internal void ShowWorkbook(bool visible) => workbookIcon.gameObject.SetActive(visible);

        internal void OpenDiagram()
        {
            if (wiring.Busy) return;
            SetModal(true);
            circuit.Show();
        }

        internal void OpenWorkbook()
        {
            if (wiring.Busy || !atBench) return;
            SetModal(true);
            workbook.Show();
        }

        internal void OpenCheck(OhmCheck check)
        {
            quiz.Prepare(check);
            OpenPage(quiz.Page);
        }

        void CompleteCheck(OhmCheck check)
        {
            progress.reviewedChecks |= 1 << (int)check;
            QueueSave();
            PlayClick();
            ClosePage();
        }

        GameObject BuildProcedure(bool initial)
        {
            var panel = CreatePage(initial ? "Opening procedure" : "Procedure page", .095f, .085f, .905f, .915f);
            OhmUi.Label(panel.transform, "PHYSICS 14.1", .055f, .916f, .94f, .98f, 22).color = PracticalUiStyle.EntryHighlight;
            OhmUi.Label(panel.transform, "Verify Ohm’s Law", .055f, .807f, .94f, .925f, 42);
            var theory = OhmUi.Rect(panel.transform, "Ohm’s law theory", .055f, .543f, .945f, .80f);
            var theoryFace = OhmUi.Paint(theory, new Color(.125f, .225f, .247f), false);
            theoryFace.sprite = PracticalUIController.RoundedSprite();
            theoryFace.type = Image.Type.Sliced;
            var lawTitle = OhmUi.Label(theory, "Ohm’s law", .026f, .68f, .582f, .93f, 25);
            lawTitle.alignment = TextAnchor.MiddleLeft;
            lawTitle.color = PracticalUiStyle.EntryHighlight;
            var law = OhmUi.Label(theory, "Current through a conductor is directly proportional to the potential difference across it when its temperature and physical state stay the same.",
                .026f, .09f, .59f, .68f, 23);
            law.alignment = TextAnchor.MiddleLeft;
            OhmUi.Paint(OhmUi.Rect(theory, "Theory divider", .608f, .11f, .61f, .89f), new Color(.30f, .45f, .45f), false);
            OhmUi.Label(theory, "V = I × R", .637f, .58f, .963f, .95f, 34).color = PracticalUiStyle.EntryHighlight;
            var units = OhmUi.Label(theory, "V   Voltage across the wire (volts)\nI    Current through the wire (amperes)\nR   Resistance of the wire (ohms)",
                .642f, .07f, .978f, .58f, 20);
            units.alignment = TextAnchor.MiddleLeft;
            var steps = new[]
            {
                "Meet the equipment and draw the circuit.",
                "Clean a lead, mount the sample, set the meters and wire the circuit.",
                "Check both meters at zero. Set the supply to 6.0 V.",
                "Move the rheostat and record five pairs of readings.",
                "Switch off, calculate resistance, and plot the graph."
            };
            OhmUi.Label(panel.transform, "What you’ll do", .045f, .466f, .46f, .537f, 28);
            OhmUi.Label(panel.transform, "What’s on the bench", .53f, .466f, .955f, .537f, 28);
            var equipment = new[]
            {
                "DC supply · red + is the higher voltage. Black − is the return.",
                "Multimeters · left reads current (A), right reads voltage (V).",
                "Plug key · the switch that opens or closes the circuit.",
                "Rheostat · move its slider to change the current.",
                "Nichrome · a nickel–chromium alloy used as the test wire."
            };
            for (int i = 0; i < steps.Length; i++)
            {
                float y = .404f - i * .061f;
                var line = OhmUi.Label(panel.transform, $"{i + 1}.  {steps[i]}", .055f, y, .49f, y + .06f, 22);
                line.alignment = TextAnchor.MiddleLeft;
                var description = OhmUi.Label(panel.transform, equipment[i], .53f, y, .95f, y + .06f, 22);
                description.alignment = TextAnchor.MiddleLeft;
            }
            if (initial)
            {
                beginGuided = OhmUi.Button(panel.transform, "Begin Guided", .16f, .048f, .49f, .135f, () => BeginNew(true));
                beginAssessment = OhmUi.Button(panel.transform, "Begin Assessment", .51f, .048f, .84f, .135f, () => BeginNew(false));
                resumeRun = OhmUi.Button(panel.transform, "Resume saved run", .37f, .048f, .63f, .135f, ResumeRun);
            }
            else OhmUi.Button(panel.transform, "Close procedure", .34f, .048f, .66f, .135f, ClosePage);
            return panel;
        }

        void UpdateOpeningChoices()
        {
            bool canResume = savedRun != null;
            resumeRun.gameObject.SetActive(canResume);
            if (canResume)
                resumeRun.GetComponentInChildren<Text>().text = savedRun.guided ? "Resume Guided" : "Resume Assessment";
            Place(beginGuided, canResume ? "New Guided run" : "Begin Guided", canResume ? .075f : .16f, canResume ? .35f : .49f);
            Place(beginAssessment, canResume ? "New Assessment run" : "Begin Assessment", canResume ? .65f : .51f, canResume ? .925f : .84f);

            static void Place(Button button, string label, float left, float right)
            {
                button.GetComponentInChildren<Text>().text = label;
                var rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2(left, rect.anchorMin.y);
                rect.anchorMax = new Vector2(right, rect.anchorMax.y);
            }
        }

        void BuildPause()
        {
            pause = CreatePage("Pause overlay", .34f, .23f, .66f, .77f);
            OhmUi.Label(pause.transform, "Paused", .1f, .75f, .9f, .96f, 35);
            OhmUi.Button(pause.transform, "Resume", .16f, .58f, .84f, .73f, ClosePage);
            OhmUi.Button(pause.transform, "Reset practical", .16f, .39f, .84f, .54f, () => confirmReset.SetActive(true));
            confirmReset = OhmUi.Button(pause.transform, "Confirm reset", .16f, .18f, .84f, .33f, ResetPractical).gameObject;
            confirmReset.SetActive(false);
        }

        void ResetPractical()
        {
            Restore(new OhmProgress());
            Save();
            savedRun = null;
            UpdateOpeningChoices();
            atBench = started = false;
            View = null;
            targetFov = ordinaryFov;
            Rig.ResetStation();
            ClosePage();
            OpenPage(opening);
        }

        void BuildWiringTools()
        {
            toolsPanel = CreatePage("Wiring options", .027f, .104f, .248f, .38f);
            OhmUi.Button(toolsPanel.transform, "Undo last wire", .06f, .67f, .94f, .94f, () => { wiring.Undo(); CloseTools(); });
            removeLead = OhmUi.Button(toolsPanel.transform, "Remove selected", .06f, .35f, .94f, .62f,
                () => { wiring.RemoveSelected(); CloseTools(); });
            OhmUi.Button(toolsPanel.transform, "Close tools", .06f, .04f, .94f, .30f, CloseTools);
        }

        void ToggleTools()
        {
            if (wiring.Busy) return;
            toolsPanel.SetActive(!toolsPanel.activeSelf);
            removeLead.interactable = wiring.HasSelection;
        }

        void CloseTools()
        {
            toolsPanel.SetActive(false);
            walkthrough.Invalidate();
        }

        GameObject CreatePage(string name, float left, float bottom, float right, float top)
        {
            var rect = OhmUi.Rect(safe, name, left, bottom, right, top);
            var face = OhmUi.Paint(rect, PracticalUiStyle.Document);
            face.sprite = PracticalUIController.RoundedSprite();
            face.type = Image.Type.Sliced;
            rect.gameObject.SetActive(false);
            return rect.gameObject;
        }

        void OpenPage(GameObject page)
        {
            if (wiring.Busy) return;
            SetModal(true);
            page.SetActive(true);
            page.transform.SetAsLastSibling();
        }

        void ClosePage()
        {
            opening.SetActive(false);
            procedure.SetActive(false);
            pause.SetActive(false);
            quiz.Page.SetActive(false);
            confirmReset.SetActive(false);
            circuit.Hide();
            workbook.Hide();
            SetModal(false);
            walkthrough.Invalidate();
        }

        void SetModal(bool open)
        {
            modal = open;
            pageBackdrop.SetActive(open);
            if (open) pageBackdrop.transform.SetAsLastSibling();
            Rig.Suspend(open);
            wiring.gameObject.SetActive(atBench && !open);
            movePad.SetActive(started && !atBench && !open);
            useBench.gameObject.SetActive(false);
            hud.Show(!open);
            if (open)
            {
                CloseTools();
                ShowTools(false);
            }
        }

        void Restore(OhmProgress saved)
        {
            progress = saved;
            wiring.Guided = saved.guided;
            wiring.Restore(saved.leads, saved.currentRange, saved.voltageRange, saved.prepared,
                saved.sampleMounted || saved.leads.Count > 0 || saved.zeroChecked);
            measurements.Restore(saved.supply, saved.rheostat, saved.zeroChecked, saved.pairs);
            circuit.Restore(saved.circuitTiles, saved.diagram);
            workbook.Restore(saved);
            walkthrough.Restore(saved);
        }

        internal void QueueSave() => saveAt = Time.unscaledTime + .5f;

        bool Save()
        {
            saveAt = float.PositiveInfinity;
            if (store == null || !started) return true;
            progress.guided = wiring.Guided;
            progress.leads = new List<Lead>(wiring.Leads);
            progress.currentRange = wiring.CurrentRange;
            progress.voltageRange = wiring.VoltageRange;
            progress.prepared = wiring.Prepared;
            progress.sampleMounted = wiring.SampleMounted;
            progress.zeroChecked = measurements.ZeroChecked;
            progress.supply = measurements.Supply;
            progress.rheostat = measurements.Rheostat;
            progress.pairs = measurements.Pairs;
            progress.circuitTiles = circuit.Placed;
            progress.diagram = new List<Lead>(circuit.Links);
            if (store.Save(progress)) return true;
            Debug.LogWarning("Physics 14.1 progress could not be saved.");
            return false;
        }

        void OnApplicationPause(bool paused) { if (paused && progress != null) Save(); }

        // INTEGRATION (Tajarbagah): internal so the Android back button can use the same saving exit.
        internal void ReturnToMenu()
        {
            if (!wiring.Busy && Save()) PracticalLauncher.ReturnToMenu();
        }

        internal void PlayClick()
        {
            if (audioSource != null && click != null) audioSource.PlayOneShot(click, .45f);
        }
    }
}
