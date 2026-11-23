using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Menu
{
    public enum CardState_Azm
    {
        Available,   // unlocked and its scene is in Build Settings
        Locked,      // isLocked = true  -> "Coming soon"
        Unavailable  // unlocked, but its scene is not in Build Settings -> cannot be started
    }

    /*
     * Layer: UI (menu).
     * Purpose: One card on the practical-select grid. Fills itself from a PracticalInfo_Azm
     * (thumbnail, name, description, estimated time) and shows its state: locked cards are greyed
     * out with a padlock and a "Coming soon" label (shown, never hidden); a card whose scene is
     * missing from Build Settings shows "Unavailable" instead of crashing.
     * Talks to: built by MainMenuController_Azm; reports taps back to it through a callback.
     * Colours come from UITheme_Azm.
     */
    public class PracticalCardUI_Azm : MonoBehaviour
    {
        public Button button;
        public Image thumbnail;
        public TMP_Text nameText;
        public TMP_Text descriptionText;
        public TMP_Text timeText;
        public TMP_Text stateLabel;
        public Image lockIcon;
        [Tooltip("Everything that should fade when the card is locked/unavailable.")]
        public CanvasGroup contentGroup;

        public PracticalInfo_Azm Info { get; private set; }
        public CardState_Azm State { get; private set; }

        public void Bind(PracticalInfo_Azm info, UITheme_Azm theme, bool sceneAvailable, Action<PracticalCardUI_Azm> onClicked)
        {
            Info = info;

            if (info.isLocked) State = CardState_Azm.Locked;
            else if (!sceneAvailable) State = CardState_Azm.Unavailable;
            else State = CardState_Azm.Available;

            nameText.text = info.displayName;
            descriptionText.text = info.shortDescription;
            timeText.text = info.estimatedMinutes > 0 ? $"About {info.estimatedMinutes} min" : string.Empty;

            if (info.thumbnail != null)
            {
                thumbnail.sprite = info.thumbnail;
                thumbnail.color = Color.white;
            }

            bool dimmed = State != CardState_Azm.Available;
            contentGroup.alpha = dimmed ? 0.45f : 1f;
            lockIcon.gameObject.SetActive(dimmed);
            lockIcon.sprite = theme.GetLockSprite();
            lockIcon.color = theme.GetColor(ThemeColor_Azm.TextPrimary);
            stateLabel.gameObject.SetActive(dimmed);
            stateLabel.text = State == CardState_Azm.Locked ? "Coming soon" : "Unavailable";

            // Locked cards stay tappable on purpose: tapping one shows a "coming soon" toast.
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(this));
        }
    }
}
