using UnityEngine;
using UnityEngine.EventSystems;
using VirtualLab.Physics81;

namespace VirtualLab.Physics141
{
    public sealed class OhmSliderGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        internal LabWalkRig Rig;
        int? pointer;
        public void OnPointerDown(PointerEventData e)
        {
            if (pointer.HasValue) return;
            pointer = e.pointerId;
            Rig.LockMovement(true);
        }

        public void OnPointerUp(PointerEventData e) { if (pointer == e.pointerId) Release(); }
        void OnDisable() => Release();
        void OnApplicationFocus(bool focused) { if (!focused) Release(); }
        void Release()
        {
            if (!pointer.HasValue) return;
            pointer = null;
            if (Rig != null) Rig.LockMovement(false);
        }
    }
}
