using System;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Physics141
{
    // These IDs are saved as bits in OhmProgress. The question array defines the display order.
    internal enum OhmCheck { Wiring = 0, Readings = 1, Graph = 2, Apparatus = 3, Key = 4, Contacts = 5, MeterMode = 6, Zero = 7, Resistance = 8 }

    internal sealed class OhmQuiz
    {
        readonly struct Question
        {
            internal readonly OhmCheck Check;
            internal readonly string Prompt, Hint, Explanation;
            internal readonly string[] Choices;
            internal readonly int Correct;

            internal Question(OhmCheck check, string prompt, string[] choices, int correct, string hint, string explanation)
            {
                Check = check;
                Prompt = prompt;
                Choices = choices;
                Correct = correct;
                Hint = hint;
                Explanation = explanation;
            }
        }

        static readonly Question[] Questions =
        {
            new(OhmCheck.Apparatus, "Which instrument measures the current through the nichrome wire?",
                new[] { "The voltmeter", "The ammeter", "The rheostat" },
                1, "Look at the letter on the left meter. Current is measured in amperes (A).",
                "The ammeter measures current in amperes (A). The voltmeter measures voltage, and the rheostat changes the current."),
            new(OhmCheck.Key, "What happens when you open the plug key?",
                new[] { "The supply voltage increases.", "The sample resistance changes.", "Current stops flowing through the wire." },
                2, "Look at the gap in the switch symbol. Can current travel around a loop with a gap?",
                "An open key breaks the loop. The supply can be on, but current can only flow around a closed loop."),
            new(OhmCheck.Contacts, "Why did you rub the metal lead tip on sandpaper?",
                new[] { "To clean the tip for a good connection.", "To make the nichrome wire longer.", "To change the supply voltage." },
                0, "Think about what happens where the metal tip touches the terminal.",
                "Sandpaper removes buildup from the lead tip so clean metal can make good electrical contact."),
            new(OhmCheck.MeterMode, "Why use a DC setting on the meters for this circuit?",
                new[] { "The supply swaps + and − repeatedly.", "DC makes the circuit use less current.", "The supply keeps the same + and − sides." },
                2, "Look at the fixed + and − markings on the DC power supply.",
                "A DC supply keeps the same polarity. With the leads connected correctly, the meters show positive readings."),
            new(OhmCheck.Wiring, "Why did you connect the voltage meter across the nichrome wire?",
                new[] { "To make more current flow.", "To compare the voltage at the two ends.", "To turn the power supply on." },
                1, "Look at where the two voltage leads touch. Think about what the meter compares.",
                "The voltage meter compares the two ends of the nichrome wire. The current meter sits in the loop and measures the current flowing through it."),
            new(OhmCheck.Zero, "Why should both meters read zero with the plug key open?",
                new[] { "No current flows through the nichrome.", "Both meters are switched off.", "The rheostat always blocks current." },
                0, "The open key breaks the loop. Think about what the two meters measure at the sample.",
                "The open key stops current through the nichrome. With zero current, there is no voltage drop across that wire either."),
            new(OhmCheck.Readings, "You saved one pair. What should you change to get a different pair?",
                new[] { "Move the rheostat slider.", "Raise the power supply above 6.0 V.", "Swap the meter leads." },
                0, "Keep the supply at 6.0 V. Which control changes the resistance in the circuit?",
                "Moving the rheostat slider changes the current. The voltage across the nichrome changes too, even while the supply stays at 6.0 V."),
            new(OhmCheck.Resistance, "A reading gives 2.25 V and 0.15 A. What is the wire’s resistance?",
                new[] { "0.34 Ω", "2.40 Ω", "15 Ω" },
                2, "Use Ohm’s law. Divide the voltage across the wire by the current through it.",
                "Resistance is voltage divided by current. Dividing 2.25 V by 0.15 A gives 15 Ω."),
            new(OhmCheck.Graph, "Which reading belongs on the horizontal axis of your graph?",
                new[] { "Resistance in ohms.", "Voltage in volts.", "Current in amperes." },
                2, "In a graph of voltage against current, the second quantity goes along the bottom.",
                "Put current (I) on the horizontal axis and voltage (V) on the vertical axis. For an ohmic wire at constant temperature, the points lie close to a straight line.")
        };

        readonly Text number, prompt, feedback;
        readonly Button[] choices = new Button[3];
        readonly Text[] labels = new Text[3];
        readonly Button continueButton, skipButton;
        readonly Action<OhmCheck> close;
        Question current;
        bool answered;

        internal GameObject Page { get; }

        internal OhmQuiz(GameObject page, Action<OhmCheck> close)
        {
            Page = page;
            this.close = close;
            var root = page.transform;
            OhmUi.Label(root, "Check your understanding", .06f, .85f, .94f, .96f, 34).color = PracticalUiStyle.EntryHighlight;
            number = OhmUi.Label(root, "", .08f, .78f, .92f, .85f, 22);
            prompt = OhmUi.Label(root, "", .075f, .64f, .925f, .78f, 34);
            for (int i = 0; i < choices.Length; i++)
            {
                int option = i;
                float lower = .51f - i * .123f;
                choices[i] = OhmUi.Button(root, $"Check answer {i + 1}", .085f, lower, .915f, lower + .102f,
                    () => Choose(option));
                labels[i] = choices[i].GetComponentInChildren<Text>();
            }
            feedback = OhmUi.Label(root, "", .10f, .126f, .90f, .25f, 25);
            feedback.name = "Check feedback";
            continueButton = OhmUi.Button(root, "Continue practical", .63f, .032f, .915f, .115f, () => close(current.Check));
            skipButton = OhmUi.Button(root, "Skip check", .085f, .032f, .31f, .115f, () => close(current.Check));
        }

        internal void Prepare(OhmCheck check)
        {
            answered = false;
            int index = Array.FindIndex(Questions, question => question.Check == check);
            current = Questions[index];
            number.text = $"Question {index + 1} of {Questions.Length}";
            prompt.text = current.Prompt;
            feedback.text = "";
            for (int i = 0; i < choices.Length; i++)
            {
                labels[i].text = current.Choices[i];
                choices[i].interactable = true;
            }
            continueButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(true);
        }

        void Choose(int option)
        {
            if (answered) return;
            if (option != current.Correct)
            {
                feedback.text = "Try again. " + current.Hint;
                feedback.color = new Color(1f, .81f, .55f);
                choices[option].interactable = false;
                return;
            }
            answered = true;
            feedback.text = "Correct. " + current.Explanation;
            feedback.color = PracticalUiStyle.EntryHighlight;
            for (int i = 0; i < choices.Length; i++) choices[i].interactable = false;
            continueButton.gameObject.SetActive(true);
            skipButton.gameObject.SetActive(false);
        }
    }
}
