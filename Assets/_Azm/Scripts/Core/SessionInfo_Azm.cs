namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Core (plain data class, no behaviour).
     * Purpose: What the student is doing right now - which practical and in which mode. Held by
     * AppManager_Azm so the practical scene can ask "what was I launched with?" after the scene
     * load. It deliberately holds no practical state (trials, counters) - that lives in the
     * scene-local practical controller, so nothing leaks between sessions.
     * Talks to: written by AppManager_Azm.StartPractical; read by practical scenes.
     */
    public class SessionInfo_Azm
    {
        public PracticalInfo_Azm Practical { get; }
        public PracticalMode_Azm Mode { get; }

        public SessionInfo_Azm(PracticalInfo_Azm practical, PracticalMode_Azm mode)
        {
            Practical = practical;
            Mode = mode;
        }
    }
}
