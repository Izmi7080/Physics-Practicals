using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.Instruments;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.EditorTools
{
    // What differs between practicals in the shared shell (labels, which optional parts exist).
    public class ShellSpec_Azm
    {
        public string[] viewNames = { "Overview", "Caliper" };
        public ThemeIcon_Azm[] viewIcons = { ThemeIcon_Azm.Overview, ThemeIcon_Azm.Caliper };
        public string magnifierTitle = "Magnifier";
        public bool padDecimalKey;                 // '.' key for readings ending in .5 (screw gauge)
        public string padMainLabel = "Main scale  M (mm)";
        public string padDivisionLabel = "Division  n";
        public string leastCountTitle = "Least count";
        public string turnButtonLabel;             // non-null = a "turn one full turn" button (screw gauge)
        public string nextLineLabel = "Show next line";
        public string seeResultLabel = "See my result";
        public string resultsTitle = "Your result";
        public Vector2 resultsSize = new Vector2(1500f, 760f);
        // Drawer tabs (the pendulum adds a Graph tab) and which of them are Calc and Guide.
        public string[] drawerTabNames = { "Data", "Calc", "Guide" };
        public ThemeIcon_Azm[] drawerTabIcons = { ThemeIcon_Azm.Table, ThemeIcon_Azm.Calculator, ThemeIcon_Azm.Book };
        public int calcTabIndex = 1;
        public int guideTabIndex = 2;
        public bool defaultCalcBody = true;        // false = the practical builds its own Calc pane
        public bool secondChip;                    // a second chip left of the info chip (pendulum: "Trial 2 of 5")
        public string pauseExtraButtonLabel;       // e.g. "Restart this length" (pendulum)
        public string guideReplayDemoLabel;        // e.g. "Replay oscillation demo" (pendulum)
    }

    // The parts of the shell a practical builder fills in or wires up.
    public class Shell_Azm
    {
        public Canvas canvas;
        public RectTransform safe, leftColumn, sceneWindow, bottomRow;
        public ScreenRegions_Azm regions;
        public PanelTransition_Azm magnifier;
        public RectTransform magnifierContent;       // inside the magnifier, below its title
        public RectTransform dataPane, calcPane, guidePane;
        public TMP_Text calcBody;
        public PanelTransition_Azm resultsPanel;
        public Button retryButton, menuButton;
        public Button turnButton;                    // only when ShellSpec_Azm.turnButtonLabel is set
        public RectTransform[] panes;                // one per drawer tab
        public GameObject secondChip;                // only when ShellSpec_Azm.secondChip
        public TMP_Text secondChipText;
        public Button pauseExtraButton;              // only when ShellSpec_Azm.pauseExtraButtonLabel is set
        public RectTransform topBar;
        public GrabCoachUI_Azm coach;                // the practical sets coach.grabbables
    }

    /*
     * Layer: Editor tooling (not part of the shipped app).
     * Purpose: Builds the SHARED practical shell (TASK_hci_and_juice Stages 2-3) so every practical
     * gets the same anchored layout, built from the theme's design tokens instead of literals. It
     * replaces ~15 helpers that were copied into each practical's builder.
     * LAYOUT (canvas: Scale With Screen Size, 1920x1080, "Expand" - so the canvas is never smaller
     * than 1920x1080 units on any phone or tablet; everything is anchored to an edge):
     *   - TOP BAR (stretches across): pause | instruction card (flexible) | info chip.
     *   - RIGHT EDGE: drawer strip with icon + label; the keypad / least-count panel left of the strip;
     *     the drawer panel (flush right) when open.
     *   - LEFT COLUMN (ScreenRegions_Azm moves its right edge to clear the right-hand content):
     *     bottom row (view buttons + any practical controls), the magnifier above it, the toast at
     *     its top, the calculation buttons bottom-right; the SCENE WINDOW (top bar to magnifier) is
     *     where the camera rig frames the apparatus.
     *   - OVERLAYS (centred, fit the 1920x1080 minimum): results frame, pause, confirm, briefing.
     * Every Button gets ButtonFeel_Azm (press squash, click, haptic) in Finish().
     * Talks to: UIFactory_Azm, UIBuilder_Azm, the runtime UI components it creates.
     */
    public static class PracticalShellBuilder_Azm
    {
        public const string ThemePath = "Assets/_Azm/Data/UITheme_Azm.asset";
        public const string SoundBankPath = "Assets/_Azm/Data/SoundBank_Azm.asset";

        // Layout constants that are geometry, not style (canvas units at 1920x1080).
        private const float TopBarHeight = 120f;
        private const float BottomRowHeight = 156f;
        private const float MagnifierHeight = 300f;
        private const float StripWidth = 120f;
        private const float RightPanelWidth = 640f;
        private const float DrawerWidth = 740f;
        private const float PadHeight = 790f;
        private const float LeastCountHeight = 790f;

        private static UITheme_Azm theme;
        public static UITheme_Azm Theme => theme;

        // =====================================================================
        // Scene basics (shared by every practical)
        // =====================================================================

        public static void LoadTheme() => theme = AssetDatabase.LoadAssetAtPath<UITheme_Azm>(ThemePath);

        public static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 60f;
            camera.fieldOfView = 40f;
            camera.clearFlags = CameraClearFlags.Skybox;
            go.AddComponent<UniversalAdditionalCameraData>();
            return camera;
        }

        public static void CreateLight()
        {
            var go = new GameObject("Directional Light", typeof(Light));
            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.7f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        public static BackButtonInput_Azm CreateBootstrap(PracticalCatalog_Azm catalog)
        {
            var go = new GameObject("Bootstrap_Azm");
            var bootstrap = go.AddComponent<SceneBootstrap_Azm>();
            bootstrap.theme = theme;
            bootstrap.catalog = catalog;
            return go.AddComponent<BackButtonInput_Azm>();
        }

        public static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            go.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
        }

        public static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            init?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // A canvas that is never smaller than the 1920x1080 reference (Expand), with the safe area.
        public static Canvas CreateCanvas(string name, out RectTransform safe)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureScaler(go.GetComponent<CanvasScaler>());
            safe = UIFactory_Azm.NewRect("SafeArea", go.transform);
            UIFactory_Azm.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaContainer_Azm>();
            return go.GetComponent<Canvas>();
        }

        // HCI restructure: EXPAND instead of "match 0.5". With Expand the canvas keeps at least
        // 1920x1080 units and grows in the screen's longer direction (2520 wide on 21:9, 1440 tall
        // on 4:3), so a layout designed for 1920x1080 never overlaps and anchored parts spread out.
        public static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        // =====================================================================
        // The practical shell
        // =====================================================================

        public static Shell_Azm Build<THud>(ShellSpec_Azm spec, MonoBehaviour instrument, PracticalCameraRig_Azm rig,
            BackButtonInput_Azm backInput, PracticalGuide_Azm guide, out THud hud) where THud : PracticalHUDBase_Azm
        {
            var shell = new Shell_Azm();
            shell.canvas = CreateCanvas("Canvas", out shell.safe);
            var canvasGO = shell.canvas.gameObject;

            hud = canvasGO.AddComponent<THud>();
            hud.theme = theme;
            hud.cameraRig = rig;
            hud.backInput = backInput;
            hud.guide = guide;
            canvasGO.AddComponent<AudioSource>();
            hud.sound = canvasGO.AddComponent<UISound_Azm>();
            hud.sound.bank = AssetDatabase.LoadAssetAtPath<SoundBank_Azm>(SoundBankPath);

            float m = theme.screenMargin;

            // ----- regions -----
            shell.leftColumn = UIFactory_Azm.NewRect("LeftColumn", shell.safe);
            UIFactory_Azm.Stretch(shell.leftColumn, m, 0f, StripWidth + m + theme.spaceS, 0f);
            shell.sceneWindow = UIFactory_Azm.NewRect("SceneWindow", shell.leftColumn);
            UIFactory_Azm.Stretch(shell.sceneWindow);
            var regions = shell.safe.gameObject.AddComponent<ScreenRegions_Azm>();
            regions.leftColumn = shell.leftColumn;
            regions.sceneWindow = shell.sceneWindow;
            regions.stripReserve = StripWidth + m + theme.spaceS;
            regions.rightPanelReserve = regions.stripReserve + RightPanelWidth + theme.spaceS;
            regions.drawerReserve = DrawerWidth + theme.spaceM;
            regions.topReserve = m + TopBarHeight + theme.spaceS;
            regions.bottomRowReserve = m + BottomRowHeight + theme.spaceS;
            regions.magnifierReserve = regions.bottomRowReserve + MagnifierHeight + theme.spaceS;
            shell.regions = regions;
            hud.regions = regions;
            rig.regions = regions;

            // ----- left column content (drawn first: the right-hand panels and drawer sit above) -----
            BuildBottomRow(shell, hud, spec);
            BuildMagnifierFrame(shell, hud, spec);

            // ----- top bar -----
            BuildTopBar(shell, hud, spec);
            regions.instructionCard = hud.instructionCard;
            hud.instructionCard.clampTo = shell.leftColumn;

            // ----- right-hand panels -----
            BuildPad(shell, hud, spec, instrument);
            BuildLeastCount(shell, hud, spec);

            // ----- drawer -----
            var drawer = BuildDrawer(shell, hud, guide, spec);
            regions.drawer = drawer;
            hud.drawer = drawer;

            // ----- calculation buttons (bottom-right of the left column: never under the drawer) -----
            var calc = UIFactory_Azm.NewRect("CalcActions", shell.leftColumn);
            UIFactory_Azm.Pin(calc, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(500f, 140f), new Vector2(-theme.spaceS, m));
            hud.calcGroup = calc.gameObject;
            hud.nextLineButton = Pinned(calc, "NextLineButton", spec.nextLineLabel, ThemeColor_Azm.Primary, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(480f, 130f), Vector2.zero, theme.fontTitle);
            hud.seeResultButton = Pinned(calc, "SeeResultButton", spec.seeResultLabel, ThemeColor_Azm.Primary, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(480f, 130f), Vector2.zero, theme.fontTitle);

            // ----- toast (top of the left column, under the top bar) -----
            hud.toast = BuildToast(shell);

            // ----- overlays -----
            BuildResultsFrame(shell, spec);
            BuildPause(shell, hud, spec);
            BuildSettings(shell, hud);
            hud.confirm = BuildConfirm(shell);
            hud.briefing = BuildBriefing(shell);
            BuildOnboarding(shell, hud, rig);
            return shell;
        }

        // ----- onboarding (Stage 5): gesture coach, camera chip, HUD tour -----
        private static void BuildOnboarding(Shell_Azm shell, PracticalHUDBase_Azm hud, PracticalCameraRig_Azm rig)
        {
            // Gesture coach: over the 3D scene but UNDER every panel (right after the left column).
            var coachRect = UIFactory_Azm.NewRect("GrabCoach", shell.safe);
            UIFactory_Azm.Stretch(coachRect);
            coachRect.SetSiblingIndex(shell.leftColumn.GetSiblingIndex() + 1);
            var coach = coachRect.gameObject.AddComponent<GrabCoachUI_Azm>();
            coach.theme = theme;
            coach.hud = hud;
            coach.viewCamera = rig.viewCamera;
            coach.grabbables = rig.dragOwners;   // every drag interaction is already listed for the camera
            var ringRect = UIFactory_Azm.NewRect("Ring", coachRect);
            UIFactory_Azm.Pin(ringRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150f, 150f), Vector2.zero);
            coach.ring = ringRect.gameObject.AddComponent<Image>();
            var handRect = UIFactory_Azm.NewRect("Hand", coachRect);
            // Pivot at the fingertip of the Hand icon, so the finger touches the grab point.
            UIFactory_Azm.Pin(handRect, new Vector2(0.5f, 0.5f), new Vector2(0.375f, 0.97f), new Vector2(120f, 120f), Vector2.zero);
            coach.hand = handRect.gameObject.AddComponent<Image>();
            var handIcon = handRect.gameObject.AddComponent<ThemedIcon_Azm>();
            handIcon.theme = theme;
            handIcon.icon = ThemeIcon_Azm.Hand;
            handIcon.Apply();
            coach.hand.color = Color.white;
            coach.handGroup = handRect.gameObject.AddComponent<CanvasGroup>();
            coach.handGroup.blocksRaycasts = false;
            shell.coach = coach;

            // Camera chip: bottom of the scene window (it moves with the window).
            var chip = UIFactory_Azm.NewImage(shell.sceneWindow, "CameraGestureChip", theme, ThemeColor_Azm.PanelStrong);
            UIFactory_Azm.Pin(chip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(820f, 84f), new Vector2(0f, theme.spaceS));
            chip.raycastTarget = false;
            var chipIcon = UIFactory_Azm.NewImage(chip.transform, "Icon", theme, ThemeColor_Azm.Accent, false);
            Object.DestroyImmediate(chipIcon.GetComponent<ThemedImage_Azm>());
            var chipIconTheme = chipIcon.gameObject.AddComponent<ThemedIcon_Azm>();
            chipIconTheme.theme = theme;
            chipIconTheme.icon = ThemeIcon_Azm.Hand;
            chipIconTheme.Apply();
            chipIcon.color = theme.GetColor(ThemeColor_Azm.Accent);
            chipIcon.raycastTarget = false;
            UIFactory_Azm.Pin(chipIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(56f, 56f), new Vector2(theme.spaceM, 0f));
            var chipText = UIFactory_Azm.NewText(chip.transform, "Text", theme, CameraGestureChip_Azm.Text, theme.fontLabel, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.MidlineLeft);
            chipText.enableAutoSizing = true;
            chipText.fontSizeMin = theme.fontCaption - 2f;
            chipText.fontSizeMax = theme.fontLabel;
            UIFactory_Azm.Stretch(chipText.rectTransform, 96f, 6f, theme.spaceM, 6f);
            var chipGroup = chip.gameObject.AddComponent<CanvasGroup>();
            chipGroup.blocksRaycasts = false;
            var clamp = chip.gameObject.AddComponent<ClampWidth_Azm>();
            clamp.maxWidth = 820f;
            clamp.sideMargin = theme.spaceS;
            hud.cameraChip = chip.gameObject.AddComponent<CameraGestureChip_Azm>();
            chip.gameObject.SetActive(false);

            // HUD tour: above everything (it absorbs taps; the bubble has Next and Skip).
            var tourRect = UIFactory_Azm.NewRect("HudTour", shell.safe);
            UIFactory_Azm.Stretch(tourRect);
            var blocker = tourRect.gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.55f);   // dims the rest; the teal highlight marks the target
            tourRect.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            tourRect.gameObject.AddComponent<CanvasGroup>();
            var tour = tourRect.gameObject.AddComponent<HudTourUI_Azm>();
            var highlight = UIFactory_Azm.NewImage(tourRect, "Highlight", theme, ThemeColor_Azm.Primary);
            Object.DestroyImmediate(highlight.GetComponent<ThemedImage_Azm>());
            Color teal = theme.GetColor(ThemeColor_Azm.Primary);
            highlight.color = new Color(teal.r, teal.g, teal.b, 0.22f);
            highlight.raycastTarget = false;
            UIFactory_Azm.Pin(highlight.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(200f, 100f), Vector2.zero);
            tour.highlight = highlight.rectTransform;
            var arrow = UIFactory_Azm.NewImage(tourRect, "Arrow", theme, ThemeColor_Azm.PanelStrong, false);
            UIFactory_Azm.Pin(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, 40f), Vector2.zero);
            arrow.raycastTarget = false;
            tour.arrow = arrow.rectTransform;
            var bubble = UIFactory_Azm.NewImage(tourRect, "Bubble", theme, ThemeColor_Azm.PanelStrong);
            UIFactory_Azm.Pin(bubble.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(700f, 300f), Vector2.zero);
            tour.bubble = bubble.rectTransform;
            tour.stepText = UIFactory_Azm.NewText(bubble.transform, "Step", theme, "1 / 3", theme.fontCaption, ThemeColor_Azm.Primary, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory_Azm.Pin(tour.stepText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, 36f), new Vector2(theme.spaceM, -theme.spaceS));
            tour.bodyText = UIFactory_Azm.NewText(bubble.transform, "Body", theme, "", theme.fontBody, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft);
            tour.bodyText.enableAutoSizing = true;
            tour.bodyText.fontSizeMin = theme.fontLabel - 2f;
            tour.bodyText.fontSizeMax = theme.fontBody;
            UIFactory_Azm.Stretch(tour.bodyText.rectTransform, theme.spaceM, 120f, theme.spaceM, 56f);
            tour.skipButton = Pinned(bubble.transform, "SkipButton", "Skip", ThemeColor_Azm.Secondary, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(200f, 96f), new Vector2(theme.spaceM, theme.spaceS), theme.fontBody);
            tour.nextButton = Pinned(bubble.transform, "NextButton", HudTourUI_Azm.NextLabel, ThemeColor_Azm.Primary, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(240f, 96f), new Vector2(-theme.spaceM, theme.spaceS), theme.fontTitle);
            tour.nextLabel = tour.nextButton.GetComponentInChildren<TMP_Text>();
            hud.tour = tour;
            tourRect.gameObject.SetActive(false);
        }

        // Call after the practical has added its own parts: gives every button the shared feel.
        public static void Finish(Shell_Azm shell)
        {
            foreach (var button in shell.canvas.GetComponentsInChildren<Button>(true))
            {
                if (button.transition == Selectable.Transition.None) continue;   // invisible tap-absorbers
                if (button.GetComponent<ButtonFeel_Azm>() == null) button.gameObject.AddComponent<ButtonFeel_Azm>();
            }
        }

        // =====================================================================
        // Parts
        // =====================================================================

        private static void BuildTopBar(Shell_Azm shell, PracticalHUDBase_Azm hud, ShellSpec_Azm spec)
        {
            float m = theme.screenMargin;
            var bar = UIFactory_Azm.NewRect("TopBar", shell.safe);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(m, -m - TopBarHeight);
            bar.offsetMax = new Vector2(-m, -m);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = theme.spaceS;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            hud.pauseButton = UIFactory_Azm.NewIconButton(bar, "PauseButton", theme, ThemeIcon_Azm.Pause, ThemeColor_Azm.PanelStrong, new Vector2(112f, 112f), true);

            hud.instructionCard = BuildInstructionCard(bar);
            shell.topBar = bar;

            if (spec.secondChip)
            {
                var second = UIFactory_Azm.NewImage(bar, "SecondChip", theme, ThemeColor_Azm.PanelStrong);
                var secondLayout = second.gameObject.AddComponent<LayoutElement>();
                secondLayout.preferredWidth = 300f;
                secondLayout.minWidth = 260f;
                secondLayout.preferredHeight = 112f;
                shell.secondChip = second.gameObject;
                shell.secondChipText = UIFactory_Azm.NewText(second.transform, "Text", theme, "", theme.fontTitle * 0.85f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
                shell.secondChipText.enableAutoSizing = true;
                shell.secondChipText.fontSizeMin = theme.fontCaption;
                shell.secondChipText.fontSizeMax = theme.fontTitle * 0.85f;
                UIFactory_Azm.Stretch(shell.secondChipText.rectTransform, theme.spaceS, theme.spaceXS, theme.spaceS, theme.spaceXS);
            }

            var chip = UIFactory_Azm.NewImage(bar, "InfoChip", theme, ThemeColor_Azm.PanelStrong);
            var chipLayout = chip.gameObject.AddComponent<LayoutElement>();
            chipLayout.preferredWidth = 340f;
            chipLayout.minWidth = 300f;
            chipLayout.preferredHeight = 112f;
            hud.infoChip = chip.gameObject;
            hud.infoChipText = UIFactory_Azm.NewText(chip.transform, "Text", theme, "", theme.fontTitle * 0.85f, ThemeColor_Azm.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            hud.infoChipText.enableAutoSizing = true;
            hud.infoChipText.fontSizeMin = theme.fontCaption;
            hud.infoChipText.fontSizeMax = theme.fontTitle * 0.85f;
            UIFactory_Azm.Stretch(hud.infoChipText.rectTransform, theme.spaceS, theme.spaceXS, theme.spaceS, theme.spaceXS);
            chip.gameObject.SetActive(false);
        }

        private static InstructionCardUI_Azm BuildInstructionCard(Transform bar)
        {
            var card = UIFactory_Azm.NewImage(bar, "InstructionCard", theme, ThemeColor_Azm.PanelStrong);
            var cardLayout = card.gameObject.AddComponent<LayoutElement>();
            cardLayout.flexibleWidth = 1f;
            cardLayout.minWidth = 600f;
            cardLayout.preferredHeight = TopBarHeight;
            var ui = card.gameObject.AddComponent<InstructionCardUI_Azm>();
            ui.theme = theme;
            var rect = card.rectTransform;

            // Text block (fades in on a new step).
            var textBlock = UIFactory_Azm.NewRect("Text", rect);
            UIFactory_Azm.Stretch(textBlock, theme.spaceM, 18f, 230f, 6f);
            ui.textGroup = textBlock.gameObject.AddComponent<CanvasGroup>();
            ui.stepLabel = UIFactory_Azm.NewText(textBlock, "StepLabel", theme, "STEP 1 OF 1", theme.fontCaption, ThemeColor_Azm.Primary, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            ui.stepLabel.characterSpacing = 4f;
            UIFactory_Azm.Pin(ui.stepLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(600f, 32f), Vector2.zero);
            ui.instructionText = UIFactory_Azm.NewText(textBlock, "Instruction", theme, "", theme.fontTitle, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ui.instructionText.enableAutoSizing = true;
            ui.instructionText.fontSizeMin = theme.fontLabel;
            ui.instructionText.fontSizeMax = theme.fontTitle;
            UIFactory_Azm.Stretch(ui.instructionText.rectTransform, 0f, 0f, 0f, 32f);

            // Buttons on the right: Why? and the hint bulb.
            var buttons = UIFactory_Azm.NewRect("Buttons", rect);
            UIFactory_Azm.Pin(buttons, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(220f, 90f), new Vector2(-theme.spaceS, 4f));
            var buttonsLayout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = theme.spaceXS;
            buttonsLayout.childAlignment = TextAnchor.MiddleRight;
            buttonsLayout.childControlWidth = buttonsLayout.childControlHeight = true;
            buttonsLayout.childForceExpandWidth = buttonsLayout.childForceExpandHeight = false;
            buttonsLayout.reverseArrangement = false;
            ui.hintButton = UIFactory_Azm.NewIconButton(buttons, "HintButton", theme, ThemeIcon_Azm.Lightbulb, ThemeColor_Azm.Accent, new Vector2(86f, 86f), true);
            ui.hintButton.transform.Find("Icon").GetComponent<Image>().color = theme.GetColor(ThemeColor_Azm.OnPrimary);
            ui.whyButton = UIFactory_Azm.NewButton(buttons, "WhyButton", theme, "Why?", ThemeColor_Azm.Secondary, new Vector2(124f, 86f), theme.fontBody);

            // Progress bar along the bottom edge.
            ui.progressRoot = UIFactory_Azm.NewRect("Progress", rect);
            ui.progressRoot.anchorMin = new Vector2(0f, 0f);
            ui.progressRoot.anchorMax = new Vector2(1f, 0f);
            ui.progressRoot.pivot = new Vector2(0.5f, 0f);
            ui.progressRoot.offsetMin = new Vector2(theme.spaceM, 8f);
            ui.progressRoot.offsetMax = new Vector2(-theme.spaceM, 16f);
            var progressLayout = ui.progressRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            progressLayout.spacing = 6f;
            progressLayout.childControlWidth = progressLayout.childControlHeight = true;
            progressLayout.childForceExpandWidth = true;
            progressLayout.childForceExpandHeight = true;

            // Drop-down detail (Why? / Hint), hanging under the card, the card's width.
            var detail = UIFactory_Azm.NewImage(rect, "Detail", theme, ThemeColor_Azm.PanelStrong);
            ui.detailPanel = detail.rectTransform;
            ui.detailPanel.anchorMin = new Vector2(0f, 0f);
            ui.detailPanel.anchorMax = new Vector2(1f, 0f);
            ui.detailPanel.pivot = new Vector2(0.5f, 1f);
            ui.detailPanel.anchoredPosition = new Vector2(0f, -theme.spaceXS);
            ui.detailPanel.sizeDelta = new Vector2(0f, 120f);
            ui.detailBackground = detail;
            ui.detailGroup = detail.gameObject.AddComponent<CanvasGroup>();
            var detailLayout = detail.gameObject.AddComponent<VerticalLayoutGroup>();
            detailLayout.padding = new RectOffset((int)theme.spaceM, (int)theme.spaceM, (int)theme.spaceS, (int)theme.spaceS);
            detailLayout.spacing = 4f;
            detailLayout.childControlWidth = detailLayout.childControlHeight = true;
            detailLayout.childForceExpandWidth = true;
            detailLayout.childForceExpandHeight = false;
            detail.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ui.detailTitle = UIFactory_Azm.NewText(detail.transform, "Title", theme, "Hint", theme.fontLabel, ThemeColor_Azm.Accent, TextAlignmentOptions.Left, FontStyles.Bold);
            ui.detailText = UIFactory_Azm.NewText(detail.transform, "Body", theme, "", theme.fontBody, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Left);
            ui.detailText.overflowMode = TextOverflowModes.Overflow;
            var bodyTheme = ui.detailText.GetComponent<ThemedText_Azm>();
            bodyTheme.scalesWithLargeText = true;
            bodyTheme.baseFontSize = theme.fontBody;
            ui.detailCloseArea = detail.gameObject.AddComponent<Button>();
            ui.detailCloseArea.transition = Selectable.Transition.None;   // tap the card to close it
            return ui;
        }

        private static void BuildBottomRow(Shell_Azm shell, PracticalHUDBase_Azm hud, ShellSpec_Azm spec)
        {
            float m = theme.screenMargin;
            var row = UIFactory_Azm.NewRect("BottomRow", shell.leftColumn);
            UIFactory_Azm.Pin(row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1200f, BottomRowHeight), new Vector2(0f, m));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = theme.spaceS;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            shell.bottomRow = row;

            var viewPanel = UIFactory_Azm.NewImage(row, "ViewButtons", theme, ThemeColor_Azm.PanelStrong);
            var viewLayout = viewPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            viewLayout.padding = new RectOffset(12, 12, 12, 12);
            viewLayout.spacing = theme.spaceXS;
            viewLayout.childAlignment = TextAnchor.MiddleCenter;
            viewLayout.childControlWidth = viewLayout.childControlHeight = true;
            viewLayout.childForceExpandWidth = viewLayout.childForceExpandHeight = false;
            hud.viewButtons = new Button[spec.viewNames.Length];
            for (int i = 0; i < spec.viewNames.Length; i++)
                hud.viewButtons[i] = IconLabelButton(viewPanel.transform, spec.viewNames[i] + "View", spec.viewNames[i], spec.viewIcons[i], new Vector2(150f, 132f));
        }

        // A button with an icon on top and a label under it (view buttons, drawer strip).
        public static Button IconLabelButton(Transform parent, string name, string label, ThemeIcon_Azm iconId, Vector2 size)
        {
            var b = UIFactory_Azm.NewButton(parent, name, theme, null, ThemeColor_Azm.Secondary, size);
            var icon = UIFactory_Azm.NewImage(b.transform, "Icon", theme, ThemeColor_Azm.TextPrimary, false);
            Object.DestroyImmediate(icon.GetComponent<ThemedImage_Azm>());
            var themedIcon = icon.gameObject.AddComponent<ThemedIcon_Azm>();
            themedIcon.theme = theme;
            themedIcon.icon = iconId;
            themedIcon.Apply();
            icon.raycastTarget = false;
            float iconSize = Mathf.Min(60f, size.y * 0.45f);
            UIFactory_Azm.Pin(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(iconSize, iconSize), new Vector2(0f, -size.y * 0.1f));
            var text = UIFactory_Azm.NewText(b.transform, "Label", theme, label, theme.fontCaption + 2f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = theme.fontCaption + 2f;
            UIFactory_Azm.Pin(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(size.x - 8f, 36f), new Vector2(0f, size.y * 0.07f));
            return b;
        }

        private static void BuildMagnifierFrame(Shell_Azm shell, PracticalHUDBase_Azm hud, ShellSpec_Azm spec)
        {
            float m = theme.screenMargin;
            var magnifier = MakeOverlay(shell.leftColumn, "Magnifier", ThemeColor_Azm.PanelStrong, true, SlideDirection_Azm.FromBottom);
            var rect = (RectTransform)magnifier.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(0f, m + BottomRowHeight + theme.spaceS);
            rect.offsetMax = new Vector2(0f, m + BottomRowHeight + theme.spaceS + MagnifierHeight);
            var title = UIFactory_Azm.NewText(rect, "Title", theme, spec.magnifierTitle, theme.fontCaption, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.MidlineLeft);
            UIFactory_Azm.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1000f, 40f), new Vector2(theme.spaceM, -6f));
            shell.magnifierContent = UIFactory_Azm.NewRect("Content", rect);
            UIFactory_Azm.Stretch(shell.magnifierContent, 20f, 14f, 20f, 48f);
            shell.magnifier = magnifier;
            hud.magnifierPanel = magnifier;
        }

        private static void BuildPad(Shell_Azm shell, PracticalHUDBase_Azm hud, ShellSpec_Azm spec, MonoBehaviour instrument)
        {
            float m = theme.screenMargin;
            var padPanel = MakeOverlay(shell.safe, "ReadingPad", ThemeColor_Azm.PanelStrong, true, SlideDirection_Azm.FromRight);
            var padRect = (RectTransform)padPanel.transform;
            UIFactory_Azm.Pin(padRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(RightPanelWidth, PadHeight), new Vector2(-(StripWidth + m + theme.spaceS), m));
            var pad = padRect.gameObject.AddComponent<CaliperReadingPadUI_Azm>();
            pad.theme = theme;
            pad.instrument = instrument;

            // Status line (what to do now) with the feedback card over it.
            pad.statusText = UIFactory_Azm.NewText(padRect, "Status", theme, "", theme.fontBody, ThemeColor_Azm.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory_Azm.Pin(pad.statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(RightPanelWidth - 40f, 112f), new Vector2(0f, -12f));
            pad.statusText.enableAutoSizing = true;
            pad.statusText.fontSizeMin = theme.fontCaption - 4f;
            pad.statusText.fontSizeMax = theme.fontBody;
            pad.feedback = BuildPanelFeedback(padRect, new Vector2(RightPanelWidth - 32f, 120f), new Vector2(0f, -8f));

            pad.mainField = PadField(padRect, "MainField", spec.padMainLabel, true, out pad.mainValueText, out pad.mainLabelText);
            pad.vernierField = PadField(padRect, "DivisionField", spec.padDivisionLabel, false, out pad.vernierValueText, out pad.vernierLabelText);
            pad.formulaText = UIFactory_Azm.NewText(padRect, "Formula", theme, "", theme.fontLabel, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center);
            pad.formulaText.enableAutoSizing = true;
            pad.formulaText.fontSizeMin = 20f;
            pad.formulaText.fontSizeMax = theme.fontLabel;
            UIFactory_Azm.Pin(pad.formulaText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(RightPanelWidth - 30f, 54f), new Vector2(0f, -266f));

            // Keys: 1-9, then C (or '.' for the screw gauge), 0, backspace.
            var keys = UIFactory_Azm.NewRect("Keys", padRect);
            UIFactory_Azm.Pin(keys, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(560f, 320f), new Vector2(0f, -330f));
            var grid = keys.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(178f, 72f);
            grid.spacing = new Vector2(12f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            pad.keysGroup = keys.gameObject.AddComponent<CanvasGroup>();
            pad.digitButtons = new Button[10];
            for (int d = 1; d <= 9; d++)
                pad.digitButtons[d] = UIFactory_Azm.NewButton(keys, "Key" + d, theme, d.ToString(), ThemeColor_Azm.Secondary, new Vector2(178f, 72f), theme.fontTitle);
            if (spec.padDecimalKey)
                pad.decimalButton = UIFactory_Azm.NewButton(keys, "KeyPoint", theme, ".", ThemeColor_Azm.Secondary, new Vector2(178f, 72f), theme.fontTitle + 4f);
            else
                pad.clearButton = UIFactory_Azm.NewButton(keys, "KeyClear", theme, "C", ThemeColor_Azm.Secondary, new Vector2(178f, 72f), theme.fontBody + 4f);
            pad.digitButtons[0] = UIFactory_Azm.NewButton(keys, "Key0", theme, "0", ThemeColor_Azm.Secondary, new Vector2(178f, 72f), theme.fontTitle);
            pad.backspaceButton = UIFactory_Azm.NewButton(keys, "KeyBack", theme, "⌫", ThemeColor_Azm.Secondary, new Vector2(178f, 72f), theme.fontBody + 4f);

            pad.showMeButton = Pinned(padRect, "ShowMeButton", CaliperTexts_Azm.ShowMeButton, ThemeColor_Azm.Secondary, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(250f, 110f), new Vector2(theme.spaceL, theme.spaceM), theme.fontBody);
            pad.checkButton = Pinned(padRect, "CheckButton", CaliperTexts_Azm.CheckButton, ThemeColor_Azm.Primary, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(290f, 110f), new Vector2(-theme.spaceL, theme.spaceM), theme.fontTitle);
            pad.continueButton = Pinned(padRect, "ContinueButton", CaliperTexts_Azm.ContinueButton, ThemeColor_Azm.Primary, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(RightPanelWidth - 80f, 110f), new Vector2(0f, theme.spaceM), theme.fontTitle);
            hud.padPanel = padPanel;
            hud.pad = pad;
        }

        private static Button PadField(RectTransform pad, string name, string label, bool left, out TMP_Text value, out TMP_Text caption)
        {
            var anchor = new Vector2(left ? 0f : 1f, 1f);
            float x = left ? theme.spaceM : -theme.spaceM;
            caption = UIFactory_Azm.NewText(pad, name + "Label", theme, label, theme.fontCaption, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.Center);
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 18f;
            caption.fontSizeMax = theme.fontCaption;
            UIFactory_Azm.Pin(caption.rectTransform, anchor, anchor, new Vector2(290f, 38f), new Vector2(x, -134f));
            var field = UIFactory_Azm.NewButton(pad, name, theme, null, ThemeColor_Azm.Secondary, new Vector2(286f, 88f));
            UIFactory_Azm.Pin(field.GetComponent<RectTransform>(), anchor, anchor, new Vector2(286f, 88f), new Vector2(x, -172f));
            value = UIFactory_Azm.NewText(field.transform, "Value", theme, "–", theme.fontDisplay * 0.8f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory_Azm.Stretch(value.rectTransform);
            return field;
        }

        // The feedback card used inside the keypad and the least-count panel.
        private static PanelFeedback_Azm BuildPanelFeedback(RectTransform parent, Vector2 size, Vector2 topPosition)
        {
            var card = UIFactory_Azm.NewImage(parent, "Feedback", theme, ThemeColor_Azm.PanelStrong);
            UIFactory_Azm.Pin(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), size, topPosition);
            card.gameObject.AddComponent<CanvasGroup>();
            var feedback = card.gameObject.AddComponent<PanelFeedback_Azm>();
            feedback.theme = theme;
            feedback.background = card;
            var icon = UIFactory_Azm.NewImage(card.transform, "Icon", theme, ThemeColor_Azm.TextPrimary, false);
            Object.DestroyImmediate(icon.GetComponent<ThemedImage_Azm>());
            feedback.iconTheme = icon.gameObject.AddComponent<ThemedIcon_Azm>();
            feedback.iconTheme.theme = theme;
            feedback.iconTheme.icon = ThemeIcon_Azm.Info;
            feedback.iconTheme.Apply();
            feedback.icon = icon;
            icon.raycastTarget = false;
            UIFactory_Azm.Pin(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 48f), new Vector2(theme.spaceS, 0f));
            feedback.label = UIFactory_Azm.NewText(card.transform, "Text", theme, "", theme.fontLabel, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.MidlineLeft);
            feedback.label.enableAutoSizing = true;
            feedback.label.fontSizeMin = 18f;
            feedback.label.fontSizeMax = theme.fontLabel;
            UIFactory_Azm.Stretch(feedback.label.rectTransform, 80f, 8f, theme.spaceS, 8f);
            return feedback;
        }

        private static void BuildLeastCount(Shell_Azm shell, PracticalHUDBase_Azm hud, ShellSpec_Azm spec)
        {
            float m = theme.screenMargin;
            var panel = MakeOverlay(shell.safe, "LeastCountPanel", ThemeColor_Azm.PanelStrong, true, SlideDirection_Azm.FromRight);
            var rect = (RectTransform)panel.transform;
            UIFactory_Azm.Pin(rect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(RightPanelWidth, LeastCountHeight), new Vector2(-(StripWidth + m + theme.spaceS), m));
            var title = UIFactory_Azm.NewText(rect, "Title", theme, spec.leastCountTitle, theme.fontTitle, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory_Azm.Pin(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(600f, 60f), new Vector2(0f, -16f));
            hud.leastCountPrompt = UIFactory_Azm.NewText(rect, "Prompt", theme, "", theme.fontLabel, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.Center);
            var promptTheme = hud.leastCountPrompt.GetComponent<ThemedText_Azm>();
            promptTheme.scalesWithLargeText = true;
            promptTheme.baseFontSize = theme.fontLabel;
            hud.leastCountPrompt.enableAutoSizing = true;
            hud.leastCountPrompt.fontSizeMin = theme.fontCaption - 2f;
            hud.leastCountPrompt.fontSizeMax = theme.fontLabel;
            UIFactory_Azm.Pin(hud.leastCountPrompt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(590f, 140f), new Vector2(0f, -82f));

            if (!string.IsNullOrEmpty(spec.turnButtonLabel))
                shell.turnButton = Pinned(rect, "TurnOneTurn", spec.turnButtonLabel, ThemeColor_Azm.Primary, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(480f, 110f), new Vector2(0f, -232f), theme.fontBody + 4f);

            hud.leastCountFeedback = BuildPanelFeedback(rect, new Vector2(RightPanelWidth - 32f, 120f), new Vector2(0f, -352f));

            var grid = UIFactory_Azm.NewRect("Choices", rect);
            UIFactory_Azm.Pin(grid, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(570f, 270f), new Vector2(0f, theme.spaceL));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(275f, 125f);
            layout.spacing = new Vector2(20f, 20f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            hud.leastCountButtons = new Button[4];
            for (int i = 0; i < 4; i++)
                hud.leastCountButtons[i] = UIFactory_Azm.NewButton(grid, "Choice" + i, theme, "–", ThemeColor_Azm.Secondary, new Vector2(275f, 125f), theme.fontTitle + 2f);   // label from the config at run time
            hud.leastCountPanel = panel;
        }

        private static DrawerUI_Azm BuildDrawer(Shell_Azm shell, PracticalHUDBase_Azm hud, PracticalGuide_Azm guide, ShellSpec_Azm spec)
        {
            float m = theme.screenMargin;
            var root = UIFactory_Azm.NewRect("Drawer", shell.safe);
            UIFactory_Azm.Stretch(root);
            var drawer = root.gameObject.AddComponent<DrawerUI_Azm>();

            ThemeIcon_Azm[] icons = spec.drawerTabIcons;
            string[] names = spec.drawerTabNames;
            int tabCount = names.Length;
            // Four tabs need narrower tab buttons to fit the 740-unit panel with the close button.
            float tabWidth = tabCount > 3 ? 138f : 180f;
            float stripButtonHeight = tabCount > 3 ? 124f : 136f;

            // Collapsed strip: icon + LABEL on each button (finding 11: icon-only buttons were unclear).
            var strip = UIFactory_Azm.NewImage(root, "Strip", theme, ThemeColor_Azm.PanelStrong);
            float stripHeight = tabCount * stripButtonHeight + (tabCount - 1) * theme.spaceXS + 20f;
            UIFactory_Azm.Pin(strip.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(StripWidth, stripHeight), new Vector2(-m * 0.5f, 40f));
            var stripGroup = strip.gameObject.AddComponent<CanvasGroup>();
            var stripLayout = strip.gameObject.AddComponent<VerticalLayoutGroup>();
            stripLayout.padding = new RectOffset(8, 8, 10, 10);
            stripLayout.spacing = theme.spaceXS;
            stripLayout.childAlignment = TextAnchor.MiddleCenter;
            stripLayout.childControlWidth = stripLayout.childControlHeight = true;
            stripLayout.childForceExpandWidth = stripLayout.childForceExpandHeight = false;
            var stripButtons = new Button[tabCount];
            for (int i = 0; i < tabCount; i++)
                stripButtons[i] = IconLabelButton(strip.transform, names[i] + "Strip", names[i], icons[i], new Vector2(104f, stripButtonHeight));

            // Panel: flush right, from under the top bar to the bottom margin.
            var panel = UIFactory_Azm.NewImage(root, "Panel", theme, ThemeColor_Azm.PanelStrong);
            var panelRect = panel.rectTransform;
            float top = m + TopBarHeight + theme.spaceS, bottom = m;
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.sizeDelta = new Vector2(DrawerWidth, -(top + bottom));
            panelRect.anchoredPosition = new Vector2(0f, (bottom - top) * 0.5f);

            var tabBar = UIFactory_Azm.NewRect("Tabs", panelRect);
            UIFactory_Azm.Pin(tabBar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(DrawerWidth - 40f, 100f), new Vector2(20f, -16f));
            var tabLayout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = theme.spaceXS;
            tabLayout.childAlignment = TextAnchor.MiddleLeft;
            tabLayout.childControlWidth = tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = false;
            var tabButtons = new Button[tabCount];
            for (int i = 0; i < tabCount; i++)
                tabButtons[i] = UIFactory_Azm.NewButton(tabBar, names[i] + "Tab", theme, names[i], ThemeColor_Azm.Secondary, new Vector2(tabWidth, 100f), theme.fontBody);
            var close = UIFactory_Azm.NewIconButton(tabBar, "CloseDrawer", theme, ThemeIcon_Azm.ChevronRight, ThemeColor_Azm.Secondary, new Vector2(100f, 100f));

            var paneArea = UIFactory_Azm.NewRect("Panes", panelRect);
            UIFactory_Azm.Stretch(paneArea, 20f, 20f, 20f, 130f);
            shell.panes = new RectTransform[tabCount];
            for (int i = 0; i < tabCount; i++) shell.panes[i] = Group(paneArea, names[i] + "Pane");
            shell.dataPane = shell.panes[0];
            shell.calcPane = shell.panes[spec.calcTabIndex];
            shell.guidePane = shell.panes[spec.guideTabIndex];

            // Calc: the worked calculation text (the practical adds its CalcUI component).
            if (spec.defaultCalcBody)
            {
                shell.calcBody = UIFactory_Azm.NewText(shell.calcPane, "Body", theme, "", theme.fontBody - 2f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft);
                shell.calcBody.overflowMode = TextOverflowModes.Overflow;
                shell.calcBody.gameObject.AddComponent<TypeInText_Azm>();   // a revealed line types in (Stage 6)
                var calcTheme = shell.calcBody.GetComponent<ThemedText_Azm>();
                calcTheme.scalesWithLargeText = true;
                calcTheme.baseFontSize = theme.fontBody - 2f;
                UIFactory_Azm.Stretch(shell.calcBody.rectTransform, 10f, 10f, 10f, 0f);
            }

            // Guide: scrolling text + replay briefing.
            var guideContent = UIBuilder_Azm.NewVerticalScroll(shell.guidePane, "GuideScroll", out ScrollRect guideScroll);
            var guideScrollRect = guideScroll.GetComponent<RectTransform>();
            guideScrollRect.anchorMin = Vector2.zero;
            guideScrollRect.anchorMax = Vector2.one;
            guideScrollRect.offsetMin = new Vector2(0f, 110f);
            guideScrollRect.offsetMax = Vector2.zero;
            var guideLayout = guideContent.gameObject.AddComponent<VerticalLayoutGroup>();
            guideLayout.padding = new RectOffset(10, 10, 0, 20);
            guideLayout.childControlWidth = guideLayout.childControlHeight = true;
            guideLayout.childForceExpandWidth = true;
            guideLayout.childForceExpandHeight = false;
            guideContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var guideText = UIFactory_Azm.NewText(guideContent, "GuideText", theme, "", theme.fontLabel, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft);
            guideText.overflowMode = TextOverflowModes.Overflow;
            var guideTheme = guideText.GetComponent<ThemedText_Azm>();
            guideTheme.scalesWithLargeText = true;
            guideTheme.baseFontSize = theme.fontLabel;
            var guideView = shell.guidePane.gameObject.AddComponent<GuideViewUI_Azm>();
            guideView.theme = theme;
            guideView.guide = guide;
            guideView.body = guideText;
            if (string.IsNullOrEmpty(spec.guideReplayDemoLabel))
                guideView.replayBriefingButton = Pinned(shell.guidePane, "ReplayBriefing", "Replay briefing", ThemeColor_Azm.Secondary, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(420f, 100f), Vector2.zero, theme.fontBody);
            else
            {
                // Two replay buttons side by side.
                guideView.replayBriefingButton = Pinned(shell.guidePane, "ReplayBriefing", "Replay briefing", ThemeColor_Azm.Secondary, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(330f, 100f), new Vector2(-theme.spaceXS, 0f), theme.fontLabel);
                guideView.replayDemoButton = Pinned(shell.guidePane, "ReplayDemo", spec.guideReplayDemoLabel, ThemeColor_Azm.Secondary, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(330f, 100f), new Vector2(theme.spaceXS, 0f), theme.fontLabel);
            }
            hud.guideView = guideView;

            drawer.panel = panelRect;
            drawer.panelWidth = DrawerWidth;
            drawer.strip = strip.rectTransform;
            drawer.stripWidth = StripWidth;
            drawer.stripGroup = stripGroup;
            drawer.tabButtons = tabButtons;
            drawer.stripButtons = stripButtons;
            var paneObjects = new GameObject[tabCount];
            for (int i = 0; i < tabCount; i++) paneObjects[i] = shell.panes[i].gameObject;
            drawer.panes = paneObjects;
            drawer.closeButton = close;
            return drawer;
        }

        // The toast: top of the left column, centred, never wider than ~1100 units.
        private static ToastUI_Azm BuildToast(Shell_Azm shell)
        {
            var image = UIFactory_Azm.NewImage(shell.leftColumn, "Toast", theme, ThemeColor_Azm.PanelStrong);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(1000f, 100f);
            rect.anchoredPosition = new Vector2(0f, -(theme.screenMargin + TopBarHeight + theme.spaceS));
            image.gameObject.AddComponent<CanvasGroup>();
            var clamp = image.gameObject.AddComponent<ClampWidth_Azm>();
            clamp.maxWidth = 1100f;
            clamp.sideMargin = theme.spaceS;
            var layout = image.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 18, 18);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            image.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var label = UIFactory_Azm.NewText(image.transform, "Label", theme, "", theme.fontLabel, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Left);
            label.overflowMode = TextOverflowModes.Overflow;
            var labelTheme = label.GetComponent<ThemedText_Azm>();
            labelTheme.scalesWithLargeText = true;
            labelTheme.baseFontSize = theme.fontLabel;
            var toast = image.gameObject.AddComponent<ToastUI_Azm>();
            toast.label = label;
            toast.background = image;
            return toast;
        }

        // An empty results frame (the practical adds its values); Retry and Main menu buttons.
        private static void BuildResultsFrame(Shell_Azm shell, ShellSpec_Azm spec)
        {
            var panel = MakeOverlay(shell.safe, "ResultsPanel", ThemeColor_Azm.PanelStrong, true, SlideDirection_Azm.FromBottom);
            UIFactory_Azm.Pin(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), spec.resultsSize, new Vector2(0f, -40f));
            var title = UIFactory_Azm.NewText(panel.transform, "Title", theme, spec.resultsTitle, theme.fontDisplay, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIFactory_Azm.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(900f, 100f), new Vector2(50f, -30f));
            shell.retryButton = Pinned(panel.transform, "RetryButton", "Retry", ThemeColor_Azm.Secondary, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(420f, 130f), new Vector2(-20f, 50f), theme.fontTitle);
            shell.menuButton = Pinned(panel.transform, "MainMenuButton", "Main menu", ThemeColor_Azm.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(420f, 130f), new Vector2(20f, 50f), theme.fontTitle);
            shell.resultsPanel = panel;
        }

        // Pause menu: Resume, [practical extra], Restart practical, Settings, Guide, Exit - a vertical
        // layout, so an optional button never needs hand-placed positions. Fits the 1080 minimum.
        private static void BuildPause(Shell_Azm shell, PracticalHUDBase_Azm hud, ShellSpec_Azm spec)
        {
            var scrim = MakeScrim(shell.safe, "PausePanel");
            bool extra = !string.IsNullOrEmpty(spec.pauseExtraButtonLabel);
            int count = extra ? 6 : 5;
            const float buttonHeight = 108f;
            float boxHeight = 150f + count * buttonHeight + (count - 1) * 14f + 40f;
            var box = AbsorbingBox(scrim.transform, new Vector2(760f, boxHeight));
            var title = UIFactory_Azm.NewText(box.transform, "Title", theme, "Paused", theme.fontDisplay + 8f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory_Azm.Pin(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(700f, 110f), new Vector2(0f, -30f));

            var list = UIFactory_Azm.NewRect("Buttons", box.transform);
            UIFactory_Azm.Stretch(list, 120f, 40f, 120f, 150f);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var size = new Vector2(520f, buttonHeight);
            hud.resumeButton = UIFactory_Azm.NewButton(list, "ResumeButton", theme, "Resume", ThemeColor_Azm.Primary, size, theme.fontTitle + 2f);
            if (extra) shell.pauseExtraButton = UIFactory_Azm.NewButton(list, "ExtraButton", theme, spec.pauseExtraButtonLabel, ThemeColor_Azm.Secondary, size, theme.fontTitle - 4f);
            hud.restartButton = UIFactory_Azm.NewButton(list, "RestartButton", theme, "Restart practical", ThemeColor_Azm.Secondary, size, theme.fontTitle - 4f);
            hud.settingsButton = UIFactory_Azm.NewButton(list, "SettingsButton", theme, "Settings", ThemeColor_Azm.Secondary, size, theme.fontTitle - 4f);
            hud.guideButton = UIFactory_Azm.NewButton(list, "GuideButton", theme, "Guide", ThemeColor_Azm.Secondary, size, theme.fontTitle - 4f);
            hud.exitButton = UIFactory_Azm.NewButton(list, "ExitButton", theme, "Exit to menu", ThemeColor_Azm.Secondary, size, theme.fontTitle - 4f);
            hud.pausePanel = scrim;
        }

        // The same settings as the main menu (SettingsPanelUI_Azm), opened from the pause menu, so a
        // student can turn sound, hints, vibration, motion or text size up/down without leaving.
        private static void BuildSettings(Shell_Azm shell, PracticalHUDBase_Azm hud)
        {
            var scrim = MakeScrim(shell.safe, "SettingsPanel");
            var box = AbsorbingBox(scrim.transform, new Vector2(1300f, 900f));
            var title = UIFactory_Azm.NewText(box.transform, "Title", theme, "Settings", theme.fontDisplay, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIFactory_Azm.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, 110f), new Vector2(60f, -24f));
            hud.settingsCloseButton = UIFactory_Azm.NewIconButton(box.transform, "Close", theme, ThemeIcon_Azm.Close, ThemeColor_Azm.Secondary, new Vector2(110f, 110f));
            UIFactory_Azm.Pin(hud.settingsCloseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(110f, 110f), new Vector2(-24f, -24f));

            var rows = UIFactory_Azm.NewRect("Rows", box.transform);
            UIFactory_Azm.Stretch(rows, 60f, 40f, 60f, 150f);
            var rowsLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 12f;
            rowsLayout.childAlignment = TextAnchor.UpperLeft;
            rowsLayout.childControlWidth = rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = rowsLayout.childForceExpandHeight = false;

            var ui = box.gameObject.AddComponent<VirtualLab.Azm.Menu.SettingsPanelUI_Azm>();
            ui.masterSlider = SliderRow(rows, "Master volume");
            ui.sfxSlider = SliderRow(rows, "SFX volume");
            var row1 = Row(rows, "ToggleRow1");
            ui.hintsToggle = UIBuilder_Azm.NewToggle(row1, "HintsToggle", theme, "Show hints", new Vector2(570f, 104f));
            ui.hapticsToggle = UIBuilder_Azm.NewToggle(row1, "HapticsToggle", theme, "Vibration", new Vector2(570f, 104f));
            var row2 = Row(rows, "ToggleRow2");
            ui.reduceMotionToggle = UIBuilder_Azm.NewToggle(row2, "ReduceMotionToggle", theme, "Reduce motion", new Vector2(570f, 104f));
            ui.largeTextToggle = UIBuilder_Azm.NewToggle(row2, "LargeTextToggle", theme, "Large text", new Vector2(570f, 104f));
            var row3 = Row(rows, "QualityRow");
            var label = UIFactory_Azm.NewText(row3, "Label", theme, "Graphics", theme.fontTitle - 4f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.MidlineLeft);
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 300f;
            labelLayout.preferredHeight = 104f;
            ui.lowQualityButton = UIFactory_Azm.NewButton(row3, "LowButton", theme, "Low", ThemeColor_Azm.Secondary, new Vector2(260f, 100f), theme.fontTitle - 4f);
            ui.highQualityButton = UIFactory_Azm.NewButton(row3, "HighButton", theme, "High", ThemeColor_Azm.Secondary, new Vector2(260f, 100f), theme.fontTitle - 4f);
            ui.resetTutorialsButton = UIFactory_Azm.NewButton(row3, "ResetTutorialsButton", theme, "Reset tutorials", ThemeColor_Azm.Secondary, new Vector2(300f, 100f), theme.fontLabel);
            ui.toast = null;
            hud.settingsPanel = scrim;
        }

        private static RectTransform Row(Transform parent, string name)
        {
            var row = UIFactory_Azm.NewRect(name, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = theme.spaceS;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var size = row.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = 108f;
            size.minHeight = 108f;
            return row;
        }

        private static Slider SliderRow(Transform parent, string label)
        {
            var row = Row(parent, label.Replace(" ", "") + "Row");
            var text = UIFactory_Azm.NewText(row, "Label", theme, label, theme.fontTitle - 4f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.MidlineLeft);
            var textLayout = text.gameObject.AddComponent<LayoutElement>();
            textLayout.preferredWidth = 400f;
            textLayout.preferredHeight = 104f;
            return UIBuilder_Azm.NewSlider(row, "Slider", theme, new Vector2(700f, 104f));
        }

        private static ConfirmDialog_Azm BuildConfirm(Shell_Azm shell)
        {
            var scrim = MakeScrim(shell.safe, "ConfirmDialog");
            var backdrop = scrim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;
            var box = AbsorbingBox(scrim.transform, new Vector2(1000f, 480f));
            var message = UIFactory_Azm.NewText(box.transform, "Message", theme, "", theme.fontTitle, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center);
            UIFactory_Azm.Pin(message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 240f), new Vector2(0f, -40f));
            var no = Pinned(box.transform, "NoButton", "Cancel", ThemeColor_Azm.Secondary, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(400f, 120f), new Vector2(-15f, 40f), theme.fontTitle);
            var yes = Pinned(box.transform, "YesButton", "OK", ThemeColor_Azm.Invalid, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(400f, 120f), new Vector2(15f, 40f), theme.fontTitle);
            var dialog = scrim.gameObject.AddComponent<ConfirmDialog_Azm>();
            dialog.messageText = message;
            dialog.yesLabel = yes.GetComponentInChildren<TMP_Text>();
            dialog.yesButton = yes;
            dialog.noButton = no;
            dialog.backdropButton = backdrop;
            scrim.Hide(false);
            return dialog;
        }

        private static BriefingUI_Azm BuildBriefing(Shell_Azm shell)
        {
            var scrim = MakeScrim(shell.safe, "Briefing");
            var blocker = scrim.gameObject.AddComponent<Button>();
            blocker.transition = Selectable.Transition.None;   // nothing behind it can be tapped
            var box = AbsorbingBox(scrim.transform, new Vector2(1200f, 700f));
            var briefing = scrim.gameObject.AddComponent<BriefingUI_Azm>();
            briefing.titleText = UIFactory_Azm.NewText(box.transform, "Title", theme, "", theme.fontDisplay, ThemeColor_Azm.Primary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIFactory_Azm.Pin(briefing.titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(900f, 100f), new Vector2(60f, -40f));
            briefing.pageText = UIFactory_Azm.NewText(box.transform, "Page", theme, "", theme.fontBody, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.MidlineRight);
            UIFactory_Azm.Pin(briefing.pageText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(200f, 100f), new Vector2(-60f, -40f));
            briefing.bodyText = UIFactory_Azm.NewText(box.transform, "Body", theme, "", theme.fontTitle - 4f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft);
            briefing.bodyText.enableAutoSizing = true;
            briefing.bodyText.fontSizeMin = theme.fontLabel;
            briefing.bodyText.fontSizeMax = theme.fontTitle - 4f;
            UIFactory_Azm.Stretch(briefing.bodyText.rectTransform, 60f, 190f, 60f, 160f);
            briefing.skipButton = Pinned(box.transform, "SkipButton", "Skip", ThemeColor_Azm.Secondary, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(300f, 120f), new Vector2(60f, 50f), theme.fontTitle);
            briefing.nextButton = Pinned(box.transform, "NextButton", "Next", ThemeColor_Azm.Primary, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(360f, 120f), new Vector2(-60f, 50f), theme.fontTitle + 4f);
            briefing.nextLabel = briefing.nextButton.GetComponentInChildren<TMP_Text>();
            scrim.Hide(false);
            return briefing;
        }

        // =====================================================================
        // Small helpers
        // =====================================================================

        public static PanelTransition_Azm MakeOverlay(Transform parent, string name, ThemeColor_Azm role, bool rounded, SlideDirection_Azm slide)
        {
            var image = UIFactory_Azm.NewImage(parent, name, theme, role, rounded);
            image.gameObject.AddComponent<CanvasGroup>();
            var transition = image.gameObject.AddComponent<PanelTransition_Azm>();
            transition.theme = theme;
            transition.slideFrom = slide;
            return transition;
        }

        public static PanelTransition_Azm MakeScrim(Transform parent, string name)
        {
            var scrim = MakeOverlay(parent, name, ThemeColor_Azm.Scrim, false, SlideDirection_Azm.None);
            scrim.GetComponent<Image>().sprite = null;
            scrim.GetComponent<Image>().type = Image.Type.Simple;
            UIFactory_Azm.Stretch(scrim.GetComponent<RectTransform>());
            return scrim;
        }

        public static Image AbsorbingBox(Transform parent, Vector2 size)
        {
            var box = UIFactory_Azm.NewImage(parent, "Box", theme, ThemeColor_Azm.PanelStrong);
            UIFactory_Azm.Pin(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, Vector2.zero);
            var absorb = box.gameObject.AddComponent<Button>();
            absorb.transition = Selectable.Transition.None;   // taps on the box do not reach the scrim
            return box;
        }

        public static Button Pinned(Transform parent, string name, string label, ThemeColor_Azm role, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos, float font)
        {
            var b = UIFactory_Azm.NewButton(parent, name, theme, label, role, size, font);
            UIFactory_Azm.Pin(b.GetComponent<RectTransform>(), anchor, pivot, size, pos);
            return b;
        }

        public static RectTransform Group(Transform parent, string name)
        {
            var rect = UIFactory_Azm.NewRect(name, parent);
            UIFactory_Azm.Stretch(rect);
            return rect;
        }
    }
}
