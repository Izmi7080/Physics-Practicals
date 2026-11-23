using System;

/*
 * Layer: Practical logic support (station-based navigation).
 * Purpose: Device-agnostic contract for moving the student's camera rig between Stations.
 * Station-to-station movement is the ONLY movement model in this app, in both the desktop
 * build and the eventual VR build (see TASK_system_architecture.md, Global Constraints >
 * Movement) - no free-roam character controller. Today there is exactly one implementation
 * (FadeStationNavigator); this interface exists so a different transition style (e.g. a
 * snap-only mode driven by a comfort setting) can be swapped in later without any calling
 * code changing, mirroring how IPracticalInput lets input devices be swapped later.
 * Talks to: implemented by FadeStationNavigator. Called by station-select UI (a world-space
 * panel or an in-world station marker, driven through the existing IPracticalInput/EventSystem
 * path - never a hardcoded input check) and, later, the app-flow shell.
 */
public interface IStationNavigator
{
    // The station the rig is currently at (or was most recently moving to). Null before the
    // first MoveTo call.
    Station CurrentStation { get; }

    // True while a transition is in progress - callers should not start a second MoveTo.
    bool IsTransitioning { get; }

    /// <summary>
    /// Begins moving the rig to the given station. No-ops (returns false) if already
    /// transitioning, already at this station, or the station is gated unavailable.
    /// </summary>
    bool MoveTo(Station target);

    // Fired once the rig has arrived and settled at the new station.
    event Action<Station> StationChanged;
}
