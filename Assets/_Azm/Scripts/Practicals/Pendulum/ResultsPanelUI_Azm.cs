using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI.
     * Purpose: The results panel at the end of the practical: the final g (counting up to its value),
     * its % error against the true 9.8 m/s^2, a thumbnail of the graph, and two buttons - Retry
     * (reload this practical) and Main Menu. In Learn mode the REFLECTION (ReflectionUI_Azm) sits
     * under the graph (TASK_menus_and_pendulum 4.8). A result within 2 % of the true value gets a
     * small celebration (Stage 6 juice) - it rewards careful timing, it is never the goal.
     * Talks to: reads PendulumController_Azm.Analysis; shown/hidden by the HUD; buttons call
     * AppManager_Azm (reload / go to menu); Motion_Azm / FeedbackFX_Azm / ConfettiBurst_Azm.
     */
    public class ResultsPanelUI_Azm : MonoBehaviour
    {
        public PendulumController_Azm controller;
        public PanelTransition_Azm panel;
        public TMP_Text gText;
        public TMP_Text errorText;
        public TMP_Text detailText;
        public GraphView_Azm thumbnail;
        public Button retryButton;
        public Button menuButton;
        [Tooltip("Learn mode only.")]
        public ReflectionUI_Azm reflection;
        [Tooltip("Optional: plays when the result is within celebrateWithinPercent of the true value.")]
        public ConfettiBurst_Azm confetti;
        [Tooltip("ASSUMPTION (feel): a result this close to the true g is worth a small celebration.")]
        public float celebrateWithinPercent = 2f;

        public bool IsVisible => panel.IsVisible;
        public bool ShowReflection { get; set; } = true;

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
            if (visible)
            {
                if (reflection != null) reflection.gameObject.SetActive(ShowReflection);
                Fill();
                panel.Show();
                FeedbackFX_Azm.Success(panel.transform);
                var a = controller.Analysis;
                if (confetti != null && a.hasFit && a.percentError <= celebrateWithinPercent) confetti.Play();
            }
            else if (panel.IsVisible) panel.Hide();
        }

        private static string F(float v, int d) => v.ToString("F" + d, CultureInfo.InvariantCulture);

        private void Fill()
        {
            var a = controller.Analysis;
            if (!a.hasFit)
            {
                gText.text = "g = –";
                errorText.text = "";
                detailText.text = "Not enough valid rows to fit a line.";
            }
            else
            {
                // The value counts up to the student's result (the number "arrives" rather than pops).
                Motion_Azm.CountUp(gText, 0f, a.g, "F2", "g = ", " m/s²", 1.1f);
                errorText.text = $"{F(a.percentError, 1)} % error  (true value {F(controller.config.trueGravity, 1)} m/s²)";
                detailText.text = $"slope = 4π²/g = {F(a.slope, 3)} s²/m\n" +
                                  (a.nearOrigin ? PendulumTexts_Azm.OriginGood : PendulumTexts_Azm.OriginOff(a.intercept));
            }

            var cmPoints = new List<Vector2>();
            foreach (var p in a.points) cmPoints.Add(new Vector2(p.x * 100f, p.y));
            thumbnail.SetData(cmPoints, a.hasFit, a.slope / 100f, a.intercept);
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
