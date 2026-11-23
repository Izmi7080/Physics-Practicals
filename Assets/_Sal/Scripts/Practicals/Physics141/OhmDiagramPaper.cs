using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Physics141
{
    public sealed class OhmDiagramPaper : RawImage
    {
        const int Spacing = 22;
        static Texture2D gridTile;

        protected override void Awake()
        {
            base.Awake();
            if (gridTile == null)
            {
                gridTile = new Texture2D(Spacing, Spacing, TextureFormat.RGBA32, false)
                    { name = "Diagram graph paper tile", wrapMode = TextureWrapMode.Repeat };
                var pixels = new Color32[Spacing * Spacing];
                var line = new Color32(255, 255, 255, 255);
                for (int i = 0; i < Spacing; i++)
                {
                    pixels[i] = line;
                    pixels[i * Spacing] = line;
                }
                gridTile.SetPixels32(pixels);
                gridTile.Apply(false, true);
            }
            texture = gridTile;
            raycastTarget = false;
            FitGrid();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            FitGrid();
        }

        void FitGrid()
        {
            var sheet = rectTransform.rect;
            uvRect = new Rect(0, 0, sheet.width / Spacing, sheet.height / Spacing);
        }
    }
}
