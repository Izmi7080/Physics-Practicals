using UnityEngine;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (layout helper).
     * Purpose: Keeps a centred element (the toast) as wide as its parent allows but never wider than
     * maxWidth: on a 21:9 phone a full-width message line would be too long to read comfortably;
     * on a narrow layout it must not stick out of its column. Runs in LateUpdate, so it follows the
     * left column as panels open and close.
     * Talks to: its own RectTransform and its parent's size.
     */
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class ClampWidth_Azm : MonoBehaviour
    {
        public float maxWidth = 1100f;
        [Tooltip("Space kept free on each side inside the parent.")]
        public float sideMargin = 16f;

        private void LateUpdate()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            float width = Mathf.Min(maxWidth, parent.rect.width - 2f * sideMargin);
            if (!Mathf.Approximately(rect.sizeDelta.x, width))
                rect.sizeDelta = new Vector2(Mathf.Max(100f, width), rect.sizeDelta.y);
        }
    }
}
