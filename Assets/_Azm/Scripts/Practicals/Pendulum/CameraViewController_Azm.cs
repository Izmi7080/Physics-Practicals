using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.Pendulum
{
    [Serializable]
    public class CameraViewDef_Azm
    {
        public CameraViewId_Azm id;
        public Vector3 position;
        public Vector3 target;      // the point the camera looks at (and orbits around)
        public float fieldOfView = 50f;
    }

    /*
     * Layer: UI / presentation (camera control). Reads input only through IPracticalInput_Azm.
     * Purpose: The named camera views (Overview, Rig, Timing, Caliper). Each step asks for its view
     * and the camera glides there with an eased move (about 0.65 s); the student can also pick a view
     * with the buttons. Inside a view the student can orbit with a one-finger drag and zoom with a
     * pinch, both clamped so the apparatus can never leave the screen; a double tap returns to the
     * view's default framing. A drag that starts on the bob (BobDragInteraction_Azm) or on another
     * object interaction such as the caliper jaw (otherDragOwners) does not move the camera; touches
     * on UI never reach this script (the input layer drops them).
     *
     * DRAWER OFFSET: when the right-hand drawer opens the apparatus must stay centred in the visible
     * area to its left. This uses CAMERA LENS SHIFT (the physical-camera "lens shift") rather than
     * moving the camera or the look-at point: it slides the image sideways without changing the
     * perspective or the orbit, so nothing in the 3D view appears to "move" except a smooth pan of
     * the picture, and it composes cleanly with orbit, zoom and view changes.
     * Talks to: Camera; PendulumController_Azm.StepChanged (views per step); DrawerUI_Azm via
     * SetDrawerOpenAmount; BobDragInteraction_Azm.IsDragging.
     */
    public class CameraViewController_Azm : MonoBehaviour
    {
        public Camera viewCamera;
        public PendulumController_Azm controller;
        [Tooltip("Any component implementing IPracticalInput_Azm.")]
        public MonoBehaviour inputSource;
        public BobDragInteraction_Azm bobDrag;
        [Tooltip("Other object interactions (components implementing IDragOwner_Azm, e.g. the caliper jaw). While one of them drags, the camera does not orbit.")]
        public MonoBehaviour[] otherDragOwners = new MonoBehaviour[0];
        [Tooltip("The non-rotating pivot point; the Timing view frames the bob relative to it.")]
        public Transform pivotAnchor;
        [Tooltip("Timing view: how far (metres) above the screen centre the bob's rest position is framed.")]
        public float timingBobAboveCentre = 0.20f;
        public List<CameraViewDef_Azm> views = new List<CameraViewDef_Azm>();

        [Header("Motion")]
        public float transitionSeconds = 0.65f;
        public float orbitDegreesPerPixel = 0.12f;
        public float maxYawDegrees = 28f;
        public float maxPitchDegrees = 14f;
        public float minZoom = 0.72f;   // fraction of the default distance (closer)
        public float maxZoom = 1.25f;   // (further)
        [Tooltip("Lens shift (fraction of the image width) applied when the drawer is fully open.")]
        public float drawerLensShift = 0.17f;

        public event Action<CameraViewId_Azm> ViewChanged;
        public CameraViewId_Azm CurrentView { get; private set; } = CameraViewId_Azm.Overview;

        private IPracticalInput_Azm input;
        private Vector3 basePosition, baseTarget;
        private float baseFov = 50f;
        private float yaw, pitch, zoom = 1f;
        private float targetYaw, targetPitch, targetZoom = 1f;
        private float yawVel, pitchVel, zoomVel;
        private float drawerAmount;
        private Coroutine transition;
        private bool initialised;

        private void Awake()
        {
            input = inputSource as IPracticalInput_Azm;
            if (input == null) Debug.LogError("[CameraViewController_Azm] inputSource does not implement IPracticalInput_Azm.", this);

            // Lens shift needs the physical-camera model; set the field of view afterwards so the
            // picture keeps the same framing.
            float fov = viewCamera.fieldOfView;
            viewCamera.usePhysicalProperties = true;
            viewCamera.fieldOfView = fov;
        }

        private void OnEnable()
        {
            if (controller != null) controller.StepChanged += OnStepChanged;
            if (input != null)
            {
                input.PointerDrag += OnDrag;
                input.Pinch += OnPinch;
                input.DoubleTap += OnDoubleTap;
            }
        }

        private void OnDisable()
        {
            if (controller != null) controller.StepChanged -= OnStepChanged;
            if (input != null)
            {
                input.PointerDrag -= OnDrag;
                input.Pinch -= OnPinch;
                input.DoubleTap -= OnDoubleTap;
            }
        }

        private void Start()
        {
            // Begin exactly on the first step's view (no glide from wherever the camera was placed).
            GoTo(controller != null ? controller.CurrentStep.view : CameraViewId_Azm.Overview, true);
        }

        private void OnStepChanged(int index)
        {
            GoTo(controller.CurrentStep.view, false);
        }

        private CameraViewDef_Azm Find(CameraViewId_Azm id)
        {
            foreach (var v in views) if (v.id == id) return v;
            return views.Count > 0 ? views[0] : null;
        }

        // The Timing view follows the bob's rest position: it frames the bob and its whole swing in the
        // UPPER part of the screen, so the tap button and stopwatch at the bottom never cover it,
        // whatever the thread length.
        private CameraViewDef_Azm Resolve(CameraViewId_Azm id)
        {
            var view = Find(id);
            if (view == null || id != CameraViewId_Azm.Timing || pivotAnchor == null || controller == null) return view;

            float bobRestY = pivotAnchor.position.y - (controller.LengthCm + controller.TrueRadiusCm) / 100f;
            var adjusted = new CameraViewDef_Azm
            {
                id = view.id,
                position = view.position,
                target = view.target,
                fieldOfView = view.fieldOfView
            };
            adjusted.target.y = bobRestY - timingBobAboveCentre;
            adjusted.position.y = adjusted.target.y + 0.05f;
            return adjusted;
        }

        // Also called by the view buttons.
        public void GoTo(CameraViewId_Azm id, bool instant = false)
        {
            var view = Resolve(id);
            if (view == null) return;

            CurrentView = id;
            ViewChanged?.Invoke(id);

            // A new view always starts from its default framing.
            targetYaw = 0f; targetPitch = 0f; targetZoom = 1f;

            if (transition != null) StopCoroutine(transition);
            if (instant || !initialised)
            {
                basePosition = view.position;
                baseTarget = view.target;
                baseFov = view.fieldOfView;
                yaw = pitch = 0f; zoom = 1f;
                initialised = true;
                return;
            }
            transition = StartCoroutine(Glide(view));
        }

        private IEnumerator Glide(CameraViewDef_Azm view)
        {
            Vector3 p0 = basePosition, t0 = baseTarget;
            float f0 = baseFov;
            float t = 0f;
            while (t < transitionSeconds)
            {
                t += Time.unscaledDeltaTime;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / transitionSeconds));
                basePosition = Vector3.Lerp(p0, view.position, e);
                baseTarget = Vector3.Lerp(t0, view.target, e);
                baseFov = Mathf.Lerp(f0, view.fieldOfView, e);
                yield return null;
            }
            basePosition = view.position;
            baseTarget = view.target;
            baseFov = view.fieldOfView;
            transition = null;
        }

        // 0 = drawer closed, 1 = open (from DrawerUI_Azm.OpenAmount).
        public void SetDrawerOpenAmount(float amount)
        {
            drawerAmount = amount;
        }

        // ----- gestures -----

        private void OnDrag(Vector2 position, Vector2 delta)
        {
            if (bobDrag != null && bobDrag.IsDragging) return;
            foreach (var owner in otherDragOwners)
                if (owner is IDragOwner_Azm drag && drag.IsDragging) return;
            targetYaw = Mathf.Clamp(targetYaw - delta.x * orbitDegreesPerPixel, -maxYawDegrees, maxYawDegrees);
            targetPitch = Mathf.Clamp(targetPitch + delta.y * orbitDegreesPerPixel, -maxPitchDegrees, maxPitchDegrees);
        }

        private void OnPinch(float factor)
        {
            targetZoom = Mathf.Clamp(targetZoom / factor, minZoom, maxZoom);
        }

        private void OnDoubleTap(Vector2 position)
        {
            targetYaw = 0f; targetPitch = 0f; targetZoom = 1f;
        }

        // ----- apply -----

        private void LateUpdate()
        {
            if (!initialised) return;

            // Smooth the gesture values so orbit and zoom feel soft rather than twitchy.
            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVel, 0.12f, Mathf.Infinity, Time.unscaledDeltaTime);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVel, 0.12f, Mathf.Infinity, Time.unscaledDeltaTime);
            zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVel, 0.12f, Mathf.Infinity, Time.unscaledDeltaTime);

            Vector3 offset = basePosition - baseTarget;
            Vector3 forward = -offset.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Quaternion rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, right);

            viewCamera.transform.position = baseTarget + rotation * (offset * zoom);
            viewCamera.transform.LookAt(baseTarget);
            viewCamera.fieldOfView = baseFov;
            viewCamera.lensShift = new Vector2(drawerLensShift * Mathf.SmoothStep(0f, 1f, drawerAmount), 0f);
        }
    }
}
