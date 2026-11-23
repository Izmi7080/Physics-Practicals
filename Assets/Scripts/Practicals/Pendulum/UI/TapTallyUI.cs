using UnityEngine;
using TMPro;

/*
 * Layer: UI / presentation
 * Purpose: Shows the running oscillation tap tally ("Run 1: 14 / 20") during the Swinging step
 * (TASK_pendulum_demo_v2.md item 4), so the student always knows where they are in the count.
 * Stage 1 (TASK_system_architecture.md) adds the run number to the label, since Swinging is now
 * entered twice per trial (two timing runs) and the tally alone doesn't say which run this is.
 * Talks to: subscribes to PendulumExperimentController.OscillationTapped/StepChanged; reads
 * CurrentRunIndex.
 */
public class TapTallyUI : MonoBehaviour
{
    public PendulumExperimentController controller;
    public TextMeshProUGUI tallyText;
    public GameObject panel;

    void OnEnable()
    {
        controller.OscillationTapped += HandleTapped;
        controller.StepChanged += HandleStepChanged;
        HandleStepChanged(controller.CurrentStep); // sync visibility to whatever step we're already in
    }

    void OnDisable()
    {
        controller.OscillationTapped -= HandleTapped;
        controller.StepChanged -= HandleStepChanged;
    }

    private void HandleStepChanged(PendulumStep step)
    {
        bool active = step == PendulumStep.Swinging;
        if (panel != null) panel.SetActive(active);
        if (active) tallyText.text = $"Run {controller.CurrentRunIndex}: 0 / {controller.oscillationCount}";
    }

    private void HandleTapped(int count)
    {
        tallyText.text = $"Run {controller.CurrentRunIndex}: {count} / {controller.oscillationCount}";
    }
}
