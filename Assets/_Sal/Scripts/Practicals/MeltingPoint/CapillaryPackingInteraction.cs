using UnityEngine;

public class CapillaryPackingInteraction : MonoBehaviour
{
    public int tapsRequired = 4;
    private int currentTaps = 0;

    public void OnTapPack()
    {
        currentTaps++;
        if (currentTaps >= tapsRequired)
        {
            MeltingPointManager.Instance.capillaryWellPacked = true;
            MeltingPointManager.Instance.currentState = MeltingPointManager.ExperimentState.Attaching;
        }
    }

    public void SkipPacking()
    {
        MeltingPointManager.Instance.capillaryWellPacked = false;
        MeltingPointManager.Instance.currentState = MeltingPointManager.ExperimentState.Attaching;
    }
}