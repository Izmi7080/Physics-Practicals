using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.Menu;
using VirtualLab.Azm.Pendulum;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.EditorTools
{
    /*
     * Layer: Editor tooling (not part of the shipped app).
     * Purpose: The scene-building half of ShellBuilder_Azm: builds Splash_Azm, MainMenu_Azm and
     * SimplePendulum_Azm (still an empty lab with a placeholder screen), plus the small helpers
     * they share (camera, canvas, event system, bootstrap, panels). All colours come from the
     * UITheme_Azm through the Themed* components; all layout is in the 1920x1080 reference
     * resolution inside a safe-area container.
     * Talks to: UIBuilder_Azm, the runtime scripts in Core/Menu/UI/Practicals, EditorSceneManager.
     */
    public static partial class ShellBuilder_Azm
    {
        // ---------- shared scene helpers ----------

        private static void NewEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static Camera CreateCamera(Color background, bool useSkybox)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.fieldOfView = 50f;
            camera.clearFlags = useSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            go.AddComponent<UniversalAdditionalCameraData>();
            return camera;
        }

        private static void CreateLight()
        {
            var go = new GameObject("Directional Light", typeof(Light));
            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.6f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static BackButtonInput_Azm CreateBootstrap(UITheme_Azm theme, PracticalCatalog_Azm catalog)
        {
            var go = new GameObject("Bootstrap_Azm");
            var bootstrap = go.AddComponent<SceneBootstrap_Azm>();
            bootstrap.theme = theme;
            bootstrap.catalog = catalog;
            return go.AddComponent<BackButtonInput_Azm>();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static Canvas CreateCanvas(string name, out RectTransform safeArea)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // HCI restructure: "Expand" (never smaller than 1920x1080 units), shared with the practicals.
            PracticalShellBuilder_Azm.ConfigureScaler(go.GetComponent<CanvasScaler>());

            // All interactive UI sits under the safe-area container (CLAUDE.md).
            safeArea = UIBuilder_Azm.NewRect("SafeArea", go.transform);
            UIBuilder_Azm.Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaContainer_Azm>();
            return canvas;
        }

        private static RectTransform NewGroup(Transform parent, string name)
        {
            var rect = UIBuilder_Azm.NewRect(name, parent);
            UIBuilder_Azm.Stretch(rect);
            rect.gameObject.AddComponent<CanvasGroup>();
            return rect;
        }

        private static PanelTransition_Azm MakePanel(Transform parent, string name, UITheme_Azm theme,
            ThemeColor_Azm role, SlideDirection_Azm slide)
        {
            var image = UIBuilder_Azm.NewPanel(parent, name, theme, role);
            image.gameObject.AddComponent<CanvasGroup>();
            var transition = image.gameObject.AddComponent<PanelTransition_Azm>();
            transition.theme = theme;
            transition.slideFrom = slide;
            return transition;
        }

        private static void Fixed(Component component, float width, float height)
        {
            var layout = component.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.minHeight = height;
        }

        private static Button NewBackButton(Transform parent, UITheme_Azm theme)
        {
            var button = UIBuilder_Azm.NewButton(parent, "BackButton", theme, "<", ThemeColor_Azm.Secondary,
                new Vector2(UIBuilder_Azm.TouchSize, UIBuilder_Azm.TouchSize), 64f);
            UIBuilder_Azm.Pin(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(UIBuilder_Azm.TouchSize, UIBuilder_Azm.TouchSize), new Vector2(30f, -30f));
            return button;
        }

        // A header title to the right of the back button.
        private static TextMeshProUGUI NewPanelTitle(Transform parent, UITheme_Azm theme, string text)
        {
            var title = UIBuilder_Azm.NewText(parent, "Title", theme, text, 64f, ThemeColor_Azm.TextPrimary,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIBuilder_Azm.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(900f, UIBuilder_Azm.TouchSize), new Vector2(180f, -30f));
            return title;
        }

        // ---------- Splash_Azm ----------

        private static void BuildSplashScene(UITheme_Azm theme, PracticalCatalog_Azm catalog)
        {
            NewEmptyScene();
            CreateCamera(new Color(0.04f, 0.07f, 0.13f), false);
            CreateBootstrap(theme, catalog);
            CreateEventSystem();

            Canvas canvas = CreateCanvas("Canvas", out RectTransform safe);

            var backdrop = UIBuilder_Azm.NewPanel(canvas.transform, "Backdrop", theme, ThemeColor_Azm.PanelStrong, false);
            UIBuilder_Azm.Stretch(backdrop.rectTransform);
            backdrop.transform.SetAsFirstSibling();

            // 1. University logo (placeholder slot until the real sprite is assigned)
            var logoGroup = NewGroup(safe, "LogoGroup");
            var logoImage = UIBuilder_Azm.NewPanel(logoGroup, "Logo", theme, ThemeColor_Azm.TextPrimary, false);
            UIBuilder_Azm.Pin(logoImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 360f), Vector2.zero);
            logoImage.preserveAspect = true;

            var placeholder = UIBuilder_Azm.NewPanel(logoGroup, "LogoPlaceholder", theme, ThemeColor_Azm.Secondary);
            UIBuilder_Azm.Pin(placeholder.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 360f), Vector2.zero);
            var placeholderLabel = UIBuilder_Azm.NewText(placeholder.transform, "Label", theme, "University logo", 38f,
                ThemeColor_Azm.TextSecondary, TextAlignmentOptions.Center);
            UIBuilder_Azm.Stretch(placeholderLabel.rectTransform, 20f, 20f, 20f, 20f);

            // 2. App title + tagline
            var titleGroup = NewGroup(safe, "TitleGroup");
            var title = UIBuilder_Azm.NewText(titleGroup, "Title", theme, "Virtual Science Lab", 120f,
                ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIBuilder_Azm.Pin(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1600f, 170f), new Vector2(0f, 60f));
            var tagline = UIBuilder_Azm.NewText(titleGroup, "Tagline", theme, "Learn by doing - practise every practical before your exam", 46f,
                ThemeColor_Azm.TextSecondary, TextAlignmentOptions.Center);
            UIBuilder_Azm.Pin(tagline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1600f, 90f), new Vector2(0f, -70f));

            // 3. Credit line
            var creditGroup = NewGroup(safe, "CreditGroup");
            var credit = UIBuilder_Azm.NewText(creditGroup, "Credit", theme, "Final Year Project - Information Technology University", 40f,
                ThemeColor_Azm.TextSecondary, TextAlignmentOptions.Center);
            UIBuilder_Azm.Pin(credit.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1600f, 80f), new Vector2(0f, 100f));

            // Invisible full-screen tap target for "tap to skip" (enabled by the controller only
            // after the first launch).
            var skipRect = UIBuilder_Azm.NewRect("SkipButton", canvas.transform);
            UIBuilder_Azm.Stretch(skipRect);
            var skipImage = skipRect.gameObject.AddComponent<Image>();
            skipImage.color = new Color(0f, 0f, 0f, 0f);
            var skipButton = skipRect.gameObject.AddComponent<Button>();
            skipButton.targetGraphic = skipImage;
            skipButton.transition = Selectable.Transition.None;

            var controller = canvas.gameObject.AddComponent<SplashController_Azm>();
            controller.logoGroup = logoGroup.GetComponent<CanvasGroup>();
            controller.titleGroup = titleGroup.GetComponent<CanvasGroup>();
            controller.creditGroup = creditGroup.GetComponent<CanvasGroup>();
            controller.logoImage = logoImage;
            controller.logoPlaceholder = placeholder.gameObject;
            controller.skipButton = skipButton;

            EnsureFolder(SceneFolder);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), SplashScenePath);
        }

        // ---------- MainMenu_Azm ----------

        private static void BuildMainMenuScene(UITheme_Azm theme, PracticalCatalog_Azm catalog, AssetCredits_Azm credits, GameObject labPrefab)
        {
            NewEmptyScene();

            var cameraComponent = CreateCamera(Color.black, true);
            CreateLight();
            BackButtonInput_Azm backInput = CreateBootstrap(theme, catalog);
            CreateEventSystem();

            // Background: the lab room, the simplified pendulum stand on the island bench, and a
            // camera that slowly drifts around it.
            var lab = (GameObject)PrefabUtility.InstantiatePrefab(labPrefab);
            lab.transform.position = Vector3.zero;

            Transform stand = BuildMenuStand(new Vector3(0.23f, 0.988f, -0.15f));

            var drift = cameraComponent.gameObject.AddComponent<MenuCameraDrift_Azm>();
            drift.target = stand;
            drift.lookOffset = new Vector3(-0.8f, 0.6f, 0f);   // look left of the stand so it sits right of the menu panel
            drift.radius = 3.0f;
            drift.heightAboveTarget = 0.15f;
            drift.centreAngleDegrees = 180f;                   // camera on the -Z side, facing the cabinets
            drift.sweepDegrees = 20f;
            drift.periodSeconds = 30f;

            PracticalCardUI_Azm cardPrefab = BuildCardPrefab(theme);

            Canvas canvas = CreateCanvas("Canvas", out RectTransform safe);

            // ----- Main panel (left side) -----
            var mainPanel = MakePanel(safe, "MainPanel", theme, ThemeColor_Azm.Panel, SlideDirection_Azm.FromLeft);
            var mainRect = mainPanel.GetComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0f, 0f);
            mainRect.anchorMax = new Vector2(0f, 1f);
            mainRect.pivot = new Vector2(0f, 0.5f);
            mainRect.sizeDelta = new Vector2(760f, -120f);
            mainRect.anchoredPosition = new Vector2(60f, 0f);

            var mainLayout = mainPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            mainLayout.padding = new RectOffset(50, 50, 40, 40);
            mainLayout.spacing = 20f;
            mainLayout.childAlignment = TextAnchor.UpperCenter;
            mainLayout.childControlWidth = true;
            mainLayout.childControlHeight = true;
            mainLayout.childForceExpandWidth = false;
            mainLayout.childForceExpandHeight = false;

            var appTitle = UIBuilder_Azm.NewText(mainPanel.transform, "AppTitle", theme, "Virtual Science Lab", 76f,
                ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            // Baseline finding 5: at 76 the title did not fit its box and was cut ("Virtual Scienc...").
            // It now shrinks to fit (never below 52) instead of being cut.
            appTitle.enableAutoSizing = true;
            appTitle.fontSizeMin = 52f;
            appTitle.fontSizeMax = 76f;
            appTitle.overflowMode = TextOverflowModes.Overflow;
            Fixed(appTitle, 640f, 180f);
            var subtitle = UIBuilder_Azm.NewText(mainPanel.transform, "Subtitle", theme, "Do every practical yourself", 38f,
                ThemeColor_Azm.TextSecondary, TextAlignmentOptions.Center);
            Fixed(subtitle, 640f, 56f);
            var spacer = UIBuilder_Azm.NewRect("Spacer", mainPanel.transform);
            var spacerLayout = spacer.gameObject.AddComponent<LayoutElement>();
            spacerLayout.preferredHeight = 10f;
            spacerLayout.minHeight = 10f;

            Button startButton = UIBuilder_Azm.NewButton(mainPanel.transform, "StartButton", theme, "Start", ThemeColor_Azm.Primary, new Vector2(640f, 130f), 52f);
            Button settingsButton = UIBuilder_Azm.NewButton(mainPanel.transform, "SettingsButton", theme, "Settings", ThemeColor_Azm.Secondary, new Vector2(640f, 120f));
            Button aboutButton = UIBuilder_Azm.NewButton(mainPanel.transform, "AboutButton", theme, "About", ThemeColor_Azm.Secondary, new Vector2(640f, 120f));
            Button quitButton = UIBuilder_Azm.NewButton(mainPanel.transform, "QuitButton", theme, "Quit", ThemeColor_Azm.Secondary, new Vector2(640f, 120f));

            // ----- Practical select -----
            var selectPanel = MakePanel(safe, "PracticalSelectPanel", theme, ThemeColor_Azm.PanelStrong, SlideDirection_Azm.FromRight);
            UIBuilder_Azm.Stretch(selectPanel.GetComponent<RectTransform>(), 60f, 60f, 60f, 60f);
            Button selectBack = NewBackButton(selectPanel.transform, theme);
            NewPanelTitle(selectPanel.transform, theme, "Choose a practical");

            var tabRow = UIBuilder_Azm.NewRect("SubjectTabs", selectPanel.transform);
            UIBuilder_Azm.Pin(tabRow, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1000f, UIBuilder_Azm.TouchSize), new Vector2(-30f, -30f));
            var tabLayout = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 20f;
            tabLayout.childAlignment = TextAnchor.MiddleRight;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = false;
            tabLayout.childForceExpandHeight = false;
            var subjectTabs = new Button[3];
            string[] subjectNames = { "Physics", "Chemistry", "Biology" };
            for (int i = 0; i < 3; i++)
                subjectTabs[i] = UIBuilder_Azm.NewButton(tabRow, subjectNames[i] + "Tab", theme, subjectNames[i], ThemeColor_Azm.Secondary, new Vector2(310f, UIBuilder_Azm.TouchSize), 40f);

            ScrollRect scroll;
            RectTransform cardContent = UIBuilder_Azm.NewVerticalScroll(selectPanel.transform, "CardScroll", out scroll);
            UIBuilder_Azm.Stretch(scroll.GetComponent<RectTransform>(), 30f, 30f, 30f, 180f);
            var grid = cardContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(490f, 480f);
            grid.spacing = new Vector2(34f, 34f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = cardContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ----- Practical detail / mode -----
            var detailPanel = MakePanel(safe, "PracticalDetailPanel", theme, ThemeColor_Azm.PanelStrong, SlideDirection_Azm.FromRight);
            UIBuilder_Azm.Pin(detailPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1500f, 900f), Vector2.zero);
            Button detailBack = NewBackButton(detailPanel.transform, theme);

            var detailName = UIBuilder_Azm.NewText(detailPanel.transform, "Name", theme, "Practical", 64f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIBuilder_Azm.Pin(detailName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1200f, 80f), new Vector2(180f, -30f));
            var detailTime = UIBuilder_Azm.NewText(detailPanel.transform, "Time", theme, "", 36f, ThemeColor_Azm.Accent, TextAlignmentOptions.MidlineLeft);
            UIBuilder_Azm.Pin(detailTime.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1200f, 50f), new Vector2(180f, -110f));

            var detailDescription = UIBuilder_Azm.NewText(detailPanel.transform, "Description", theme, "", 38f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft);
            UIBuilder_Azm.Pin(detailDescription.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(820f, 200f), new Vector2(60f, -200f));
            var apparatusHeading = UIBuilder_Azm.NewText(detailPanel.transform, "ApparatusHeading", theme, "Apparatus", 40f, ThemeColor_Azm.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIBuilder_Azm.Pin(apparatusHeading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(820f, 56f), new Vector2(60f, -430f));
            var detailApparatus = UIBuilder_Azm.NewText(detailPanel.transform, "Apparatus", theme, "", 34f, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.TopLeft);
            UIBuilder_Azm.Pin(detailApparatus.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(820f, 360f), new Vector2(60f, -490f));

            var modeHeading = UIBuilder_Azm.NewText(detailPanel.transform, "ModeHeading", theme, "Mode", 40f, ThemeColor_Azm.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIBuilder_Azm.Pin(modeHeading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(480f, 56f), new Vector2(940f, -200f));
            Button learnButton = UIBuilder_Azm.NewButton(detailPanel.transform, "LearnButton", theme, "Learn", ThemeColor_Azm.Primary, new Vector2(480f, 120f), 44f);
            UIBuilder_Azm.Pin(learnButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(480f, 120f), new Vector2(940f, -270f));
            Button performButton = UIBuilder_Azm.NewButton(detailPanel.transform, "PerformButton", theme, "Perform", ThemeColor_Azm.Secondary, new Vector2(480f, 120f), 44f);
            UIBuilder_Azm.Pin(performButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(480f, 120f), new Vector2(940f, -410f));
            var performState = UIBuilder_Azm.NewText(detailPanel.transform, "PerformState", theme, "Coming soon", 32f, ThemeColor_Azm.Accent, TextAlignmentOptions.TopLeft);
            UIBuilder_Azm.Pin(performState.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(480f, 46f), new Vector2(940f, -540f));

            Button detailStart = UIBuilder_Azm.NewButton(detailPanel.transform, "StartPracticalButton", theme, "Start", ThemeColor_Azm.Primary, new Vector2(480f, 130f), 52f);
            UIBuilder_Azm.Pin(detailStart.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(480f, 130f), new Vector2(-80f, 60f));

            var detailUI = detailPanel.gameObject.AddComponent<PracticalDetailPanelUI_Azm>();
            detailUI.nameText = detailName;
            detailUI.descriptionText = detailDescription;
            detailUI.timeText = detailTime;
            detailUI.apparatusText = detailApparatus;
            detailUI.learnButton = learnButton;
            detailUI.performButton = performButton;
            detailUI.performStateLabel = performState;
            detailUI.startButton = detailStart;

            // ----- Settings -----
            var settingsPanel = MakePanel(safe, "SettingsPanel", theme, ThemeColor_Azm.PanelStrong, SlideDirection_Azm.FromRight);
            UIBuilder_Azm.Pin(settingsPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1300f, 1010f), Vector2.zero);
            Button settingsBack = NewBackButton(settingsPanel.transform, theme);
            NewPanelTitle(settingsPanel.transform, theme, "Settings");

            var rows = UIBuilder_Azm.NewRect("Rows", settingsPanel.transform);
            UIBuilder_Azm.Pin(rows, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1180f, 820f), new Vector2(60f, -170f));
            var rowsLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 14f;
            rowsLayout.childAlignment = TextAnchor.UpperLeft;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = false;
            rowsLayout.childForceExpandHeight = false;

            Slider masterSlider = NewSliderRow(rows, theme, "Master volume");
            Slider sfxSlider = NewSliderRow(rows, theme, "SFX volume");
            // Two toggles per row (HCI restructure added haptics, reduce motion and large text).
            var toggleRow1 = NewRow(rows, "ToggleRow1");
            Toggle hintsToggle = UIBuilder_Azm.NewToggle(toggleRow1, "HintsToggle", theme, "Show hints", new Vector2(570f, 120f));
            Toggle hapticsToggle = UIBuilder_Azm.NewToggle(toggleRow1, "HapticsToggle", theme, "Vibration", new Vector2(570f, 120f));
            var toggleRow2 = NewRow(rows, "ToggleRow2");
            Toggle reduceMotionToggle = UIBuilder_Azm.NewToggle(toggleRow2, "ReduceMotionToggle", theme, "Reduce motion", new Vector2(570f, 120f));
            Toggle largeTextToggle = UIBuilder_Azm.NewToggle(toggleRow2, "LargeTextToggle", theme, "Large text", new Vector2(570f, 120f));

            var qualityRow = NewRow(rows, "QualityRow");
            var qualityLabel = UIBuilder_Azm.NewText(qualityRow, "Label", theme, "Graphics quality", 40f);
            qualityLabel.alignment = TextAlignmentOptions.MidlineLeft;
            Fixed(qualityLabel, 440f, 120f);
            Button lowButton = UIBuilder_Azm.NewButton(qualityRow, "LowButton", theme, "Low", ThemeColor_Azm.Secondary, new Vector2(330f, 110f), 40f);
            Button highButton = UIBuilder_Azm.NewButton(qualityRow, "HighButton", theme, "High", ThemeColor_Azm.Secondary, new Vector2(330f, 110f), 40f);

            Button resetButton = UIBuilder_Azm.NewButton(rows, "ResetTutorialsButton", theme, "Reset tutorials", ThemeColor_Azm.Secondary, new Vector2(620f, 120f), 40f);

            var settingsUI = settingsPanel.gameObject.AddComponent<SettingsPanelUI_Azm>();
            settingsUI.masterSlider = masterSlider;
            settingsUI.sfxSlider = sfxSlider;
            settingsUI.hintsToggle = hintsToggle;
            settingsUI.hapticsToggle = hapticsToggle;
            settingsUI.reduceMotionToggle = reduceMotionToggle;
            settingsUI.largeTextToggle = largeTextToggle;
            settingsUI.lowQualityButton = lowButton;
            settingsUI.highQualityButton = highButton;
            settingsUI.resetTutorialsButton = resetButton;

            // ----- About -----
            var aboutPanel = MakePanel(safe, "AboutPanel", theme, ThemeColor_Azm.PanelStrong, SlideDirection_Azm.FromRight);
            UIBuilder_Azm.Pin(aboutPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1300f, 900f), Vector2.zero);
            Button aboutBack = NewBackButton(aboutPanel.transform, theme);
            NewPanelTitle(aboutPanel.transform, theme, "About");

            var aboutInfo = UIBuilder_Azm.NewText(aboutPanel.transform, "Info", theme, "", 36f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.TopLeft);
            UIBuilder_Azm.Pin(aboutInfo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(580f, 680f), new Vector2(60f, -180f));
            var creditsHeading = UIBuilder_Azm.NewText(aboutPanel.transform, "CreditsHeading", theme, "Asset credits", 40f, ThemeColor_Azm.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIBuilder_Azm.Pin(creditsHeading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(580f, 56f), new Vector2(680f, -180f));
            var aboutCredits = UIBuilder_Azm.NewText(aboutPanel.transform, "Credits", theme, "", 30f, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.TopLeft);
            UIBuilder_Azm.Pin(aboutCredits.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(580f, 620f), new Vector2(680f, -240f));

            var aboutUI = aboutPanel.gameObject.AddComponent<AboutPanelUI_Azm>();
            aboutUI.infoText = aboutInfo;
            aboutUI.creditsText = aboutCredits;
            aboutUI.credits = credits;

            // ----- Toast (hint/"coming soon" pill) -----
            var toastImage = UIBuilder_Azm.NewPanel(safe, "Toast", theme, ThemeColor_Azm.PanelStrong);
            UIBuilder_Azm.Pin(toastImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1000f, 110f), new Vector2(0f, 70f));
            toastImage.gameObject.AddComponent<CanvasGroup>();
            var toastLabel = UIBuilder_Azm.NewText(toastImage.transform, "Label", theme, "", 38f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center);
            UIBuilder_Azm.Stretch(toastLabel.rectTransform, 30f, 10f, 30f, 10f);
            var toast = toastImage.gameObject.AddComponent<ToastUI_Azm>();
            toast.label = toastLabel;

            // ----- Quit confirmation (full-screen scrim + dialog, above everything) -----
            var quitPanel = MakePanel(canvas.transform, "QuitConfirmPanel", theme, ThemeColor_Azm.Scrim, SlideDirection_Azm.None);
            quitPanel.GetComponent<ThemedImage_Azm>().rounded = false;
            quitPanel.GetComponent<ThemedImage_Azm>().Apply();
            quitPanel.GetComponent<Image>().type = Image.Type.Simple;
            quitPanel.GetComponent<Image>().sprite = null;
            UIBuilder_Azm.Stretch(quitPanel.GetComponent<RectTransform>());

            var dialog = UIBuilder_Azm.NewPanel(quitPanel.transform, "Dialog", theme, ThemeColor_Azm.PanelStrong);
            UIBuilder_Azm.Pin(dialog.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(900f, 440f), Vector2.zero);
            var dialogTitle = UIBuilder_Azm.NewText(dialog.transform, "Title", theme, "Quit the app?", 64f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIBuilder_Azm.Pin(dialogTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(800f, 120f), new Vector2(0f, -50f));
            var dialogBody = UIBuilder_Azm.NewText(dialog.transform, "Body", theme, "Your progress in a practical is not saved.", 36f, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.Center);
            UIBuilder_Azm.Pin(dialogBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(800f, 60f), new Vector2(0f, -170f));
            Button quitNo = UIBuilder_Azm.NewButton(dialog.transform, "StayButton", theme, "Stay", ThemeColor_Azm.Primary, new Vector2(380f, 120f), 44f);
            UIBuilder_Azm.Pin(quitNo.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(380f, 120f), new Vector2(-15f, 50f));
            Button quitYes = UIBuilder_Azm.NewButton(dialog.transform, "QuitConfirmButton", theme, "Quit", ThemeColor_Azm.Invalid, new Vector2(380f, 120f), 44f);
            UIBuilder_Azm.Pin(quitYes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(380f, 120f), new Vector2(15f, 50f));

            // ----- Controller -----
            var controllerGO = new GameObject("MenuController_Azm");
            var menu = controllerGO.AddComponent<MainMenuController_Azm>();
            menu.catalog = catalog;
            menu.theme = theme;
            menu.mainPanel = mainPanel;
            menu.practicalSelectPanel = selectPanel;
            menu.detailPanel = detailPanel;
            menu.settingsPanel = settingsPanel;
            menu.aboutPanel = aboutPanel;
            menu.quitConfirmPanel = quitPanel;
            menu.startButton = startButton;
            menu.settingsButton = settingsButton;
            menu.aboutButton = aboutButton;
            menu.quitButton = quitButton;
            menu.quitYesButton = quitYes;
            menu.quitNoButton = quitNo;
            menu.backButtons = new[] { selectBack, detailBack, settingsBack, aboutBack };
            menu.subjectTabs = subjectTabs;
            menu.cardContainer = cardContent;
            menu.cardPrefab = cardPrefab;
            menu.detailUI = detailUI;
            menu.toast = toast;
            menu.backInput = backInput;

            settingsUI.toast = toast;

            // HCI restructure: the menu gets the same button feel and sounds as the practicals.
            canvas.gameObject.AddComponent<AudioSource>();
            var sound = canvas.gameObject.AddComponent<UISound_Azm>();
            sound.bank = AssetDatabase.LoadAssetAtPath<SoundBank_Azm>(PracticalShellBuilder_Azm.SoundBankPath);
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                if (button.transition != Selectable.Transition.None && button.GetComponent<ButtonFeel_Azm>() == null)
                    button.gameObject.AddComponent<ButtonFeel_Azm>();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MainMenuScenePath);
        }

        private static RectTransform NewRow(Transform parent, string name)
        {
            var row = UIBuilder_Azm.NewRect(name, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var size = row.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = 120f;
            size.minHeight = 120f;
            return row;
        }

        private static Slider NewSliderRow(Transform parent, UITheme_Azm theme, string label)
        {
            var row = NewRow(parent, label.Replace(" ", "") + "Row");
            var text = UIBuilder_Azm.NewText(row, "Label", theme, label, 40f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            Fixed(text, 440f, 120f);
            return UIBuilder_Azm.NewSlider(row, "Slider", theme, new Vector2(700f, 120f));
        }

        // The decorative pendulum stand on the bench: base board, two posts, a top bar, thread and
        // a gold bob, all from primitives and three shared materials (cheap: 4 draw calls).
        private static Transform BuildMenuStand(Vector3 benchTop)
        {
            Material metal = GetMaterial("MenuStandMetal_Azm", new Color(0.12f, 0.13f, 0.15f), 0.7f, 0.5f);
            Material wood = GetMaterial("MenuStandBoard_Azm", new Color(0.72f, 0.52f, 0.33f), 0f, 0.3f);
            Material gold = GetMaterial("MenuBobGold_Azm", new Color(0.95f, 0.75f, 0.2f), 0.9f, 0.75f);
            Material thread = GetMaterial("MenuThread_Azm", new Color(0.92f, 0.92f, 0.86f), 0f, 0.2f);

            var root = new GameObject("MenuPendulumStand").transform;
            root.position = benchTop;

            Primitive(root, "Board", PrimitiveType.Cube, new Vector3(0f, 0.015f, 0f), new Vector3(0.56f, 0.03f, 0.30f), wood);
            Primitive(root, "PostLeft", PrimitiveType.Cube, new Vector3(-0.23f, 0.58f, 0f), new Vector3(0.028f, 1.1f, 0.028f), metal);
            Primitive(root, "PostRight", PrimitiveType.Cube, new Vector3(0.23f, 0.58f, 0f), new Vector3(0.028f, 1.1f, 0.028f), metal);
            Primitive(root, "TopBar", PrimitiveType.Cube, new Vector3(0f, 1.13f, 0f), new Vector3(0.49f, 0.028f, 0.028f), metal);

            // The pivot sits on the top bar; the thread and bob hang from it.
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0f, 1.115f, 0f);
            Primitive(pivot, "Thread", PrimitiveType.Cylinder, new Vector3(0f, -0.36f, 0f), new Vector3(0.006f, 0.36f, 0.006f), thread);
            Primitive(pivot, "Bob", PrimitiveType.Sphere, new Vector3(0f, -0.755f, 0f), new Vector3(0.07f, 0.07f, 0.07f), gold);

            var sway = root.gameObject.AddComponent<MenuPendulumSway_Azm>();
            sway.pivot = pivot;
            return root;
        }

        private static void Primitive(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());   // decorative only
            go.isStatic = false;
        }

        private static Material GetMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureFolder(MaterialFolder);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // The card used by the practical-select grid, saved as a prefab so the menu can spawn one
        // per catalog entry.
        private static PracticalCardUI_Azm BuildCardPrefab(UITheme_Azm theme)
        {
            string path = PrefabFolder + "/PracticalCard_Azm.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<PracticalCardUI_Azm>();

            var rootImage = UIBuilder_Azm.NewPanel(null, "PracticalCard_Azm", theme, ThemeColor_Azm.Secondary);
            var root = rootImage.rectTransform;
            root.sizeDelta = new Vector2(490f, 480f);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = rootImage;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            button.colors = colors;

            // Everything that fades when the card is locked/unavailable.
            var content = NewGroup(root, "Content");

            var thumbnail = UIBuilder_Azm.NewPanel(content, "Thumbnail", theme, ThemeColor_Azm.PanelStrong);
            UIBuilder_Azm.Pin(thumbnail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(450f, 220f), new Vector2(0f, -20f));

            var nameText = UIBuilder_Azm.NewText(content, "Name", theme, "Practical name", 40f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIBuilder_Azm.Pin(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(450f, 60f), new Vector2(0f, -256f));

            var descriptionText = UIBuilder_Azm.NewText(content, "Description", theme, "Description", 28f, ThemeColor_Azm.TextSecondary, TextAlignmentOptions.TopLeft);
            UIBuilder_Azm.Pin(descriptionText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(450f, 100f), new Vector2(0f, -320f));

            var timeText = UIBuilder_Azm.NewText(content, "Time", theme, "", 28f, ThemeColor_Azm.Accent, TextAlignmentOptions.MidlineLeft);
            UIBuilder_Azm.Pin(timeText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(220f, 44f), new Vector2(-225f, 16f));

            // Outside the faded group so the padlock and label stay fully readable.
            var lockImage = UIBuilder_Azm.NewRect("LockIcon", root).gameObject.AddComponent<Image>();
            lockImage.raycastTarget = false;
            UIBuilder_Azm.Pin(lockImage.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(72f, 72f), new Vector2(-34f, -34f));

            var stateLabel = UIBuilder_Azm.NewText(root, "StateLabel", theme, "Coming soon", 30f, ThemeColor_Azm.Accent, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UIBuilder_Azm.Pin(stateLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(260f, 44f), new Vector2(-24f, 16f));

            var card = root.gameObject.AddComponent<PracticalCardUI_Azm>();
            card.button = button;
            card.thumbnail = thumbnail;
            card.nameText = nameText;
            card.descriptionText = descriptionText;
            card.timeText = timeText;
            card.stateLabel = stateLabel;
            card.lockIcon = lockImage;
            card.contentGroup = content.GetComponent<CanvasGroup>();

            EnsureFolder(PrefabFolder);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return saved.GetComponent<PracticalCardUI_Azm>();
        }

        // ---------- SimplePendulum_Azm (empty lab + placeholder screen until Stage 2) ----------

        private static void BuildPendulumScene(UITheme_Azm theme, PracticalCatalog_Azm catalog, GameObject labPrefab)
        {
            NewEmptyScene();

            var camera = CreateCamera(Color.black, true);
            CreateLight();
            BackButtonInput_Azm backInput = CreateBootstrap(theme, catalog);
            CreateEventSystem();

            var lab = (GameObject)PrefabUtility.InstantiatePrefab(labPrefab);
            lab.transform.position = Vector3.zero;

            // Static placeholder view of the island bench; Stage 2 replaces this with the camera-view system.
            camera.transform.position = new Vector3(0.23f, 1.8f, -3.2f);
            camera.transform.LookAt(new Vector3(0.23f, 1.2f, -0.15f));

            Canvas canvas = CreateCanvas("Canvas", out RectTransform safe);

            var card = UIBuilder_Azm.NewPanel(safe, "PlaceholderPanel", theme, ThemeColor_Azm.Panel);
            UIBuilder_Azm.Pin(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1100f, 420f), Vector2.zero);
            var info = UIBuilder_Azm.NewText(card.transform, "Info", theme, "", 54f, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center);
            UIBuilder_Azm.Stretch(info.rectTransform, 40f, 40f, 40f, 40f);

            Button exit = UIBuilder_Azm.NewButton(safe, "ExitButton", theme, "Exit to menu", ThemeColor_Azm.Secondary, new Vector2(460f, 120f), 44f);
            UIBuilder_Azm.Pin(exit.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(460f, 120f), new Vector2(60f, 60f));

            var controllerGO = new GameObject("PendulumController_Stub");
            var stub = controllerGO.AddComponent<PendulumSceneStub_Azm>();
            stub.infoText = info;
            stub.exitButton = exit;
            stub.backInput = backInput;

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), PendulumScenePath);
        }
    }
}
