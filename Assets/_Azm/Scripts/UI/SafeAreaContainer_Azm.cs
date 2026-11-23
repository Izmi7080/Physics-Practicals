using UnityEngine;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: Keeps a full-stretch container inside Screen.safeArea (notches, punch-hole
     * cameras, rounded corners) - CLAUDE.md requires all interactive UI to sit inside one. Put
     * this on a RectTransform child of the Canvas and parent the interactive UI under it.
     * Re-applies whenever the safe area or screen size changes (e.g. rotating left<->right).
     * Defensive: the Unity Editor's Game view sometimes reports a safe area that does not match
     * the current Game view size (stale after a resize). An impossible rectangle (bigger than the
     * screen, or off-screen) is ignored and the full screen is used instead, so the UI can never
     * be pushed off-screen by a bad value.
     * Talks to: reads Screen.safeArea; writes this RectTransform's anchors.
     */
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeAreaContainer_Azm : MonoBehaviour
    {
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
                Apply();
        }

        private void Apply()
        {
            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = lastSafeArea;
            bool valid = safe.width > 0f && safe.height > 0f
                         && safe.xMin >= -1f && safe.yMin >= -1f
                         && safe.xMax <= Screen.width + 1f && safe.yMax <= Screen.height + 1f;
            if (!valid) safe = new Rect(0f, 0f, Screen.width, Screen.height);

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(Mathf.Clamp01(safe.xMin / Screen.width), Mathf.Clamp01(safe.yMin / Screen.height));
            rect.anchorMax = new Vector2(Mathf.Clamp01(safe.xMax / Screen.width), Mathf.Clamp01(safe.yMax / Screen.height));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
