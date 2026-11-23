using UnityEngine;

public class ThermometerAttachInteraction : MonoBehaviour
{
    public Transform capillaryTube;      // drag your Glass Test Tube here
    public Transform correctAttachPoint; // an empty GameObject marking the "correct" spot
    public Transform waterZoneTarget;    // position to move to when lowered into the beaker
    public float snapDistance = 0.05f;

    public void OnCapillaryDropped()
    {
        float distance = Vector3.Distance(capillaryTube.position, correctAttachPoint.position);

        if (distance <= snapDistance)
        {
            capillaryTube.position = correctAttachPoint.position;
            capillaryTube.rotation = correctAttachPoint.rotation;
            MeltingPointManager.Instance.thermometerAttachedCorrectly = true;
        }
        else
        {
            MeltingPointManager.Instance.thermometerAttachedCorrectly = false;
        }

        // Parent the tube so it now moves together with the thermometer assembly.
        capillaryTube.SetParent(transform, true);

        MeltingPointManager.Instance.currentState = MeltingPointManager.ExperimentState.Immersing;
    }

    // Called when the player lowers the whole assembly into the water bath.
    public void LowerIntoWater()
    {
        if (waterZoneTarget != null)
        {
            transform.position = waterZoneTarget.position;
        }
    }
}

