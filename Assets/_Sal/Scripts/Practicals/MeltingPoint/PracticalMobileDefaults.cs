using UnityEngine;

// These settings belong to this practical scene, not the rest of a team's app.
public static class PracticalMobileDefaults
{
    static int users,previousFrameRate,previousVSync,previousSleep;
    public static void Enter()
    {
        if(!Application.isMobilePlatform)return;
        if(users++>0)return;
        previousFrameRate=Application.targetFrameRate;previousVSync=QualitySettings.vSyncCount;previousSleep=Screen.sleepTimeout;
        Application.targetFrameRate=30;QualitySettings.vSyncCount=0;Screen.sleepTimeout=SleepTimeout.NeverSleep;
    }
    public static void Exit()
    {
        if(!Application.isMobilePlatform||users<=0||--users>0)return;
        Application.targetFrameRate=previousFrameRate;QualitySettings.vSyncCount=previousVSync;Screen.sleepTimeout=previousSleep;
    }
}
