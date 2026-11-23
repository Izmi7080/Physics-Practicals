using UnityEngine;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (layout).
     * Purpose: Keeps the practical screen's regions from overlapping at ANY aspect ratio (16:9,
     * 20:9, 21:9 phones, 4:3 tablets). The baseline evaluation found panels drawn at fixed pixel
     * positions sliding under each other (findings 3, 4, 14). The screen is now split into anchored
     * regions that resize with the screen:
     *   - RIGHT: the drawer strip (always), the keypad / least-count panel (during measuring steps)
     *     or the open drawer panel - each anchored to the right edge with a fixed width;
     *   - LEFT COLUMN: everything else (magnifier, bottom row of buttons, toasts, calculation
     *     buttons). Its right edge is moved here so it always ends where the right-hand content
     *     begins: max(strip, keypad when shown, drawer when open), smoothly animated;
     *   - SCENE WINDOW: the part of the left column between the top bar and the magnifier (or the
     *     bottom row). The camera rig keeps the apparatus centred in this window, so the instrument
     *     is never hidden behind a panel.
     * Widths/heights are in canvas units; the canvas uses CanvasScaler "Expand" at 1920x1080, so
     * the screen is never narrower than 1920 or shorter than 1080 units and these numbers fit.
     * Talks to: DrawerUI_Azm (OpenAmount); the HUD calls SetRightPanel / SetMagnifier; read by
     * PracticalCameraRig_Azm (SceneWindow).
     */
    public class ScreenRegions_Azm : MonoBehaviour
    {
        public RectTransform leftColumn;
        public RectTransform sceneWindow;
        public DrawerUI_Azm drawer;
        [Tooltip("While its Why?/Hint detail is open, the scene window starts below it.")]
        public InstructionCardUI_Azm instructionCard;

        [Header("Reserved widths from the right edge (canvas units)")]
        [Tooltip("Collapsed drawer strip + margins.")]
        public float stripReserve = 168f;
        [Tooltip("Keypad / least-count panel + strip + margins.")]
        public float rightPanelReserve = 836f;
        [Tooltip("Open drawer panel + margin.")]
        public float drawerReserve = 768f;

        [Header("Scene window (inside the left column)")]
        [Tooltip("Top bar height + margins.")]
        public float topReserve = 172f;
        [Tooltip("Bottom row (view buttons) height + margins.")]
        public float bottomRowReserve = 196f;
        [Tooltip("Bottom row + magnifier + gaps.")]
        public float magnifierReserve = 520f;

        [Tooltip("Seconds to settle when a panel appears or disappears.")]
        public float smoothSeconds = 0.18f;

        public bool RightPanelVisible { get; private set; }
        public bool MagnifierVisible { get; private set; }
        // Extra space a practical needs at the bottom of the left column (e.g. the pendulum's big
        // round tap button and stopwatch while timing); the scene window ends above it.
        public float ExtraBottomReserve { get; set; }
        public RectTransform SceneWindow => sceneWindow;

        private float right, rightVel, bottom, bottomVel, top, topVel;
        private bool initialised;

        public void SetRightPanel(bool visible) => RightPanelVisible = visible;
        public void SetMagnifier(bool visible) => MagnifierVisible = visible;

        private float TargetRight()
        {
            float r = stripReserve;
            if (RightPanelVisible) r = Mathf.Max(r, rightPanelReserve);
            if (drawer != null) r = Mathf.Max(r, Mathf.Lerp(stripReserve, drawerReserve, drawer.OpenAmount));
            return r;
        }

        private float TargetBottom() => Mathf.Max(MagnifierVisible ? magnifierReserve : bottomRowReserve, ExtraBottomReserve);

        private float TargetTop() => topReserve + (instructionCard != null ? instructionCard.VisibleDetailHeight : 0f);

        private void LateUpdate()
        {
            float tr = TargetRight(), tb = TargetBottom(), tt = TargetTop();
            if (!initialised)
            {
                right = tr; bottom = tb; top = tt; initialised = true;
            }
            else
            {
                right = Mathf.SmoothDamp(right, tr, ref rightVel, smoothSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
                bottom = Mathf.SmoothDamp(bottom, tb, ref bottomVel, smoothSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
                top = Mathf.SmoothDamp(top, tt, ref topVel, smoothSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
            }

            if (leftColumn != null)
            {
                var max = leftColumn.offsetMax;
                leftColumn.offsetMax = new Vector2(-right, max.y);
            }
            if (sceneWindow != null)
            {
                sceneWindow.offsetMin = new Vector2(sceneWindow.offsetMin.x, bottom);
                sceneWindow.offsetMax = new Vector2(sceneWindow.offsetMax.x, -top);
            }
        }

        // The scene window in screen pixels (for the camera rig).
        public Rect SceneWindowScreenRect()
        {
            if (sceneWindow == null) return new Rect(0, 0, Screen.width, Screen.height);
            var corners = new Vector3[4];
            sceneWindow.GetWorldCorners(corners);
            // Screen Space - Overlay: world corners are screen pixels.
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
