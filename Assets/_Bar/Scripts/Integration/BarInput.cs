using UnityEngine;
using UnityEngine.InputSystem;

/// INTEGRATION (Tajarbagah, added during the Stage 4 import - see INTEGRATION_GUIDE.md).
/// Layer: Interaction. The project uses the new Input System only, where the old UnityEngine.Input
/// calls throw an exception every frame. This adapter gives the same calls the CuSO4 and Reflection
/// practicals used (mouse buttons, mouse position, touches, scroll) on top of the Input System, so
/// their logic stays unchanged. Like the old Input, the first finger also acts as the left mouse
/// button (old Input.simulateMouseWithTouches behaviour).
public static class BarInput
{
    public struct TouchInfo { public Vector2 position; }

    static bool PrimaryTouchPressed => Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;

    public static Vector3 mousePosition
    {
        get
        {
            if (Touchscreen.current != null && (PrimaryTouchPressed || Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
                return Touchscreen.current.primaryTouch.position.ReadValue();
            return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
        }
    }

    public static bool GetMouseButtonDown(int button) =>
        Button(button, b => b.wasPressedThisFrame) || (button == 0 && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

    public static bool GetMouseButton(int button) =>
        Button(button, b => b.isPressed) || (button == 0 && PrimaryTouchPressed);

    public static bool GetMouseButtonUp(int button) =>
        Button(button, b => b.wasReleasedThisFrame) || (button == 0 && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame);

    // Old Input reports one notch of the wheel as 1; the Input System reports 120 on most platforms.
    public static Vector2 mouseScrollDelta
    {
        get
        {
            if (Mouse.current == null) return Vector2.zero;
            Vector2 v = Mouse.current.scroll.ReadValue();
            return Mathf.Abs(v.y) >= 10f ? v / 120f : v;
        }
    }

    public static int touchCount
    {
        get
        {
            if (Touchscreen.current == null) return 0;
            int n = 0;
            foreach (var t in Touchscreen.current.touches) if (t.press.isPressed) n++;
            return n;
        }
    }

    public static TouchInfo GetTouch(int index)
    {
        int n = 0;
        if (Touchscreen.current != null)
            foreach (var t in Touchscreen.current.touches)
                if (t.press.isPressed) { if (n == index) return new TouchInfo { position = t.position.ReadValue() }; n++; }
        return new TouchInfo { position = Vector2.zero };
    }

    static bool Button(int button, System.Func<UnityEngine.InputSystem.Controls.ButtonControl, bool> test)
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;
        var control = button == 0 ? mouse.leftButton : button == 1 ? mouse.rightButton : mouse.middleButton;
        return test(control);
    }
}
