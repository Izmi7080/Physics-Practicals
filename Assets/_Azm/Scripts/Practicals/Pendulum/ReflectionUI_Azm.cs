using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI.
     * Purpose: The Learn-mode REFLECTION at the end of the results panel (TASK_menus_and_pendulum
     * 4.8): "Why might your value of g differ from 9.8 m/s²?" with five tappable causes
     * (multi-select, no grading - every option is a real cause), then one line that ties the chosen
     * causes to the precautions that control them. It turns the result into the viva question the
     * student will actually be asked.
     * Talks to: its own option buttons and summary text; PendulumTexts_Azm (wording).
     */
    public class ReflectionUI_Azm : MonoBehaviour
    {
        public TMP_Text questionText;
        public Button[] optionButtons;
        public TMP_Text summaryText;

        private readonly List<int> chosen = new List<int>();

        private void Awake()
        {
            questionText.text = PendulumTexts_Azm.ReflectionQuestion;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i;
                optionButtons[i].GetComponentInChildren<TMP_Text>().text = PendulumTexts_Azm.ReflectionOptions[i];
                optionButtons[i].onClick.AddListener(() => Toggle(index));
            }
            Refresh();
        }

        private void Toggle(int index)
        {
            if (chosen.Contains(index)) chosen.Remove(index); else chosen.Add(index);
            chosen.Sort();
            Motion_Azm.Punch(optionButtons[index].transform, 0.08f);
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool on = chosen.Contains(i);
                var image = optionButtons[i].GetComponent<ThemedImage_Azm>();
                var label = optionButtons[i].GetComponentInChildren<ThemedText_Azm>();
                if (image != null) image.SetRole(on ? ThemeColor_Azm.Primary : ThemeColor_Azm.Secondary);
                if (label != null) label.SetRole(on ? ThemeColor_Azm.OnPrimary : ThemeColor_Azm.TextPrimary);
            }
            summaryText.text = PendulumTexts_Azm.ReflectionSummary(chosen);
        }
    }
}
