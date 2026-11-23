using UnityEngine;

/*
 * Layer: Practical logic support (station-based navigation).
 * Purpose: Marks one navigable "stop" in the lab - a named anchor with a position and look
 * direction that an IStationNavigator moves the student's camera rig to, plus optional gating
 * (e.g. "unavailable until the length is confirmed"). Per TASK_system_architecture.md's Global
 * Constraints > Movement, station-to-station movement is the ONLY way the student moves, in
 * both the desktop build and the eventual VR build - there is no free-roam - so every place the
 * student can stand must be represented as a Station.
 * The station's own Transform position/rotation ARE the anchor position and look direction -
 * there are no separate fields for them - so placing and aiming a station in the Editor is just
 * moving/rotating its GameObject like any other object in the scene.
 * Talks to: read by IStationNavigator implementations (e.g. FadeStationNavigator) to know where
 * to move the rig. Gating state is set by whichever practical/behaviour script owns the gating
 * condition (e.g. PendulumExperimentController), never by the navigator or this component
 * itself - that keeps Station generic across every future practical.
 */
public class Station : MonoBehaviour
{
    [Tooltip("Unique id used to reference this station from code/config, e.g. \"ApparatusBench\".")]
    public string stationId;

    [Tooltip("Human-readable label shown on station-select UI.")]
    public string displayName;

    // Defaults to available; a practical's logic layer can gate it closed (see SetAvailability).
    public bool IsAvailable { get; private set; } = true;
    public string UnavailableReason { get; private set; } = string.Empty;

    // The station IS its Transform - no duplicate position/rotation fields to keep in sync.
    public Vector3 AnchorPosition => transform.position;
    public Quaternion AnchorRotation => transform.rotation;

    /// <summary>
    /// Opens or closes this station for navigation. Called by whatever owns the gating
    /// condition (e.g. a practical requiring a previous step to be confirmed first) - Station
    /// itself has no opinion on WHY a station might be unavailable, only that it can be.
    /// </summary>
    public void SetAvailability(bool available, string reason = "")
    {
        IsAvailable = available;
        UnavailableReason = available ? string.Empty : reason;
    }

    // Visualises the anchor position/facing in the Scene view so stations are easy to place
    // and aim without entering Play mode.
    private void OnDrawGizmos()
    {
        Gizmos.color = IsAvailable ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.6f);
    }
}
