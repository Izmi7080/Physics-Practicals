using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VirtualLab.Physics81
{
    public sealed class LabWalkPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
    {
        [SerializeField] LabWalkRig rig;
        [SerializeField] bool isLookArea;
        [SerializeField] RectTransform handle;
        int? pointer;
        LabSetupSession placements;
        bool movingApparatus;

        public void Configure(LabWalkRig target, bool look, RectTransform knob = null)
        { rig = target; isLookArea = look; handle = knob; }

        internal static RectTransform CreateJoystick(Transform parent, LabWalkRig rig, Sprite circle)
        {
            var joystick = Circle("Walk joystick", parent, Vector2.zero, new Vector2(25, 25), new Vector2(176, 176),
                new Color(.06f, .20f, .24f, .80f), true);
            var stick = Circle("Stick", joystick, new Vector2(.5f, .5f), new Vector2(-29, -29), new Vector2(29, 29),
                PracticalUiStyle.ButtonFace, false);
            var caption = new GameObject("Stick text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var rect = caption.rectTransform;
            rect.SetParent(stick, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = 18;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.color = Color.white;
            caption.text = "Move";
            caption.raycastTarget = false;
            joystick.gameObject.AddComponent<LabWalkPad>().Configure(rig, false, stick);
            return joystick;

            RectTransform Circle(string name, Transform container, Vector2 anchor, Vector2 min, Vector2 max,
                Color tint, bool receivesInput)
            {
                var face = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                var bounds = face.rectTransform;
                bounds.SetParent(container, false);
                bounds.anchorMin = bounds.anchorMax = anchor;
                bounds.offsetMin = min;
                bounds.offsetMax = max;
                face.sprite = circle;
                face.color = tint;
                face.raycastTarget = receivesInput;
                return bounds;
            }
        }

        public void UseApparatus(LabSetupSession session) => placements = session;

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer.HasValue || e.button != PointerEventData.InputButton.Left) return;
            if (isLookArea && placements != null && placements.BeginPlacement(e.position, e.pointerId))
            {
                movingApparatus = true;
                pointer = e.pointerId;
                return;
            }
            if (!rig.CanWalk || isLookArea && e.position.x < Screen.width * .42f) return;
            pointer = e.pointerId;
            if (!isLookArea) UpdateStick(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            if (movingApparatus) { placements.MovePlacement(e.position, e.pointerId); return; }
            if (!rig.CanWalk) return;
            if (isLookArea) rig.Look(e.delta);
            else UpdateStick(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            if (movingApparatus) placements.EndPlacement(e.position, e.pointerId);
            movingApparatus = false;
            ResetPad();
        }

        void UpdateStick(PointerEventData e)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out Vector2 local)) return;
            float reach = Mathf.Min(rect.rect.width, rect.rect.height) * .33f;
            Vector2 raw = Vector2.ClampMagnitude((local - rect.rect.center) / Mathf.Max(1f, reach), 1f);
            if (handle != null) handle.anchoredPosition = raw * reach;
            float magnitude = Mathf.InverseLerp(.1f, 1f, raw.magnitude);
            rig.SetWalk(raw.normalized * magnitude);
        }

        public void ResetPad()
        {
            if (movingApparatus && pointer.HasValue && placements.OwnsPlacement(pointer.Value)) placements.CancelPlacement();
            movingApparatus = false;
            pointer = null;
            if (isLookArea) return;
            if (rig != null) rig.SetWalk(Vector2.zero);
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }

        void Update()
        {
            if (pointer.HasValue && (movingApparatus ? !placements.OwnsPlacement(pointer.Value) : !rig.CanWalk)) ResetPad();
        }
        void OnDisable() => ResetPad();
        void OnApplicationFocus(bool focused) { if (!focused) ResetPad(); }
        void OnApplicationPause(bool paused) { if (paused) ResetPad(); }
    }
}
