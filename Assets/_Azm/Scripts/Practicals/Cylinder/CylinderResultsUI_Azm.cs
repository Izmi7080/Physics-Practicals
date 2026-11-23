using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Cylinder
{
    /*
     * Layer: UI.
     * Purpose: The results panel: the area of cross-section A (Exp 1.1) and the volume V (Exp 1.2)
     * from the student's own corrected readings, the means and the zero correction used, and how
     * close they came to the accepted values (shown only now, after the measurement, so it never
     * shortcuts the experiment). Buttons: Retry (reload the practical) and Main menu.
     * Talks to: CylinderController_Azm (values); shown/hidden by the HUD; buttons call AppManager_Azm.
     */
    public class CylinderResultsUI_Azm : MonoBehaviour
    {
        public CylinderController_Azm controller;
        public PanelTransition_Azm panel;
        public TMP_Text areaText;
        public TMP_Text volumeText;
        public TMP_Text detailText;
        public Button retryButton;
        public Button menuButton;

        public bool IsVisible => panel.IsVisible;

        private void Awake()
        {
            retryButton.onClick.AddListener(Retry);
            menuButton.onClick.AddListener(() => { if (AppManager_Azm.Instance != null) AppManager_Azm.Instance.GoToMainMenu(); });
        }

        private void Start()
        {
            panel.Hide(false);
        }

        public void SetVisible(bool visible)
        {
            // Juice (Stage 6): the results arrive with the success sound and the numbers count up.
            if (visible) { Fill(); panel.Show(); FeedbackFX_Azm.Success(panel.transform); }
            else if (panel.IsVisible) panel.Hide();
        }

        private static float PercentDiff(float value, float accepted) => accepted > 0f ? Mathf.Abs(value - accepted) / accepted * 100f : 0f;

        private void Fill()
        {
            var c = controller;
            // Count up to exactly the values CylinderTexts_Azm.AreaResult / VolumeResult would show.
            areaText.text = CylinderTexts_Azm.AreaResult(c.AreaCm2);
            volumeText.text = CylinderTexts_Azm.VolumeResult(c.VolumeCm3);
            Motion_Azm.CountUp(areaText, 0f, c.AreaCm2, "F3", "A = ", " cm²", 1.0f);
            Motion_Azm.CountUp(volumeText, 0f, c.VolumeCm3, "F2", "V = ", " cm³", 1.2f);
            detailText.text = CylinderTexts_Azm.ResultDetail(c.MeanDiameterCm, c.MeanLengthCm, c.RecordedZeroErrorMm / 10f,
                c.TrueAreaCm2, c.TrueVolumeCm3, PercentDiff(c.AreaCm2, c.TrueAreaCm2), PercentDiff(c.VolumeCm3, c.TrueVolumeCm3));
        }

        // Reload the practical: through the session when there is one, otherwise just this scene.
        public static void Retry()
        {
            var app = AppManager_Azm.Instance;
            if (app == null) return;
            if (app.CurrentSession != null) app.ReloadCurrentPractical();
            else app.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
