using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public partial class NaphthalenePracticalSession
{
    readonly List<PracticalTrial> completedTrials=new List<PracticalTrial>();
    public IReadOnlyList<PracticalTrial> CompletedTrials { get { return completedTrials; } }
    public int TrialNumber { get { return saved==null?1:saved.currentTrial; } }
    public int RequiredTrials { get { return Practice?1:Mathf.Max(1,Definition.requiredTrials); } }
    void LoadTrialHistory(bool resume)
    {
        completedTrials.Clear();
        if(resume)foreach(var t in saved.trials)if(t.completed&&t.number<saved.currentTrial)completedTrials.Add(JsonUtility.FromJson<PracticalTrial>(JsonUtility.ToJson(t)));
    }
    public string RecordTrialAndRepeat()
    {
        if(!At("notebook")||ShowingResults||TrialNumber>=RequiredTrials)return "Complete the current trial before repeating.";
        float first,second;
        if(!PracticalTrialStatistics.Reading(Draft.t1,out first)||!PracticalTrialStatistics.Reading(IsBoiling?Draft.t1:Draft.t2,out second)||second<first)
            return IsBoiling?"Enter the saved continuous-bubble temperature.":"Enter both saved temperatures. The final reading must be at least the first.";
        if(!Simulation.HasRequiredObservations)return "Record the required observations before repeating.";
        if(string.IsNullOrWhiteSpace(Draft.initialAppearance)||string.IsNullOrWhiteSpace(Draft.onsetDescription)||(!IsBoiling&&string.IsNullOrWhiteSpace(Draft.completionDescription)))
            return IsBoiling?"Describe the initial sample and the bubble stream.":"Describe the initial sample, first liquid and final disappearance of solid.";
        var record=CurrentTrial(true);completedTrials.Add(record);
        Draft.trial1Reading=Draft.t1;
        saved.currentTrial++;
        Draft.t1=Draft.t2=Draft.initialAppearance=Draft.groundAppearance=Draft.onsetDescription=Draft.completionDescription="";
        StopAllCoroutines();Busy=false;Sealing=false;StopTap();toolHeld=false;drag.Cancel();
        ui.CloseForTrialTransition();ShowingResults=false;IsPaused=false;Time.timeScale=1;
        manager.ResetBath();manager.paused=false;
        preparation.capillarySealed=false;preparation.collected=false;preparation.capillaryPlaced=false;preparation.band=false;
        preparation.clamped=preparation.immersed=preparation.stirrer=preparation.burnerStarted=preparation.shutdownConfirmed=false;
        preparation.sampleHeightMm=preparation.packedHeightMm=0;preparation.packing=0;preparation.sampleAccepted=false;
        preparation.sample=preparation.pestle=false;preparation.grind=0;preparation.liquidFraction=0;preparation.capillaryInverted=false;
        // The second run uses a fresh sample, capillary and cool bath.
        preparation.waterVolumeMl=WaterSettings.targetMl;
        ResetVisuals();RestoreCompleted();ui.ShowRun();steps.GoTo("seal");cameraDirector.SnapTo("Overview");
        Message="Trial "+TrialNumber+": prepare a fresh sample and capillary. The water bath is cool again.";
        SaveCheckpoint();return "";
    }
    PracticalTrial ScoreTrial(PracticalTrial trial)
    {
        float first,second;PracticalTrialStatistics.Reading(trial.enteredOnset,out first);PracticalTrialStatistics.Reading(trial.enteredCompletion,out second);
        trial.preparationScore=preparation.IsPrepared(Definition)?40+(IsBoiling?(Mathf.Abs(preparation.liquidFraction-1f/3f)<=.06f?10:5):preparation.QualityScore(SampleSettings)):0;
        trial.heatingScore=Simulation.HeatingScore;trial.stirringScore=Simulation.StirringScore;
        trial.timingScore=(Simulation.IsTimely(Simulation.onset)?5:0)+(Simulation.IsTimely(Simulation.completion)?5:0);
        trial.readingScore=(Mathf.Abs(first-Simulation.onset.temperature)<=Simulation.settings.readingTolerance?5:0)+(Mathf.Abs(second-Simulation.completion.temperature)<=Simulation.settings.readingTolerance?5:0);
        if(IsBoiling){trial.timingScore=Simulation.IsTimely(Simulation.onset)?10:0;trial.readingScore=Mathf.Abs(first-Simulation.onset.temperature)<=Simulation.settings.readingTolerance?10:0;trial.observationFormat="continuous-bubbles-v1";}
        trial.score=trial.preparationScore+trial.heatingScore+trial.stirringScore+trial.timingScore+trial.readingScore;
        trial.feedback=SampleFeedback()+string.Join("\n\n",Simulation.Feedback(first,second));
        trial.initialAppearance=Draft.initialAppearance;trial.groundAppearance=Draft.groundAppearance;trial.onsetDescription=Draft.onsetDescription;trial.completionDescription=Draft.completionDescription;
        return trial;
    }
}
