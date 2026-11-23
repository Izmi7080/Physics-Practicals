using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Physics141
{
    internal static class OhmSheet
    {
        internal static readonly Color Ink = new(.13f, .20f, .20f);
        internal static readonly Color Accent = new(.06f, .40f, .32f);
        internal static readonly Color Paper = new(.985f, .965f, .904f);
        internal static readonly Color GraphPaper = new(.981f, .982f, .945f);
        internal static readonly Color Note = new(.92f, .917f, .854f);
        internal static readonly Color Neutral = new(.86f, .89f, .837f);
        internal static readonly Color Rule = new(.75f, .77f, .71f);

        internal static RectTransform PinnedPaper(RectTransform board, string name)
        {
            Surface(board, new Color(.30f, .208f, .139f));
            Surface(OhmUi.Rect(board, "Paper drop shadow", .017f, .024f, .986f, .981f), new Color(.15f, .105f, .072f, .65f));
            var sheet = OhmUi.Rect(board, name, .013f, .031f, .981f, .982f);
            Surface(sheet, Paper);
            Tack(sheet, .025f);
            Tack(sheet, .975f);
            return sheet;
        }

        internal static void Surface(RectTransform rect, Color color)
        {
            var image = OhmUi.Paint(rect, color, false);
            image.sprite = PracticalUIController.RoundedSprite();
            image.type = Image.Type.Sliced;
        }

        static void Tack(RectTransform sheet, float x)
        {
            var circle = PracticalUIController.CircleSprite();
            var pin = OhmUi.Rect(sheet, "Board tack", x, .956f, x, .956f);
            pin.sizeDelta = new Vector2(24, 24);
            OhmUi.Paint(OhmUi.Rect(pin, "Tack shadow", .08f, -.16f, 1.14f, .90f), new Color(.35f, .22f, .12f, .38f), false).sprite = circle;
            OhmUi.Paint(OhmUi.Rect(pin, "Tack head", 0, 0, 1, 1), new Color(.15f, .40f, .43f), false).sprite = circle;
            OhmUi.Paint(OhmUi.Rect(pin, "Tack light", .20f, .54f, .52f, .86f), new Color(.66f, .84f, .79f), false).sprite = circle;
        }

        internal static Button PaperButton(Button button, Color surface, Color ink)
        {
            button.image.color = surface;
            var gloss = button.transform.Find("Glass highlight");
            if (gloss != null) gloss.gameObject.SetActive(false);
            button.GetComponentInChildren<Text>().color = ink;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.91f, .97f, .91f);
            colors.pressedColor = new Color(.79f, .86f, .78f);
            colors.disabledColor = new Color(.93f, .94f, .91f, .83f);
            button.colors = colors;
            return button;
        }
    }
}
