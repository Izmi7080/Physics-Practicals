using UnityEngine;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI / presentation (draws the apparatus from the controller's numbers; the controller
     * never touches a Transform).
     * Purpose: Positions the thread and bob from the thread length l, the bob radius r and the
     * angle, in real units (1 Unity unit = 1 m): the bob's centre sits l + r below the pivot. Also
     * draws the two world-space helpers the spec allows: the protractor arc behind the bob
     * (green up to the small-angle limit, red beyond, shown while the student sets the angle) and a
     * pulsing ring that shows the bob can be grabbed (the real 2 cm bob is tiny on a phone screen).
     * Both are generated as sprites at run time from the config (so the 15 degree boundary always
     * matches PendulumConfig_Azm), so no art assets are needed.
     * Talks to: reads PendulumController_Azm every frame; writes the rig's Transforms and sprites.
     */
    public class PendulumRigView_Azm : MonoBehaviour
    {
        public PendulumController_Azm controller;
        public Camera viewCamera;

        [Header("Rig parts")]
        [Tooltip("Non-rotating point at the pivot. The bob-drag interaction measures against this.")]
        public Transform anchor;
        [Tooltip("Rotates about the pivot; thread and bob are its children.")]
        public Transform pivot;
        public Transform thread;
        public Transform bob;
        public SphereCollider bobHit;

        [Header("World-space helpers")]
        public SpriteRenderer protractor;
        public SpriteRenderer grabRing;
        public Color grabRingColor = new Color(0.18f, 0.8f, 0.74f, 1f);

        [Tooltip("How much bigger than the swing radius the protractor is drawn (metres).")]
        public float protractorMargin = 0.07f;

        private Sprite protractorSprite;
        private Sprite ringSprite;
        private SpriteRenderer centreLine;

        private void Awake()
        {
            protractorSprite = BuildProtractor(controller.config.maxReleaseAngleDeg);
            protractor.sprite = protractorSprite;
            ringSprite = BuildRing();
            grabRing.sprite = ringSprite;

            // A faint vertical line through the rest position ("the centre") shown while timing: the
            // instruction says "tap at the centre", and this makes the mean position easy to see.
            var lineGO = new GameObject("CentreLine", typeof(SpriteRenderer));
            lineGO.transform.SetParent(transform, false);
            centreLine = lineGO.GetComponent<SpriteRenderer>();
            var lineTex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            lineTex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            lineTex.Apply();
            var lineSprite = Sprite.Create(lineTex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 1f), 2f);   // 1 x 1 unit, pivot top
            lineSprite.hideFlags = HideFlags.HideAndDontSave;
            centreLine.sprite = lineSprite;
            centreLine.sortingOrder = -1;
        }

        private void LateUpdate()
        {
            float lengthM = controller.LengthCm / 100f;
            float radiusM = controller.TrueRadiusCm / 100f;   // the real bob, not the student's reading

            pivot.localRotation = Quaternion.Euler(0f, 0f, controller.DisplayAngleDeg);

            // Default Unity cylinder is 2 units tall, hence the /2 to turn a length into a scale.
            thread.localScale = new Vector3(thread.localScale.x, lengthM * 0.5f, thread.localScale.z);
            thread.localPosition = new Vector3(0f, -lengthM * 0.5f, 0f);
            // The thread ends at the TOP of the bob, so the bob's centre is l + r below the pivot.
            bob.localPosition = new Vector3(0f, -(lengthM + radiusM), 0f);
            float diameter = radiusM * 2f;
            bob.localScale = new Vector3(diameter, diameter, diameter);

            UpdateProtractor(lengthM + radiusM);
            UpdateGrabRing();
            UpdateCentreLine(lengthM + radiusM);
        }

        private void UpdateCentreLine(float effectiveLengthM)
        {
            bool show = controller.IsSwinging;
            centreLine.gameObject.SetActive(show);
            if (!show) return;

            var color = Color.white; color.a = 0.28f;
            centreLine.color = color;
            centreLine.transform.position = anchor.position + anchor.forward * 0.02f;
            centreLine.transform.rotation = anchor.rotation;
            centreLine.transform.localScale = new Vector3(0.006f, effectiveLengthM + 0.08f, 1f);
        }

        private void UpdateProtractor(float effectiveLengthM)
        {
            StepId_Azm step = controller.CurrentStepId;
            float alpha = 0f;
            if (step == StepId_Azm.Release && !controller.IsSwinging) alpha = 1f;
            else if (controller.IsSwinging) alpha = 0.12f;   // very faint during timing: shows the swing stays small

            protractor.gameObject.SetActive(alpha > 0.01f);
            if (alpha <= 0.01f) return;

            var color = protractor.color; color.a = alpha; protractor.color = color;
            protractor.transform.position = anchor.position + anchor.forward * 0.03f;  // just behind the swing plane
            protractor.transform.rotation = anchor.rotation;
            protractor.transform.localScale = Vector3.one * (effectiveLengthM + protractorMargin);
        }

        private void UpdateGrabRing()
        {
            bool grabbable = controller.DragMode != DragMode_Azm.None && !controller.IsSwinging;
            grabRing.gameObject.SetActive(grabbable);
            if (!grabbable) return;

            float pulse = controller.BobHeld ? 1f : 0.65f + 0.35f * Mathf.Sin(Time.time * 4f);
            var color = grabRingColor; color.a = pulse; grabRing.color = color;
            grabRing.transform.position = bob.position;
            grabRing.transform.localScale = Vector3.one * (controller.BobHeld ? 0.06f : 0.05f);
            if (viewCamera != null) grabRing.transform.rotation = viewCamera.transform.rotation;   // always faces the viewer
        }

        // A half disc hanging below the pivot (pivot at the top-centre of the sprite, radius = 1
        // unit): green within +-limit degrees of straight down, red beyond, with a tick every 5
        // degrees and strong lines at the limit.
        private static Sprite BuildProtractor(float limitDeg)
        {
            const int w = 512, h = 256;
            const float ppu = 256f;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var green = new Color(0.30f, 0.85f, 0.45f, 0.34f);
            var red = new Color(0.95f, 0.30f, 0.30f, 0.30f);
            var white = new Color(1f, 1f, 1f, 0.9f);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - w * 0.5f;
                float dy = h - (y + 0.5f);          // distance below the pivot line
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = h - 2f;
                if (r > outer + 1f || dy < 0f) { tex.SetPixel(x, y, Color.clear); continue; }

                float angle = Mathf.Atan2(Mathf.Abs(dx), dy) * Mathf.Rad2Deg;   // 0 = straight down
                Color c = angle <= limitDeg ? green : red;

                // Rim and tick marks
                float rim = Mathf.Clamp01(1.5f - Mathf.Abs(r - outer));
                c = Color.Lerp(c, white, rim * 0.85f);
                float nearestTick = Mathf.Abs(angle - Mathf.Round(angle / 5f) * 5f);
                float tickWidth = 0.6f / Mathf.Max(r / 60f, 1f);
                if (r > outer - 14f && nearestTick < tickWidth) c = Color.Lerp(c, white, 0.8f);
                // Strong radial line at the limit (both sides)
                if (Mathf.Abs(angle - limitDeg) < 0.45f) c = Color.Lerp(c, white, 0.85f);
                if (angle < 0.4f) c = Color.Lerp(c, white, 0.5f);

                c.a *= Mathf.Clamp01(outer + 1f - r);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), ppu);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite BuildRing()
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f));
                float d = Mathf.Abs(r - (s / 2f - 8f)) - 5f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s / 2f);  // radius = 1 unit
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
