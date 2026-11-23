using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (runtime helpers).
     * Purpose: Creates themed uGUI elements from code - used by the table and graph (which build
     * rows and axis labels at runtime) and by the scene builders. Every colour/font comes from
     * UITheme_Azm through ThemedImage_Azm / ThemedText_Azm, so nothing here hardcodes a style.
     * Sizes are in the 1920x1080 reference resolution.
     * Talks to: UITheme_Azm; creates GameObjects under the parent it is given.
     */
    public static class UIFactory_Azm
    {
        public const float TouchSize = 120f;

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void Pin(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
        }

        public static Image NewImage(Transform parent, string name, UITheme_Azm theme, ThemeColor_Azm role, bool rounded = true)
        {
            var rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            var themed = rect.gameObject.AddComponent<ThemedImage_Azm>();
            themed.theme = theme;
            themed.role = role;
            themed.rounded = rounded;
            themed.Apply();
            return image;
        }

        public static TextMeshProUGUI NewText(Transform parent, string name, UITheme_Azm theme, string text, float size,
            ThemeColor_Azm role = ThemeColor_Azm.TextPrimary, TextAlignmentOptions align = TextAlignmentOptions.Left,
            FontStyles style = FontStyles.Normal)
        {
            var rect = NewRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            var themed = rect.gameObject.AddComponent<ThemedText_Azm>();
            themed.theme = theme;
            themed.role = role;
            themed.Apply();
            return label;
        }

        // A rounded (or round) button with a centred label. Teal buttons use dark text.
        public static Button NewButton(Transform parent, string name, UITheme_Azm theme, string label, ThemeColor_Azm role,
            Vector2 size, float fontSize = 40f, bool circle = false)
        {
            var image = NewImage(parent, name, theme, role, !circle);
            image.rectTransform.sizeDelta = size;
            if (circle)
            {
                var themed = image.GetComponent<ThemedImage_Azm>();
                themed.rounded = false;
                themed.circle = true;   // the disc sprite is generated at run time by ThemedImage_Azm
                themed.Apply();
            }

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            colors.pressedColor = new Color(0.70f, 0.70f, 0.70f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var layout = image.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = size.x;
            layout.minHeight = size.y;
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;

            if (!string.IsNullOrEmpty(label))
            {
                var text = NewText(image.transform, "Label", theme, label, fontSize,
                    role == ThemeColor_Azm.Primary ? ThemeColor_Azm.OnPrimary : ThemeColor_Azm.TextPrimary,
                    TextAlignmentOptions.Center, FontStyles.Bold);
                Stretch(text.rectTransform, 10f, 4f, 10f, 4f);
            }
            return button;
        }

        // A button showing one of the theme's drawn icons (optionally with a caption under it).
        public static Button NewIconButton(Transform parent, string name, UITheme_Azm theme, ThemeIcon_Azm icon, ThemeColor_Azm role,
            Vector2 size, bool circle = false)
        {
            var button = NewButton(parent, name, theme, null, role, size, 40f, circle);
            var iconImage = NewImage(button.transform, "Icon", theme, ThemeColor_Azm.TextPrimary, false);
            var themedIcon = iconImage.gameObject.AddComponent<ThemedIcon_Azm>();
            themedIcon.theme = theme;
            themedIcon.icon = icon;
            themedIcon.Apply();
            iconImage.raycastTarget = false;
            var themed = iconImage.GetComponent<ThemedImage_Azm>();
            // The sprite is the icon and the tint is set by the caller, so the themed-colour component is not needed.
            if (Application.isPlaying) Object.Destroy(themed); else Object.DestroyImmediate(themed);
            float inset = Mathf.Min(size.x, size.y) * 0.22f;
            Stretch(iconImage.rectTransform, inset, inset, inset, inset);
            return button;
        }
    }
}
