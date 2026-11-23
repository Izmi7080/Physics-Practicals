using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VirtualLab.Physics141
{
    internal static class OhmUi
    {
        internal static RectTransform Rect(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal static Image Paint(RectTransform rect, Color color, bool raycast = true)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        internal static Text Label(Transform parent, string value, float left, float bottom, float right, float top, int size)
        {
            var rect = Rect(parent, value, left, bottom, right, top);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = size;
            text.fontSize = size;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        internal static InputField Field(Transform parent, string name, float left, float bottom, float right, float top,
            bool numeric, UnityAction<string> changed)
        {
            var rect = Rect(parent, name, left, bottom, right, top);
            var face = Paint(rect, PracticalUiStyle.Entry);
            var text = Label(rect, "", .05f, .05f, .95f, .95f, 24);
            text.color = PracticalUiStyle.Ink;
            var field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = face;
            field.textComponent = text;
            field.lineType = numeric ? InputField.LineType.SingleLine : InputField.LineType.MultiLineNewline;
            field.characterLimit = numeric ? 10 : 350;
            field.contentType = numeric ? InputField.ContentType.DecimalNumber : InputField.ContentType.Standard;
            field.onValueChanged.AddListener(changed);
            return field;
        }

        internal static Slider Slider(Transform parent, string name, float left, float bottom, float right, float top,
            float min, float max, float value, UnityAction<float> changed)
        {
            var root = Rect(parent, name, left, bottom, right, top);
            var track = Rect(root, "Track", .02f, .34f, .98f, .66f);
            Paint(track, PracticalUiStyle.Panel, false);
            var area = Rect(root, "Handle area", .045f, 0, .955f, 1);
            var handle = Rect(area, "Handle", 0, .12f, 0, .88f);
            handle.sizeDelta = new Vector2(42, 0);
            var face = Paint(handle, PracticalUiStyle.ButtonFace);
            PracticalUiStyle.StyleTextFace(face);
            var background = Paint(root, Color.clear);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.targetGraphic = background;
            slider.handleRect = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(changed);
            return slider;
        }

        internal static Button Button(Transform parent, string caption, float left, float bottom, float right, float top, UnityAction action)
        {
            var rect = Rect(parent, caption, left, bottom, right, top);
            var face = rect.gameObject.AddComponent<Image>();
            PracticalUiStyle.StyleTextFace(face);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            PracticalUiStyle.ApplyButtonStates(button);
            button.onClick.AddListener(action);
            Label(rect, caption, .035f, .03f, .965f, .97f, 23);
            return button;
        }
    }
}
