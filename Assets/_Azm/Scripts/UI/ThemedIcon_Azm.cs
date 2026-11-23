using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: Puts one of the theme's code-drawn icons on an Image. The icon sprite is generated at
     * run time (it cannot be saved into a scene), so this component assigns it every time the object
     * wakes up - which is why icons use this component rather than a sprite assigned in the editor.
     * Talks to: reads UITheme_Azm.GetIcon; writes the sibling Image's sprite.
     */
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public class ThemedIcon_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public ThemeIcon_Azm icon;

        private void Awake() => Apply();
        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            if (theme == null) return;
            var image = GetComponent<Image>();
            image.sprite = theme.GetIcon(icon);
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
        }
    }
}
