using UnityEngine;
using TMPro;

/*
 * Layer: UI / presentation (instrument reading view - the "actual skill happens here" panel
 * called for by PRACTICAL_AUTHORING_GUIDE.md section 6: the 3D caliper prop only has to read as
 * "a caliper"; legible reading happens on a dedicated, crisp 2D panel, not by reading engraved
 * geometry through a cheap VR lens).
 * Purpose: Lets the student compose a vernier-caliper reading from a main-scale value (0.1cm
 * steps) plus a vernier-scale division (0.01cm steps - the caliper's least count, matching
 * PendulumExperimentController.caliperLeastCountMeters) and submit it for validation.
 *
 * FLAGGED SIMPLIFICATION (per CLAUDE.md's "flag assumptions" rule): a real vernier caliper is
 * read by finding which vernier division line coincides with a main-scale line, not by picking
 * two numbers from steppers. Rendering that coincidence accurately (two overlapping ruled scales
 * the student visually aligns) is a distinct, fairly involved 2D-rendering task in its own right
 * - similar in scope to the metre-scale reading view, which was explicitly deferred as a
 * follow-up in this same pass. This component builds the CORRECT underlying skill structure
 * (main-scale reading + a fine vernier division, summed, checked against a real least count)
 * without yet building the true dual-scale visual - swap ComposeReadingMeters's inputs for a
 * real coincidence-detecting scale widget later without changing anything else here.
 *
 * Talks to: opened by VernierCaliperTool.CaliperSelected; calls
 * PendulumExperimentController.SubmitCaliperReading on Confirm; subscribes to
 * PendulumExperimentController.StepChanged/ValidationFailed to show/hide and report rejections.
 */
public class VernierCaliperReadingUI : MonoBehaviour
{
    public PendulumExperimentController controller;
    public VernierCaliperTool caliperTool;
    public GameObject panel;

    [Header("Main scale (whole + tenths of a cm)")]
    [Tooltip("Step size for the main-scale reading, in centimetres.")]
    public float mainScaleStepCm = 0.1f;
    public float mainScaleMinCm = 0f;
    public float mainScaleMaxCm = 3f;

    [Header("Vernier scale (fine division - the caliper's least count)")]
    [Tooltip("Number of vernier divisions available, e.g. 10 divisions x 0.01cm least count.")]
    public int vernierDivisionCount = 10;

    public TextMeshProUGUI readoutText;

    private float mainScaleCm;
    private int vernierDivisionIndex;

    void OnEnable()
    {
        if (controller != null)
        {
            controller.StepChanged += HandleStepChanged;
            controller.ValidationFailed += HandleValidationFailed;
        }
        if (caliperTool != null)
            caliperTool.CaliperSelected += HandleCaliperSelected;

        SetPanelActive(false);
    }

    void OnDisable()
    {
        if (controller != null)
        {
            controller.StepChanged -= HandleStepChanged;
            controller.ValidationFailed -= HandleValidationFailed;
        }
        if (caliperTool != null)
            caliperTool.CaliperSelected -= HandleCaliperSelected;
    }

    private void HandleCaliperSelected()
    {
        if (controller.CurrentStep != PendulumStep.MeasuringDiameter) return;
        mainScaleCm = mainScaleMinCm;
        vernierDivisionIndex = 0;
        SetPanelActive(true);
        RefreshReadout();
    }

    private void HandleStepChanged(PendulumStep step)
    {
        // Once the reading is accepted the controller has already moved past
        // MeasuringDiameter, so close the panel regardless of what step we land on next.
        if (step != PendulumStep.MeasuringDiameter)
            SetPanelActive(false);
    }

    private void HandleValidationFailed(string message)
    {
        // A rejected reading re-opens the jaws visually so the student's next attempt reads as
        // a fresh measurement rather than the caliper looking like it's still clamped shut.
        if (controller.CurrentStep == PendulumStep.MeasuringDiameter && caliperTool != null)
            caliperTool.ReopenJaws();
    }

    // Wired to the main-scale +/- buttons' OnClick.
    public void IncrementMainScale() => SetMainScale(mainScaleCm + mainScaleStepCm);
    public void DecrementMainScale() => SetMainScale(mainScaleCm - mainScaleStepCm);

    // Wired to the vernier +/- buttons' OnClick.
    public void IncrementVernier() => SetVernierDivision(vernierDivisionIndex + 1);
    public void DecrementVernier() => SetVernierDivision(vernierDivisionIndex - 1);

    private void SetMainScale(float cm)
    {
        mainScaleCm = Mathf.Clamp(cm, mainScaleMinCm, mainScaleMaxCm);
        RefreshReadout();
    }

    private void SetVernierDivision(int index)
    {
        vernierDivisionIndex = Mathf.Clamp(index, 0, vernierDivisionCount - 1);
        RefreshReadout();
    }

    private float ComposeReadingMeters()
    {
        // main-scale reading + (vernier division x least count) - the standard vernier formula,
        // with the least count taken from the controller so the two stay in lockstep.
        float leastCountCm = controller.caliperLeastCountMeters * 100f;
        float totalCm = mainScaleCm + (vernierDivisionIndex * leastCountCm);
        return totalCm / 100f;
    }

    private void RefreshReadout()
    {
        if (readoutText == null) return;
        readoutText.text = $"Reading: {ComposeReadingMeters() * 100f:F2} cm";
    }

    // Wired to the Confirm Reading button's OnClick.
    public void ConfirmReading()
    {
        controller.SubmitCaliperReading(ComposeReadingMeters());
    }

    private void SetPanelActive(bool active)
    {
        if (panel != null) panel.SetActive(active);
    }
}
