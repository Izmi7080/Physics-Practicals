using UnityEngine;
using UnityEngine.InputSystem;

/*
 * Layer: Interaction (lab-scene object pickup, independent of any single practical's own
 * IPracticalInput abstraction - this is "click/tap on a real 3D tool in the room and drag it",
 * not a practical's dedicated drag-to-set-a-value control).
 * Purpose: Raycasts from the player camera to let the student grab the platinum wire tool,
 * drag it across a fixed horizontal plane above the table, and release it. While the wire is
 * held, camera freelook is suppressed via LabFirstPersonMovement.SetWireHeld so dragging doesn't
 * also spin the view.
 * Converted from the legacy UnityEngine.Input API to Unity's Input System package - reads
 * Mouse/Touchscreen "current" device state directly, matching the same approach used in
 * MouseTouchInput.cs and LabFirstPersonMovement.cs, rather than introducing a generated
 * .inputactions asset for what is simple continuous polling.
 * Talks to: raycasts against interactableLayer to find a PlatinumWireTool; calls
 * wireTool.GrabTool/UpdateDragPosition/ReleaseTool and playerMovement.SetWireHeld.
 */
public class LabInteractionRaycaster : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlatinumWireTool wireTool;
    [SerializeField] private BunsenBurnerFX burnerFX;
    [SerializeField] private LabFirstPersonMovement playerMovement;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private float tableSurfaceHeight = 0.85f;

    private bool isHoldingWire = false;
    private int activeTouchId = -1;
    private Plane dragPlane;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<LabFirstPersonMovement>();

        if (wireTool != null && burnerFX != null)
            wireTool.SetBurnerReference(burnerFX);

        dragPlane = new Plane(Vector3.up, new Vector3(0f, tableSurfaceHeight + 0.08f, 0f));
    }

    private void Update()
    {
#if UNITY_EDITOR
        HandleEditorInput();
#else
        HandleMobileTouch();
#endif
    }

    private void HandleEditorInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            TryGrabWire(mouse.position.ReadValue());
        }
        else if (mouse.leftButton.isPressed && isHoldingWire)
        {
            DragWire(mouse.position.ReadValue());
        }
        else if (mouse.leftButton.wasReleasedThisFrame && isHoldingWire)
        {
            ReleaseWire();
        }
    }

    private void HandleMobileTouch()
    {
        if (Touchscreen.current == null) return;

        // Loop over all active touches so multi-touch (joystick + grab) works cleanly
        foreach (var touch in Touchscreen.current.touches)
        {
            var phase = touch.phase.ReadValue();
            bool isLive = touch.press.isPressed;
            if (!isLive && phase != UnityEngine.InputSystem.TouchPhase.Ended && phase != UnityEngine.InputSystem.TouchPhase.Canceled)
                continue; // this touch slot isn't doing anything this frame

            int touchId = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();

            // 1. Try to grab if we aren't holding the wire yet
            if (!isHoldingWire)
            {
                if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    // Ignore touches originating on UI elements (like the virtual joystick or HUD)
                    if (UnityEngine.EventSystems.EventSystem.current != null &&
                        UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touchId))
                    {
                        continue;
                    }

                    if (TryGrabWire(position))
                    {
                        activeTouchId = touchId;
                        break;
                    }
                }
            }
            // 2. If holding, track ONLY the finger that initiated the grab
            else if (touchId == activeTouchId)
            {
                if (phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    DragWire(position);
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    ReleaseWire();
                }
                break;
            }
        }
    }

    private bool TryGrabWire(Vector2 screenPosition)
    {
#if UNITY_EDITOR
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }
#endif

        Ray ray = playerCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 10.0f, interactableLayer))
        {
            PlatinumWireTool tool = hit.collider.GetComponentInParent<PlatinumWireTool>();
            if (tool != null)
            {
                isHoldingWire = true;
                wireTool.GrabTool(hit.point);

                // Lock camera freelook in the movement script
                if (playerMovement != null)
                {
                    playerMovement.SetWireHeld(true);
                }

                Debug.Log("Successfully grabbed wire tool!");
                return true;
            }
        }

        return false;
    }

    private void DragWire(Vector2 screenPosition)
    {
        Ray ray = playerCamera.ScreenPointToRay(screenPosition);
        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 targetPosition = ray.GetPoint(enter);
            wireTool.UpdateDragPosition(targetPosition);
        }
    }

    private void ReleaseWire()
    {
        isHoldingWire = false;
        activeTouchId = -1;

        if (wireTool != null)
        {
            wireTool.ReleaseTool();
        }

        // Re-enable camera freelook
        if (playerMovement != null)
        {
            playerMovement.SetWireHeld(false);
        }
    }
}
