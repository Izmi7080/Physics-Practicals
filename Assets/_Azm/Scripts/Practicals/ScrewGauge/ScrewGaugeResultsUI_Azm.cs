using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.ScrewGaugePractical
{
    /*
     * Layer: UI.
     * Purpose: The results panel: the thickness of the strip from the student's own corrected
     * readings (textbook result line "Thickness of the metal strip is ___ mm"), the zero correction
     * used, and - only now, after the measurement - how close it is to the accepted value.
     * Buttons: Retry (reload the practical) and Main menu.
     * Talks to: ScrewGaugeController_Azm; shown/hidden by the HUD; buttons call AppManager_Azm.
     */
    public class ScrewGaugeResultsUI_Azm : MonoBehaviour
    {
        public ScrewGaugeController_Azm controller;
        public PanelTransition_Azm panel;
        public TMP_Text valueText;
        public TMP_Text captionText;
        public TMP_Text detailText;
        public Button retryButton;
        public Button menuButton;

        public bool IsVisible => panel.IsVisible;

        private void Awake()
        {
            retryButton.onClick.AddListener(Retry);
            menuButton.onClick.AddListener(() => { if (AppManager_Azm.Instance != null) AppManager_Azm.Instance.GoToMainMenu(); });
        }

        private void Start() => panel.Hide(false);

        public void SetVisible(bool visible)
        {
            // Juice (Stage 6): the result arrives with the success sound and the number counts up.
            if (visible) { Fill(); panel.Show(); FeedbackFX_Azm.Success(panel.transform); }
            else if (panel.IsVisible) panel.Hide();
        }

        private void Fill()
        {
            var c = controller;
            string quantity = c.config.QuantityName == "diameter" ? "Diameter d" : "Thickness t";
            captionText.text = c.config.QuantityName == "diameter" ? "Diameter of the wire" : "Thickness of the metal strip";
            valueText.text = ScrewGaugePracticalTexts_Azm.ResultValue(quantity, c.MeanThicknessMm);
            // Count up to exactly the value ResultValue shows ("<quantity> = 0.53 mm").
            Motion_Azm.CountUp(valueText, 0f, c.MeanThicknessMm, "F2", quantity + " = ", " mm", 1.0f);
            float diff = c.TrueThicknessMm > 0f ? Mathf.Abs(c.MeanThicknessMm - c.TrueThicknessMm) / c.TrueThicknessMm * 100f : 0f;
            detailText.text = ScrewGaugePracticalTexts_Azm.ResultDetail(c.RecordedZeroErrorMm, c.TrueThicknessMm, diff);
        }

        public static void Retry()
        {
            var app = AppManager_Azm.Instance;
            if (app == null) return;
            if (app.CurrentSession != null) app.ReloadCurrentPractical();
            else app.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
