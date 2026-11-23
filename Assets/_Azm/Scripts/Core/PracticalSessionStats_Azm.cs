using System.Collections.Generic;
using UnityEngine;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Core (plain data, no MonoBehaviour).
     * Purpose: What happened in ONE run of a practical, counted by the HUD as the student works:
     * wrong readings, hints shown, "Show me" used, help escalations, and the time spent on each
     * step. Used by the results "report card" (TASK_hci_and_juice Stage 7) and the optional local
     * interaction log for the usability study (Stage 8).
     * It is an INSTANCE owned by the scene's HUD - never static - so nothing leaks into the next
     * practical (CLAUDE.md: no static fields holding practical state).
     * Talks to: written by PracticalHUDBase_Azm; read by results UI / the interaction log.
     */
    public class PracticalSessionStats_Azm
    {
        public int WrongReadings { get; private set; }
        public int HintsShown { get; private set; }
        public int ShowMeUsed { get; private set; }
        public int Escalations { get; private set; }
        public float StartedAt { get; } = Time.realtimeSinceStartup;

        // Seconds spent per step index (a step visited twice adds up).
        public readonly Dictionary<int, float> SecondsPerStep = new Dictionary<int, float>();

        private int currentStep = -1;
        private float stepStartedAt;

        public void WrongReading() => WrongReadings++;
        public void Hint() => HintsShown++;
        public void ShowMe() => ShowMeUsed++;
        public void Escalated() => Escalations++;

        public void EnterStep(int index)
        {
            float now = Time.realtimeSinceStartup;
            if (currentStep >= 0)
            {
                SecondsPerStep.TryGetValue(currentStep, out float sum);
                SecondsPerStep[currentStep] = sum + (now - stepStartedAt);
            }
            currentStep = index;
            stepStartedAt = now;
        }

        public float TotalSeconds => Time.realtimeSinceStartup - StartedAt;
    }
}
