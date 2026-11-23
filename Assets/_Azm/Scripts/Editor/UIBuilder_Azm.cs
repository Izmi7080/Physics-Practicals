using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.EditorTools
{
    /*
     * Layer: Editor tooling (not part of the shipped app).
     * Purpose: Small helpers that create themed uGUI elements (panels, labels, buttons, sliders,
     * toggles, scroll views) in code, so the scene builders can lay out whole screens repeatably.
     * Every element takes its colours from UITheme_Azm through ThemedImage_Azm / ThemedText_Azm -
     * nothing here hardcodes a colour. Sizes are in the 1920x1080 reference resolution, and
     * interactive controls are at least ~120 px high (CLAUDE.md touch-target rule).
     * Talks to: used by ShellBuilder_Azm (and later scene builders); creates GameObjects in the
     * currently open scene.
     */
    public static class UIBuilder_Azm
    {
        public const float TouchSize = 120f;

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // Fill the parent, with optional insets (left, bottom, right, top).
        public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // Pin to a single anchor point with a fixed size: anchor/pivot in 0..1, pos in canvas units.
        public static void Pin(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
        }

        public static Image NewPanel(Transform parent, string name, UITheme_Azm theme, ThemeColor_Azm role, bool rounded = true)
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

        public static Button NewButton(Transform parent, string name, UITheme_Azm theme, string label, ThemeColor_Azm role,
            Vector2 size, float fontSize = 44f)
        {
            var rect = NewRect(name, parent);
            rect.sizeDelta = size;

            var image = rect.gameObject.AddComponent<Image>();
            var themed = rect.gameObject.AddComponent<ThemedImage_Azm>();
            themed.theme = theme;
            themed.role = role;
            themed.rounded = true;
            themed.Apply();

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            // Visible press state: the button darkens slightly while pressed (Stage 4 adds sound).
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            colors.pressedColor = new Color(0.70f, 0.70f, 0.70f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = size.x;
            layout.minHeight = size.y;
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;

            // Teal buttons use dark text for contrast; everything else uses the primary text colour.
            var text = NewText(rect, "Label", theme, label, fontSize,
                role == ThemeColor_Azm.Primary ? ThemeColor_Azm.OnPrimary : ThemeColor_Azm.TextPrimary,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(text.rectTransform, 12f, 6f, 12f, 6f);
            return button;
        }

        public static Slider NewSlider(Transform parent, string name, UITheme_Azm theme, Vector2 size)
        {
            var root = NewRect(name, parent);
            root.sizeDelta = size;
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = size.x;
            layout.minHeight = size.y;
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;

            // Track
            var background = NewPanel(root, "Background", theme, ThemeColor_Azm.Secondary);
            background.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            background.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            background.rectTransform.offsetMin = new Vector2(0f, -14f);
            background.rectTransform.offsetMax = new Vector2(0f, 14f);

            // Filled part of the track
            var fillArea = NewRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(0f, -14f);
            fillArea.offsetMax = new Vector2(0f, 14f);
            var fill = NewPanel(fillArea, "Fill", theme, ThemeColor_Azm.Primary);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            // Handle - big enough to grab with a thumb.
            var slideArea = NewRect("Handle Slide Area", root);
            slideArea.anchorMin = new Vector2(0f, 0.5f);
            slideArea.anchorMax = new Vector2(1f, 0.5f);
            slideArea.offsetMin = new Vector2(40f, -40f);
            slideArea.offsetMax = new Vector2(-40f, 40f);
            var handle = NewPanel(slideArea, "Handle", theme, ThemeColor_Azm.TextPrimary);
            handle.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(80f, 0f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        public static Toggle NewToggle(Transform parent, string name, UITheme_Azm theme, string label, Vector2 size)
        {
            var root = NewRect(name, parent);
            root.sizeDelta = size;
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = size.x;
            layout.minHeight = size.y;
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;

            var box = NewPanel(root, "Background", theme, ThemeColor_Azm.Secondary);
            Pin(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 84f), Vector2.zero);

            var check = NewPanel(box.transform, "Checkmark", theme, ThemeColor_Azm.Primary);
            Stretch(check.rectTransform, 16f, 16f, 16f, 16f);

            var text = NewText(root, "Label", theme, label, 40f);
            Stretch(text.rectTransform, 110f, 0f, 0f, 0f);
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = true;
            return toggle;
        }

        // A vertical scroll view; returns the content rect to put items under.
        public static RectTransform NewVerticalScroll(Transform parent, string name, out ScrollRect scroll)
        {
            var root = NewRect(name, parent);
            scroll = root.gameObject.AddComponent<ScrollRect>();

            var viewport = NewRect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);   // invisible, but lets a finger drag the list
            catcher.raycastTarget = true;

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            return content;
        }
    }
}
