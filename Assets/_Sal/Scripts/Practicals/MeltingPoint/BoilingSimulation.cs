using System;
using System.Collections.Generic;

// The assigned-book endpoint is a continuous bubble stream from the inverted capillary.
[Serializable]
public sealed class BoilingSimulation : ThermalSimulation
{
    public float liquidFraction=1f/3f,bubbleIntensity;
    public override PracticalWorkflow Workflow { get { return PracticalWorkflow.Boiling; } }
    public override bool HasRequiredObservations { get { return onset.captured; } }
    public bool ContinuousBubbles { get { return bubbleIntensity>=.99f; } }
    public override void Tick(float seconds)
    {
        while(seconds>.00001f)
        {
            float dt=Math.Min(seconds,.02f);seconds-=dt;BathStep(dt);
            float delay=(RecentlyStirred?settings.stirredResponseSeconds:settings.unstirredResponseSeconds)*(1+Math.Max(0,liquidFraction-1f/3f)*3);
            // Heat beyond the boiling point goes into vaporisation rather than unlimited liquid warming.
            float target=Math.Min(bath,settings.onsetTemperature);
            sample+=(target-sample)*(1-(float)Math.Exp(-dt/delay));
            bool continuous=sample>=settings.onsetTemperature-.04f&&bath>=settings.onsetTemperature+.05f;
            bubbleIntensity=continuous?1:Clamp((sample-(settings.onsetTemperature-5))/5,0,.35f);
            if(continuous&&onsetTime<0){onsetTime=endTime=elapsed;}
        }
    }
    public override MeltingObservation Capture(bool first)
    {
        if(!first)throw new InvalidOperationException("Boiling records one continuous-bubble observation per trial.");
        onset=new MeltingObservation{captured=true,temperature=thermometer,sampleTemperature=sample,time=elapsed,
            secondsAfterEvent=onsetTime<0?-1:elapsed-onsetTime,sampleCondition=ContinuousBubbles?"Continuous bubble stream":"Isolated bubbles or still liquid",kind="continuous-bubbles",bubbleIntensity=bubbleIntensity};return onset;
    }
    public override int Score(float first,float second,bool prepared)
    {return (prepared?50:0)+HeatingScore+StirringScore+(IsTimely(onset)?10:0)+(onset.captured&&Math.Abs(first-onset.temperature)<=settings.readingTolerance?10:0);}
    public override List<string> Feedback(float first,float second)
    {
        var result=base.Feedback(first,second);
        if(!IsTimely(onset))result.Add("A few isolated air bubbles do not meet the continuous-stream endpoint.");
        result.Add("The reference is for approximately atmospheric pressure. This virtual method follows the book's heating endpoint.");return result;
    }
}
