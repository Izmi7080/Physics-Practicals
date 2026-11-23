using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Core (presentation helper owned by AppManager_Azm).
     * Purpose: A full-screen black overlay that fades in/out over scene loads, with a small
     * animated "Loading" label. Built entirely in code so it needs no prefab and exists the moment
     * AppManager_Azm does. It is a Screen Space - Overlay canvas on purpose (CLAUDE.md: the
     * working UI is screen space) sitting above everything else (sorting order 1000). It also
     * blocks taps while visible so a student cannot press a button mid-transition.
     * All timing uses unscaled time so fades still run if a scene has paused the simulation.
     * Talks to: created and driven by AppManager_Azm only.
     */
    public class ScreenFader_Azm : MonoBehaviour
    {
        private CanvasGroup group;
        private TextMeshProUGUI loadingLabel;
        private bool loadingVisible;

        public static ScreenFader_Azm Create(Transform parent)
        {
            var root = new GameObject("ScreenFader_Azm", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            // Same scaling rule as every other canvas in the project (CLAUDE.md UI rules).
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // HCI restructure: never smaller than 1920x1080

            var fader = root.AddComponent<ScreenFader_Azm>();
            fader.group = root.GetComponent<CanvasGroup>();

            var blackGO = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            blackGO.transform.SetParent(root.transform, false);
            var blackRect = blackGO.GetComponent<RectTransform>();
            blackRect.anchorMin = Vector2.zero;
            blackRect.anchorMax = Vector2.one;
            blackRect.offsetMin = Vector2.zero;
            blackRect.offsetMax = Vector2.zero;
            blackGO.GetComponent<Image>().color = Color.black;

            var labelGO = new GameObject("LoadingLabel", typeof(RectTransform));
            labelGO.transform.SetParent(root.transform, false);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 90f);
            labelRect.sizeDelta = new Vector2(600f, 60f);
            fader.loadingLabel = labelGO.AddComponent<TextMeshProUGUI>();
            fader.loadingLabel.alignment = TextAlignmentOptions.Center;
            fader.loadingLabel.fontSize = 36;
            fader.loadingLabel.color = new Color(1f, 1f, 1f, 0.8f);
            fader.loadingLabel.raycastTarget = false;
            fader.loadingLabel.text = string.Empty;

            fader.SetAlpha(0f);
            return fader;
        }

        public void SetAlpha(float alpha)
        {
            group.alpha = alpha;
            // Only intercept taps while the overlay is actually visible.
            group.blocksRaycasts = alpha > 0.01f;
        }

        public IEnumerator FadeTo(float targetAlpha, float seconds)
        {
            float start = group.alpha;
            float t = 0f;
            group.blocksRaycasts = true;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            SetAlpha(targetAlpha);
        }

        public void SetLoadingVisible(bool visible)
        {
            loadingVisible = visible;
            if (!visible) loadingLabel.text = string.Empty;
        }

        private void Update()
        {
            if (!loadingVisible) return;
            // Cheap animated dots: "Loading", "Loading.", "Loading..", "Loading..."
            int dots = (int)(Time.unscaledTime * 3f) % 4;
            loadingLabel.text = "Loading" + new string('.', dots);
        }
    }
}
