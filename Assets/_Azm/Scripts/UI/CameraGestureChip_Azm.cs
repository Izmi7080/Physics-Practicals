using UnityEngine;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (onboarding; HCI restructure Stage 5).
     * Purpose: A small chip at the bottom of the scene window - "Drag empty space to look around ·
     * pinch to zoom" - shown after the HUD tour until the student moves the camera once (then it is
     * never shown again: tutorial flag "camera.gestures") or for showSeconds. If it timed out
     * unused it comes back in the next practical, because the student evidently has not found it.
     * WHY: orbit and zoom are invisible gestures; without a prompt most students never discover
     * that they can look at the scale from the side.
     * Talks to: PracticalCameraRig_Azm (UserMovedCamera event), SettingsManager_Azm (flag),
     * Motion_Azm (fade/pop).
     */
    [RequireComponent(typeof(CanvasGroup))]
    public class CameraGestureChip_Azm : MonoBehaviour
    {
        public const string TutorialId = "camera.gestures";
        public const string Text = "Drag empty space to look around · pinch to zoom";

        [Tooltip("ASSUMPTION (feel): long enough to read twice, short enough not to become furniture.")]
        public float showSeconds = 8f;

        private PracticalCameraRig_Azm rig;
        private CanvasGroup group;
        private float hideAt;
        private bool showing;

        public void ShowOnce(PracticalCameraRig_Azm cameraRig)
        {
            var settings = SettingsManager_Azm.Instance;
            if (settings == null || settings.HasSeenTutorial(TutorialId) || cameraRig == null) { gameObject.SetActive(false); return; }
            rig = cameraRig;
            rig.UserMovedCamera += OnMoved;
            group = GetComponent<CanvasGroup>();
            showing = true;
            hideAt = Time.unscaledTime + showSeconds;
            gameObject.SetActive(true);
            group.alpha = 0f;
            Motion_Azm.FadeIn(group);
            Motion_Azm.PopIn(transform, 0.9f);
        }

        // The student found the gesture: done for good.
        private void OnMoved()
        {
            if (SettingsManager_Azm.Instance != null) SettingsManager_Azm.Instance.MarkTutorialSeen(TutorialId);
            Hide();
        }

        private void Update()
        {
            if (showing && Time.unscaledTime > hideAt) Hide();
        }

        private void Hide()
        {
            if (!showing) return;
            showing = false;
            if (rig != null) rig.UserMovedCamera -= OnMoved;
            Motion_Azm.FadeOut(group);
        }

        private void OnDestroy()
        {
            if (rig != null) rig.UserMovedCamera -= OnMoved;
        }
    }
}
