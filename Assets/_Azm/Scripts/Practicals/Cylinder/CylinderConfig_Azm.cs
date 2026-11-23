using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualLab.Azm.Cylinder
{
    public enum CylinderStepId_Azm { LeastCount, ZeroError, Diameter, Length, Calculate, Results }

    // Which controls a step shows (the HUD reads this; the controller never touches UI).
    [Flags]
    public enum CylinderControls_Azm
    {
        None = 0,
        Magnifier = 1,          // the code-drawn reading magnifier
        LeastCountChoice = 2,   // the least-count question
        Keypad = 4,             // the reading keypad (M and n, or the zero error)
        CalcReveal = 8          // "Show next line" / "See my result" for the worked calculation
    }

    public enum CylinderView_Azm { Overview = 0, Caliper = 1 }

    public enum ZeroErrorSetting_Azm
    {
        None,       // a perfect instrument
        Fixed,      // always fixedZeroErrorLeastCounts
        Random      // a new non-zero error each session, positive or negative
    }

    // One step of the procedure as DATA (same pattern as the pendulum's StepDefinition_Azm).
    [Serializable]
    public class CylinderStepDefinition_Azm
    {
        public CylinderStepId_Azm id;
        [Tooltip("ONE short action, at most ~8 words (HCI: students read in a second language). {0} = the reading number, {1} = readings per quantity (Diameter/Length steps).")]
        [TextArea(1, 2)] public string instruction;
        [Tooltip("The reason for the step, shown behind the \"Why?\" chip on the instruction card.")]
        [TextArea(2, 4)] public string why;
        public CylinderView_Azm view;
        public CylinderControls_Azm controls;
        [Tooltip("Index of the matching line in the guide's procedure (highlighted in the Guide tab).")]
        public int guideProcedureIndex = -1;

        public CylinderStepDefinition_Azm(CylinderStepId_Azm id, string instruction, CylinderView_Azm view,
            CylinderControls_Azm controls, int guideProcedureIndex, string why = "")
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
     * Purpose: Everything tunable in "Vernier Calipers: Solid Cylinder" (Matric Physics Exp 1.1 area
     * of cross-section + Exp 1.2 volume, run as ONE practical): the cylinder's TRUE size (the
     * student must recover it with the caliper), the small real-world variation between readings,
     * the instrument's zero error, the least-count question and the step list.
     * Numbers marked ASSUMPTION are not in the textbook and need a teacher to confirm them.
     * The caliper's own numbers (least count, tolerance) are in the shared CaliperConfig_Azm.
     * Talks to: read by CylinderController_Azm and the cylinder HUD.
     */
    [CreateAssetMenu(fileName = "CylinderConfig_Azm", menuName = "VirtualLab_Azm/Cylinder Config")]
    public class CylinderConfig_Azm : ScriptableObject
    {
        [Header("The solid cylinder (true values)")]
        [Tooltip("True diameter, cm. ASSUMPTION: chosen so the vernier division is not 0 (2.18 cm -> M = 21 mm, n = 8).")]
        public float trueDiameterCm = 2.18f;
        [Tooltip("True length, cm. ASSUMPTION: chosen so the vernier division is not 0 (4.36 cm -> M = 43 mm, n = 6).")]
        public float trueLengthCm = 4.36f;
        [Tooltip("ASSUMPTION: a real cylinder is not perfect, so each reading (a different place on the cylinder) may differ from the true size by up to this many least counts.")]
        public int variationLeastCounts = 1;

        [Header("Procedure (textbook)")]
        [Tooltip("Textbook: measure the length and the diameter at least three times.")]
        public int readingsPerQuantity = 3;

        [Header("Zero error of the instrument")]
        public ZeroErrorSetting_Azm zeroErrorSetting = ZeroErrorSetting_Azm.Random;
        [Tooltip("Used with Fixed: zero error in least counts (+ = vernier 0 right of the main 0).")]
        public int fixedZeroErrorLeastCounts = 3;
        [Tooltip("ASSUMPTION: Random picks 1..this many least counts, positive or negative (a few hundredths of a cm, as on worn school calipers).")]
        public int randomZeroErrorMaxLeastCounts = 4;

        [Header("Least-count question")]
        [Tooltip("Choices shown, in mm. The correct one is the caliper's least count (1 mm / 10 = 0.1 mm).")]
        public float[] leastCountChoicesMm = { 1f, 0.9f, 0.1f, 0.01f };

        [Header("Steps")]
        public List<CylinderStepDefinition_Azm> steps = new List<CylinderStepDefinition_Azm>();

        private void Reset()
        {
            steps = CreateDefaultSteps();
        }

        // Textbook order (Exp 1.1 / 1.2): least count, zero error, the readings, then the results.
        // Exp 1.2 lists the length first, but both are measured the same way; the diameter goes first
        // here so the Exp 1.1 area can be worked out as soon as its readings are done.
        public static List<CylinderStepDefinition_Azm> CreateDefaultSteps()
        {
            var magnifierKeypad = CylinderControls_Azm.Magnifier | CylinderControls_Azm.Keypad;
            // Instructions are short actions (HCI restructure: one idea, ~8 words); the reasons
            // moved to "why" (shown behind the instruction card's "Why?" chip).
            return new List<CylinderStepDefinition_Azm>
            {
                new CylinderStepDefinition_Azm(CylinderStepId_Azm.LeastCount,
                    "Find the least count",
                    CylinderView_Azm.Caliper, CylinderControls_Azm.Magnifier | CylinderControls_Azm.LeastCountChoice, 0,
                    "The least count is the smallest length the calipers can measure. You need it to turn the vernier division n into a length: X = n × LC."),
                new CylinderStepDefinition_Azm(CylinderStepId_Azm.ZeroError,
                    "Close the jaws and read the zero error",
                    CylinderView_Azm.Caliper, magnifierKeypad, 1,
                    "If the two zeros do not line up when the jaws are closed, every reading is off by the same amount. Measuring this zero error lets you correct all your readings."),
                new CylinderStepDefinition_Azm(CylinderStepId_Azm.Diameter,
                    "Measure the diameter D ({0} of {1})",
                    CylinderView_Azm.Caliper, magnifierKeypad, 2,
                    "A real cylinder is never perfectly round, so you measure it at three places and use the mean. Both jaws must touch the cylinder."),
                new CylinderStepDefinition_Azm(CylinderStepId_Azm.Length,
                    "Measure the length L ({0} of {1})",
                    CylinderView_Azm.Caliper, magnifierKeypad, 3,
                    "The volume needs the length as well as the radius: V = πr²L. Three readings again, for a reliable mean."),
                new CylinderStepDefinition_Azm(CylinderStepId_Azm.Calculate,
                    "Work out the area and volume",
                    CylinderView_Azm.Overview, CylinderControls_Azm.CalcReveal, 4,
                    "Use the means of your CORRECTED readings: r = D/2, then A = πr² (Exp 1.1) and V = πr²L (Exp 1.2)."),
                new CylinderStepDefinition_Azm(CylinderStepId_Azm.Results,
                    "Your result",
                    CylinderView_Azm.Overview, CylinderControls_Azm.None, 5)
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
