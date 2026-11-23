using UnityEngine;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: Keeps a control centred in the VISIBLE area when the right-hand drawer is open: as the
     * drawer slides in, this RectTransform slides left by half the drawer's width, and slides back
     * as it closes. The bottom action area and the hint toast use it, so the drawer never covers
     * them (task spec 2.4).
     * Talks to: reads DrawerUI_Azm.OpenAmount; writes this RectTransform's anchoredPosition.x.
     */
    [RequireComponent(typeof(RectTransform))]
    public class RectShift_Azm : MonoBehaviour
    {
        public DrawerUI_Azm drawer;
        [Tooltip("How far left (canvas units) the control moves when the drawer is fully open. Normally half the drawer width.")]
        public float shiftWhenOpen = -380f;

        private RectTransform rect;
        private float baseX;
        private bool captured;

        private void Awake()
        {
            rect = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            if (drawer == null) return;
            if (!captured) { baseX = rect.anchoredPosition.x; captured = true; }
            var p = rect.anchoredPosition;
            p.x = baseX + shiftWhenOpen * drawer.OpenAmount;
            rect.anchoredPosition = p;
        }
    }
}
