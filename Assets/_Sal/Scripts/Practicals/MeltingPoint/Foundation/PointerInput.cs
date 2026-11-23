using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// One primary finger owns a gesture. Mouse works in the Editor.
public static class PointerInput
{
    static bool TouchActive { get { var t = Touchscreen.current; return t != null && (t.primaryTouch.press.isPressed || t.primaryTouch.press.wasReleasedThisFrame); } }
    public static bool Down { get { return TouchActive ? Touchscreen.current.primaryTouch.press.wasPressedThisFrame : Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame; } }
    public static bool Held { get { return TouchActive ? Touchscreen.current.primaryTouch.press.isPressed : Mouse.current != null && Mouse.current.leftButton.isPressed; } }
    public static bool Up { get { return TouchActive ? Touchscreen.current.primaryTouch.press.wasReleasedThisFrame : Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame; } }
    public static Vector2 Position { get { return TouchActive ? Touchscreen.current.primaryTouch.position.ReadValue() : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero; } }
    static readonly List<RaycastResult> Hits = new List<RaycastResult>();
    public static bool OverUI()
    {
        if (EventSystem.current == null) return false;
        Hits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Position }, Hits);
        return Hits.Count != 0;
    }
}
