using System;
using System.Collections;
using UnityEngine;

/*
 * Layer: Practical logic support (station-based navigation) - the one IStationNavigator
 * implementation for today's desktop/mobile-touch build.
 * Purpose: Moves a rig Transform (the Player) instantly to a Station's anchor position and
 * rotation, hidden behind a short fade to black rather than an interpolated glide. Per
 * TASK_system_architecture.md's Global Constraints > Movement, smooth locomotion under 3DoF
 * head tracking is one of the most reliable ways to make a student motion-sick on a
 * phone-based headset, so the transition is deliberately a fade/snap, never a lerp between
 * positions - this is a comfort decision, not a shortcut.
 * The fade quad lives on a WORLD-SPACE canvas parented to the camera (the "camera-anchored"
 * category from the Global Constraints > UI space rule), not a Screen Space Overlay canvas,
 * so this component is VR-structurally-correct from Stage 0 instead of needing to be rebuilt
 * during the Stage 2 UI migration.
 * Talks to: moves rigTransform (assign the Player root) and fades fadeCanvasGroup; raises
 * StationChanged so station-select UI / practical logic can react (e.g. re-enabling controls
 * once settled at the new station).
 */
public class FadeStationNavigator : MonoBehaviour, IStationNavigator
{
    [Tooltip("The root transform that represents where the student is standing/looking - the Player rig.")]
    public Transform rigTransform;

    [Tooltip("World-space CanvasGroup (parented to the camera) holding a fullscreen black Image, alpha 0 by default.")]
    public CanvasGroup fadeCanvasGroup;

    [Tooltip("Seconds to fade to black and back (each direction) - a full transition takes 2x this.")]
    public float fadeSeconds = 0.25f;

    public Station CurrentStation { get; private set; }
    public bool IsTransitioning { get; private set; }

    public event Action<Station> StationChanged;

    public bool MoveTo(Station target)
    {
        if (target == null || IsTransitioning || target == CurrentStation) return false;

        if (!target.IsAvailable)
        {
            // Gated stations fail loudly to the console rather than silently doing nothing -
            // an author wiring up station-select UI needs to notice a station never responds.
            Debug.Log($"Station '{target.stationId}' unavailable: {target.UnavailableReason}");
            return false;
        }

        StartCoroutine(TransitionRoutine(target));
        return true;
    }

    private IEnumerator TransitionRoutine(Station target)
    {
        IsTransitioning = true;

        yield return Fade(0f, 1f); // fade to black

        // The actual "movement" is an instant teleport of the rig, performed while the screen
        // is fully black - there is no in-between frame where the student sees themselves slide
        // through the room, which is what the no-smooth-locomotion rule is protecting against.
        if (rigTransform != null)
        {
            rigTransform.SetPositionAndRotation(target.AnchorPosition, target.AnchorRotation);
        }
        CurrentStation = target;

        yield return Fade(1f, 0f); // fade back in, already at the new station

        IsTransitioning = false;
        StationChanged?.Invoke(target);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeCanvasGroup == null) yield break;

        float t = 0f;
        fadeCanvasGroup.alpha = from;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeSeconds));
            yield return null;
        }
        fadeCanvasGroup.alpha = to;
    }
}
