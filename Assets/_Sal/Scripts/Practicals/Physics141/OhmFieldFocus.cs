using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VirtualLab.Physics141
{
    public sealed class OhmFieldFocus : MonoBehaviour, ISelectHandler
    {
        internal Action Focused;
        public void OnSelect(BaseEventData eventData) => Focused?.Invoke();
    }
}
