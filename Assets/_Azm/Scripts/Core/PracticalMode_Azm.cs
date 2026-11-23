/*
 * Layer: Core (shared data type).
 * Purpose: Which way a practical is run. Learn = the practical plus the guidance layer (briefing,
 * hints, worked calculations, prediction, reflection). Perform = the SAME practical with the
 * guidance switched off. There is only ever one implementation of a practical; this value just
 * decides which guidance features run (see TASK_menus_and_pendulum.md Stage 4.1).
 * Talks to: stored in SessionInfo_Azm by AppManager_Azm; read by practical scenes.
 */
namespace VirtualLab.Azm.Core
{
    public enum PracticalMode_Azm
    {
        Learn,
        Perform
    }
}
