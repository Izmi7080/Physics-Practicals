using System;
using UnityEngine;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Interaction (contract).
     * Purpose: How the student's hand reaches a practical, independent of the device. Everything
     * above this layer (logic, UI, camera control) reacts to these events and never reads the
     * mouse, touchscreen or keyboard itself - so the Editor mouse, a phone's touchscreen, a
     * scripted test, or a future VR input can be swapped without touching any other script
     * (CLAUDE.md three-layer rule).
     * Only pointers that BEGAN over the 3D view are reported: a finger that starts on a button or
     * panel belongs to the UI and never reaches these events, so touching UI cannot also move the
     * camera or drag the bob.
     * Talks to: implemented by TouchMouseInput_Azm and ScriptedInput_Azm; consumed by the bob-drag
     * interaction and the camera view controller.
     */
    public interface IPracticalInput_Azm
    {
        // A single pointer (finger or mouse button) went down on the 3D view. Screen pixels.
        event Action<Vector2> PointerDown;

        // The pointer moved while down. Arguments: current position, movement since last call (px).
        event Action<Vector2, Vector2> PointerDrag;

        // The pointer was released normally.
        event Action<Vector2> PointerUp;

        // The gesture was interrupted (e.g. a second finger landed to pinch) - no normal release.
        event Action PointerCancel;

        // Two quick taps in the same place.
        event Action<Vector2> DoubleTap;

        // Pinch / scroll wheel zoom as a factor: > 1 means fingers spread apart (zoom in).
        event Action<float> Pinch;
    }
}
