using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (celebration effect; no logic).
     * Purpose: A small, cheap confetti burst in the theme colours (teal, amber, green, white) for a
     * genuinely good result. 28 plain UI squares are created once and re-used; each Play() flings
     * them outward from this rect's centre with a spin and a fade (Motion_Azm.Fling). With Reduce
     * motion on, nothing moves. Kept short (about 1 s) so it rewards without distracting.
     * Talks to: Motion_Azm, UITheme_Azm (colours).
     */
    [RequireComponent(typeof(RectTransform))]
    public class ConfettiBurst_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public int pieces = 28;
        public float spread = 520f;
        public float duration = 1.1f;

        private readonly List<Image> pool = new List<Image>();

        private void Build()
        {
            if (pool.Count > 0) return;
            ThemeColor_Azm[] roles = { ThemeColor_Azm.Primary, ThemeColor_Azm.Accent, ThemeColor_Azm.Valid, ThemeColor_Azm.TextPrimary };
            for (int i = 0; i < pieces; i++)
            {
                var rect = UIFactory_Azm.NewRect("Confetti" + i, transform);
                rect.sizeDelta = new Vector2(i % 2 == 0 ? 18f : 12f, i % 2 == 0 ? 10f : 18f);
                var image = rect.gameObject.AddComponent<Image>();
                image.color = theme != null ? theme.GetColor(roles[i % roles.Length]) : Color.white;
                image.raycastTarget = false;
                rect.gameObject.SetActive(false);
                pool.Add(image);
            }
        }

        public void Play()
        {
            Build();
            for (int i = 0; i < pool.Count; i++)
            {
                var image = pool[i];
                var rect = image.rectTransform;
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.identity;
                var c = image.color; c.a = 1f; image.color = c;
                // Mostly upward and outward, like a party popper.
                float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                float distance = Random.Range(0.45f, 1f) * spread;
                var to = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance * 0.7f - Random.Range(0f, 120f));
                Motion_Azm.Fling(rect, image, to, Random.Range(-540f, 540f), duration * Random.Range(0.8f, 1.15f));
            }
        }
    }
}
