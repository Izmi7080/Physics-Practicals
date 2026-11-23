using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Physics141
{
    internal enum OhmSymbol { Supply, Key, Rheostat, Ammeter, Nichrome, Voltmeter }

    internal static class OhmCircuitSymbols
    {
        const int Resolution = 112;
        internal const float LeadReach = .44f;
        static readonly Sprite[] Sprites = new Sprite[6];

        internal static Image Create(Transform parent, OhmSymbol symbol, float size, Color ink)
        {
            var rect = OhmUi.Rect(parent, symbol + " symbol", .5f, .5f, .5f, .5f);
            rect.sizeDelta = Vector2.one * size;
            var image = OhmUi.Paint(rect, ink, false);
            int id = (int)symbol;
            if (Sprites[id] == null) Sprites[id] = Draw(symbol);
            image.sprite = Sprites[id];
            if (symbol is OhmSymbol.Ammeter or OhmSymbol.Voltmeter)
            {
                var letter = OhmUi.Label(rect, symbol == OhmSymbol.Ammeter ? "A" : "V", .24f, .24f, .76f, .76f,
                    Mathf.RoundToInt(size * .43f));
                letter.color = ink;
                letter.fontStyle = FontStyle.Bold;
            }
            return image;
        }

        static Sprite Draw(OhmSymbol symbol)
        {
            var opacity = new float[Resolution * Resolution];
            if (symbol == OhmSymbol.Supply)
            {
                Path(new(-.30f, -.16f), new(.30f, -.16f), new(.30f, .16f), new(-.30f, .16f), new(-.30f, -.16f));
                Line(-LeadReach, 0, -.19f, 0);
                Line(.19f, 0, LeadReach, 0);
                Line(-.235f, .074f, -.14f, .074f);
                Line(-.188f, .026f, -.188f, .12f);
                Line(.14f, .074f, .235f, .074f);
            }
            else if (symbol == OhmSymbol.Key)
            {
                Line(-LeadReach, 0, -.18f, 0);
                Line(.18f, 0, LeadReach, 0);
                Line(-.18f, .025f, .13f, .17f);
                Circle(-.18f, 0, .027f);
                Circle(.18f, 0, .027f);
            }
            else if (symbol is OhmSymbol.Ammeter or OhmSymbol.Voltmeter)
            {
                Circle(0, 0, .268f);
                if (symbol == OhmSymbol.Ammeter)
                {
                    Line(0, -LeadReach, 0, -.268f);
                    Line(0, .268f, 0, LeadReach);
                }
                else
                {
                    Line(-LeadReach, 0, -.268f, 0);
                    Line(.268f, 0, LeadReach, 0);
                }
            }
            else
            {
                Path(new(-LeadReach, 0), new(-.31f, 0), new(-.259f, .094f), new(-.155f, -.094f),
                    new(-.051f, .094f), new(.051f, -.094f), new(.155f, .094f), new(.259f, -.094f), new(.31f, 0), new(LeadReach, 0));
                if (symbol == OhmSymbol.Rheostat)
                {
                    Line(-.19f, -.20f, .195f, .21f);
                    Path(new(.093f, .175f), new(.195f, .21f), new(.169f, .102f));
                }
            }

            var pixels = new Color32[opacity.Length];
            for (int i = 0; i < opacity.Length; i++) pixels[i] = new Color32(255, 255, 255, (byte)(255 * opacity[i]));
            var sheet = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
                { name = symbol + " ink", wrapMode = TextureWrapMode.Clamp };
            sheet.SetPixels32(pixels);
            sheet.Apply(false, true);
            return Sprite.Create(sheet, new Rect(0, 0, Resolution, Resolution), new Vector2(.5f, .5f));

            void Path(params Vector2[] points)
            {
                for (int i = 1; i < points.Length; i++)
                    Line(points[i - 1].x, points[i - 1].y, points[i].x, points[i].y);
            }

            void Line(float x, float y, float x2, float y2)
            {
                var from = new Vector2(x, y);
                var delta = new Vector2(x2, y2) - from;
                Ink(point => Vector2.Distance(point, from + delta * Mathf.Clamp01(Vector2.Dot(point - from, delta) / delta.sqrMagnitude)));
            }

            void Circle(float x, float y, float radius) =>
                Ink(point => Mathf.Abs(Vector2.Distance(point, new Vector2(x, y)) - radius));

            void Ink(System.Func<Vector2, float> distance)
            {
                for (int y = 0; y < Resolution; y++)
                for (int x = 0; x < Resolution; x++)
                {
                    var point = new Vector2((x + .5f) / Resolution - .5f, (y + .5f) / Resolution - .5f);
                    int index = y * Resolution + x;
                    opacity[index] = Mathf.Max(opacity[index], Mathf.Clamp01((.023f - distance(point)) * Resolution));
                }
            }
        }
    }
}
