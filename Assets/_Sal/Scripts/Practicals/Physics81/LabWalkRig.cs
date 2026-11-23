using UnityEngine;
using UnityEngine.InputSystem;

namespace VirtualLab.Physics81
{
    // Movement adapted from FlameTest / LabFirstPersonMovement: camera-relative
    // CharacterController motion, local pitch, yaw, gravity and separate touch inputs.
    [RequireComponent(typeof(CharacterController))]
    public sealed class LabWalkRig : MonoBehaviour
    {
        [SerializeField] Camera view;
        [SerializeField] float speed = 2.1f;
        [SerializeField] float gravity = -9.81f;
        [SerializeField] float lookDegreesPerScreenHeight = 125f;

        CharacterController controller;
        Vector2 walk;
        float pitch, verticalSpeed;
        Vector3 eyeOffset;
        Vector3 startPosition;
        Quaternion startRotation;
        float startPitch;
        Transform inspection;
        bool returning, suspended, handsBusy;
        float cameraBlend, zoom;
        Vector3 cameraFrom;
        Quaternion cameraFromRotation;
        public bool IsInspecting => inspection != null || returning;
        public bool IsViewing(Transform target) => target != null && inspection == target && !IsTransitioning;
        public bool IsSuspended => suspended;
        public Camera ViewCamera => view;
        public bool IsTransitioning => IsInspecting && cameraBlend < 1f;
        public bool CanZoom => inspection != null && !suspended && !handsBusy;
        public bool CanWalk => !IsInspecting && !suspended && !handsBusy;

        public void LockMovement(bool locked)
        {
            handsBusy = locked;
            if (locked) walk = Vector2.zero;
        }

        public void Configure(Camera camera) => view = camera;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (view == null) view = GetComponentInChildren<Camera>();
            if (view == null) { enabled = false; return; }
            eyeOffset = view.transform.localPosition;
            pitch = Mathf.DeltaAngle(0, view.transform.localEulerAngles.x);
            startPosition = transform.position;
            startRotation = transform.rotation;
            startPitch = pitch;
        }

        public void ResetStation()
        {
            inspection = null; returning = handsBusy = false;
            walk = Vector2.zero; verticalSpeed = 0; zoom = 0;
            controller.enabled = false;
            transform.SetPositionAndRotation(startPosition, startRotation);
            controller.enabled = true;
            pitch = startPitch;
            view.transform.SetLocalPositionAndRotation(eyeOffset, Quaternion.Euler(pitch, 0, 0));
        }

        public void SetWalk(Vector2 input) => walk = CanWalk ? Vector2.ClampMagnitude(input, 1f) : Vector2.zero;

        public void Look(Vector2 pixelDelta)
        {
            if (!CanWalk) return;
            float scale = lookDegreesPerScreenHeight / Mathf.Max(1, Screen.height);
            transform.Rotate(0, pixelDelta.x * scale, 0, Space.Self);
            pitch = Mathf.Clamp(pitch - pixelDelta.y * scale, -40f, 65f);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        public void Inspect(Transform target)
        {
            if (suspended || handsBusy || target == null || target == inspection) return;
            walk = Vector2.zero;
            returning = false;
            inspection = target;
            zoom = 0f;
            BeginCameraBlend();
        }

        public void Zoom(float delta)
        {
            if (!CanZoom) return;
            float next = Mathf.Clamp(zoom + delta, -.12f, .095f);
            if (Mathf.Approximately(zoom, next)) return;
            zoom = next;
            BeginCameraBlend();
        }

        public void LeaveInspection()
        {
            if (inspection == null) return;
            inspection = null;
            returning = true;
            BeginCameraBlend();
        }

        void BeginCameraBlend()
        {
            cameraBlend = 0;
            cameraFrom = view.transform.position;
            cameraFromRotation = view.transform.rotation;
        }

        public void Suspend(bool value)
        {
            suspended = value;
            walk = Vector2.zero;
        }

        void Update()
        {
            if (suspended || handsBusy) return;
            if (IsInspecting)
            {
                cameraBlend = Mathf.MoveTowards(cameraBlend, 1f, Time.unscaledDeltaTime / .28f);
                float eased = Mathf.SmoothStep(0f, 1f, cameraBlend);
                Vector3 target = returning ? transform.TransformPoint(eyeOffset) : inspection.position + inspection.forward * zoom;
                Quaternion rotation = returning ? transform.rotation * Quaternion.Euler(pitch, 0, 0) : inspection.rotation;
                view.transform.SetPositionAndRotation(Vector3.Lerp(cameraFrom, target, eased), Quaternion.Slerp(cameraFromRotation, rotation, eased));
                if (returning && cameraBlend >= 1f) returning = false;
                return;
            }

            Vector2 input = walk;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                        - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                float y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                        - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                if (x != 0 || y != 0) input = new Vector2(x, y);
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed) Look(mouse.delta.ReadValue());

            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
            verticalSpeed += gravity * dt;
            Vector2 unit = Vector2.ClampMagnitude(input, 1f);
            Vector3 horizontal = (transform.right * unit.x + transform.forward * unit.y) * speed;
            controller.Move((horizontal + Vector3.up * verticalSpeed) * dt);
        }

        void OnApplicationFocus(bool hasFocus) { if (!hasFocus) walk = Vector2.zero; }
        void OnApplicationPause(bool paused) { if (paused) walk = Vector2.zero; }
        void OnDisable() => walk = Vector2.zero;
    }
}
