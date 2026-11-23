using UnityEngine;

// Put one of these on the WaterZone trigger (main body of water) and another
// on a thin WallZone trigger hugging the inner glass wall. Both react to the
// SAME tag ("ThermometerAssembly") since that's the only thing that moves.
// Note: entering the water no longer auto-starts heating - lighting the
// burner (BurnerController.LightBurner) is what actually starts it.
public class ImmersionController : MonoBehaviour
{
    [Tooltip("Check this on the thin WallZone collider that hugs the inner glass wall.")]
    public bool isWallZone = false;

    private void OnTriggerStay(Collider other)
    {
        if (isWallZone && other.CompareTag("ThermometerAssembly"))
        {
            MeltingPointManager.Instance.thermometerTouchingGlass = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (isWallZone && other.CompareTag("ThermometerAssembly"))
        {
            MeltingPointManager.Instance.thermometerTouchingGlass = false;
        }
    }
}
