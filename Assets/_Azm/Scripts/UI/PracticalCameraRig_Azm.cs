using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.UI
{
    [Serializable]
    public class CameraViewEntry_Azm
    {
        public string name;
        public Vector3 position;
        public Vector3 target;      // the point the camera looks at (and orbits around)
        [Tooltip("Used when frameSize is zero. Same meaning as before the HCI restructure: the horizontal framing of a 3:2 physical sensor at this vertical FOV, kept the same at every aspect ratio.")]
        public float fieldOfView = 50f;
        [Tooltip("Size (metres, width x height) of the apparatus to fit inside the scene window. Zero = use fieldOfView.")]
        public Vector2 frameSize;
    }

    /*
     * Layer: UI / presentation (camera control). Reads input only through IPracticalInput_Azm.
     * Purpose: A practical-independent version of the pendulum's camera controller, for new
     * practicals: a list of named views (by index); GoTo(index) glides there with an eased move;
     * inside a view a one-finger drag orbits and a pinch zooms, both clamped so the apparatus never
     * leaves the screen; a double tap returns to the view's default framing. A drag owned by an
     * object interaction (dragOwners, e.g. the caliper jaw) never orbits the camera, and touches on
     * UI never reach this script (the input layer drops them).
     * FRAMING (HCI restructure): the panels on screen change from step to step (keypad, magnifier,
     * drawer) and the screen shape changes from phone to phone (16:9 ... 21:9, 4:3 tablets). The rig
     * keeps the view's target CENTRED IN THE SCENE WINDOW (ScreenRegions_Azm: the free area between
     * the panels) using an OFF-CENTRE PROJECTION: the projection matrix is shifted in clip space so
     * the picture slides without changing perspective (the same effect the old physical-camera lens
     * shift gave for the drawer, but for any window and any aspect ratio). Views with a frameSize
     * also choose the field of view so that the apparatus (frameSize, metres) fits the window with a
     * margin - so the caliper is as large as the free space allows, never cut off. Views without
     * frameSize keep their fixed horizontal framing. Changes of window are eased, so the picture
     * glides aside when a panel opens.
     * Talks to: Camera; IPracticalInput_Azm; IDragOwner_Azm components; ScreenRegions_Azm (window);
     * the practical's HUD calls GoTo.
     */
    public class PracticalCameraRig_Azm : MonoBehaviour
    {
        public Camera viewCamera;
        [Tooltip("Any component implementing IPracticalInput_Azm.")]
        public MonoBehaviour inputSource;
        [Tooltip("Components implementing IDragOwner_Azm; while one drags, the camera does not orbit.")]
        public MonoBehaviour[] dragOwners = new MonoBehaviour[0];
        public List<CameraViewEntry_Azm> views = new List<CameraViewEntry_Azm>();
        public int startView;

        [Header("Motion")]
        public float transitionSeconds = 0.65f;
        public float orbitDegreesPerPixel = 0.12f;
        public float maxYawDegrees = 20f;
        public float maxPitchDegrees = 10f;
        public float minZoom = 0.75f;
        public float maxZoom = 1.25f;
        [Tooltip("Old drawer offset (only used when no ScreenRegions_Azm is assigned).")]
        public float drawerLensShift = 0.19f;

        [Header("Framing")]
        [Tooltip("The screen regions; the target is kept centred in its scene window.")]
        public ScreenRegions_Azm regions;
        [Tooltip("Extra room around frameSize (1.15 = 15 % margin).")]
        public float frameMargin = 1.15f;
        [Tooltip("How quickly the picture follows a change of window (1/seconds).")]
        public float framingSharpness = 9f;

        public event Action<int> ViewChanged;
        // Raised when the student orbits or zooms by hand (the onboarding chip listens for it).
        public event Action UserMovedCamera;
        public int CurrentView { get; private set; }

        private IPracticalInput_Azm input;
        private Vector3 basePosition, baseTarget;
        private float baseFov = 50f;
        private Vector2 baseFrame;
        private Vector2 clipOffset;          // eased off-centre shift in normalised device coordinates
        private float tanHalfVertical = -1f; // eased so a new window size zooms smoothly
        private float yaw, pitch, zoom = 1f;
        private float targetYaw, targetPitch, targetZoom = 1f;
        private float yawVel, pitchVel, zoomVel;
        private float drawerAmount;
        private Coroutine transition;
        private bool initialised;

        private void Awake()
        {
            input = inputSource as IPracticalInput_Azm;
            if (input == null) Debug.LogError("[PracticalCameraRig_Azm] inputSource does not implement IPracticalInput_Azm.", this);
            // The off-centre projection is set directly (see LateUpdate), so the physical-camera
            // model is not used any more.
            viewCamera.usePhysicalProperties = false;
        }

        private void OnEnable()
        {
            if (input == null) return;
            input.PointerDrag += OnDrag;
            input.Pinch += OnPinch;
            input.DoubleTap += OnDoubleTap;
        }

        private void OnDisable()
        {
            if (viewCamera != null) viewCamera.ResetProjectionMatrix();
            if (input == null) return;
            input.PointerDrag -= OnDrag;
            input.Pinch -= OnPinch;
            input.DoubleTap -= OnDoubleTap;
        }

        private void Start()
        {
            if (!initialised) GoTo(startView, true);
        }

        public void GoTo(int index, bool instant = false)
        {
            if (index < 0 || index >= views.Count) return;
            var view = views[index];
            CurrentView = index;
            ViewChanged?.Invoke(index);
            targetYaw = 0f; targetPitch = 0f; targetZoom = 1f;

            if (transition != null) StopCoroutine(transition);
            if (instant || !initialised)
            {
                basePosition = view.position;
                baseTarget = view.target;
                baseFov = view.fieldOfView;
                baseFrame = view.frameSize;
                yaw = pitch = 0f; zoom = 1f;
                tanHalfVertical = -1f;   // snap the framing on the first frame
                initialised = true;
                return;
            }
            transition = StartCoroutine(Glide(view));
        }

        private IEnumerator Glide(CameraViewEntry_Azm view)
        {
            Vector3 p0 = basePosition, t0 = baseTarget;
            float f0 = baseFov, t = 0f;
            Vector2 s0 = baseFrame;
            while (t < transitionSeconds)
            {
                t += Time.unscaledDeltaTime;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / transitionSeconds));
                basePosition = Vector3.Lerp(p0, view.position, e);
                baseTarget = Vector3.Lerp(t0, view.target, e);
                baseFov = Mathf.Lerp(f0, view.fieldOfView, e);
                baseFrame = Vector2.Lerp(s0, view.frameSize, e);
                yield return null;
            }
            basePosition = view.position;
            baseTarget = view.target;
            baseFov = view.fieldOfView;
            baseFrame = view.frameSize;
            transition = null;
        }

        public void SetDrawerOpenAmount(float amount) => drawerAmount = amount;

        // Moves a view to a new target (keeping the camera's offset from it) and/or a new frame size,
        // e.g. the pendulum's Timing view following the bob when the thread length changes. If the
        // view is showing, the camera glides to it.
        public void UpdateView(int index, Vector3 target, Vector2 frameSize)
        {
            if (index < 0 || index >= views.Count) return;
            var view = views[index];
            Vector3 offset = view.position - view.target;
            view.target = target;
            view.position = target + offset;
            view.frameSize = frameSize;
            if (index == CurrentView && initialised)
            {
                if (transition != null) StopCoroutine(transition);
                transition = StartCoroutine(Glide(view));
            }
        }

        private void OnDrag(Vector2 position, Vector2 delta)
        {
            foreach (var owner in dragOwners)
                if (owner is IDragOwner_Azm drag && drag.IsDragging) return;
            targetYaw = Mathf.Clamp(targetYaw - delta.x * orbitDegreesPerPixel, -maxYawDegrees, maxYawDegrees);
            targetPitch = Mathf.Clamp(targetPitch + delta.y * orbitDegreesPerPixel, -maxPitchDegrees, maxPitchDegrees);
            UserMovedCamera?.Invoke();
        }

        private void OnPinch(float factor)
        {
            targetZoom = Mathf.Clamp(targetZoom / factor, minZoom, maxZoom);
            UserMovedCamera?.Invoke();
        }

        // Juice (Stage 6): a tiny bump of the camera towards the apparatus, felt as "contact" when the
        // jaws touch the object. Skipped with Reduce motion.
        private Vector3 nudge, nudgeVel;
        public void Nudge(float metres = 0.004f)
        {
            if (Motion_Azm.ReduceMotion || viewCamera == null) return;
            nudge += viewCamera.transform.forward * metres;
        }

        private void OnDoubleTap(Vector2 position) { targetYaw = 0f; targetPitch = 0f; targetZoom = 1f; }

        private void LateUpdate()
        {
            if (!initialised) return;
            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVel, 0.12f, Mathf.Infinity, Time.unscaledDeltaTime);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVel, 0.12f, Mathf.Infinity, Time.unscaledDeltaTime);
            zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVel, 0.12f, Mathf.Infinity, Time.unscaledDeltaTime);

            Vector3 offset = basePosition - baseTarget;
            Vector3 forward = -offset.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Quaternion rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, right);

            viewCamera.transform.position = baseTarget + rotation * (offset * zoom);
            viewCamera.transform.LookAt(baseTarget);
            // A nudge (juice) settles back to zero within ~0.2 s.
            nudge = Vector3.SmoothDamp(nudge, Vector3.zero, ref nudgeVel, 0.07f, Mathf.Infinity, Time.unscaledDeltaTime);
            viewCamera.transform.position += nudge;
            ApplyProjection(offset.magnitude);
        }

        // The off-centre projection. The camera looks AT the target, so with a centred projection the
        // target is at the middle of the screen (NDC 0,0). Shifting clip space by (tx, ty) moves it to
        // NDC (tx, ty): we pick the centre of the scene window. The field of view is chosen from the
        // window size when the view has a frameSize (the apparatus fits the free area).
        private void ApplyProjection(float baseDistance)
        {
            float aspect = Mathf.Max(0.1f, viewCamera.pixelWidth / (float)Mathf.Max(1, viewCamera.pixelHeight));

            // Window in viewport units (0..1). Without regions: full screen, plus the old drawer offset.
            Rect window = new Rect(0f, 0f, 1f, 1f);
            if (regions != null && regions.SceneWindow != null)
            {
                Rect px = regions.SceneWindowScreenRect();
                float w = Mathf.Max(1f, viewCamera.pixelWidth), h = Mathf.Max(1f, viewCamera.pixelHeight);
                window = Rect.MinMaxRect(px.xMin / w, px.yMin / h, px.xMax / w, px.yMax / h);
                if (window.width < 0.05f || window.height < 0.05f) window = new Rect(0f, 0f, 1f, 1f);
            }

            // Horizontal half-angle tangent for the whole screen.
            float tanH;
            if (baseFrame.x > 0.0001f && baseFrame.y > 0.0001f && baseDistance > 0.0001f)
            {
                // Fit width and height of the apparatus into the window (each tangent is relative to
                // the full screen, so divide by the window's share of the screen).
                float fitWidth = (baseFrame.x * 0.5f * frameMargin / baseDistance) / window.width;
                float fitHeight = (baseFrame.y * 0.5f * frameMargin / baseDistance) / window.height * aspect;
                tanH = Mathf.Max(fitWidth, fitHeight);
            }
            else
            {
                // Legacy framing: a 3:2 physical sensor (36 x 24 mm) with horizontal gate fit, i.e.
                // the horizontal half-angle tangent is 1.5 x tan(vertical FOV / 2), at every aspect.
                tanH = 1.5f * Mathf.Tan(baseFov * 0.5f * Mathf.Deg2Rad);
            }
            float targetTanV = tanH / aspect;

            // Window centre in NDC (-1..1).
            Vector2 desired = new Vector2(window.center.x * 2f - 1f, window.center.y * 2f - 1f);
            if (regions == null)
                desired.x = -2f * drawerLensShift * Mathf.SmoothStep(0f, 1f, drawerAmount);   // old behaviour

            float k = 1f - Mathf.Exp(-framingSharpness * Time.unscaledDeltaTime);
            if (tanHalfVertical < 0f) { tanHalfVertical = targetTanV; clipOffset = desired; }
            else
            {
                tanHalfVertical = Mathf.Lerp(tanHalfVertical, targetTanV, k);
                clipOffset = Vector2.Lerp(clipOffset, desired, k);
            }

            float fov = 2f * Mathf.Atan(tanHalfVertical) * Mathf.Rad2Deg;
            viewCamera.fieldOfView = fov;   // kept in sync for any code that reads it
            Matrix4x4 projection = Matrix4x4.Perspective(fov, aspect, viewCamera.nearClipPlane, viewCamera.farClipPlane);
            // Clip-space shift: x' = x + tx * w, y' = y + ty * w  ->  NDC moves by (tx, ty).
            Matrix4x4 shift = Matrix4x4.identity;
            shift.m03 = clipOffset.x;
            shift.m13 = clipOffset.y;
            viewCamera.projectionMatrix = shift * projection;
        }
    }
}
