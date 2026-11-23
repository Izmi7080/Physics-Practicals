using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: The Learn-mode briefing: a short, skippable sequence of cards (e.g. Aim -> Apparatus
     * -> What you will measure) shown over a dimmed screen before the practical starts. Next moves
     * on, the last card's button says "Start", Skip is always available. Whoever opens it decides
     * when (first time only, or replayed from the Guide tab) and gets a callback when it closes.
     * Shared by every practical.
     * Talks to: PanelTransition_Azm (fade); the practical's HUD calls Show(cards, onClosed).
     */
    [RequireComponent(typeof(PanelTransition_Azm))]
    public class BriefingUI_Azm : MonoBehaviour
    {
        public struct Card
        {
            public string title;
            public string body;
            public Card(string title, string body) { this.title = title; this.body = body; }
        }

        public TMP_Text titleText;
        public TMP_Text bodyText;
        public TMP_Text pageText;
        public Button nextButton;
        public TMP_Text nextLabel;
        public Button skipButton;

        public bool IsOpen => transition != null && transition.IsVisible;

        private PanelTransition_Azm transition;
        private readonly List<Card> cards = new List<Card>();
        private int index;
        private Action onClosed;

        private void Awake()
        {
            transition = GetComponent<PanelTransition_Azm>();
            nextButton.onClick.AddListener(Next);
            skipButton.onClick.AddListener(Close);
        }

        public void Show(IList<Card> sequence, Action closed)
        {
            if (transition == null) transition = GetComponent<PanelTransition_Azm>();
            cards.Clear();
            cards.AddRange(sequence);
            onClosed = closed;
            index = 0;
            Fill();
            transition.Show();
        }

        private void Fill()
        {
            var card = cards[index];
            titleText.text = card.title;
            bodyText.text = card.body;
            pageText.text = $"{index + 1} / {cards.Count}";
            nextLabel.text = index == cards.Count - 1 ? "Start" : "Next";
        }

        private void Next()
        {
            if (index < cards.Count - 1) { index++; Fill(); }
            else Close();
        }

        public void Close()
        {
            if (!IsOpen) return;
            transition.Hide();
            var callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }
    }
}
