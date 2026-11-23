using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualLab.Azm.ScrewGaugePractical
{
    public enum SGStepId_Azm { LeastCount, ZeroError, Thickness, Calculate, Results }

    [Flags]
    public enum SGControls_Azm
    {
        None = 0,
        Magnifier = 1,          // the code-drawn reading magnifier
        LeastCountPanel = 2,    // "turn one full turn" + the least-count question
        GaugeControls = 4,      // Open / Ratchet (hold) / Lock
        Keypad = 8,             // the reading keypad
        CalcReveal = 16         // "Show next line" / "See my result"
    }

    public enum SGView_Azm { Overview = 0, Gauge = 1 }

    public enum SGZeroErrorSetting_Azm { None, Fixed, Random }

    public enum SGObjectKind_Azm { MetalStrip, Wire }

    [Serializable]
    public class SGStepDefinition_Azm
    {
        public SGStepId_Azm id;
        [Tooltip("ONE short action, at most ~8 words (HCI: students read in a second language). {0} = reading number, {1} = readings (Thickness step).")]
        [TextArea(1, 2)] public string instruction;
        [Tooltip("The reason for the step, shown behind the \"Why?\" chip on the instruction card.")]
        [TextArea(2, 4)] public string why;
        public SGView_Azm view;
        public SGControls_Azm controls;
        [Tooltip("Index of the matching line in the guide's procedure (highlighted in the Guide tab).")]
        public int guideProcedureIndex = -1;

        public SGStepDefinition_Azm(SGStepId_Azm id, string instruction, SGView_Azm view, SGControls_Azm controls, int guideProcedureIndex, string why = "")
        {
            this.id = id;
            this.instruction = instruction;
            this.view = view;
            this.controls = controls;
            this.guideProcedureIndex = guideProcedureIndex;
            this.why = why;
        }
    }

    /*
     * Layer: Logic support (data asset - CLAUDE.md: tunable numbers live in ScriptableObject configs).
     * Purpose: Everything tunable in "Screw Gauge: Thickness of a Metal Strip" (Matric Physics
     * Exp 1.3): the object (metal strip, or a wire as an option) and its TRUE thickness (the student
     * must recover it), the small variation between places on it, the instrument's zero error, the
     * least-count question and the step list. The instrument's own numbers (pitch, divisions,
     * turning speeds) are in the shared ScrewGaugeConfig_Azm.
     * Numbers marked ASSUMPTION are not in the textbook and need a teacher to confirm them.
     * Talks to: read by ScrewGaugeController_Azm and the screw gauge HUD.
     */
    [CreateAssetMenu(fileName = "ScrewGaugePracticalConfig_Azm", menuName = "VirtualLab_Azm/Screw Gauge Practical Config")]
    public class ScrewGaugePracticalConfig_Azm : ScriptableObject
    {
        [Header("The object (true values)")]
        [Tooltip("Textbook: a flat metal strip or a kink-free wire.")]
        public SGObjectKind_Azm objectKind = SGObjectKind_Azm.MetalStrip;
        [Tooltip("True thickness (or wire diameter), mm. ASSUMPTION: 0.73 mm gives M = 0.5 mm and n = 23, so both scales are used.")]
        public float trueThicknessMm = 0.73f;
        [Tooltip("ASSUMPTION: a real strip is not perfectly even, so each place may differ from the true thickness by up to this many least counts.")]
        public int variationLeastCounts = 1;

        [Header("Procedure (textbook)")]
        [Tooltip("Textbook: one reading plus two more at different positions.")]
        public int readings = 3;

        [Header("Zero error of the instrument")]
        public SGZeroErrorSetting_Azm zeroErrorSetting = SGZeroErrorSetting_Azm.Random;
        [Tooltip("Used with Fixed: zero error in least counts (+ = circular 0 below the datum line).")]
        public int fixedZeroErrorLeastCounts = 2;
        [Tooltip("ASSUMPTION: Random picks 1..this many least counts, positive or negative (the textbook examples are +0.02 and -0.04 mm).")]
        public int randomZeroErrorMaxLeastCounts = 5;

        [Header("Least-count question")]
        [Tooltip("Choices shown, in mm. The correct one is pitch / divisions = 0.5 / 50 = 0.01 mm.")]
        public float[] leastCountChoicesMm = { 0.5f, 0.05f, 0.01f, 0.1f };
        [Tooltip("Seconds the 'turn one full turn' demonstration takes.")]
        public float pitchTurnSeconds = 1.6f;

        [Header("Steps")]
        public List<SGStepDefinition_Azm> steps = new List<SGStepDefinition_Azm>();

        private void Reset() => steps = CreateDefaultSteps();

        public string ObjectName => objectKind == SGObjectKind_Azm.Wire ? "wire" : "strip";
        public string QuantityName => objectKind == SGObjectKind_Azm.Wire ? "diameter" : "thickness";

        // Textbook order: least count, zero error, the readings, then the result.
        public static List<SGStepDefinition_Azm> CreateDefaultSteps()
        {
            var reading = SGControls_Azm.Magnifier | SGControls_Azm.GaugeControls | SGControls_Azm.Keypad;
            // Instructions are short actions (HCI restructure: one idea, ~8 words); the reasons
            // moved to "why" (shown behind the instruction card's "Why?" chip).
            return new List<SGStepDefinition_Azm>
            {
                new SGStepDefinition_Azm(SGStepId_Azm.LeastCount, "Find the pitch and least count",
                    SGView_Azm.Gauge, SGControls_Azm.Magnifier | SGControls_Azm.LeastCountPanel, 0,
                    "Pitch = how far the spindle moves in one full turn of the thimble. Least count = pitch ÷ number of circular divisions: the smallest thickness the gauge can measure."),
                new SGStepDefinition_Azm(SGStepId_Azm.ZeroError, "Close with the ratchet, read the zero error",
                    SGView_Azm.Gauge, reading, 1,
                    "The ratchet slips at the same gentle pressure every time. If the circular 0 is not on the datum line when the gauge is closed, every reading needs the same correction."),
                new SGStepDefinition_Azm(SGStepId_Azm.Thickness, "Measure the thickness t ({0} of {1})",
                    SGView_Azm.Gauge, reading, 3,
                    "A strip is not perfectly even, so you measure it at three different places and use the mean. Always close with the ratchet."),
                new SGStepDefinition_Azm(SGStepId_Azm.Calculate, "Find the mean corrected thickness",
                    SGView_Azm.Overview, SGControls_Azm.CalcReveal, 8,
                    "Add the zero correction to each reading (t = Y + ZC), then take the mean of the three."),
                new SGStepDefinition_Azm(SGStepId_Azm.Results, "Your result", SGView_Azm.Overview, SGControls_Azm.None, 9)
            };
        }

        // Copies the default instruction and "why" texts into an existing asset (by step id), so a
        // rebuilt scene gets the new wording without losing any other change made in the asset.
        public void ApplyDefaultTexts()
        {
            var defaults = CreateDefaultSteps();
            foreach (var step in steps)
                foreach (var d in defaults)
                    if (d.id == step.id) { step.instruction = d.instruction; step.why = d.why; }
        }
    }
}
