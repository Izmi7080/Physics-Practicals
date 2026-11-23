using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualLab.Azm.Pendulum
{
    // New ids are added at the END so the step lists already saved in assets keep their meaning.
    public enum StepId_Azm { Intro, SetLength, Release, TimeRun1, TimeRun2, Record, Analyse, Results, MeasureDiameter }

    public enum CameraViewId_Azm { Overview, Rig, Timing, Caliper }

    // Which controls the bottom-centre "context action area" shows for a step.
    [Flags]
    public enum ContextControls_Azm
    {
        None = 0,
        Continue = 1,
        ConfirmLength = 2,
        TapTimer = 4,
        NextLength = 8,
        FinishAnalyse = 16,
        ShowResults = 32,
        Caliper = 64            // magnifier + reading keypad (measure the bob's diameter)
    }

    public enum DrawerAction_Azm { Keep, OpenData, OpenGraph, Close }

    public enum DragMode_Azm { None, Length, Angle }

    public enum MessageKind_Azm { Info, Warning, Error }

    // One step of the procedure, as DATA: its instruction text, the camera view it asks for, the
    // controls it shows and what the side drawer does. The controller only follows this list, so
    // other practicals can reuse the same pattern with a different list.
    [Serializable]
    public class StepDefinition_Azm
    {
        public StepId_Azm id;
        [Tooltip("ONE short action, at most ~8 words (HCI: students read in a second language).")]
        [TextArea(1, 2)] public string instruction;
        [Tooltip("The reason for the step, shown behind the \"Why?\" chip on the instruction card.")]
        [TextArea(2, 4)] public string why;
        public CameraViewId_Azm view;
        public ContextControls_Azm controls;
        public DrawerAction_Azm drawer;
        [Tooltip("Index of the matching line in the guide's procedure (highlighted in the Guide tab).")]
        public int guideProcedureIndex = -1;

        public StepDefinition_Azm(StepId_Azm id, string instruction, CameraViewId_Azm view,
            ContextControls_Azm controls, DrawerAction_Azm drawer = DrawerAction_Azm.Keep, string why = "", int guideProcedureIndex = -1)
        {
            this.id = id;
            this.instruction = instruction;
            this.view = view;
            this.controls = controls;
            this.drawer = drawer;
            this.why = why;
            this.guideProcedureIndex = guideProcedureIndex;
        }
    }

    /*
     * Layer: Logic support (data asset - every tunable number of the experiment lives here, not in
     * code: CLAUDE.md "tunable numbers live in ScriptableObject configs").
     * Purpose: The true constants of the simulation, the procedure rules from the Matric syllabus,
     * the quality thresholds for the student's timing, and the step list.
     * Every number says where it came from. Numbers marked ASSUMPTION are not in the syllabus
     * document and need a teacher to confirm them.
     * Talks to: read by PendulumController_Azm and the analysis/timing classes.
     */
    [CreateAssetMenu(fileName = "PendulumConfig_Azm", menuName = "VirtualLab_Azm/Pendulum Config")]
    public class PendulumConfig_Azm : ScriptableObject
    {
        [Header("Physics (true values the student must recover)")]
        [Tooltip("True g driving the simulation, m/s^2. The student's job is to find it from their own data.")]
        public float trueGravity = 9.8f;

        [Header("Procedure (syllabus)")]
        [Tooltip("Oscillations timed per run (syllabus: 20).")]
        public int oscillationCount = 20;
        [Tooltip("Different lengths needed before analysis (syllabus: at least 5).")]
        public int minimumLengths = 5;
        [Tooltip("TRUE bob diameter, cm. The swing uses it; the student must read it on the caliper (within one least count).")]
        public float bobDiameterCm = 2.0f;
        [Tooltip("On: the student measures d with the vernier caliper (syllabus procedure step 2). Off (fallback): d is given in the Intro step.")]
        public bool useCaliperStep = true;

        [Header("Thread length ℓ (pivot to top of bob)")]
        public float minLengthCm = 30f;
        public float maxLengthCm = 100f;
        [Tooltip("Smallest step of the length, cm.")]
        public float lengthResolutionCm = 0.1f;
        public float startingLengthCm = 60f;
        [Tooltip("Two lengths closer than this (cm) do not give separate graph points and are rejected.")]
        public float minLengthSeparationCm = 2f;

        [Header("Release")]
        [Tooltip("Syllabus: small displacement, at most 15 degrees, so that simple harmonic motion holds.")]
        public float maxReleaseAngleDeg = 15f;
        [Tooltip("ASSUMPTION: a release smaller than this is too small to time reliably.")]
        public float minReleaseAngleDeg = 3f;
        [Tooltip("How far the student may drag the bob sideways, so the red zone can be shown.")]
        public float maxDragAngleDeg = 35f;
        [Tooltip("ASSUMPTION: bob speed (deg/s) at the moment the finger lifts above which the release counts as a push (syllabus: release from rest). Tune by hand.")]
        public float pushSpeedThresholdDegPerSec = 45f;

        [Header("Timing quality (silent tracking, used for feedback in Stage 4)")]
        [Tooltip("ASSUMPTION: a tap with |angle|/amplitude above this is 'off the mean position'.")]
        [Range(0.1f, 1f)] public float offMeanAngleFraction = 0.65f;
        [Tooltip("ASSUMPTION: average tap interval below this fraction of the true period means the student taps every pass (half-swing counting).")]
        [Range(0.3f, 0.9f)] public float halfSwingIntervalRatio = 0.7f;
        [Tooltip("Taps needed before half-swing counting is judged.")]
        public int halfSwingMinIntervals = 3;
        [Tooltip("ASSUMPTION: an interval above this multiple of the true period means a missed count.")]
        public float missedCountIntervalRatio = 1.6f;

        [Header("Analysis")]
        [Tooltip("ASSUMPTION: the fitted line 'passes close to the origin' when |intercept| is below this fraction of the largest T^2.")]
        [Range(0.01f, 0.5f)] public float originInterceptFraction = 0.10f;

        [Header("Steps")]
        public List<StepDefinition_Azm> steps = new List<StepDefinition_Azm>();

        private void Reset()
        {
            steps = CreateDefaultSteps();
        }

        // The step that measures the bob with the caliper (syllabus procedure step 2). Kept separate
        // so the scene builder can add it to a step list saved before Stage 3.
        public static StepDefinition_Azm CreateMeasureDiameterStep()
        {
            return new StepDefinition_Azm(StepId_Azm.MeasureDiameter,
                "Measure the bob's diameter d",
                CameraViewId_Azm.Caliper, ContextControls_Azm.Caliper, DrawerAction_Azm.Close,
                "The thread length ℓ is measured to the TOP of the bob, but the bob swings about its centre. You need the radius r = d/2 to get the real (effective) length L = ℓ + r.", 1);
        }

        // The syllabus loop. Only one of Intro (d given) and MeasureDiameter (d measured) runs,
        // chosen by useCaliperStep. Steps SetLength..Record repeat for every length.
        // Instructions are short actions (HCI restructure, ~8 words); the reasons are in "why".
        // guideProcedureIndex points at the matching line of the guide's procedure (0-based).
        public static List<StepDefinition_Azm> CreateDefaultSteps()
        {
            return new List<StepDefinition_Azm>
            {
                new StepDefinition_Azm(StepId_Azm.Intro,
                    "Bob diameter d = 2.00 cm (given)",
                    CameraViewId_Azm.Overview, ContextControls_Azm.Continue, DrawerAction_Azm.Keep,
                    "With d given, r = d/2 = 1.00 cm. The bob swings about its centre, so the effective length is L = ℓ + r.", 1),
                CreateMeasureDiameterStep(),
                new StepDefinition_Azm(StepId_Azm.SetLength,
                    "Drag the bob to set the length ℓ",
                    CameraViewId_Azm.Rig, ContextControls_Azm.ConfirmLength, DrawerAction_Azm.Close,
                    "You time the pendulum at several different lengths, then plot T² against L. Each length gives one point on the graph.", 2),
                new StepDefinition_Azm(StepId_Azm.Release,
                    "Pull the bob aside (≤ 15°), let go",
                    CameraViewId_Azm.Rig, ContextControls_Azm.None, DrawerAction_Azm.Keep,
                    "Only small swings (15° or less) are simple harmonic motion, where T = 2π√(L/g) holds. Let go from rest - a push adds energy.", 4),
                new StepDefinition_Azm(StepId_Azm.TimeRun1,
                    "Tap at the centre, then each full swing",
                    CameraViewId_Azm.Timing, ContextControls_Azm.TapTimer, DrawerAction_Azm.Keep,
                    "Timing 20 oscillations, not one, spreads your reaction-time error over 20 swings. Start and stop at the centre: the bob is fastest there, so the moment is easiest to judge.", 5),
                new StepDefinition_Azm(StepId_Azm.TimeRun2,
                    "Time 20 oscillations again",
                    CameraViewId_Azm.Timing, ContextControls_Azm.TapTimer, DrawerAction_Azm.Keep,
                    "Two runs averaged reduce the effect of one badly timed run.", 8),
                new StepDefinition_Azm(StepId_Azm.Record,
                    "Row recorded - add another length",
                    CameraViewId_Azm.Rig, ContextControls_Azm.NextLength | ContextControls_Azm.FinishAnalyse, DrawerAction_Azm.OpenData,
                    "The syllabus asks for at least 5 different lengths, so the graph has enough points to show a straight line.", 10),
                new StepDefinition_Azm(StepId_Azm.Analyse,
                    "Check your graph: T² against L",
                    CameraViewId_Azm.Overview, ContextControls_Azm.ShowResults, DrawerAction_Azm.OpenGraph,
                    "T² = (4π²/g) L, so T² against L is a straight line through the origin with slope 4π²/g. That gives g = 4π² / slope.", 13),
                new StepDefinition_Azm(StepId_Azm.Results,
                    "Your result",
                    CameraViewId_Azm.Overview, ContextControls_Azm.None, DrawerAction_Azm.Close, "", 14)
            };
        }

        // Copies the default instruction, "why" and guide index into an existing asset (by step id),
        // so a rebuilt scene gets the new wording without losing other changes made in the asset.
        public void ApplyDefaultTexts()
        {
            var defaults = CreateDefaultSteps();
            foreach (var step in steps)
                foreach (var d in defaults)
                    if (d.id == step.id)
                    {
                        step.instruction = d.instruction;
                        step.why = d.why;
                        step.guideProcedureIndex = d.guideProcedureIndex;
                    }
        }
    }
}
