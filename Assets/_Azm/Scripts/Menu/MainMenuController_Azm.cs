using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Menu
{
    /*
     * Layer: UI (menu flow).
     * Purpose: Runs the main-menu scene: main panel -> practical select -> detail/mode panel, plus
     * Settings, About and the Quit confirmation. Panels are kept on a small navigation stack so the
     * Android back button (Escape) goes back one panel, and on the main panel opens the Quit
     * confirmation instead of quitting.
     *
     * DATA-DRIVEN: the practical cards are built at runtime from PracticalCatalog_Azm. Adding a
     * teammate's practical later must only require editing the catalog asset and adding their scene
     * to Build Settings - NO menu code changes. This script never names a specific practical. If a
     * card's scene is not in Build Settings the card shows as unavailable and a warning is logged;
     * it never crashes.
     *
     * Locked items (and whole subjects that only hold locked placeholders) are SHOWN, never hidden;
     * tapping one shows a small "coming soon" toast.
     * Talks to: PracticalCatalog_Azm (data), AppManager_Azm (scene info/quit), PracticalCardUI_Azm,
     * PracticalDetailPanelUI_Azm, ToastUI_Azm, BackButtonInput_Azm.
     */
    public class MainMenuController_Azm : MonoBehaviour
    {
        [Header("Data")]
        public PracticalCatalog_Azm catalog;
        public UITheme_Azm theme;

        [Header("Panels")]
        public PanelTransition_Azm mainPanel;
        public PanelTransition_Azm practicalSelectPanel;
        public PanelTransition_Azm detailPanel;
        public PanelTransition_Azm settingsPanel;
        public PanelTransition_Azm aboutPanel;
        public PanelTransition_Azm quitConfirmPanel;

        [Header("Main panel buttons")]
        public Button startButton;
        public Button settingsButton;
        public Button aboutButton;
        public Button quitButton;

        [Header("Quit dialog")]
        public Button quitYesButton;
        public Button quitNoButton;

        [Tooltip("Every 'back' button in the sub-panels.")]
        public Button[] backButtons;

        [Header("Practical select")]
        [Tooltip("In order: Physics, Chemistry, Biology.")]
        public Button[] subjectTabs;
        public Transform cardContainer;
        public PracticalCardUI_Azm cardPrefab;

        [Header("Shared")]
        public PracticalDetailPanelUI_Azm detailUI;
        public ToastUI_Azm toast;
        public BackButtonInput_Azm backInput;

        private readonly List<PanelTransition_Azm> stack = new List<PanelTransition_Azm>();
        private bool quitOpen;

        private void Awake()
        {
            startButton.onClick.AddListener(OnStart);
            settingsButton.onClick.AddListener(() => Open(settingsPanel));
            aboutButton.onClick.AddListener(() => Open(aboutPanel));
            quitButton.onClick.AddListener(OpenQuit);
            quitYesButton.onClick.AddListener(() => AppManager_Azm.Instance?.Quit());
            quitNoButton.onClick.AddListener(CloseQuit);

            foreach (var back in backButtons)
                back.onClick.AddListener(Back);

            for (int i = 0; i < subjectTabs.Length; i++)
            {
                int index = i; // capture for the lambda
                subjectTabs[i].onClick.AddListener(() => SelectSubject((SubjectType_Azm)index));
            }
        }

        private void OnEnable()
        {
            if (backInput != null) backInput.BackPressed += Back;
        }

        private void OnDisable()
        {
            if (backInput != null) backInput.BackPressed -= Back;
        }

        private void Start()
        {
            var app = AppManager_Azm.Instance;
            if (catalog == null && app != null) catalog = app.Catalog;
            if (theme == null && app != null) theme = app.Theme;

            detailUI.Initialize(theme, toast);

            // Start with only the main panel visible.
            foreach (var panel in new[] { practicalSelectPanel, detailPanel, settingsPanel, aboutPanel, quitConfirmPanel })
                panel.Hide(false);
            mainPanel.Show(false);
            stack.Clear();
            stack.Add(mainPanel);
            quitOpen = false;   // the quit dialog was just hidden above, so the flag must agree
        }

        // --- Navigation ---

        private void OnStart()
        {
            SelectSubject(SubjectType_Azm.Physics);
            Open(practicalSelectPanel);
        }

        private void Open(PanelTransition_Azm panel)
        {
            if (quitOpen || IsBusy()) return;
            stack[stack.Count - 1].Hide();
            panel.Show();
            stack.Add(panel);
        }

        // Android back button / on-screen back arrows.
        public void Back()
        {
            if (IsBusy()) return;

            if (quitOpen)
            {
                CloseQuit();
                return;
            }

            if (stack.Count > 1)
            {
                var top = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                top.Hide();
                stack[stack.Count - 1].Show();
            }
            else
            {
                // On the main panel back must never quit straight away (CLAUDE.md).
                OpenQuit();
            }
        }

        private void OpenQuit()
        {
            if (quitOpen) return;
            quitOpen = true;
            quitConfirmPanel.Show();
        }

        private void CloseQuit()
        {
            quitOpen = false;
            quitConfirmPanel.Hide();
        }

        private bool IsBusy() => AppManager_Azm.Instance != null && AppManager_Azm.Instance.IsLoading;

        // --- Practical select ---

        private void SelectSubject(SubjectType_Azm subject)
        {
            for (int i = 0; i < subjectTabs.Length; i++)
            {
                bool selected = i == (int)subject;
                var image = subjectTabs[i].GetComponent<ThemedImage_Azm>();
                var label = subjectTabs[i].GetComponentInChildren<ThemedText_Azm>();
                if (image != null) image.SetRole(selected ? ThemeColor_Azm.Primary : ThemeColor_Azm.Secondary);
                if (label != null) label.SetRole(selected ? ThemeColor_Azm.OnPrimary : ThemeColor_Azm.TextPrimary);
            }

            RebuildCards(subject);
        }

        private void RebuildCards(SubjectType_Azm subject)
        {
            // Only the cards this script spawned are cleared.
            for (int i = cardContainer.childCount - 1; i >= 0; i--)
                Destroy(cardContainer.GetChild(i).gameObject);

            if (catalog == null)
            {
                Debug.LogWarning("[MainMenuController_Azm] No PracticalCatalog_Azm assigned - practical select will be empty.");
                return;
            }

            var app = AppManager_Azm.Instance;
            int index = 0;
            foreach (var info in catalog.GetForSubject(subject))
            {
                bool sceneOk = app == null || app.IsSceneInBuild(info.sceneName);
                if (!info.isLocked && !sceneOk)
                    Debug.LogWarning($"[MainMenuController_Azm] Scene '{info.sceneName}' for '{info.displayName}' is not in Build Settings - showing the card as unavailable.");

                var card = Instantiate(cardPrefab, cardContainer);
                card.gameObject.SetActive(true);
                card.Bind(info, theme, sceneOk, OnCardClicked);
                FloatIn(card.transform, index++);
            }
        }

        // Juice (HCI restructure Stage 6): the cards float in one after another (60 ms apart)
        // instead of all appearing at once - the list reads as "a set of choices for you".
        private static void FloatIn(Transform card, int index)
        {
            var group = card.GetComponent<CanvasGroup>();
            if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
            if (Motion_Azm.ReduceMotion) { group.alpha = 1f; return; }
            group.alpha = 0f;
            card.localScale = Vector3.one * 0.9f;
            Motion_Azm.Delay(card, 0.06f * index, () =>
            {
                if (card == null) return;
                Motion_Azm.FadeIn(group, Motion_Azm.Normal);
                Motion_Azm.PopIn(card, 0.9f);
            });
        }

        private void OnCardClicked(PracticalCardUI_Azm card)
        {
            switch (card.State)
            {
                case CardState_Azm.Locked:
                    toast.Show("Coming soon");
                    break;
                case CardState_Azm.Unavailable:
                    toast.Show("This practical isn't installed in this build yet.");
                    break;
                default:
                    detailUI.Show(card.Info);
                    Open(detailPanel);
                    break;
            }
        }
    }
}
