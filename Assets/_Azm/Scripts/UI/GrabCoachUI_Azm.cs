using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (onboarding; HCI restructure Stage 5). No practical logic.
     * Purpose: Teaches the drag gestures on the apparatus without words:
     *   - a pulsing teal RING over a grab area (caliper jaw, screw gauge thimble) whenever it can be
     *     dragged, until the student has grabbed it once this session (the pendulum bob has its own
     *     world ring, so it gets no second one);
     *   - a GHOST HAND that shows the gesture (press, drag along the hint motion, lift) the first
     *     time each gesture is needed. It loops until the student does it (or 4 loops), then never
     *     shows again (tutorial flag "coach.<id>"; Settings > Reset tutorials brings it back).
     * WHY: in the baseline evaluation students tapped the jaw instead of dragging it, and nothing on
     * screen said "this moves". A demonstrated gesture is faster to understand than a sentence.
     * Nothing shows while the briefing, the HUD tour or the pause menu is open (hud.OnboardingBusy).
     * With Reduce motion the ring does not pulse and the hand stands still at the start point.
     * The ring and hand follow the grab point every frame (screen projection of a world point), so
     * they stay on the apparatus while the camera glides or is orbited.
     * Talks to: IGrabbable_Azm (the drag interactions, read-only), PracticalHUDBase_Azm
     * (OnboardingBusy), SettingsManager_Azm (tutorial flags), the view camera.
     */
    public class GrabCoachUI_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public PracticalHUDBase_Azm hud;
        public Camera viewCamera;
        [Tooltip("Components implementing IGrabbable_Azm, in priority order.")]
        public MonoBehaviour[] grabbables = new MonoBehaviour[0];
        public Image ring;
        public Image hand;
        public CanvasGroup handGroup;
        [Tooltip("Seconds for one demonstrated drag (press, move, lift).")]
        public float handSeconds = 1.4f;
        [Tooltip("Pause between two demonstrations.")]
        public float handPause = 0.7f;
        [Tooltip("ASSUMPTION (feel): after this many loops the student has seen it; stop nagging.")]
        public int maxLoops = 4;

        private IGrabbable_Azm[] items = new IGrabbable_Azm[0];
        private readonly HashSet<string> grabbedThisSession = new HashSet<string>();
        private RectTransform root;
        private string handId;
        private float handTime;
        private Texture2D ringTexture;

        private const string TutorialPrefix = "coach.";

        private void Awake()
        {
            root = (RectTransform)transform;
            var list = new List<IGrabbable_Azm>();
            foreach (var g in grabbables) if (g is IGrabbable_Azm grab) list.Add(grab);
            items = list.ToArray();
            ring.sprite = MakeRingSprite();
            ring.color = theme != null ? theme.GetColor(ThemeColor_Azm.Primary) : Color.cyan;
            ring.raycastTarget = false;
            hand.raycastTarget = false;
            ring.gameObject.SetActive(false);
            hand.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (ringTexture != null) Destroy(ringTexture);
        }

        private void Update()
        {
            // Remember every gesture the student has performed (any grabbable, any time).
            foreach (var item in items)
            {
                if (!item.IsDragging) continue;
                string id = item.GrabHintId;
                if (grabbedThisSession.Add(id)) MarkSeen(id);
                if (id == handId) StopHand();
            }

            IGrabbable_Azm active = null;
            foreach (var item in items) if (item.GrabAvailable) { active = item; break; }

            bool busy = hud != null && hud.OnboardingBusy;
            Vector2 start = Vector2.zero, end = Vector2.zero;
            bool onScreen = active != null && ToCanvas(active.GrabPoint, out start) && ToCanvas(active.GrabPoint + active.GrabHintMotion, out end);
            bool calm = active == null || busy || !onScreen || active.IsDragging;
            string activeId = active != null ? active.GrabHintId : null;

            // ----- ring -----
            bool showRing = !calm && !active.HasOwnAffordance && !grabbedThisSession.Contains(activeId);
            if (ring.gameObject.activeSelf != showRing) ring.gameObject.SetActive(showRing);
            if (showRing)
            {
                ring.rectTransform.anchoredPosition = start;
                bool still = Motion_Azm.ReduceMotion;
                float s = still ? 1f : 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 4f);
                ring.rectTransform.localScale = new Vector3(s, s, 1f);
                var c = ring.color;
                c.a = still ? 0.8f : 0.6f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
                ring.color = c;
            }

            // ----- ghost hand -----
            bool showHand = !calm && !grabbedThisSession.Contains(activeId) && !HasSeen(activeId);
            if (!showHand) { if (hand.gameObject.activeSelf) hand.gameObject.SetActive(false); return; }
            if (handId != activeId) { handId = activeId; handTime = 0f; }
            if (!hand.gameObject.activeSelf) hand.gameObject.SetActive(true);
            AnimateHand(start, end);
        }

        // One demonstration: fade in and "press" at the grab point, move along the hint, lift.
        private void AnimateHand(Vector2 start, Vector2 end)
        {
            var rect = hand.rectTransform;
            if (Motion_Azm.ReduceMotion)
            {
                rect.anchoredPosition = start;
                rect.localScale = Vector3.one;
                handGroup.alpha = 0.9f;
                return;
            }

            handTime += Time.unscaledDeltaTime;
            float cycle = handSeconds + handPause;
            if (handTime >= cycle * maxLoops) { MarkSeen(handId); StopHand(); return; }
            float t = (handTime % cycle) / handSeconds;          // 0..1 during the gesture, >1 in the pause

            float press = Mathf.Clamp01(t / 0.18f);                 // 0 - 0.18: appear and press
            float move = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.85f, t));
            float lift = Mathf.InverseLerp(0.85f, 1f, t);           // 0.85 - 1: lift and fade
            rect.anchoredPosition = Vector2.Lerp(start, end, move);
            rect.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.95f, press) * Mathf.Lerp(1f, 1.1f, lift);
            handGroup.alpha = t > 1f ? 0f : Mathf.Min(press, 1f - lift) * 0.95f;
        }

        private void StopHand()
        {
            handId = null;
            handTime = 0f;
            if (hand.gameObject.activeSelf) hand.gameObject.SetActive(false);
        }

        private static bool HasSeen(string id)
        {
            var s = SettingsManager_Azm.Instance;
            return s != null && s.HasSeenTutorial(TutorialPrefix + id);
        }

        private static void MarkSeen(string id)
        {
            var s = SettingsManager_Azm.Instance;
            if (s != null && id != null) s.MarkTutorialSeen(TutorialPrefix + id);
        }

        // World point -> this rect's local space (the safe area, pivot at its centre). False when the
        // point is behind the camera.
        private bool ToCanvas(Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;
            if (viewCamera == null) return false;
            Vector3 screen = viewCamera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out local);
        }

        // A soft-edged ring (no sprite asset needed): alpha 1 between 78 % and 92 % of the radius.
        private Sprite MakeRingSprite()
        {
            const int size = 128;
            ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01((r - 0.74f) / 0.04f) * Mathf.Clamp01((0.96f - r) / 0.04f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            ringTexture.SetPixels32(pixels);
            ringTexture.Apply();
            return Sprite.Create(ringTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
