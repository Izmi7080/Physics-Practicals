using System;
using System.Collections.Generic;

// Pure simulation: deterministic; seconds here are real seconds.
[Serializable]
public sealed class MeltingObservation
{
    public string scalePath;
    public string kind="melting";
    public float bubbleIntensity;
    public bool captured;
    public float temperature, sampleTemperature, meltFraction, time, secondsAfterEvent;
    public string sampleCondition;
}
[Serializable]
public sealed class MeltingSimulation : ThermalSimulation
{
    public const float ClockScale=8;
    public override PracticalWorkflow Workflow { get { return PracticalWorkflow.Melting; } }
    public override bool HasRequiredObservations { get { return onset.captured&&completion.captured; } }
    public override float MeltFraction { get { return Clamp((sample-settings.onsetTemperature)/(settings.completionTemperature-settings.onsetTemperature),0,1); } }
    public override void Tick(float seconds)
    {
        while(seconds>.00001f)
        {
            float dt=Math.Min(seconds,.02f);seconds-=dt;
            float thermalDt=BathStep(dt);
            float preparationDelay=1+Math.Max(0,sampleColumnMm-settings.nominalSampleColumnMm)*.12f+(1-Clamp(packingFraction,0,1))*.8f;
            sample+=(bath-sample)*(1-(float)Math.Exp(-thermalDt/((RecentlyStirred?settings.stirredResponseSeconds:settings.unstirredResponseSeconds)*preparationDelay)));
            if(sample>=settings.onsetTemperature&&onsetTime<0)onsetTime=elapsed;
            if(sample>=settings.completionTemperature&&endTime<0)endTime=elapsed;
        }
    }
    public override MeltingObservation Capture(bool first)
    {
        float eventTime=first?onsetTime:endTime;
        var observation=new MeltingObservation{captured=true,temperature=thermometer,sampleTemperature=sample,meltFraction=MeltFraction,time=elapsed,secondsAfterEvent=eventTime<0?-1:elapsed-eventTime,
            sampleCondition=MeltFraction<=0?"Granular solid":MeltFraction>=1?"Clear liquid; no solid remains":MeltFraction<.15f?"First wetting / liquid":"Partly melted; solid remains",kind=first?"melting-onset":"melting-completion"};
        if(first)onset=observation;else completion=observation;return observation;
    }
    public static bool Timely(MeltingObservation observation,float allowance=3){return observation.captured&&observation.secondsAfterEvent>=0&&observation.secondsAfterEvent<=allowance;}
}
