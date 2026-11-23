using TMPro;
using UnityEngine;

namespace VirtualLab.Azm.UI
{
    // The colour roles a UI element can ask the theme for. Elements store a ROLE, never a raw
    // colour, so restyling the whole app is a one-asset change.
    public enum ThemeColor_Azm
    {
        Panel,          // dark navy, ~85 % opaque - the standard panel background
        PanelStrong,    // same navy, almost opaque - pop-ups and dialogs
        Primary,        // teal - primary actions
        Secondary,      // muted blue - secondary buttons, unselected tabs
        Accent,         // amber - hints and highlights
        Valid,          // green
        Invalid,        // red
        Locked,         // grey - "coming soon" items
        Scrim,          // translucent black behind dialogs
        TextPrimary,    // white
        TextSecondary,  // soft grey-blue
        OnPrimary       // dark navy text that stays readable on teal
    }

    // Icons the theme can draw in code (see UITheme_Azm.GetIcon).
    public enum ThemeIcon_Azm
    {
        Table, Graph, Calculator, Book,
        Overview, Rig, Timing, Caliper,
        Pause, ChevronLeft, ChevronRight, Close,
        // Added for the HCI restructure (keep the old values first: scenes store the number).
        Lightbulb, Info, Check, Hand, Gauge
    }

    // The type scale (TASK_hci_and_juice Stage 1): five sizes used everywhere instead of ad-hoc
    // numbers. Sizes are in canvas units at the 1920x1080 reference.
    public enum TextStyle_Azm { Display, Title, Body, Label, Caption }

    /*
     * Layer: UI (shared style data).
     * Purpose: The single source of colours, font, corner radius and transition speed for every
     * panel in the app (CLAUDE.md: "visual style comes from one UITheme_Azm"). Style: dark navy
     * translucent panels, white text, teal primary actions, amber hints, green/red valid/invalid,
     * rounded corners (12-16 px).
     * Since the HCI restructure it also holds the DESIGN TOKENS: a five-step type scale, a spacing
     * scale, the screen margin and touch size, and motion durations, so every builder and HUD uses
     * the same numbers instead of literals.
     * Also generates the 9-sliced rounded-corner sprite at runtime from cornerRadius, so the
     * corner radius in this asset is the real corner radius on screen and no sprite art is needed.
     * Talks to: read by ThemedImage_Azm / ThemedText_Azm and by menu scripts that build UI in code.
     */
    [CreateAssetMenu(fileName = "UITheme_Azm", menuName = "VirtualLab_Azm/UI Theme")]
    public class UITheme_Azm : ScriptableObject
    {
        [Header("Panels")]
        public Color panelColor = new Color(0.09f, 0.13f, 0.22f, 0.85f);
        public Color panelStrongColor = new Color(0.09f, 0.13f, 0.22f, 0.97f);
        public Color scrimColor = new Color(0f, 0f, 0f, 0.6f);

        [Header("Actions")]
        public Color primaryColor = new Color(0.18f, 0.80f, 0.74f, 1f);
        public Color secondaryColor = new Color(0.20f, 0.27f, 0.40f, 1f);
        public Color accentColor = new Color(1f, 0.75f, 0.25f, 1f);
        public Color validColor = new Color(0.30f, 0.75f, 0.42f, 1f);
        public Color invalidColor = new Color(0.90f, 0.32f, 0.32f, 1f);
        public Color lockedColor = new Color(0.40f, 0.44f, 0.52f, 1f);

        [Header("Text")]
        public Color textPrimaryColor = Color.white;
        public Color textSecondaryColor = new Color(0.72f, 0.78f, 0.88f, 1f);
        [Tooltip("Text drawn on top of the teal primary colour (teal + white text has poor contrast).")]
        public Color onPrimaryTextColor = new Color(0.05f, 0.10f, 0.18f, 1f);
        [Tooltip("Rounded sans-serif TMP font (Poppins or Inter, SIL OFL). Leave empty to use TMP's default.")]
        public TMP_FontAsset font;

        [Header("Shape and motion")]
        [Range(12, 16)] public int cornerRadius = 14;
        [Tooltip("Panel slide/fade time in seconds (task spec: 0.2-0.3 s).")]
        public float transitionSeconds = 0.25f;

        // ----- Design tokens (HCI restructure, Stage 1). Builders use these instead of literals. -----
        [Header("Type scale (canvas units at 1920x1080)")]
        public float fontDisplay = 56f;    // results, big numbers
        public float fontTitle = 40f;      // panel titles, the instruction line
        public float fontBody = 32f;       // reading text, buttons
        public float fontLabel = 28f;      // field captions, tab labels
        public float fontCaption = 24f;    // small print (never smaller: readability on a 6" phone)
        [Tooltip("Multiplier for reading text when the Large text setting is on.")]
        [Range(1f, 1.4f)] public float largeTextScale = 1.15f;

        [Header("Spacing (canvas units)")]
        public float spaceXS = 8f;
        public float spaceS = 16f;
        public float spaceM = 24f;
        public float spaceL = 32f;
        public float spaceXL = 48f;
        [Tooltip("Distance of the HUD from the safe-area edge.")]
        public float screenMargin = 28f;
        [Tooltip("CLAUDE.md: touch targets at least ~48 dp = about 120 units at 1920x1080.")]
        public float touchSize = 120f;

        [Header("Motion (seconds)")]
        public float motionFast = 0.12f;
        public float motionNormal = 0.25f;
        public float motionSlow = 0.45f;

        public float FontSize(TextStyle_Azm style)
        {
            switch (style)
            {
                case TextStyle_Azm.Display: return fontDisplay;
                case TextStyle_Azm.Title: return fontTitle;
                case TextStyle_Azm.Label: return fontLabel;
                case TextStyle_Azm.Caption: return fontCaption;
                default: return fontBody;
            }
        }

        // Built lazily and not saved - recreated after a domain reload.
        [System.NonSerialized] private Sprite roundedSprite;
        [System.NonSerialized] private Sprite lockSprite;
        [System.NonSerialized] private Sprite circleSprite;
        [System.NonSerialized] private System.Collections.Generic.Dictionary<ThemeIcon_Azm, Sprite> iconCache;

        public Color GetColor(ThemeColor_Azm role)
        {
            switch (role)
            {
                case ThemeColor_Azm.Panel: return panelColor;
                case ThemeColor_Azm.PanelStrong: return panelStrongColor;
                case ThemeColor_Azm.Primary: return primaryColor;
                case ThemeColor_Azm.Secondary: return secondaryColor;
                case ThemeColor_Azm.Accent: return accentColor;
                case ThemeColor_Azm.Valid: return validColor;
                case ThemeColor_Azm.Invalid: return invalidColor;
                case ThemeColor_Azm.Locked: return lockedColor;
                case ThemeColor_Azm.Scrim: return scrimColor;
                case ThemeColor_Azm.TextPrimary: return textPrimaryColor;
                case ThemeColor_Azm.TextSecondary: return textSecondaryColor;
                case ThemeColor_Azm.OnPrimary: return onPrimaryTextColor;
                default: return Color.white;
            }
        }

        // A white rounded rectangle, 9-sliced so any size keeps the same corner radius. Use with
        // Image.type = Sliced and tint it with a theme colour.
        public Sprite GetRoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;

            int r = cornerRadius;
            int size = (r * 2) + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance from the nearest corner's circle centre; inside the straight
                    // edges the pixel is always fully opaque.
                    float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                    float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float alpha = Mathf.Clamp01(r - dist + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            // Border = corner radius on every side; ppu 100 matches the canvas reference ppu so
            // one sprite pixel is one canvas unit.
            roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            roundedSprite.hideFlags = HideFlags.HideAndDontSave;
            return roundedSprite;
        }

        // A white padlock icon drawn from simple shapes (distance fields), tinted by the Image
        // colour. Drawn in code so the menu needs no icon art; replace with a real sprite later by
        // assigning one directly on the card if preferred.
        public Sprite GetLockSprite()
        {
            if (lockSprite != null) return lockSprite;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);

                    // Body: rounded box, x 14..50, y 6..34.
                    Vector2 bodyCentre = new Vector2(32f, 20f);
                    Vector2 q = new Vector2(Mathf.Abs(p.x - bodyCentre.x), Mathf.Abs(p.y - bodyCentre.y)) - new Vector2(18f - 5f, 14f - 5f);
                    float body = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 5f;

                    // Shackle: the upper half of a ring centred on the top of the body.
                    float shackle = 1000f;
                    if (p.y >= 34f)
                        shackle = Mathf.Abs(Vector2.Distance(p, new Vector2(32f, 34f)) - 10.25f) - 2.75f;

                    float shape = Mathf.Min(body, shackle);
                    float alpha = Mathf.Clamp01(0.5f - shape);

                    // Keyhole: a dot with a slot under it, cut out of the body.
                    float dot = Vector2.Distance(p, new Vector2(32f, 23f)) - 3.5f;
                    float slot = Mathf.Max(Mathf.Abs(p.x - 32f) - 1.5f, Mathf.Abs(p.y - 17f) - 5f);
                    float keyhole = Mathf.Min(dot, slot);
                    alpha *= Mathf.Clamp01(0.5f + keyhole);

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            lockSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            lockSprite.hideFlags = HideFlags.HideAndDontSave;
            return lockSprite;
        }

        // A white anti-aliased disc (round buttons such as the timing tap button and pause button).
        public Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) - (size / 2f - 1f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
            }
            texture.Apply();
            circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            circleSprite.hideFlags = HideFlags.HideAndDontSave;
            return circleSprite;
        }

        // Simple white icons drawn in code from distance fields (no icon art needed). Tint them with
        // the Image colour. 64 x 64 px, y up.
        public Sprite GetIcon(ThemeIcon_Azm icon)
        {
            if (iconCache == null) iconCache = new System.Collections.Generic.Dictionary<ThemeIcon_Azm, Sprite>();
            if (iconCache.TryGetValue(icon, out Sprite cached) && cached != null) return cached;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = IconDistance(icon, new Vector2(x + 0.5f, y + 0.5f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            iconCache[icon] = sprite;
            return sprite;
        }

        // --- distance-field helpers: negative = inside the shape ---
        private static float SdBox(Vector2 p, Vector2 c, Vector2 half)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half;
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
        }
        private static float SdCircle(Vector2 p, Vector2 c, float r) => Vector2.Distance(p, c) - r;
        private static float SdRing(Vector2 p, Vector2 c, float r, float w) => Mathf.Abs(Vector2.Distance(p, c) - r) - w * 0.5f;
        private static float SdOutline(float sd, float w) => Mathf.Abs(sd) - w * 0.5f;
        private static float SdSeg(Vector2 p, Vector2 a, Vector2 b, float w)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - w * 0.5f;
        }

        private static float IconDistance(ThemeIcon_Azm icon, Vector2 p)
        {
            float d = 1000f;
            switch (icon)
            {
                case ThemeIcon_Azm.Table:
                    d = SdOutline(SdBox(p, new Vector2(32, 32), new Vector2(24, 20)), 4f);
                    d = Mathf.Min(d, SdSeg(p, new Vector2(8, 26), new Vector2(56, 26), 3f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(8, 38), new Vector2(56, 38), 3f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(24, 12), new Vector2(24, 52), 3f));
                    break;
                case ThemeIcon_Azm.Graph:
                    d = SdSeg(p, new Vector2(10, 10), new Vector2(10, 54), 4f);
                    d = Mathf.Min(d, SdSeg(p, new Vector2(10, 10), new Vector2(56, 10), 4f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(14, 16), new Vector2(28, 32), 4f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(28, 32), new Vector2(38, 26), 4f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(38, 26), new Vector2(54, 48), 4f));
                    break;
                case ThemeIcon_Azm.Calculator:
                    d = SdOutline(SdBox(p, new Vector2(32, 32), new Vector2(17, 26)) - 4f, 4f);
                    d = Mathf.Min(d, SdBox(p, new Vector2(32, 44), new Vector2(11, 5)));
                    for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                        d = Mathf.Min(d, SdCircle(p, new Vector2(23 + col * 9f, 31 - row * 9f), 3f));
                    break;
                case ThemeIcon_Azm.Book:
                    d = SdOutline(SdBox(p, new Vector2(20, 32), new Vector2(13, 20)), 4f);
                    d = Mathf.Min(d, SdOutline(SdBox(p, new Vector2(44, 32), new Vector2(13, 20)), 4f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(14, 40), new Vector2(26, 40), 3f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(38, 40), new Vector2(50, 40), 3f));
                    break;
                case ThemeIcon_Azm.Overview:
                    d = SdSeg(p, new Vector2(10, 22), new Vector2(10, 10), 4f);
                    d = Mathf.Min(d, SdSeg(p, new Vector2(10, 10), new Vector2(22, 10), 4f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(54, 22), new Vector2(54, 10), 4f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(54, 10), new Vector2(42, 10), 4f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(10, 42), new Vector2(10, 54), 4f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(10, 54), new Vector2(22, 54), 4f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(54, 42), new Vector2(54, 54), 4f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(54, 54), new Vector2(42, 54), 4f));
                    d = Mathf.Min(d, SdCircle(p, new Vector2(32, 32), 7f));
                    break;
                case ThemeIcon_Azm.Rig:
                    d = SdSeg(p, new Vector2(10, 56), new Vector2(54, 56), 5f);
                    d = Mathf.Min(d, SdSeg(p, new Vector2(30, 56), new Vector2(40, 24), 3f));
                    d = Mathf.Min(d, SdCircle(p, new Vector2(41, 18), 8f));
                    break;
                case ThemeIcon_Azm.Timing:
                    d = SdRing(p, new Vector2(32, 27), 19f, 4f);
                    d = Mathf.Min(d, SdBox(p, new Vector2(32, 54), new Vector2(5, 3)));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(32, 27), new Vector2(32, 41), 3.5f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(32, 27), new Vector2(42, 22), 3.5f));
                    break;
                case ThemeIcon_Azm.Caliper:
                    d = SdSeg(p, new Vector2(6, 22), new Vector2(58, 22), 5f);
                    d = Mathf.Min(d, SdSeg(p, new Vector2(12, 22), new Vector2(12, 52), 5f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(40, 22), new Vector2(40, 42), 5f));
                    break;
                case ThemeIcon_Azm.Pause:
                    d = Mathf.Min(SdBox(p, new Vector2(23, 32), new Vector2(5, 15)), SdBox(p, new Vector2(41, 32), new Vector2(5, 15)));
                    break;
                case ThemeIcon_Azm.ChevronLeft:
                    d = Mathf.Min(SdSeg(p, new Vector2(40, 12), new Vector2(22, 32), 6f), SdSeg(p, new Vector2(22, 32), new Vector2(40, 52), 6f));
                    break;
                case ThemeIcon_Azm.ChevronRight:
                    d = Mathf.Min(SdSeg(p, new Vector2(24, 12), new Vector2(42, 32), 6f), SdSeg(p, new Vector2(42, 32), new Vector2(24, 52), 6f));
                    break;
                case ThemeIcon_Azm.Close:
                    d = Mathf.Min(SdSeg(p, new Vector2(16, 16), new Vector2(48, 48), 6f), SdSeg(p, new Vector2(16, 48), new Vector2(48, 16), 6f));
                    break;
                case ThemeIcon_Azm.Lightbulb:
                    // Bulb (ring) on a two-line base.
                    d = SdRing(p, new Vector2(32, 38), 15f, 5f);
                    d = Mathf.Min(d, SdSeg(p, new Vector2(25, 18), new Vector2(39, 18), 5f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(27, 10), new Vector2(37, 10), 5f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(27, 25), new Vector2(25, 18), 4f));
                    d = Mathf.Min(d, SdSeg(p, new Vector2(37, 25), new Vector2(39, 18), 4f));
                    break;
                case ThemeIcon_Azm.Info:
                    d = SdRing(p, new Vector2(32, 32), 24f, 5f);
                    d = Mathf.Min(d, SdSeg(p, new Vector2(32, 18), new Vector2(32, 34), 6f));
                    d = Mathf.Min(d, SdCircle(p, new Vector2(32, 44), 4f));
                    break;
                case ThemeIcon_Azm.Check:
                    d = Mathf.Min(SdSeg(p, new Vector2(12, 32), new Vector2(26, 18), 7f), SdSeg(p, new Vector2(26, 18), new Vector2(52, 46), 7f));
                    break;
                case ThemeIcon_Azm.Hand:
                    // A pointing finger with a fist (the gesture coach).
                    d = SdBox(p, new Vector2(30, 20), new Vector2(13, 12)) - 4f;
                    d = Mathf.Min(d, SdSeg(p, new Vector2(24, 30), new Vector2(24, 56), 9f));
                    break;
                case ThemeIcon_Azm.Gauge:
                    // Screw gauge: U-frame, spindle and thimble.
                    d = SdOutline(SdBox(p, new Vector2(18, 30), new Vector2(10, 14)), 5f);
                    d = Mathf.Min(d, SdSeg(p, new Vector2(18, 38), new Vector2(46, 38), 4f));
                    d = Mathf.Min(d, SdBox(p, new Vector2(50, 38), new Vector2(7, 8)));
                    break;
            }
            return d;
        }
    }
}
