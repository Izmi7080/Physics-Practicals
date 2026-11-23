using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Pendulum
{
    public enum Prediction_Azm { Larger, Smaller, Same }

    /*
     * Layer: UI.
     * Purpose: The Learn-mode PREDICTION card (TASK_menus_and_pendulum 4.5): from the second length
     * onward, before timing, it asks whether T will be larger, smaller or about the same for the new
     * length. One tap, never scored - predicting before measuring makes the student think about
     * T² ∝ L instead of only collecting numbers. The HUD decides when to ask and compares the answer
     * with the recorded row afterwards.
     * Talks to: its own buttons; raises Chosen for PendulumHUD_Azm.
     */
    [RequireComponent(typeof(PanelTransition_Azm))]
    public class PredictionUI_Azm : MonoBehaviour
    {
        public TMP_Text questionText;
        public Button largerButton;
        public Button smallerButton;
        public Button sameButton;

        public event Action<Prediction_Azm> Chosen;
        public bool IsOpen => transition != null && transition.IsVisible;

        private PanelTransition_Azm transition;

        private void Awake()
        {
            transition = GetComponent<PanelTransition_Azm>();
            largerButton.onClick.AddListener(() => Choose(Prediction_Azm.Larger));
            smallerButton.onClick.AddListener(() => Choose(Prediction_Azm.Smaller));
            sameButton.onClick.AddListener(() => Choose(Prediction_Azm.Same));
        }

        public void Ask(bool longer)
        {
            if (transition == null) transition = GetComponent<PanelTransition_Azm>();
            questionText.text = PendulumTexts_Azm.PredictionQuestion(longer);
            transition.Show();
            Motion_Azm.PopIn(transform, 0.9f);
        }

        public void Hide()
        {
            if (IsOpen) transition.Hide();
        }

        private void Choose(Prediction_Azm p)
        {
            Hide();
            Chosen?.Invoke(p);
        }
    }
}
