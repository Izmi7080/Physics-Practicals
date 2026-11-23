using UnityEngine;

public class BurnerController : MonoBehaviour
{
    public GameObject flameVisual; // optional particle effect or flame sprite
    private bool isLit = false;

    public void LightBurner()
    {
        if (isLit) return;
        isLit = true;
        if (flameVisual != null) flameVisual.SetActive(true);
        MeltingPointManager.Instance.StartHeating();
    }

    public void ExtinguishBurner()
    {
        isLit = false;
        if (flameVisual != null) flameVisual.SetActive(false);
    }
}
