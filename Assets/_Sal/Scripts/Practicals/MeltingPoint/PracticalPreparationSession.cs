using System.Collections;
using UnityEngine;

public partial class NaphthalenePracticalSession
{
    public Transform closedCapillaryTip;
    public bool Sealing { get; private set; }
    public bool TaskAvailable(string id) { return PracticalTaskRules.Available(id,preparation,Definition,Simulation); }
    public bool TaskDone(string id) { return PracticalTaskRules.Done(id,preparation,Definition,Simulation); }
    public void ChooseAssessmentTask(string id)
    {
        if(steps.ShowHints||!TaskAvailable(id)||TaskDone(id)||InMenu||ShowingResults||Busy||drag.IsHolding)return;
        if(DocumentOpen)ui.ClosePage();
        steps.GoTo(id);
    }
    string NextAvailableTask()
    {
        foreach(var step in steps.steps)if(!TaskDone(step.id)&&TaskAvailable(step.id))return step.id;
        return "notebook";
    }
    void SelectDroppedTask(string id,string zone)
    {
        if(steps.ShowHints)return;
        string task=zone=="InvertCapillary"?"invert":zone=="AttachFusion"?"attach":zone=="SealCapillary"?"seal":zone=="CollectPowder"?"pack":zone=="AttachCapillary"?"attach":zone=="LoadSample"?"sample":zone=="PlacePestle"?"grind":zone=="ClampThermometer"?"immerse":zone=="PlaceStirrer"?"heat":zone=="FillAtSink"||zone=="PlaceBeaker"&&waterFill.visitedSink?"water":"setup";
        if(TaskAvailable(task)&&!At(task))steps.GoTo(task);
    }
    void ConfigureAssessmentInputs()
    {
        if(!preparation.tripod)Enable("tripod","PlaceTripod");
        if(preparation.tripod&&!preparation.gauze)Enable("gauze","PlaceGauze");
        if(preparation.gauze&&!preparation.beaker&&!waterFill.visitedSink)Enable("beaker","PlaceBeaker");
        if(!preparation.water)
        {
            lookup["beaker"].item.canDrag=!waterFill.tapOn;
            sinkDestination.canSnap=!waterFill.atSink;
            if(preparation.gauze&&CanReturnWater)destinations["PlaceBeaker"].canSnap=true;
        }
        if(!preparation.sample)Enable("sample","LoadSample");
        if(preparation.sample&&!preparation.pestle)Enable("pestle","PlacePestle");
        if(!preparation.capillarySealed)Enable("capillary","SealCapillary");
        else if(TaskAvailable("pack")&&!preparation.sampleAccepted&&preparation.sampleHeightMm<SampleSettings.capacityMm)Enable("capillary","CollectPowder");
        if(preparation.sampleAccepted&&!preparation.capillaryPlaced)Enable("capillary","AttachCapillary");
        if(TaskAvailable("immerse")&&!preparation.clamped)Enable("thermometer","ClampThermometer");
        else if(preparation.clamped&&!preparation.immersed)
        {
            var d=lookup["thermometer"].item;d.canDrag=true;d.movement=DragMovement.Vertical;d.minY=immersedRootY-.02f;d.maxY=immersedRootY+.16f;
        }
        if(preparation.burnerStarted&&!preparation.stirrer&&!Simulation.HasRequiredObservations)Enable("stirrer","PlaceStirrer");
    }
    IEnumerator SealCapillary()
    {
        Busy=true;Sealing=true;var cap=lookup["capillary"].item;
        if(AudioAvailable)sounds.Ignite();
        while(cap.IsGliding)yield return null;
        yield return new WaitForSeconds(1.1f);
        preparation.capillarySealed=true;closedCapillaryTip.gameObject.SetActive(true);Sealing=false;
        cap.GlideTo(cap.HomePosition,cap.HomeRotation,.4f);while(cap.IsGliding)yield return null;
        destinations["SealCapillary"].Clear();Busy=false;Message="Capillary sealed at one end. Let it cool before loading powder.";ConfigureInputs();RefreshReadiness();
    }
}
