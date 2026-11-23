/*
 * Layer: Core (shared data type).
 * Purpose: The three science subjects the app is organised around. Used by PracticalInfo_Azm to
 * tag each practical and by the practical-select screen to build its subject tabs.
 * Talks to: nothing - a plain enum.
 */
namespace VirtualLab.Azm.Core
{
    public enum SubjectType_Azm
    {
        Physics,
        Chemistry,
        Biology
    }
}
