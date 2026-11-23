namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Interaction (contract).
     * Purpose: Lets the camera know that a drag belongs to an object interaction (the bob, the
     * caliper jaw...), so the same finger movement does not also orbit the camera.
     * Talks to: implemented by drag interactions; checked by camera controllers.
     */
    public interface IDragOwner_Azm
    {
        bool IsDragging { get; }
    }
}
