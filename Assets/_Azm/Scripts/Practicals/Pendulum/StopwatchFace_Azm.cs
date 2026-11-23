using TMPro;
using UnityEngine;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI / presentation (world-space, on the instrument tray).
     * Purpose: The stopwatch face on the tray, showing the running time "mm:ss.cc" of the current
     * timing run. It runs exactly as the student's own taps define it: started by the first tap,
     * stopped by the last counted tap (the time is the simulation's clock, which stops while paused).
     * Talks to: reads PendulumController_Azm.StopwatchSeconds; writes a TMP label.
     */
    public class StopwatchFace_Azm : MonoBehaviour
    {
        public PendulumController_Azm controller;
        public TMP_Text label;

        private void LateUpdate()
        {
            label.text = PendulumTexts_Azm.Stopwatch(controller.StopwatchSeconds);
        }
    }
}
