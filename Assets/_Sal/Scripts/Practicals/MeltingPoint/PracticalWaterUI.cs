using System.Collections;
using UnityEngine;

public partial class PracticalUIController
{
    CanvasGroup waterTransferFade;
    string beakerOptionsStep;
    void BuildWaterControls()
    {
        AddContext("Empty",()=>session.At("water")&&session.waterFill.atSink&&session.waterFill.volumeMl>0&&!session.waterFill.tapOn&&!session.CanReturnWater,()=>session.EmptyBathWater(),()=>session.CanInteract);
        StyleSecondaryAction(AddContext("Refill water",()=>beakerOptionsStep==session.StepId&&session.CanRefillWater&&session.preparation.water,() =>
        {
            CloseBeakerOptions();
            session.RefillWater();
        }));
        var shade = Panel("Water station transition", safe, Vector2.zero, Vector2.one, new Color(.045f,.08f,.092f));
        shade.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        waterTransferFade = shade.gameObject.AddComponent<CanvasGroup>();
        ResetWaterTransferFade();
    }
    internal void ToggleBeakerOptions()
    {
        ClosePackingOptions();
        beakerOptionsStep=beakerOptionsStep==session.StepId?null:session.StepId;
    }
    internal void CloseBeakerOptions() => beakerOptionsStep=null;
    internal IEnumerator FadeWaterTransfer(float target)
    {
        waterTransferFade.transform.SetAsLastSibling();
        waterTransferFade.gameObject.SetActive(true);
        while (!Mathf.Approximately(waterTransferFade.alpha, target))
        {
            waterTransferFade.alpha = Mathf.MoveTowards(waterTransferFade.alpha, target, Time.unscaledDeltaTime / .15f);
            yield return null;
        }
        if (target == 0) waterTransferFade.gameObject.SetActive(false);
    }

    void ResetWaterTransferFade()
    {
        if (waterTransferFade == null) return;
        waterTransferFade.alpha = 0;
        waterTransferFade.gameObject.SetActive(false);
    }
}
