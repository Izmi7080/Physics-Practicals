using UnityEngine;
using UnityEngine.InputSystem;

/*
 * Layer: Interaction (player rig movement - independent of any single practical's
 * IPracticalInput abstraction; this drives walking around the lab space itself, not a
 * practical's own drag/tap controls).
 * Purpose: First-person movement + look for the lab scene. On a touchscreen, the left half of
 * the screen is a fixed on-screen joystick for movement and the right half is swipe-to-look
 * (disabled while isWireHeld, so dragging a practical's wire/handle doesn't also spin the
 * camera). In the Editor, WASD/arrow keys move and holding the right mouse button looks around,
 * for testing without a device.
 * Converted from the legacy UnityEngine.Input API to Unity's Input System package - reads
 * Touchscreen/Keyboard/Mouse "current" device state directly (same low-level approach as
 * MouseTouchInput.cs in the pendulum practical), rather than routing through a generated
 * .inputactions asset, since this script's job is simple continuous polling, not action-mapped
 * bindings. Requires the project's Active Input Handling to include the Input System package
 * (already the case here, since EventSystem already uses InputSystemUIInputModule).
 * Talks to: reads CharacterController on the same GameObject; SetWireHeld is called externally
 * by whatever practical/interaction script picks up a draggable object, to suppress look-swipe.
 */
[RequireComponent(typeof(CharacterController))]
public class LabFirstPersonMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Look Settings")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float touchLookSensitivity = 0.12f;
    [Tooltip("Degrees of rotation per raw pixel of mouse delta. Lower than you'd expect from the old Input.GetAxis-based value (was 2.0) because Mouse.current.delta reports raw per-frame pixel movement, not the pre-scaled value the legacy Input Manager returned - re-tune to taste in the Inspector.")]
    [SerializeField] private float mouseLookSensitivity = 0.1f;
    [SerializeField] private float minPitch = -40f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Interaction Lock")]
    [Tooltip("When true, camera freelook is disabled so touches drag the wire instead.")]
    public bool isWireHeld = false;

    [Header("Fixed Android UI Joystick")]
    [SerializeField] private RectTransform joystickBackground;
    [SerializeField] private RectTransform joystickHandle;
    [SerializeField] private float joystickRadius = 75f;

    private CharacterController controller;
    private Vector3 velocity;
    private float pitch = 15f;
    private float yaw = 0f;

    // Touch tracking - touchId (Input System) plays the same role fingerId did under the old Input class.
    private int moveTouchId = -1;
    private int lookTouchId = -1;
    private Vector2 moveInput;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        Vector3 rot = cameraTransform.eulerAngles;
        pitch = rot.x;
        yaw = rot.y;

        if (joystickBackground != null)
        {
            joystickBackground.gameObject.SetActive(true);
            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = Vector2.zero;
            }
        }
    }

    private void Update()
    {
        if (HasActiveTouch())
        {
            HandleTouchMovement();
        }
#if UNITY_EDITOR
        else
        {
            HandleEditorMovement();
        }
#endif
        ApplyGravityAndMove();
    }

    // True if at least one finger is currently down (or just released this frame, so the
    // Ended/Canceled phase below still gets a chance to reset tracking) - mirrors what
    // Input.touchCount > 0 used to tell us under the legacy Input class.
    private bool HasActiveTouch()
    {
        if (Touchscreen.current == null) return false;

        foreach (var touch in Touchscreen.current.touches)
        {
            var phase = touch.phase.ReadValue();
            if (touch.press.isPressed || phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                return true;
        }
        return false;
    }

    private void HandleTouchMovement()
    {
        foreach (var touch in Touchscreen.current.touches)
        {
            var phase = touch.phase.ReadValue();
            bool isLive = touch.press.isPressed;
            if (!isLive && phase != UnityEngine.InputSystem.TouchPhase.Ended && phase != UnityEngine.InputSystem.TouchPhase.Canceled)
                continue; // this touch slot isn't doing anything this frame

            int touchId = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();

            // --- FIXED JOYSTICK REGION (LEFT SIDE) ---
            if (position.x < Screen.width * 0.5f)
            {
                if (phase == UnityEngine.InputSystem.TouchPhase.Began && moveTouchId == -1)
                {
                    if (joystickBackground != null)
                    {
                        Vector2 bgScreenPos = RectTransformUtility.WorldToScreenPoint(null, joystickBackground.position);
                        float distanceToCenter = Vector2.Distance(position, bgScreenPos);

                        if (distanceToCenter <= joystickRadius * 1.5f)
                        {
                            moveTouchId = touchId;
                            UpdateJoystickInput(position);
                        }
                    }
                }
                else if (touchId == moveTouchId)
                {
                    if (phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                    {
                        UpdateJoystickInput(position);
                    }
                    else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        ResetMoveTouch();
                    }
                }
            }
            // --- RIGHT HALF: SWIPE TO LOOK (DISABLED IF WIRE IS HELD) ---
            else
            {
                // If wire is picked up, cancel any ongoing look touch and skip this one.
                if (isWireHeld)
                {
                    lookTouchId = -1;
                    continue;
                }

                if (phase == UnityEngine.InputSystem.TouchPhase.Began && lookTouchId == -1)
                {
                    lookTouchId = touchId;
                }
                else if (touchId == lookTouchId)
                {
                    if (phase == UnityEngine.InputSystem.TouchPhase.Moved)
                    {
                        Vector2 delta = touch.delta.ReadValue();
                        yaw += delta.x * touchLookSensitivity;
                        pitch -= delta.y * touchLookSensitivity;
                        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

                        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                    }
                    else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        lookTouchId = -1;
                    }
                }
            }
        }
    }

    private void UpdateJoystickInput(Vector2 currentTouchPos)
    {
        if (joystickBackground == null) return;

        Vector2 bgScreenPos = RectTransformUtility.WorldToScreenPoint(null, joystickBackground.position);
        Vector2 delta = currentTouchPos - bgScreenPos;

        moveInput = Vector2.ClampMagnitude(delta, joystickRadius) / joystickRadius;

        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = moveInput * joystickRadius;
        }
    }

    private void ResetMoveTouch()
    {
        moveTouchId = -1;
        moveInput = Vector2.zero;

        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }
    }

    private void HandleEditorMovement()
    {
        Vector2 keyboardInput = Vector2.zero;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) keyboardInput.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) keyboardInput.x -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) keyboardInput.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) keyboardInput.y -= 1f;
        }
        moveInput = keyboardInput.normalized;

        // Mouse look disabled if holding wire.
        var mouse = Mouse.current;
        if (!isWireHeld && mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            yaw += mouseDelta.x * mouseLookSensitivity;
            pitch -= mouseDelta.y * mouseLookSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void ApplyGravityAndMove()
    {
        Vector3 move = (transform.forward * moveInput.y + transform.right * moveInput.x) * walkSpeed;

        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move((move + velocity) * Time.deltaTime);
    }

    public void SetWireHeld(bool held)
    {
        isWireHeld = held;
        if (held)
        {
            lookTouchId = -1;
        }
    }
}
