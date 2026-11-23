using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VirtualLab.Physics141
{
    public sealed class OhmGraphInput : MonoBehaviour, IPointerClickHandler
    {
        internal Action<Vector2> Chosen;
        public void OnPointerClick(PointerEventData e)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local)) return;
            Chosen?.Invoke(new Vector2(Mathf.Clamp01(Mathf.InverseLerp(rect.rect.xMin, rect.rect.xMax, local.x)),
                Mathf.Clamp01(Mathf.InverseLerp(rect.rect.yMin, rect.rect.yMax, local.y))));
        }
    }
}
