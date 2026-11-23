using System;

[Serializable]
public class WaterBathSettings
{
    public float capacityMl=250,targetMl=100,toleranceMl=10,flowMlPerSecond=25;
}

// Volume is independent of camera, meshes and frame rate.
[Serializable]
public class WaterBathFill
{
    public float volumeMl;
    public bool tapOn,atSink,visitedSink,returnedToBath,overflowed;
    public void Tick(float seconds,WaterBathSettings settings,bool receiving)
    {
        if(!tapOn||!receiving||!atSink||seconds<=0)return;
        volumeMl+=seconds*settings.flowMlPerSecond;
        if(volumeMl>settings.capacityMl){volumeMl=settings.capacityMl;overflowed=true;}
    }
    public bool NearTarget(WaterBathSettings s) { return Math.Abs(volumeMl-s.targetMl)<=s.toleranceMl; }
    public bool Transportable(WaterBathSettings s,bool guided)
    { return visitedSink&&!tapOn&&(guided?NearTarget(s):volumeMl>=60&&volumeMl<=s.capacityMl*.84f); }
    public void Empty() { volumeMl=0;overflowed=false;returnedToBath=false; }
}
