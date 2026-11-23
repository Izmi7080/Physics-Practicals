using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: One reusable yes/no confirmation pop-up ("Discard row 3?", "Exit to menu?"). It
     * dims the screen behind it, shows a message and two buttons, and calls the supplied action
     * only if the student confirms. Tapping the dimmed background or "No" closes it.
     * Talks to: PanelTransition_Azm (fade in/out); callers pass the message and the confirm action.
     */
    [RequireComponent(typeof(PanelTransition_Azm))]
    public class ConfirmDialog_Azm : MonoBehaviour
    {
        public TMP_Text messageText;
        public TMP_Text yesLabel;
        public Button yesButton;
        public Button noButton;
        public Button backdropButton;

        private PanelTransition_Azm transition;
        private Action onYes;

        public bool IsOpen => transition != null && transition.IsVisible;

        private void Awake()
        {
            transition = GetComponent<PanelTransition_Azm>();
            yesButton.onClick.AddListener(Confirm);
            noButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);
        }

        public void Ask(string message, string yesText, Action confirmAction)
        {
            if (transition == null) transition = GetComponent<PanelTransition_Azm>();
            messageText.text = message;
            yesLabel.text = yesText;
            onYes = confirmAction;
            transition.Show();
        }

        public void Close()
        {
            onYes = null;
            transition.Hide();
        }

        private void Confirm()
        {
            var action = onYes;
            Close();
            action?.Invoke();
        }
    }
}
