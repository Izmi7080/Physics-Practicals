using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Physics141;

public partial class PracticalUIController
{
    RectTransform PaperDocument(string name, out RectTransform sheet)
    {
        var overlay = Panel(name, safe, Vector2.zero, Vector2.one, new Color(0, 0, 0, .44f));
        overlay.GetComponent<Image>().raycastTarget = true;
        var board = Rect("Document board", overlay, new Vector2(.04f, .055f), new Vector2(.96f, .945f));
        sheet = OhmSheet.PinnedPaper(board, "Practical worksheet paper");
        return overlay;
    }

    static void StylePaperContents(RectTransform sheet)
    {
        foreach (var text in sheet.GetComponentsInChildren<Text>(true)) text.color = OhmSheet.Ink;
        foreach (var button in sheet.GetComponentsInChildren<Button>(true))
            OhmSheet.PaperButton(button, OhmSheet.Neutral, OhmSheet.Ink);
    }

    static void SelectPaperTab(Button button, bool selected)
    {
        button.targetGraphic.color = selected ? OhmSheet.Accent : OhmSheet.Neutral;
        button.GetComponentInChildren<Text>().color = selected ? Color.white : OhmSheet.Ink;
    }
}
