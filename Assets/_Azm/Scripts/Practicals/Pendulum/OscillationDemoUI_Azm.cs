using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI (a teaching animation; it does not touch the experiment).
     * Purpose: The Learn-mode OSCILLATION DEMONSTRATION (TASK_menus_and_pendulum 4.3, high
     * priority). Beginners often think one swing to the side is an oscillation, or start timing at
     * an extreme. This plays ONE full oscillation slowly with a simple 2D pendulum:
     *   centre -> one extreme -> centre -> other extreme -> back to the centre (moving the same way),
     * tracing the bob's path, a counter going 0 -> 1, and a marker at the centre that flashes at the
     * two moments the stopwatch should start and stop. A caption names each quarter. It loops until
     * the student taps "Got it". Shown before the first timing run in Learn mode, and replayable
     * from the tap button area and the Guide tab.
     * Drawn with plain UI images (no 3D, no art): the thread is a thin image rotated about the pivot.
     * Talks to: its own UI parts (built by the scene builder); raises Closed.
     */
    [RequireComponent(typeof(PanelTransition_Azm))]
    public class OscillationDemoUI_Azm : MonoBehaviour
    {
        public RectTransform stage;          // the drawing area; the pivot is at its top centre
        public RectTransform thread;         // pivot (0.5, 1)
        public RectTransform bob;
        public Image centreMarker;           // vertical line under the pivot
        public Image traceDotTemplate;       // small circle, copied for the path
        public TMP_Text counterText;
        public TMP_Text captionText;
        public Button gotItButton;

        [Tooltip("Seconds for the one demonstrated oscillation (slowed down so each quarter can be read).")]
        public float periodSeconds = 5f;
        public float amplitudeDeg = 14f;
        public float holdSeconds = 1.6f;

        public event Action Closed;
        public bool IsOpen => transition != null && transition.IsVisible;

        private PanelTransition_Azm transition;
        private readonly List<Image> trace = new List<Image>();
        private float time;
        private int lastQuarter = -1;
        private int lastDot;
        private const int TraceDots = 48;

        private void Awake()
        {
            transition = GetComponent<PanelTransition_Azm>();
            gotItButton.onClick.AddListener(Close);
            for (int i = 0; i < TraceDots; i++)
            {
                var dot = Instantiate(traceDotTemplate, traceDotTemplate.transform.parent);
                dot.gameObject.SetActive(false);
                trace.Add(dot);
            }
            traceDotTemplate.gameObject.SetActive(false);
        }

        public void Show()
        {
            if (transition == null) transition = GetComponent<PanelTransition_Azm>();
            time = 0f;
            lastQuarter = -1;
            ResetTrace();
            transition.Show();
        }

        public void Close()
        {
            if (!IsOpen) return;
            transition.Hide();
            Closed?.Invoke();
        }

        private void ResetTrace()
        {
            foreach (var d in trace) d.gameObject.SetActive(false);
            lastDot = 0;
            counterText.text = "0";
        }

        private void Update()
        {
            if (!IsOpen) return;
            time += Time.unscaledDeltaTime;
            float cycle = periodSeconds + holdSeconds;
            if (time > cycle) { time -= cycle; lastQuarter = -1; ResetTrace(); }

            float t = Mathf.Min(time, periodSeconds);
            // Starting AT the centre, moving right: θ = A sin(2π t / P).
            float theta = amplitudeDeg * Mathf.Sin(2f * Mathf.PI * t / periodSeconds);
            Place(theta);

            // Trace the path (one dot every 1/TraceDots of the oscillation).
            int dotIndex = Mathf.FloorToInt(t / periodSeconds * TraceDots);
            while (lastDot < dotIndex && lastDot < trace.Count)
            {
                var dot = trace[lastDot++];
                dot.gameObject.SetActive(true);
                dot.rectTransform.anchoredPosition = bob.anchoredPosition;
            }

            // Caption stages: 0 = start at the centre (first 6 % of the swing), 1..3 = the first three
            // quarters, 4 = the last quarter and the hold, when the oscillation is complete.
            int stage = time >= periodSeconds ? 5
                : t < periodSeconds * 0.06f ? 0
                : Mathf.Min(4, 1 + Mathf.FloorToInt(t / (periodSeconds * 0.25f)));
            if (stage != lastQuarter)
            {
                lastQuarter = stage;
                captionText.text = PendulumTexts_Azm.DemoCaptions[Mathf.Min(stage, 4)];
                if (stage != 5) Motion_Azm.PopIn(captionText.transform, 0.96f);
                // The stopwatch starts and stops as the bob passes the centre: flash the marker there.
                if (stage == 0 || stage == 5) Motion_Azm.Flash(centreMarker, Color.white, Motion_Azm.Slow);
                if (stage == 5)
                {
                    counterText.text = "1";
                    Motion_Azm.Punch(counterText.transform, 0.3f);
                    FeedbackFX_Azm.Tick();
                }
            }
        }

        private void Place(float thetaDeg)
        {
            float length = stage.rect.height * 0.72f;
            float rad = thetaDeg * Mathf.Deg2Rad;
            Vector2 pivot = new Vector2(0f, stage.rect.height * 0.5f - 10f);
            thread.anchoredPosition = pivot;
            thread.sizeDelta = new Vector2(4f, length);
            thread.localRotation = Quaternion.Euler(0f, 0f, thetaDeg);
            bob.anchoredPosition = pivot + new Vector2(Mathf.Sin(rad) * length, -Mathf.Cos(rad) * length);
        }
    }
}
