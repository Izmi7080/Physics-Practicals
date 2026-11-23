using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VirtualLab.Physics81
{
    // First milestone: validate the real table-to-tap route before adding the jug and thermal model.
    public sealed class LabRoutePreview : MonoBehaviour
    {
        [SerializeField] LabWalkRig walker;
        [SerializeField] Transform table, sink, sinkView;
        [SerializeField] RectTransform safeArea;
        [SerializeField] Text instruction, direction, actionText;
        [SerializeField] Button action, pauseButton;
        [SerializeField] Text pauseText;
        [SerializeField] GameObject joystick;
        bool reachedSink, returned;
        Rect lastSafe;
        Vector2Int lastSize;

        public void Configure(LabWalkRig rig, Transform tableStation, Transform sinkStation, Transform inspect,
            RectTransform safe, Text cue, Text bearing, Button context, Text contextLabel,
            Button pause, Text pauseLabel, GameObject movement)
        {
            walker = rig; table = tableStation; sink = sinkStation; sinkView = inspect;
            safeArea = safe; instruction = cue; direction = bearing; action = context;
            actionText = contextLabel; pauseButton = pause; pauseText = pauseLabel; joystick = movement;
        }

        void OnEnable() { action.onClick.AddListener(UseStation); pauseButton.onClick.AddListener(TogglePause); }
        void OnDisable() { action.onClick.RemoveListener(UseStation); pauseButton.onClick.RemoveListener(TogglePause); }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);
        bool At(Transform station) => Flat(station.position - walker.transform.position).sqrMagnitude <= .7f * .7f;

        void Update()
        {
            UpdateSafeArea();
            if (!walker.IsSuspended)
            {
                if (At(sink)) reachedSink = true;
                if (reachedSink && At(table)) returned = true;
            }
            bool inspecting = walker.IsInspecting;
            bool paused = walker.IsSuspended;
            Transform destination = reachedSink ? table : sink;
            Vector3 delta = Flat(destination.position - walker.transform.position);
            float angle = Vector3.SignedAngle(walker.transform.forward, delta, Vector3.up);
            string heading = Mathf.Abs(angle) > 150f ? "behind" : angle < -25f ? "left" : angle > 25f ? "right" : "ahead";
            direction.text = paused || inspecting || returned || delta.magnitude < .7f ? "" :
                $"{(reachedSink ? "Centre table" : "Main sink")}  ·  {delta.magnitude:F1} m  ·  {heading}";
            instruction.text = paused ? "Paused" : inspecting ? "Original lab tap · sink inspection" : returned ?
                "Route complete · centre table" : !reachedSink ? "Walk from the centre table to the main sink." :
                At(sink) ? "Main sink reached. Inspect the original tap or walk back." : "Walk back to the centre table.";
            joystick.SetActive(walker.CanWalk);
            action.gameObject.SetActive(!paused && (inspecting || At(sink) || returned));
            actionText.text = inspecting ? "Back to room" : At(sink) ? "Inspect tap" : "Walk again";
            pauseText.text = paused ? "Resume" : "Pause";
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (inspecting && !paused) walker.LeaveInspection();
                else TogglePause();
            }
        }

        void UseStation()
        {
            if (walker.IsSuspended) return;
            if (walker.IsInspecting) walker.LeaveInspection();
            else if (At(sink)) walker.Inspect(sinkView);
            else if (returned) { reachedSink = false; returned = false; }
        }

        void TogglePause() => walker.Suspend(!walker.IsSuspended);

        void UpdateSafeArea()
        {
            Rect rect = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if ((lastSafe == rect && lastSize == size) || size.x < 1 || size.y < 1) return;
            lastSafe = rect; lastSize = size;
            safeArea.anchorMin = new Vector2(rect.xMin / size.x, rect.yMin / size.y);
            safeArea.anchorMax = new Vector2(rect.xMax / size.x, rect.yMax / size.y);
        }
    }
}
