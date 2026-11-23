using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI (temporary placeholder).
     * Purpose: Stands in for the empty SimplePendulum_Azm scene during Stage 1 so the whole app
     * flow can be tested end to end: it shows which practical and mode the menu launched it with
     * (proving the session was handed over correctly) and gives a way back to the main menu
     * (button, or the Android back button). Replaced by the real practical controller in Stage 2.
     * Talks to: reads AppManager_Azm.CurrentSession; calls AppManager_Azm.GoToMainMenu;
     * listens to BackButtonInput_Azm.
     */
    public class PendulumSceneStub_Azm : MonoBehaviour
    {
        public TMP_Text infoText;
        public Button exitButton;
        public BackButtonInput_Azm backInput;

        private void Awake()
        {
            exitButton.onClick.AddListener(ExitToMenu);
        }

        private void OnEnable()
        {
            if (backInput != null) backInput.BackPressed += ExitToMenu;
        }

        private void OnDisable()
        {
            if (backInput != null) backInput.BackPressed -= ExitToMenu;
        }

        private void Start()
        {
            var session = AppManager_Azm.Instance != null ? AppManager_Azm.Instance.CurrentSession : null;
            infoText.text = session != null
                ? $"{session.Practical.displayName}\nMode: {session.Mode}\n\nThe practical itself is built in Stage 2."
                : "Simple Pendulum\n(scene opened directly - no session)\n\nThe practical itself is built in Stage 2.";
        }

        private void ExitToMenu()
        {
            if (AppManager_Azm.Instance != null) AppManager_Azm.Instance.GoToMainMenu();
        }
    }
}
