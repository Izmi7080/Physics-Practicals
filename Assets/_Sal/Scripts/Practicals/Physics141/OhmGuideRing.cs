using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Physics141
{
    public sealed class OhmGuideRing : Image
    {
        static Sprite ringSprite;

        internal void Configure()
        {
            if (ringSprite == null)
            {
                const int size = 96;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { name = "Ohm guide ring", wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(47.5f, 47.5f));
                    float alpha = Mathf.Clamp01(3.5f - Mathf.Abs(distance - 41));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * alpha));
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
            }
            sprite = ringSprite;
            raycastTarget = false;
        }
    }
}
