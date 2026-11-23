using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VirtualLab.Physics141
{
    public sealed class OhmDiagramTerminal : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, ICanvasRaycastFilter
    {
        internal Action<PointerEventData> Started, Moved, Released;
        internal Func<Vector2, Camera, bool> HitTest;

        public bool IsRaycastLocationValid(Vector2 point, Camera camera) => HitTest?.Invoke(point, camera) == true;

        public void OnBeginDrag(PointerEventData e)
        {
            e.eligibleForClick = false;
            Started?.Invoke(e);
        }

        public void OnDrag(PointerEventData e) => Moved?.Invoke(e);
        public void OnEndDrag(PointerEventData e) => Released?.Invoke(e);
    }
}
