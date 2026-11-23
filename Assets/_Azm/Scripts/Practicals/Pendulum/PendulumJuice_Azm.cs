using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI / presentation (feel only; HCI restructure Stage 6). No experiment logic.
     * Purpose: The pendulum's physical feel:
     *   - picking the bob up: a light click; while dragging it, a soft tick per whole centimetre of
     *     length or per whole degree of pull (like the detents of a real clamp/protractor);
     *   - letting go: a "snap" (tock + vibration) as the swing starts;
     *   - LEARN MODE ONLY: a soft glow at the bob's mean (centre) position that brightens as the bob
     *     passes through it, during the timing runs. WHY: the hardest skill in this practical is
     *     tapping exactly as the bob passes the centre; the glow trains the eye on the right spot.
     *     Perform mode has no glow, as in the exam.
     * (The tap ripple, count pop, 20th-tap burst, graph drop and g count-up live in the HUD,
     * GraphView_Azm and ResultsPanelUI_Azm.)
     * THE JUICE RULE: the ticks follow the student's hand only; the glow marks a place the student
     * should already be watching. Nothing here shows a time, a length or g.
     * Talks to: PendulumController_Azm (events, read-only state), BobDragInteraction_Azm,
     * PendulumRigView_Azm (anchor), the view camera (projection), FeedbackFX_Azm.
     */
    public class PendulumJuice_Azm : MonoBehaviour
    {
        public PendulumController_Azm controller;
        public BobDragInteraction_Azm bobDrag;
        public PendulumRigView_Azm rigView;
        public Camera viewCamera;
        [Tooltip("A soft disc in the screen-space canvas (its parent stretches over the safe area).")]
        public Image centreGlow;
        [Tooltip("ASSUMPTION (feel): the glow is brightest within this fraction of the amplitude.")]
        public float glowWidthFraction = 0.3f;
        public float minTickInterval = 0.035f;

        private bool learnMode = true;
        private int lastWholeCm = int.MinValue;
        private int lastWholeDeg = int.MinValue;
        private bool wasHeld;
        private float lastTickTime;
        private StepId_Azm lastStep;
        private RectTransform glowParent;
        private Texture2D glowTexture;

        private void Awake()
        {
            var session = AppManager_Azm.Instance != null ? AppManager_Azm.Instance.CurrentSession : null;
            learnMode = session == null || session.Mode == PracticalMode_Azm.Learn;
            if (centreGlow != null)
            {
                glowParent = (RectTransform)centreGlow.rectTransform.parent;
                centreGlow.sprite = MakeGlowSprite();
                centreGlow.raycastTarget = false;
                centreGlow.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            controller.LengthChanged += OnLengthChanged;
            controller.AngleChanged += OnAngleChanged;
            controller.StepChanged += OnStepChanged;
        }

        private void OnDisable()
        {
            controller.LengthChanged -= OnLengthChanged;
            controller.AngleChanged -= OnAngleChanged;
            controller.StepChanged -= OnStepChanged;
        }

        private void OnDestroy()
        {
            if (glowTexture != null) Destroy(glowTexture);
        }

        private void Tick()
        {
            if (Time.unscaledTime - lastTickTime < minTickInterval) return;
            lastTickTime = Time.unscaledTime;
            FeedbackFX_Azm.Tick();
        }

        private void OnLengthChanged()
        {
            int cm = Mathf.FloorToInt(controller.LengthCm);
            bool crossed = lastWholeCm != int.MinValue && cm != lastWholeCm;
            lastWholeCm = cm;
            if (crossed && bobDrag != null && bobDrag.IsDragging) Tick();
        }

        private void OnAngleChanged()
        {
            int deg = Mathf.FloorToInt(Mathf.Abs(controller.DisplacementDeg));
            bool crossed = lastWholeDeg != int.MinValue && deg != lastWholeDeg;
            lastWholeDeg = deg;
            if (crossed && controller.BobHeld) Tick();
        }

        // Releasing the bob moves the practical from Release to the first timing run: the "snap".
        private void OnStepChanged(int index)
        {
            var id = controller.CurrentStepId;
            if (lastStep == StepId_Azm.Release && id == StepId_Azm.TimeRun1) { FeedbackFX_Azm.Contact(); seenAmplitude = 0f; }
            lastStep = id;
        }

        private void Update()
        {
            // Picking the bob up: a light click (once per grab).
            bool held = (bobDrag != null && bobDrag.IsDragging) || controller.BobHeld;
            if (held && !wasHeld) FeedbackFX_Azm.Tap(null);
            wasHeld = held;

            UpdateGlow();
        }

        private void UpdateGlow()
        {
            if (centreGlow == null) return;
            var id = controller.CurrentStepId;
            bool timing = learnMode && controller.IsSwinging && !controller.IsPaused &&
                          (id == StepId_Azm.TimeRun1 || id == StepId_Azm.TimeRun2);
            Vector2 local = Vector2.zero;
            if (timing)
            {
                // The bob's rest (mean) position: under the pivot by the effective length.
                Vector3 rest = rigView.anchor.position - Vector3.up * (controller.EffectiveLengthCm / 100f);
                Vector3 screen = viewCamera.WorldToScreenPoint(rest);
                timing = screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(glowParent, screen, null, out local);
            }
            if (centreGlow.gameObject.activeSelf != timing) centreGlow.gameObject.SetActive(timing);
            if (!timing) return;

            centreGlow.rectTransform.anchoredPosition = local;
            float amplitude = Mathf.Max(0.5f, MaxAmplitude());
            float near = 1f - Mathf.Clamp01(Mathf.Abs(controller.DisplayAngleDeg) / (amplitude * glowWidthFraction));
            var c = centreGlow.color;
            c.a = 0.15f + 0.55f * near * near;
            centreGlow.color = c;
        }

        // The current swing's amplitude, tracked from the angles seen (the controller keeps it private).
        private float seenAmplitude;
        private float MaxAmplitude()
        {
            seenAmplitude = Mathf.Max(seenAmplitude * 0.999f, Mathf.Abs(controller.DisplayAngleDeg));
            return seenAmplitude;
        }

        // A soft radial glow (no texture asset): alpha falls off smoothly from the centre.
        private Sprite MakeGlowSprite()
        {
            const int size = 64;
            glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01(1f - r);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            glowTexture.SetPixels32(pixels);
            glowTexture.Apply();
            return Sprite.Create(glowTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
