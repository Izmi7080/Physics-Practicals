using TMPro;
using UnityEngine;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (presentation only; HCI restructure Stage 6).
     * Purpose: When the text of a label changes by a SHORT new part (a worked-calculation line
     * revealed with "Show next line"), the new characters "type in" quickly instead of appearing all
     * at once, so the eye follows the new line. Everything that was already there stays visible.
     * A big change (switching to another row, a rebuilt text) or Reduce motion shows at once.
     * It only animates how much of the text is visible; the text itself is set by the owner
     * (CylinderCalcUI_Azm, ScrewGaugeCalcUI_Azm, PendulumCalcUI_Azm), so the values are never altered.
     * Talks to: its TMP_Text; Motion_Azm.ReduceMotion.
     */
    [RequireComponent(typeof(TMP_Text))]
    public class TypeInText_Azm : MonoBehaviour
    {
        [Tooltip("Typing speed (visible characters per second).")]
        public float charactersPerSecond = 110f;
        [Tooltip("A change longer than this many characters is shown at once (not a single new line).")]
        public int maxTypedCharacters = 140;

        private TMP_Text label;
        private string lastText;
        private float visible;
        private int total;
        private bool typing;

        private void Awake()
        {
            label = GetComponent<TMP_Text>();
            lastText = label.text;
        }

        private void OnDisable()
        {
            // Never leave a half-typed text behind (e.g. the drawer closed mid-line).
            if (label != null) label.maxVisibleCharacters = 99999;
            typing = false;
            lastText = label != null ? label.text : lastText;
        }

        private void LateUpdate()
        {
            if (label.text != lastText)
            {
                string previous = lastText ?? "";
                lastText = label.text;
                StartTyping(previous);
            }
            if (!typing) return;
            visible += charactersPerSecond * Time.unscaledDeltaTime;
            if (visible >= total) { typing = false; label.maxVisibleCharacters = 99999; return; }
            label.maxVisibleCharacters = Mathf.FloorToInt(visible);
        }

        private void StartTyping(string previous)
        {
            label.maxVisibleCharacters = 99999;
            if (Motion_Azm.ReduceMotion || string.IsNullOrEmpty(previous)) { typing = false; return; }

            // First source character that differs from the old text.
            string now = label.text;
            int diff = 0;
            int max = Mathf.Min(previous.Length, now.Length);
            while (diff < max && previous[diff] == now[diff]) diff++;

            // Convert it to a VISIBLE character index (rich-text tags are not visible characters).
            label.ForceMeshUpdate();
            var info = label.textInfo;
            int firstNew = info.characterCount;
            for (int i = 0; i < info.characterCount; i++)
                if (info.characterInfo[i].index >= diff) { firstNew = i; break; }

            total = info.characterCount;
            if (total - firstNew <= 0 || total - firstNew > maxTypedCharacters) { typing = false; return; }
            visible = firstNew;
            typing = true;
            label.maxVisibleCharacters = firstNew;
        }
    }
}
