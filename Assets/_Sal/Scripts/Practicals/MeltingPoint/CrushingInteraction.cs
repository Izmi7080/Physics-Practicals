using UnityEngine;

public class CrushingInteraction : MonoBehaviour
{
    public int tapsRequired = 5;
    private int currentTaps = 0;

    // Call this from a Button's OnClick (or a tap/touch event on the naphthalene object)
    public void OnTapCrush()
    {
        currentTaps++;
        if (currentTaps >= tapsRequired)
        {
            MeltingPointManager.Instance.naphthaleneWellCrushed = true;
            MeltingPointManager.Instance.currentState = MeltingPointManager.ExperimentState.Packing;
        }
    }

    // Optional shortcut button: "skip" but with a quality penalty
    public void SkipCrushing()
    {
        MeltingPointManager.Instance.naphthaleneWellCrushed = false;
        MeltingPointManager.Instance.currentState = MeltingPointManager.ExperimentState.Packing;
    }
}