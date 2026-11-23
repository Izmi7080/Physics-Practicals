using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VirtualLab.Physics81
{
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public sealed partial class LabSetupHud : MonoBehaviour
    {
        [SerializeField] LabSetupSession session;
        [SerializeField] Sprite rounded, round;
        static readonly Color Navy = PracticalUiStyle.Panel, Teal = PracticalUiStyle.ButtonFace, Selected = PracticalUiStyle.Selected;
        RectTransform safe, actionHud, joystick, meterPanel, learningPanel;
        Text cue, headingLabel, context, primaryLabel, inspectLabel, cupAngleLabel;
        Image pauseIcon;
        Button primary, inspect, zoomIn, zoomOut, drain, tare, reweigh, useWater, cupAngle;
        Text learningTitle, learningBody, learningToggle;
        bool? showLearningNotes;
        string cueCopy = "";
        float cueCharacters;
        Font font;
        SetupStep lastStep = (SetupStep)(-1);
        Rect lastSafe;
        Vector2Int lastSize;

        public void Configure(LabSetupSession owner, Sprite panelSprite, Sprite circleSprite)
        { session = owner; rounded = panelSprite; round = circleSprite; }

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            safe = Box("Safe area", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            actionHud = Fill("Experiment controls", safe);
            var look = Fill("Bench drag and swipe", actionHud);
            ImageFor(look, Color.clear, true);
            var touchArea = look.gameObject.AddComponent<LabWalkPad>();
            touchArea.Configure(session.Rig, true);
            touchArea.UseApparatus(session);

            var heading = Box("Procedure instructions", actionHud, new Vector2(.21f, 1), new Vector2(.59f, 1), new Vector2(0, -146), new Vector2(0, -20));
            ImageFor(heading, Navy, false);
            headingLabel = Label(Box("Title", heading, new Vector2(0, .61f), Vector2.one, new Vector2(16, 0), new Vector2(-10, 0)), "", 22, TextAnchor.MiddleLeft);
            cue = Label(Box("Current action", heading, Vector2.zero, new Vector2(1, .62f), new Vector2(16, 5), new Vector2(-16, 0)), "", 24, TextAnchor.MiddleLeft);

            var navigation = PracticalUiStyle.Navigation(actionHud, () => session.PlaySound("Click"), ("Study", "book", () => OpenPage(Page.Study)),
                ("Tasks", "tasks", () => OpenPage(Page.Tasks)), ("Readings", "readings", () => OpenPage(Page.Readings)),
                ("Workbook", "notebook", () => OpenPage(Page.Workbook)), ("All practicals", "launcher", ReturnToLauncher),
                ("Pause", "pause", session.Pause));
            navigation[4].gameObject.SetActive(PracticalLauncher.Available);
            pauseIcon = navigation[^1].transform.Find("Icon").GetComponent<Image>();
            inspect = ButtonAt("Inspect", actionHud, Vector2.one, new Vector2(-240, -148), new Vector2(-22, -89), session.Inspect, out inspectLabel);
            zoomOut = ButtonAt("Zoom out", actionHud, Vector2.one, new Vector2(-377, -148), new Vector2(-318, -89), () => session.Rig.Zoom(-.045f), out var outLabel);
            zoomIn = ButtonAt("Zoom in", actionHud, Vector2.one, new Vector2(-309, -148), new Vector2(-250, -89), () => session.Rig.Zoom(.045f), out var inLabel);
            cupAngle = ButtonAt("Cup depth angle", actionHud, Vector2.one, new Vector2(-580, -148), new Vector2(-390, -89), session.ToggleCupDepthView, out cupAngleLabel);
            outLabel.text = "−"; inLabel.text = "+";
            primary = ButtonAt("Place or record", actionHud, Vector2.right, new Vector2(-294, 24), new Vector2(-22, 83), () =>
            {
                if (session.RecordingTemperature) session.RecordTemperature(temperatureEntry.text);
                else session.Primary();
            }, out primaryLabel);
            drain = ButtonAt("Empty and refill", actionHud, Vector2.right, new Vector2(-294, 99), new Vector2(-22, 157), session.DrainVessel, out _);
            useWater = ButtonAt("Use this amount", actionHud, Vector2.right, new Vector2(-294, 168), new Vector2(-22, 226), session.UseFilledVessel, out _);
            tare = ButtonAt("Tare", actionHud, Vector2.right, new Vector2(-165, 24), new Vector2(-22, 83), session.TareScale, out _);
            reweigh = ButtonAt("Tare & reweigh", actionHud, Vector2.right, new Vector2(-294, 99), new Vector2(-22, 157), session.TareAndReweigh, out _);

            meterPanel = Box("Reading", actionHud, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-272, 26), new Vector2(272, 81));
            ImageFor(meterPanel, Navy, false); context = Label(Fill("Reading text", meterPanel), "", 22);

            joystick = LabWalkPad.CreateJoystick(actionHud, session.Rig, round);

            BuildTemperatureControls();
            BuildLearningNote();
            BuildPages();
        }

        void Update()
        {
            Rect screen = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if ((lastSafe != screen || lastSize != size) && size.x > 0 && size.y > 0)
            {
                safe.anchorMin = new Vector2(screen.xMin / size.x, screen.yMin / size.y);
                safe.anchorMax = new Vector2(screen.xMax / size.x, screen.yMax / size.y);
                lastSafe = screen; lastSize = size;
            }
            actionHud.gameObject.SetActive(session.Started && !PageVisible);
            if (PageVisible || !session.Started)
            {
                session.PlayTyping(false);
                if (activePage == Page.Welcome) RefreshWelcome();
                if (pageScroll.gameObject.activeSelf && Mathf.Abs(bodyWidth - pageBody.rectTransform.rect.width) > .5f) ResizePageBody();
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ClosePage();
                return;
            }

            bool paused = session.Paused, local = session.CanInteract;
            headingLabel.text = Physics81ProcedureText.Heading(session.Step);
            UpdateInstruction(paused);
            pauseIcon.sprite = PracticalUIController.EmojiSprite(paused ? "resume" : "pause");
            string reading = session.SaveFailed ? "Could not save. Try again before switching." : paused ? "" :
                !local ? session.Direction : session.Context;
            context.text = reading; meterPanel.gameObject.SetActive(!string.IsNullOrEmpty(reading));
            bool readingView = session.InspectingReading;
            meterPanel.anchorMin = meterPanel.anchorMax = new Vector2(readingView ? 0 : .5f, 0);
            meterPanel.offsetMin = new Vector2(readingView ? 24 : -272, 26);
            meterPanel.offsetMax = meterPanel.offsetMin + new Vector2(544, 55);
            RefreshTemperatureControls(local);
            RefreshLearningNote(local);
            primaryLabel.text = session.PrimaryTitle;
            primary.gameObject.SetActive((local || session.PrimaryEnabled) && !session.IsPlacement && primaryLabel.text.Length > 0);
            primary.interactable = session.PrimaryEnabled;
            PositionPrimary();
            inspect.gameObject.SetActive(!paused && !session.PlacementBusy && (session.Rig.IsInspecting || session.CanInspect));
            inspectLabel.text = session.Rig.IsInspecting ? "Back to station" : "Inspect";
            zoomIn.gameObject.SetActive(session.Rig.CanZoom);
            zoomOut.gameObject.SetActive(session.Rig.CanZoom);
            cupAngle.gameObject.SetActive(session.Step == SetupStep.SetCupDepth && session.CanInspect && !session.Rig.IsTransitioning);
            cupAngleLabel.text = session.AtCupDepthView ? "Side view" : "Look into cup";
            joystick.gameObject.SetActive(session.Rig.CanWalk);
            drain.gameObject.SetActive(session.CanDrain);
            useWater.gameObject.SetActive(session.CanUseFilled);
            tare.gameObject.SetActive(session.CanTare);
            reweigh.gameObject.SetActive(session.CanReweigh);
            if (lastStep != session.Step)
            {
                temperatureEntry.SetTextWithoutNotify("");
                lastStep = session.Step;
                if (lastStep == SetupStep.Review && !session.Data.LowWaterStop)
                {
                    calculatorOpen = true;
                    OpenPage(Page.Workbook);
                }
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (!paused && session.Rig.IsInspecting) session.Inspect(); else session.Pause();
            }
        }

        void BuildLearningNote()
        {
            learningPanel = Box("Lab note", actionHud, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            ImageFor(learningPanel, Navy, false);
            learningTitle = Label(Box("Lab note title", learningPanel, Vector2.up, Vector2.one,
                new Vector2(14, -42), new Vector2(-93, -5)), "", 21, TextAnchor.MiddleLeft);
            learningTitle.fontStyle = FontStyle.Bold;
            learningTitle.color = new Color(.61f, .94f, .88f);
            learningBody = Label(Box("Lab note text", learningPanel, Vector2.zero, Vector2.one,
                new Vector2(14, 7), new Vector2(-14, -44)), "", 20, TextAnchor.UpperLeft);
            learningBody.resizeTextForBestFit = true;
            learningBody.resizeTextMinSize = 15;
            learningBody.resizeTextMaxSize = 20;
            ButtonAt("Toggle lab note", learningPanel, Vector2.one, new Vector2(-87, -42), new Vector2(-6, -5),
                () => showLearningNotes = !(showLearningNotes ?? session.Data.Guided), out learningToggle);
        }

        void RefreshLearningNote(bool local)
        {
            var note = Physics81ProcedureText.LearningNote(session.Step);
            bool available = local && session.Rig.IsInspecting && !string.IsNullOrEmpty(note.title);
            learningPanel.gameObject.SetActive(available);
            if (!available) return;

            bool expanded = showLearningNotes ?? session.Data.Guided;
            const float bottom = 94f;
            float top = session.RecordingTemperature ? Mathf.Min(220f, safe.rect.height * .5f - 147f) : 220f;
            learningPanel.offsetMin = new Vector2(24, bottom);
            learningPanel.offsetMax = new Vector2(expanded ? 430 : 118, expanded ? Mathf.Max(bottom + 83, top) : bottom + 47);
            learningTitle.gameObject.SetActive(expanded);
            learningBody.gameObject.SetActive(expanded);
            learningTitle.text = note.title;
            learningBody.text = note.body;
            learningToggle.text = expanded ? "Hide" : "Why?";
        }

        void UpdateInstruction(bool paused)
        {
            string next = paused ? "Paused. Tap Resume to continue." : session.Cue;
            if (next != cueCopy) { cueCopy = next; cueCharacters = paused ? next.Length : 0f; }
            cueCharacters = Mathf.MoveTowards(cueCharacters, cueCopy.Length, Time.unscaledDeltaTime * 170f);
            cue.text = cueCopy;
            cue.color = new Color(1, 1, 1, Mathf.Lerp(.6f, 1, cueCharacters / Mathf.Max(1, cueCopy.Length)));
            session.PlayTyping(!paused && cueCharacters < cueCopy.Length);
        }

        void PositionPrimary()
        {
            if (session.Rig.IsTransitioning && lastStep == session.Step) return;
            var rect = (RectTransform)primary.transform;
            Vector2 min = new(-294f, 24f);
            if (!session.Rig.IsTransitioning && session.TryGetActionPosition(out var point))
            {
                var camera = session.Rig.ViewCamera;
                Vector3 screen = camera.WorldToScreenPoint(point);
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out var pos)
                    && safe.rect.Contains(pos))
                {
                    min.x = Mathf.Clamp(pos.x - safe.rect.xMax + 42f, -safe.rect.width * .39f, -294f);
                    min.y = Mathf.Clamp(pos.y - safe.rect.yMin - 26f, 100f, safe.rect.height - 231f);
                }
            }
            if (session.Data.Measuring) min.y = Mathf.Max(min.y, 242f);
            rect.offsetMin = min;
            rect.offsetMax = min + new Vector2(272f, 59f);
        }

        static RectTransform Fill(string name, Transform parent) => Box(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        static RectTransform Box(string name, Transform parent, Vector2 min, Vector2 max, Vector2 a, Vector2 b)
        {
            var t = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            t.SetParent(parent, false); t.anchorMin = min; t.anchorMax = max; t.offsetMin = a; t.offsetMax = b; return t;
        }
        Image ImageFor(RectTransform rect, Color color, bool target, bool circle = false)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = target;
            if (color.a > 0) { image.sprite = circle ? round : rounded; image.type = circle ? Image.Type.Simple : Image.Type.Sliced; }
            return image;
        }
        Text Label(RectTransform rect, string label, int size, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var text = rect.gameObject.AddComponent<Text>(); text.font = font; text.text = label; text.fontSize = size;
            text.alignment = align; text.color = Color.white; text.raycastTarget = false; return text;
        }
        Button ButtonAt(string name, Transform parent, Vector2 anchor, Vector2 a, Vector2 b, UnityEngine.Events.UnityAction click, out Text label)
            => ButtonIn(Box(name, parent, anchor, anchor, a, b), name, click, out label);
        Button ButtonIn(RectTransform rect, string title, UnityEngine.Events.UnityAction click, out Text label)
        {
            var image = ImageFor(rect, Teal, true);
            PracticalUiStyle.StyleTextFace(image);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            PracticalUiStyle.ApplyButtonStates(button);
            button.onClick.AddListener(() => { session.PlaySound("Click"); click(); });
            label = Label(Fill("Label", rect), title, 22); return button;
        }
    }
}
