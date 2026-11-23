using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: Makes an Image take its colour (and optional rounded-corner sprite) from the
     * UITheme_Azm by ROLE, so no panel hardcodes a colour. Applies in the editor whenever the
     * role or theme changes, and again at runtime.
     * Talks to: reads UITheme_Azm; writes the sibling Image component.
     */
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public class ThemedImage_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public ThemeColor_Azm role = ThemeColor_Azm.Panel;
        [Tooltip("Use the theme's rounded 9-sliced sprite (panels, buttons, cards).")]
        public bool rounded = true;
        [Tooltip("Use the theme's round disc sprite instead (round buttons). Overrides 'rounded'.")]
        public bool circle;

        private void Awake() => Apply();
        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        // Also callable from code after swapping the theme or role at runtime.
        public void Apply()
        {
            if (theme == null) return;
            var image = GetComponent<Image>();
            image.color = theme.GetColor(role);
            if (circle)
            {
                image.sprite = theme.GetCircleSprite();
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }
            else if (rounded)
            {
                image.sprite = theme.GetRoundedSprite();
                image.type = Image.Type.Sliced;
            }
        }

        public void SetRole(ThemeColor_Azm newRole)
        {
            role = newRole;
            Apply();
        }
    }
}
