using UnityEngine;
using TMPro;

public class PendulumController : MonoBehaviour
{
    [Header("Physics Parameters (Meters)")]
    public float gravity = 9.8f;
    public float bobRadius = 0.0125f; // 1.25 cm
    public float threadLength = 0.6f; // Start at 60 cm
    public float maxAngle = 10f; // Small angle approximation

    [Header("Hierarchy References")]
    public Transform pivot;        // Empty GameObject at the top (Rotation point)
    public Transform threadVisual; // Cylinder mesh
    public Transform bobVisual;    // Sphere mesh

    [Header("UI References")]
    public TextMeshProUGUI lengthText;

    private bool isCocked = false;
    private bool isSwinging = false;
    private float timer = 0f;

    void Start()
    {
        UpdatePendulumLength(threadLength);
    }

    // Hook this method to your UI Slider's "OnValueChanged (Single)" event
    public void UpdatePendulumLength(float newLengthInMeters)
    {
        if (isSwinging) return; // Prevent length changes during an active run

        threadLength = newLengthInMeters;
        float totalLength = threadLength + bobRadius;

        // Scale the thread cylinder (assuming default Unity cylinder height is 2 units)
        threadVisual.localScale = new Vector3(threadVisual.localScale.x, threadLength / 2f, threadVisual.localScale.z);
        threadVisual.localPosition = new Vector3(0, -threadLength / 2f, 0);

        // Move the bob to the end of the total length
        bobVisual.localPosition = new Vector3(0, -totalLength, 0);

        // Display to the student in cm
        lengthText.text = $"Thread Length (l): {(threadLength * 100f):F1} cm";
    }

    // Triggered by clicking a collider on the metallic bob
    void OnMouseDown()
    {
        if (!isCocked && !isSwinging)
        {
            // First click: Cock the pendulum to the starting angle
            isCocked = true;
            pivot.localRotation = Quaternion.Euler(maxAngle, 0, 0);
        }
        else if (isCocked && !isSwinging)
        {
            // Second click: Release and start swinging
            isCocked = false;
            isSwinging = true;
            timer = 0f;
        }
    }

    void Update()
    {
        if (isSwinging)
        {
            timer += Time.deltaTime;
            float totalLength = threadLength + bobRadius;

            // Simple Harmonic Motion: θ(t) = θ_max * cos(ωt) where ω = sqrt(g/L)
            float angularFrequency = Mathf.Sqrt(gravity / totalLength);
            float currentAngle = maxAngle * Mathf.Cos(angularFrequency * timer);

            pivot.localRotation = Quaternion.Euler(currentAngle, 0, 0);
        }
    }
}