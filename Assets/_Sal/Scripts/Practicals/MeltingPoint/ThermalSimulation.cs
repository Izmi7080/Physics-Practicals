using System;
using System.Collections.Generic;
using UnityEngine;

// Shared bath and instrument, with a separate sample model for each experiment type.
[Serializable]
public abstract class ThermalSimulation
{
    public ThermalSettings settings=new ThermalSettings();
    public float bath=25,sample=25,thermometer=25,heatSetting=1f/6f;
    public float elapsed,heatingSeconds,stirredSeconds,fastSeconds;
    public float lastStir=-100,onsetTime=-1,endTime=-1;
    public bool lit,overheated;
    public float sampleColumnMm=3,packingFraction=1;
    public MeltingObservation onset=new MeltingObservation(),completion=new MeltingObservation();
    public abstract PracticalWorkflow Workflow { get; }
    public abstract bool HasRequiredObservations { get; }
    public virtual float MeltFraction { get { return 0; } }
    public float ActiveClockScale
    {
        get
        {
            if(settings.transitionClockScale<=0)return settings.clockScale;
            // Ease in over the last two degrees before melting begins.
            float approach = Clamp((bath - settings.onsetTemperature + 2f) / 2f, 0, 1);
            return Mathf.SmoothStep(settings.clockScale, settings.transitionClockScale, approach);
        }
    }
    public float Rate { get { return heatSetting*settings.maximumHeatingRate; } }
    public float StirCoverage { get { return heatingSeconds>0?stirredSeconds/heatingSeconds:0; } }
    public bool RecentlyStirred { get { return elapsed-lastStir<=settings.stirringAllowanceSeconds; } }
    public void Stir(){lastStir=elapsed;}
    public void SetHeat(float value){heatSetting=Clamp(value,0,1);}
    protected float BathStep(float dt)
    {
        elapsed+=dt;
        float clock=ActiveClockScale;
        float thermalDt=dt*clock/settings.clockScale;
        if(lit)
        {
            heatingSeconds+=dt;if(RecentlyStirred)stirredSeconds+=dt;
            if(bath>=settings.slowHeatingTemperature&&Rate>settings.excessiveRate)fastSeconds+=thermalDt;
            bath+=Rate*clock/60*dt;
            if(bath>=settings.maximumBathTemperature){bath=settings.maximumBathTemperature;lit=false;overheated=true;}
        }
        else bath=Math.Max(settings.ambientTemperature,bath-settings.coolingRate*thermalDt);
        thermometer+=(bath-thermometer)*(1-(float)Math.Exp(-thermalDt/settings.thermometerResponseSeconds));
        return thermalDt;
    }
    public abstract void Tick(float seconds);
    public abstract MeltingObservation Capture(bool first);
    public bool IsTimely(MeltingObservation o){return MeltingSimulation.Timely(o,settings.observationAllowanceSeconds);}
    public int HeatingScore { get { return overheated||fastSeconds>10?0:fastSeconds>3?10:20; } }
    public int StirringScore { get { return StirCoverage>=.9f?10:StirCoverage>=.7f?5:0; } }
    public virtual int Score(float first,float second,bool prepared)
    {
        return (prepared?50:0)+HeatingScore+StirringScore+(IsTimely(onset)?5:0)+(IsTimely(completion)?5:0)+
            (onset.captured&&Math.Abs(first-onset.temperature)<=settings.readingTolerance?5:0)+(completion.captured&&Math.Abs(second-completion.temperature)<=settings.readingTolerance?5:0);
    }
    public virtual List<string> Feedback(float first,float second)
    {
        var lines=new List<string>();
        if(overheated)lines.Add("The bath reached "+settings.maximumBathTemperature.ToString("0.#")+" °C and heating stopped. Repeat with gentler heat.");
        if(fastSeconds>3)lines.Add("Heating stayed above "+settings.excessiveRate.ToString("0.#")+" °C/min near the transition. The thermometer can run ahead of the sample.");
        if(StirCoverage<.9f)lines.Add("Stirring coverage: "+(StirCoverage*100).ToString("F0")+"%. Long pauses delay sample heating.");
        if(!IsTimely(onset))lines.Add("The first observation was early or more than "+settings.observationAllowanceSeconds.ToString("0.#")+" seconds after its visible event.");
        if(Workflow==PracticalWorkflow.Melting&&!IsTimely(completion))lines.Add("Complete melting was marked early or late.");
        if(Math.Abs(first-onset.temperature)>settings.readingTolerance||Workflow==PracticalWorkflow.Melting&&Math.Abs(second-completion.temperature)>settings.readingTolerance)lines.Add("An entered reading differs from the saved reading by more than "+settings.readingTolerance.ToString("0.#")+" °C.");
        if(lines.Count==0)lines.Add("Careful technique: slow heating, regular stirring and a prompt observation.");return lines;
    }
    protected static float Clamp(float value,float min,float max){return Math.Max(min,Math.Min(max,value));}
}

// Explicit data transfer keeps Unity's JSON saves independent of polymorphic fields.
[Serializable]
public class ThermalSnapshot
{
    public PracticalWorkflow workflow;
    public ThermalSettings settings;
    public float bath,sample,thermometer,heatSetting,elapsed,heatingSeconds,stirredSeconds,fastSeconds,lastStir,onsetTime,endTime,sampleColumnMm,packingFraction;
    public bool lit,overheated;
    public float liquidFraction=1f/3f,bubbleIntensity;
    public MeltingObservation onset=new MeltingObservation(),completion=new MeltingObservation();
    public static ThermalSnapshot Capture(ThermalSimulation simulation)
    {var snapshot=JsonUtility.FromJson<ThermalSnapshot>(JsonUtility.ToJson(simulation));snapshot.workflow=simulation.Workflow;return snapshot;}
    public ThermalSimulation Restore(PracticalDefinition definition)
    {
        if(workflow!=definition.workflow)throw new InvalidOperationException("Saved thermal workflow does not match the experiment.");
        var simulation=definition.CreateSimulation();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this),simulation);
        if(simulation.settings==null)simulation.settings=definition.thermal.Copy();
        simulation.settings.transitionClockScale=definition.thermal.transitionClockScale;
        return simulation;
    }
}
