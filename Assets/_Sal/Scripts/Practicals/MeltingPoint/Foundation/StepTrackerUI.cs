using UnityEngine;
using UnityEngine.UI;

// The step progress list on the left of the screen, the "Step 2 of 9" title,
// and the Guided-mode hint bar. It only displays; StepController owns the logic.
public class StepTrackerUI : MonoBehaviour
{
    public StepController steps;
    public Image[] dots;            // one per step, top to bottom
    public Text[] dotNumbers;
    public Text stepTitle;
    public GameObject hintPanel;
    public Text hintText;

    public Color pendingColor = new Color(1f, 1f, 1f, 0.25f);
    public Color currentColor = new Color(0.98f, 0.75f, 0.15f, 1f);
    public Color doneColor = new Color(0.25f, 0.75f, 0.4f, 1f);

    void Start()
    {
        if (steps == null) steps = StepController.Instance;
        if (steps == null) return;

        steps.StepChanged += Refresh;
        if (steps.Current != null) Refresh(steps.currentIndex, steps.Current);
        else ShowIdle();
    }

    void OnDestroy()
    {
        if (steps != null) steps.StepChanged -= Refresh;
    }

    void ShowIdle()
    {
        for (int i = 0; i < dots.Length; i++) dots[i].color = pendingColor;
        if (stepTitle != null) stepTitle.text = "Get ready";
        if (hintPanel != null) hintPanel.SetActive(false);
    }

    // Finished steps turn green, the current step turns amber and slightly larger.
    void Refresh(int index, PracticalStep step)
    {
        for (int i = 0; i < dots.Length; i++)
        {
            dots[i].color = i < index ? doneColor : (i == index ? currentColor : pendingColor);
            dots[i].rectTransform.localScale = (i == index) ? Vector3.one * 1.25f : Vector3.one;
            if (dotNumbers != null && i < dotNumbers.Length) dotNumbers[i].text = (i + 1).ToString();
        }

        if (stepTitle != null)
        {
            stepTitle.text = "Step " + (index + 1) + " of " + steps.steps.Count + ":  " + step.title;
        }

        bool showHint = steps.ShowHints && !string.IsNullOrEmpty(step.guidedHint);
        if (hintPanel != null) hintPanel.SetActive(showHint);
        if (hintText != null) hintText.text = step.guidedHint;
    }
}
