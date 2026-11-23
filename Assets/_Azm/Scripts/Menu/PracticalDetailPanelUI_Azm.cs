using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Menu
{
    /*
     * Layer: UI (menu).
     * Purpose: The detail panel opened by tapping an unlocked practical card: name, description,
     * estimated time, short apparatus list, the two mode buttons (Learn enabled, Perform shown
     * as locked with "Coming soon" when the practical does not offer it) and the Start button.
     * Talks to: fed a PracticalInfo_Azm by MainMenuController_Azm; Start calls
     * AppManager_Azm.StartPractical(info, mode). Shows problems through the shared toast.
     */
    public class PracticalDetailPanelUI_Azm : MonoBehaviour
    {
        public TMP_Text nameText;
        public TMP_Text descriptionText;
        public TMP_Text timeText;
        public TMP_Text apparatusText;

        public Button learnButton;
        public Button performButton;
        public TMP_Text performStateLabel;   // "Coming soon" when Perform is locked
        public Button startButton;

        private UITheme_Azm theme;
        private ToastUI_Azm toast;
        private PracticalInfo_Azm info;
        private PracticalMode_Azm selectedMode = PracticalMode_Azm.Learn;

        private void Awake()
        {
            learnButton.onClick.AddListener(() => SelectMode(PracticalMode_Azm.Learn));
            performButton.onClick.AddListener(() => SelectMode(PracticalMode_Azm.Perform));
            startButton.onClick.AddListener(OnStartPressed);
        }

        public void Initialize(UITheme_Azm uiTheme, ToastUI_Azm sharedToast)
        {
            theme = uiTheme;
            toast = sharedToast;
        }

        public void Show(PracticalInfo_Azm practical)
        {
            info = practical;
            nameText.text = practical.displayName;
            descriptionText.text = practical.shortDescription;
            timeText.text = practical.estimatedMinutes > 0 ? $"About {practical.estimatedMinutes} minutes" : string.Empty;

            var list = new StringBuilder();
            if (practical.apparatus != null)
            {
                foreach (string item in practical.apparatus)
                    list.AppendLine("- " + item);
            }
            apparatusText.text = list.ToString();

            performStateLabel.gameObject.SetActive(!practical.performModeAvailable);
            SelectMode(practical.learnModeAvailable ? PracticalMode_Azm.Learn : PracticalMode_Azm.Perform, true);
        }

        private void SelectMode(PracticalMode_Azm mode, bool silent = false)
        {
            if (info == null) return;

            bool available = mode == PracticalMode_Azm.Learn ? info.learnModeAvailable : info.performModeAvailable;
            if (!available)
            {
                // Perform mode is visible but locked - say so instead of silently ignoring the tap.
                if (!silent && toast != null) toast.Show("Perform mode: coming soon");
                return;
            }

            selectedMode = mode;
            Highlight(learnButton, selectedMode == PracticalMode_Azm.Learn);
            Highlight(performButton, selectedMode == PracticalMode_Azm.Perform);
        }

        // Selected mode = teal with dark text; unselected = muted blue with white text.
        private void Highlight(Button button, bool selected)
        {
            var image = button.GetComponent<ThemedImage_Azm>();
            var label = button.GetComponentInChildren<ThemedText_Azm>();
            if (image != null) image.SetRole(selected ? ThemeColor_Azm.Primary : ThemeColor_Azm.Secondary);
            if (label != null) label.SetRole(selected ? ThemeColor_Azm.OnPrimary : ThemeColor_Azm.TextPrimary);
        }

        private void OnStartPressed()
        {
            if (info == null || AppManager_Azm.Instance == null) return;

            if (!AppManager_Azm.Instance.StartPractical(info, selectedMode) && toast != null)
                toast.Show("This practical can't be started right now.");
        }
    }
}
