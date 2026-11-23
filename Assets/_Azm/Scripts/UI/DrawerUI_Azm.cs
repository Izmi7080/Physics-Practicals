using System;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: The right-hand drawer with tabs (Data / Graph / Calc / Guide). Open, it is a panel
     * with a tab bar; closed, it collapses to a slim vertical strip of icons on the right edge. It
     * slides open and shut (about 0.25 s). It exposes OpenAmount (0..1) so other controls (the
     * bottom action area, the toast, the camera framing) can make room for it.
     * The drawer only knows its tabs and panes - it holds no practical logic.
     * Talks to: fires OpenChanged / TabChanged; driven by the HUD (open a tab for a step) and by
     * the student's taps on the strip icons, tab buttons and close button.
     */
    public class DrawerUI_Azm : MonoBehaviour
    {
        public RectTransform panel;
        public float panelWidth = 780f;
        public RectTransform strip;
        public float stripWidth = 120f;
        public CanvasGroup stripGroup;

        public Button[] tabButtons;      // tab bar inside the panel
        public Button[] stripButtons;    // icons on the collapsed strip (same order as the tabs)
        public GameObject[] panes;       // one content pane per tab
        public Button closeButton;
        public float slideSeconds = 0.26f;

        public event Action<bool> OpenChanged;
        public event Action<int> TabChanged;

        public bool IsOpen { get; private set; }
        public int CurrentTab { get; private set; }
        // 0 = closed, 1 = fully open (eased).
        public float OpenAmount { get; private set; }

        private float linearAmount;

        private void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => Open(index));
            }
            for (int i = 0; i < stripButtons.Length; i++)
            {
                int index = i;
                stripButtons[i].onClick.AddListener(() => Open(index));
            }
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            ShowTab(0);
            ApplyPositions(0f);
        }

        public void Open(int tab)
        {
            ShowTab(tab);
            if (IsOpen) return;
            IsOpen = true;
            OpenChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            OpenChanged?.Invoke(false);
        }

        private void ShowTab(int tab)
        {
            CurrentTab = Mathf.Clamp(tab, 0, panes.Length - 1);
            for (int i = 0; i < panes.Length; i++)
                panes[i].SetActive(i == CurrentTab);

            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool selected = i == CurrentTab;
                var image = tabButtons[i].GetComponent<ThemedImage_Azm>();
                var label = tabButtons[i].GetComponentInChildren<ThemedText_Azm>();
                if (image != null) image.SetRole(selected ? ThemeColor_Azm.Primary : ThemeColor_Azm.Secondary);
                if (label != null) label.SetRole(selected ? ThemeColor_Azm.OnPrimary : ThemeColor_Azm.TextPrimary);
            }
            TabChanged?.Invoke(CurrentTab);
        }

        private void Update()
        {
            float target = IsOpen ? 1f : 0f;
            if (!Mathf.Approximately(linearAmount, target))
            {
                linearAmount = Mathf.MoveTowards(linearAmount, target, Time.unscaledDeltaTime / slideSeconds);
                ApplyPositions(linearAmount);
            }
        }

        private void ApplyPositions(float linear)
        {
            linearAmount = linear;
            OpenAmount = Mathf.SmoothStep(0f, 1f, linear);

            // Panel: fully off-screen to the right when closed, flush with the edge when open.
            var p = panel.anchoredPosition;
            p.x = (1f - OpenAmount) * (panelWidth + 20f);
            panel.anchoredPosition = p;

            // Strip: visible when closed, slides away as the panel arrives.
            var s = strip.anchoredPosition;
            s.x = OpenAmount * (stripWidth + 20f);
            strip.anchoredPosition = s;
            if (stripGroup != null)
            {
                stripGroup.alpha = 1f - OpenAmount;
                stripGroup.blocksRaycasts = OpenAmount < 0.5f;
            }
            panel.gameObject.SetActive(OpenAmount > 0.001f);
        }
    }
}
