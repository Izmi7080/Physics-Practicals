using System;
using UnityEngine;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Interaction (test / demo implementation).
     * Purpose: An IPracticalInput_Azm that is driven from code instead of a device. Lets automated
     * checks and demos play a gesture (down, drag, up, pinch...) through exactly the same events a
     * real finger would produce - the quickest proof that nothing above the interaction layer
     * depends on a particular device.
     * Talks to: raises the IPracticalInput_Azm events when its public methods are called.
     */
    public class ScriptedInput_Azm : MonoBehaviour, IPracticalInput_Azm
    {
        public event Action<Vector2> PointerDown;
        public event Action<Vector2, Vector2> PointerDrag;
        public event Action<Vector2> PointerUp;
        public event Action PointerCancel;
        public event Action<Vector2> DoubleTap;
        public event Action<float> Pinch;

        private Vector2 last;

        public void Down(Vector2 position) { last = position; PointerDown?.Invoke(position); }
        public void Drag(Vector2 position) { Vector2 d = position - last; last = position; PointerDrag?.Invoke(position, d); }
        public void Up(Vector2 position) { PointerUp?.Invoke(position); }
        public void Cancel() { PointerCancel?.Invoke(); }
        public void Double(Vector2 position) { DoubleTap?.Invoke(position); }
        public void Zoom(float factor) { Pinch?.Invoke(factor); }
    }
}
