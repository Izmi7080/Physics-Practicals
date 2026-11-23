using UnityEngine;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Interaction (contract).
     * Purpose: A drag interaction that can tell the onboarding layer (HCI restructure Stage 5) where
     * its grab point is and what a typical drag looks like, so the gesture coach can show a ghost
     * hand doing it and a pulsing ring can mark "you can grab this" - without the UI knowing anything
     * about calipers, screw gauges or pendulums.
     * Implemented by: CaliperDragInteraction_Azm (sliding jaw), ScrewGaugeDragInteraction_Azm
     * (thimble), BobDragInteraction_Azm (the bob: length and release).
     * Read by: GrabCoachUI_Azm.
     */
    public interface IGrabbable_Azm : IDragOwner_Azm
    {
        // True while this object can be dragged in the current step.
        bool GrabAvailable { get; }
        // World position of the grab area's centre (where the ring and the ghost finger go).
        Vector3 GrabPoint { get; }
        // World-space offset of a typical first drag (direction × a short distance), e.g. "slide the
        // jaw towards the object". The ghost hand moves from GrabPoint to GrabPoint + GrabHintMotion.
        Vector3 GrabHintMotion { get; }
        // A short key per gesture ("caliper.jaw", "pendulum.length"...): the coach shows each
        // gesture's ghost hand once (tutorial flag) and each ring until the first grab.
        string GrabHintId { get; }
        // True when the apparatus already draws its own grab marker (the pendulum's world ring),
        // so the coach adds only the ghost hand, not a second ring.
        bool HasOwnAffordance { get; }
    }
}
