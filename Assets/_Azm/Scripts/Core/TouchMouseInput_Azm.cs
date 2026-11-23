using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Interaction (the ONLY script in the practical scenes that reads pointer devices).
     * Purpose: Turns the phone touchscreen (and, in the Editor, the mouse) into the device-free
     * events of IPracticalInput_Azm: pointer down / drag / up / cancel, double tap, and pinch zoom
     * (two-finger pinch, or the mouse wheel in the Editor).
     * A pointer that starts over any UI element (button, panel, drawer) is ignored, so touching
     * the interface never moves the camera or the bob.
     * Talks to: Unity's Input System (Touchscreen / Mouse) and the EventSystem (UI hit test);
     * raises events for BobDragInteraction_Azm and CameraViewController_Azm.
     */
    public class TouchMouseInput_Azm : MonoBehaviour, IPracticalInput_Azm
    {
        public event Action<Vector2> PointerDown;
        public event Action<Vector2, Vector2> PointerDrag;
        public event Action<Vector2> PointerUp;
        public event Action PointerCancel;
        public event Action<Vector2> DoubleTap;
        public event Action<float> Pinch;

        [Tooltip("A press shorter than this, that barely moved, counts as a tap (seconds).")]
        public float tapMaxSeconds = 0.25f;
        [Tooltip("Two taps closer together than this are a double tap (seconds).")]
        public float doubleTapSeconds = 0.35f;
        [Tooltip("Movement (screen pixels) still counted as 'in the same place'.")]
        public float tapMaxMovePixels = 30f;

        private bool pointerActive;
        private bool gestureIsPinch;
        private Vector2 lastPosition;
        private Vector2 downPosition;
        private float downTime;
        private bool movedTooFar;
        private float lastTapTime = -10f;
        private Vector2 lastTapPosition;
        private float lastPinchDistance;

        private readonly List<Vector2> touchPositions = new List<Vector2>();
        private readonly List<int> touchIds = new List<int>();

        private void Update()
        {
            var screen = Touchscreen.current;
            if (screen != null && CollectTouches(screen))
                HandleTouches();
            else
            {
                if (gestureIsPinch) { gestureIsPinch = false; lastPinchDistance = 0f; }
                if (touchIds.Count == 0 && Mouse.current != null) HandleMouse(Mouse.current);
            }
        }

        private bool CollectTouches(Touchscreen screen)
        {
            touchPositions.Clear();
            touchIds.Clear();
            foreach (var touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;
                touchPositions.Add(touch.position.ReadValue());
                touchIds.Add(touch.touchId.ReadValue());
            }
            // No touches down but a pointer is still active: let the release be processed below.
            return touchPositions.Count > 0 || (pointerActive && !(Mouse.current != null && Mouse.current.leftButton.isPressed));
        }

        private void HandleTouches()
        {
            int count = touchPositions.Count;

            if (count >= 2)
            {
                // A second finger turns the gesture into a pinch: cancel any single-finger drag.
                if (pointerActive) { pointerActive = false; PointerCancel?.Invoke(); }
                gestureIsPinch = true;

                float distance = Vector2.Distance(touchPositions[0], touchPositions[1]);
                if (lastPinchDistance > 1f && distance > 1f)
                    Pinch?.Invoke(distance / lastPinchDistance);
                lastPinchDistance = distance;
                return;
            }

            if (count == 0)
            {
                lastPinchDistance = 0f;
                gestureIsPinch = false;
                if (pointerActive) EndPointer(lastPosition);
                return;
            }

            if (gestureIsPinch) return; // wait until every finger is lifted before dragging again

            Vector2 position = touchPositions[0];
            if (!pointerActive)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchIds[0])) return;
                BeginPointer(position);
            }
            else
            {
                MovePointer(position);
            }
        }

        private void HandleMouse(Mouse mouse)
        {
            Vector2 position = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                    BeginPointer(position);
            }
            else if (pointerActive && mouse.leftButton.isPressed)
            {
                MovePointer(position);
            }
            if (pointerActive && mouse.leftButton.wasReleasedThisFrame)
                EndPointer(position);

            // Mouse wheel = zoom (Editor convenience; phones use pinch).
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                Pinch?.Invoke(scroll > 0f ? 1.1f : 1f / 1.1f);
        }

        private void BeginPointer(Vector2 position)
        {
            pointerActive = true;
            movedTooFar = false;
            downPosition = position;
            lastPosition = position;
            downTime = Time.unscaledTime;
            PointerDown?.Invoke(position);
        }

        private void MovePointer(Vector2 position)
        {
            Vector2 delta = position - lastPosition;
            if ((position - downPosition).magnitude > tapMaxMovePixels) movedTooFar = true;
            lastPosition = position;
            if (delta.sqrMagnitude > 0f) PointerDrag?.Invoke(position, delta);
        }

        private void EndPointer(Vector2 position)
        {
            pointerActive = false;
            PointerUp?.Invoke(position);

            // A quick press that barely moved is a tap; two close taps are a double tap.
            bool isTap = !movedTooFar && Time.unscaledTime - downTime <= tapMaxSeconds;
            if (!isTap) return;

            if (Time.unscaledTime - lastTapTime <= doubleTapSeconds
                && Vector2.Distance(position, lastTapPosition) <= tapMaxMovePixels * 2f)
            {
                lastTapTime = -10f;
                DoubleTap?.Invoke(position);
            }
            else
            {
                lastTapTime = Time.unscaledTime;
                lastTapPosition = position;
            }
        }
    }
}
