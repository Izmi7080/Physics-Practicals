using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[Serializable]
public class PracticalItemBinding
{
    public string id;
    public Draggable item;
    public Vector3 homePosition;
    public Quaternion homeRotation = Quaternion.identity;
}
public partial class NaphthalenePracticalSession : MonoBehaviour
{
    public PracticalDefinition definition;
    public PracticalDefinition Definition { get { return definition!=null?definition:PracticalDefinitions.Load(); } }
    public SamplePreparationSettings SampleSettings { get { return Definition.sample; } }
    public float GrindingTarget { get { return SampleSettings.grindingRevolutions; } }
    internal bool HasLoosePowder => preparation.collected && preparation.packedHeightMm < preparation.sampleHeightMm;
    public string SampleTarget { get { return SampleSettings.ColumnDescription; } }
    public string StepId { get { return steps.Current!=null?steps.Current.id:""; } }
    public bool At(string id) { return steps.IsCurrent(id); }
    static bool IsHeatingStep(string id) { return id=="ignite"||id=="heat"||id=="shutdown"; }
    public StepController steps;
    public MeltingPointManager manager;
    public CameraDirector cameraDirector;
    public DragController drag;
    public PracticalUIController ui;
    public PracticalWorkspace workspace;
    public PracticalItemBinding[] items;
    public SnapZone[] zones;
    public Transform mortarCentre, stirCentre, waterContainer, samplePortion, rubberBand;
    public Transform waterVisual, pouringStream, flameVisual, packedSolid, packedLiquid, mercury;
    public Transform burnerMouth;
    public Transform thermometer, bandAttachedTarget;
    public Transform igniter;
    public Transform powderBed, sampleChunks;
    public float pestleHeight=.065f, mortarRimY=.96f;
    bool toolHeld;
    public bool DocumentOpen { get; private set; }
    public bool HandsFreeStirring { get; private set; }
    public WorkbookDraft Draft { get { return saved==null?null:saved.draft; } }
    public bool ToolHeld { get { return toolHeld; } }
    [SerializeField] Transform thermometerClamp;
    public float immersedRootY = 1.112f, waterSurfaceY = 1.169f, bathBottomY = 1.075f;
    public bool IsPaused { get; private set; }
    public bool InMenu { get; private set; } = true;
    public bool ShowingResults { get; private set; }
    public bool Practice { get; private set; }
    public PreparationState preparation = new PreparationState();
    public string Message { get; private set; }
    public bool WaterSelected { get; private set; }
    public bool Busy { get; private set; }
    public PracticalNotebook LastNotebook { get; private set; }
    public bool HasNotebooks { get { return saved != null && saved.notebooks.Count > 0; } }
    public int Step { get { return steps.currentIndex; } }
    bool ItemMoving { get { if(items==null)return false;foreach(var b in items)if(b.item.IsGliding)return true;return false; } }
    public bool CanInteract { get { return !InMenu && !IsPaused && !DocumentOpen && !ShowingResults && !Busy && !cameraDirector.IsMoving && !ItemMoving; } }
    public bool CanChangeView { get { return CanInteract && !drag.HasGesture && !(At("grind")&&(toolHeld||circling)); } }
    public bool CanUseTool { get { return !InMenu&&!IsPaused&&!DocumentOpen&&!ShowingResults&&!Busy&&!ItemMoving&&!drag.IsHolding&&(At("heat")||!cameraDirector.IsMoving); } }
    public bool CanRecord { get { return At("heat")&&!InMenu&&!IsPaused&&!DocumentOpen&&!ShowingResults&&!Busy&&!drag.IsHolding; } }
    public ThermalSimulation Simulation { get { return manager.simulation; } }
    PracticalSave saved;
    readonly Dictionary<string, PracticalItemBinding> lookup = new Dictionary<string, PracticalItemBinding>();
    internal Draggable BoundItem(string id) => lookup.TryGetValue(id,out var binding)?binding.item:null;
    readonly Dictionary<string, SnapZone> destinations = new Dictionary<string, SnapZone>();
    CircularMotionTracker circle = new CircularMotionTracker();
    bool circling;
    Vector3 waterScale, waterPosition, portionScale, mercuryScale;
    float gestureAngle;
    float completedDwell;
    Material solidMaterial;
    Material liquidMaterial;
    Renderer solidRenderer;
    Vector3 powderStart;
    Vector3 packedScale;

    ChemistryAudio sounds;
    bool audioFocused = true, audioBackgrounded, leavingForLauncher;
    float toolSoundUntil;
    bool AudioAvailable => audioFocused && !audioBackgrounded && !leavingForLauncher;

    public void PlayUiSound()
    {
        if(AudioAvailable)sounds?.Click();
    }

    void Start()
    {
        if(!string.IsNullOrEmpty(PracticalSelection.ExperimentId))definition=PracticalDefinitions.Load(PracticalSelection.ExperimentId);
        Definition.ValidateForLaunch();PracticalMobileDefaults.Enter();steps.Configure(Definition);ConfigureExperimentGeometry();manager.definition=Definition;manager.ResetBath();
        sounds = new ChemistryAudio(transform);
        foreach (var binding in items)
        {
            lookup[binding.id] = binding;
            var captured = binding;
            binding.item.PickedUp += _ => { if(AudioAvailable)sounds.PickUp(); };
            binding.item.Dropped += (d,z) =>
            {
                if (z == null) return;
                Placed(captured.id, z.name);
                StartCoroutine(PlayPlacementWhenSettled(d,z));
            };
        }
        foreach (var zone in zones) destinations[zone.name] = zone;
        if(workspace!=null)workspace.Initialize(this);
        InitializeTap();
        waterScale = waterVisual.localScale; waterPosition = waterVisual.localPosition;
        InstallFillMark();
        portionScale = samplePortion.localScale; powderStart = packedSolid.localPosition;
        packedScale = packedSolid.localScale;
        mercuryScale = mercury != null ? mercury.localScale : Vector3.one;
        solidRenderer = packedSolid.GetComponent<Renderer>();
        if (solidRenderer != null) solidMaterial = solidRenderer.material;
        InstallSampleGrains();
        liquidMaterial=packedLiquid.GetComponent<Renderer>().material;
        liquidMaterial.SetFloat("_Surface",1);liquidMaterial.SetFloat("_SrcBlend",5);liquidMaterial.SetFloat("_DstBlend",10);liquidMaterial.SetFloat("_ZWrite",0);
        liquidMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");liquidMaterial.renderQueue=3001;liquidMaterial.color=new Color(.82f,.92f,.96f,.22f);liquidMaterial.SetFloat("_Smoothness",.85f);
        steps.StepChanged += OnStepChanged;
        steps.PracticalFinished += Finish;
        ui.Initialize(this);
        saved = PracticalProgressStore.Load(Definition);
        if(saved.draft==null)saved.draft=new WorkbookDraft();
        HandsFreeStirring=saved.handsFreeStirring;
        ResetVisuals();
        manager.paused = true;
        steps.ShowMenu(); InMenu = true;
        Message = PracticalProgressStore.LastError ?? "";
        ui.ShowMenu(saved.resumable);
    }
    IEnumerator PlayPlacementWhenSettled(Draggable item, SnapZone zone)
    {
        while(item.IsGliding)yield return null;
        if(item.CurrentZone==zone&&AudioAvailable&&!InMenu&&!IsPaused&&!DocumentOpen)
            sounds.Place();
    }
    void OnDisable(){sounds?.Stop();}
    void OnDestroy()
    {
        PracticalMobileDefaults.Exit();
        Time.timeScale = 1f;
        if (steps != null) { steps.StepChanged -= OnStepChanged; steps.PracticalFinished -= Finish; }
        if (solidMaterial != null) Destroy(solidMaterial);
        if(liquidMaterial!=null)Destroy(liquidMaterial);ReleaseBoilingMaterials();
        ReleaseFillMark();
    }
    public void Begin(PracticalMode mode, bool resume = false)
    {
        StopAllCoroutines(); Busy = false; IsPaused = false; Time.timeScale = 1f;
        DocumentOpen=false;cameraDirector.ClearZoom();
        if(!resume){saved.draft=new WorkbookDraft{date=DateTime.Now.ToString("yyyy-MM-dd")};saved.currentTrial=1;saved.trials.Clear();}
        ShowingResults = false; InMenu = false; Practice = resume && saved.practice;LoadTrialHistory(resume);
        manager.ResetBath(); manager.paused = false;
        steps.mode = resume ? saved.mode : mode;
        // Only completed steps are restored, never partially completed gestures or observations.
        preparation = resume ? JsonUtility.FromJson<PreparationState>(JsonUtility.ToJson(saved.preparation)) : new PreparationState();
        // Preserve preparation completed in older saves that used the fixed four-tap rule.
        if(resume&&steps.IndexOf(saved.stepId)>steps.IndexOf("pack")&&preparation.collected&&preparation.sampleHeightMm<=0)
        {preparation.sampleHeightMm=SampleSettings.maximumColumnMm;preparation.packedHeightMm=SampleSettings.maximumColumnMm;preparation.sampleAccepted=true;}
        ResetVisuals(); int step = resume ? steps.IndexOf(saved.stepId) : 0;
        if(step<0)step=0;
        if (IsHeatingStep(steps.steps[step].id)) { step = steps.IndexOf("ignite"); preparation.stirrer = false;preparation.burnerStarted=false; Message = "Interrupted heating reset: fresh sample and cool water."; }
        else Message = "";
        RestoreCompleted();
        ApplySampleProperties();
        ui.ShowRun();
        if (steps.steps[step].id == "notebook" && saved.simulation != null && saved.simulation.onset.captured && (IsBoiling||saved.simulation.completion.captured))
        {
            manager.simulation=saved.simulation.Restore(Definition);manager.simulation.lit=false;
            ui.CaptureScale(true,Simulation.onset,false);if(!IsBoiling)ui.CaptureScale(false,Simulation.completion,false);
        }
        else if(steps.steps[step].id=="notebook")step=steps.IndexOf("ignite");
        steps.GoTo(step);
        cameraDirector.SnapTo("Overview");
        if(steps.steps[step].id=="notebook")ui.OpenPage("Workbook");
        SaveCheckpoint();
        if(!resume&&mode==PracticalMode.Guided)ui.ShowStudy(true);
    }
    public void Next()
    {
        if (!CanInteract || drag.IsHolding || circling) return;
        RefreshReadiness();
        if (!steps.Ready) return;
        if(At("water")&&!string.IsNullOrEmpty(preparation.waterReturnStep))
        {
            string returnStep=preparation.waterReturnStep;preparation.waterReturnStep="";
            steps.GoTo(returnStep);SaveCheckpoint();return;
        }
        SaveCheckpoint(steps.ShowHints);
        if(!steps.ShowHints)steps.GoTo(NextAvailableTask());else steps.CompleteCurrentStep();
    }
    void OnStepChanged(int index, PracticalStep step)
    {
        completedDwell = 0;
        circling = false; toolHeld=false; circle = new CircularMotionTracker(); Message = "";
        if (step.id == "attach" && !preparation.capillaryPlaced&&!IsBoiling)
        {
            if(workspace!=null)workspace.TakeOut("thermometer");
            var cap = lookup["capillary"].item;
            cap.transform.position = thermometer.position + new Vector3(.055f,.016f,-.025f);
            cap.RememberHome();
        }
        if (step.id == "notebook") manager.StopHeating();
        ConfigureInputs(); RefreshReadiness();
        SaveCheckpoint();
        if(step.id=="setup"||step.id=="water"||step.id=="immerse")QueueActionView("Action");
        if(step.id=="attach")QueueActionView(preparation.capillaryPlaced?"Attachment":"Action");
        if(step.id=="pack")QueueActionView("Capillary");
        if(step.id=="sample"&&IsBoiling)QueueActionView("Sample");
        if(step.id=="ignite")QueueActionView(IsBoiling?"Heating":"Burner");
        if(step.id=="heat"&&!preparation.stirrer)QueueActionView("Action");
        if(step.id=="notebook")ui.OpenPage("Workbook");
    }
    void ConfigureInputs()
    {
        foreach (var b in items) b.item.canDrag = false;
        foreach (var zone in zones) { zone.canSnap = false; zone.SetHighlight(false); }
        if(IsBoiling){ConfigureBoilingInputs();OfferBenchApparatus();return;}
        if(!steps.ShowHints){ConfigureAssessmentInputs();OfferBenchApparatus();return;}
        if (At("setup"))
        {
            if (!preparation.tripod) Enable("tripod", "PlaceTripod");
            else if (!preparation.gauze) Enable("gauze", "PlaceGauze");
            else if (!preparation.beaker) Enable("beaker", "PlaceBeaker");
        }
        if(At("seal")&&!preparation.capillarySealed)Enable("capillary","SealCapillary");
        if (At("sample") && !preparation.sample) Enable("sample", "LoadSample");
        if (At("grind") && !preparation.pestle) Enable("pestle", "PlacePestle");
        if (At("pack") && !preparation.sampleAccepted && !HasLoosePowder && preparation.sampleHeightMm<SampleSettings.maximumColumnMm) Enable("capillary", "CollectPowder");
        if (At("attach") && !preparation.capillaryPlaced) Enable("capillary", "AttachCapillary");
        if (At("immerse"))
        {
            if (!preparation.clamped) Enable("thermometer", "ClampThermometer");
            else if (!preparation.immersed)
            {
                var d = lookup["thermometer"].item; d.movement = DragMovement.Vertical; d.canDrag = true;
                d.minY = immersedRootY - .02f; d.maxY = immersedRootY + .16f;
            }
        }
        if (At("heat") && !preparation.stirrer) Enable("stirrer", "PlaceStirrer");
        if(At("water")&&!preparation.water&&sinkDestination!=null)ConfigureWaterInputs();
        OfferBenchApparatus();
    }
    void Enable(string item, string zone)
    {
        lookup[item].item.canDrag = true;
        destinations[zone].canSnap = true;
        if (steps.ShowHints) destinations[zone].SetHighlight(true);
    }
    public void Placed(string id, string zoneName)
    {
        SelectDroppedTask(id,zoneName);
        if(PlaceBoilingApparatus(id,zoneName))return;
        if(At("seal")&&id=="capillary"&&zoneName=="SealCapillary"){StartCoroutine(SealCapillary());return;}
        if(At("water")&&id=="beaker"){WaterPlaced(zoneName);return;}
        // Every report is checked against the active step: no skipping through stale callbacks.
        if (At("setup") && id == "tripod" && !preparation.tripod) preparation.tripod = true;
        else if (At("setup") && id == "gauze" && preparation.tripod) preparation.gauze = true;
        else if (At("setup") && id == "beaker" && preparation.gauze)
        {
            preparation.beaker = true;
            StartCoroutine(RememberBeakerDestination());
        }
        else if (At("sample") && id == "sample") { preparation.sample = true; samplePortion.position = mortarCentre.position + Vector3.up * .008f; }
        else if (At("grind") && id == "pestle") preparation.pestle = true;
        else if (At("pack") && id == "capillary")
        {
            preparation.collected = true;
            preparation.sampleHeightMm=Mathf.Min(SampleSettings.capacityMm,preparation.sampleHeightMm+SampleSettings.collectionIncrementMm);
            StartCoroutine(CollectPowder());
        }
        else if (At("attach") && id == "capillary")
        {
            preparation.capillaryPlaced = true;
            var cap = lookup["capillary"].item.transform;
            cap.SetParent(thermometer, true);
        }
        else if (At("immerse") && id == "thermometer") preparation.clamped = true;
        else if (At("heat") && id == "stirrer") preparation.stirrer = true;
        else return;
        if(workspace!=null)workspace.TakeOut(id);
        Message = PracticalWorkspace.Label(id)+" placed.";
        ConfigureInputs(); RefreshReadiness();
        if(id=="pestle"&&At("grind"))QueueActionView("Mortar");
        if(id=="capillary"&&At("attach"))QueueActionView("Attachment");
        if(id=="thermometer"&&At("immerse"))QueueActionView("Immersion");
    }
    public bool DepthValid
    {
        get
        {
            // Origins are measured from the authored rig; bulb and band move with the assembly.
            float shift = thermometer.position.y - immersedRootY;
            float bulb = immersedRootY + .017f + shift;
            float band = bandAttachedTarget.position.y;
            float sampleTop=bulb+(IsBoiling?.05f*preparation.liquidFraction:preparation.sampleHeightMm*.001f);
            if(IsBoiling)return bulb>bathBottomY+.01f&&sampleTop<waterSurfaceY-.003f&&band>waterSurfaceY+.005f&&thermometer.position.y+.067f>waterSurfaceY+.005f;
            float opening=thermometer.position.y+.056f;
            return bulb>bathBottomY+.01f&&sampleTop<waterSurfaceY-.003f&&band>waterSurfaceY+.005f&&opening>waterSurfaceY+.010f&&opening<waterSurfaceY+.025f;
        }
    }
    public string ActionLabel
    {
        get
        {
            if (At("water") && !preparation.water) return "Fill";
            if (At("attach") && preparation.capillaryPlaced && !preparation.band) return "Select rubber band";
            if (At("immerse") && preparation.clamped && !preparation.immersed) return "Confirm depth";
            if (At("ignite") && !Simulation.lit) return IsBoiling?"Switch heater on":"Light burner";
            if (At("shutdown") && Simulation.lit) return "Turn off burner";
            return "";
        }
    }
    public void Action()
    {
        if (!CanInteract) return;
        if (At("attach") && preparation.capillaryPlaced && !preparation.band) StartCoroutine(SecureBand());
        else if (At("immerse") && preparation.clamped && !preparation.immersed)
        {
            if (!DepthValid) { Message = steps.ShowHints ? "Adjust depth: immerse the sample, avoid the bottom, and keep the band above water." : "Depth cannot be confirmed."; return; }
            preparation.immersed = true; lookup["thermometer"].item.canDrag = false; Message = "Depth confirmed.";
        }
        else if (At("ignite") && !Simulation.lit) StartCoroutine(Ignite());
        else if (At("shutdown")){manager.StopHeating();preparation.shutdownConfirmed=true;}
        RefreshReadiness();
    }
    IEnumerator Ignite()
    {
        if(IsBoiling)
        {
            Busy=true;manager.StartHeating();preparation.burnerStarted=true;preparation.shutdownConfirmed=false;
            yield return new WaitForSeconds(.25f);Busy=false;Message="Electric water-bath heater switched on.";RefreshReadiness();yield break;
        }
        Busy=true;
        Vector3 start=igniter!=null?igniter.position:Vector3.zero;
        Quaternion rotation=igniter!=null?igniter.rotation:Quaternion.identity;
        if(igniter!=null)igniter.position=flameVisual.position+new Vector3(.025f,.015f,0);
        yield return new WaitForSeconds(.35f);
        if(AudioAvailable)sounds.Ignite();
        manager.StartHeating();preparation.burnerStarted=true;preparation.shutdownConfirmed=false;
        yield return new WaitForSeconds(.35f);
        if(igniter!=null)igniter.SetPositionAndRotation(start,rotation);
        Busy=false;Message="Blue flame lit.";RefreshReadiness();
    }
    IEnumerator CollectPowder()
    {
        Busy = true;
        while(ItemMoving)yield return null;
        yield return new WaitForSeconds(.25f);
        packedSolid.gameObject.SetActive(true);
        var cap = lookup["capillary"].item.transform;
        var start = cap.position; var rotation = cap.rotation;
        for (float t=0;t<.5f;t+=Time.deltaTime)
        {
            cap.SetPositionAndRotation(Vector3.Lerp(start,lookup["capillary"].homePosition,t/.5f),Quaternion.Slerp(rotation,lookup["capillary"].homeRotation,t/.5f));
            yield return null;
        }
        cap.SetPositionAndRotation(lookup["capillary"].homePosition,lookup["capillary"].homeRotation);
        destinations["CollectPowder"].Clear();
        Busy = false; Message="Powder collected. Tap the tube to settle it.";
        ConfigureInputs();
        if(preparation.packing==0)QueueActionView("Capillary");
    }
    public void PackTube()
    {
        if(!CanChangeView||!At("pack")||!HasLoosePowder)return;
        StartCoroutine(PackMotion());
    }
    IEnumerator PackMotion()
    {
        Busy=true;var cap=lookup["capillary"].item.transform;var start=cap.position;
        preparation.packing++;
        float settled=preparation.packedHeightMm;
        float target=Mathf.Min(preparation.sampleHeightMm,settled+SampleSettings.packingIncrementMm);
        for(float t=0;t<.36f;t+=Time.deltaTime)
        {
            float progress=t/.36f;
            cap.position=start+Vector3.up*(Mathf.Abs(Mathf.Sin(progress*2f*Mathf.PI))*.009f);
            preparation.packedHeightMm=Mathf.SmoothStep(settled,target,Mathf.Clamp01((progress-.4f)/.6f));
            yield return null;
        }
        preparation.packedHeightMm=target;
        cap.position=start;
        if(AudioAvailable)sounds.Place();
        Busy=false;Message="Powder settled at the sealed end.";
        RefreshReadiness();ConfigureInputs();
    }
    public void AcceptSample()
    {
        if(!CanChangeView||!At("pack")||preparation.sampleHeightMm<=0)return;
        if(steps.ShowHints&&!SampleSettings.Accepts(preparation.sampleHeightMm,preparation.packedHeightMm))
        {Message="Collect a "+SampleTarget+" column and settle all powder before using it.";return;}
        preparation.sampleAccepted=true;if(workspace!=null)workspace.TakeOut("thermometer");ApplySampleProperties();ConfigureInputs();RefreshReadiness();
    }
    public void EmptyCapillary()
    {
        if(!CanChangeView||!At("pack"))return;
        preparation.sampleHeightMm=preparation.packedHeightMm=0;preparation.packing=0;
        preparation.collected=preparation.sampleAccepted=false;
        destinations["CollectPowder"].Clear();Message="Tube emptied. Collect a fresh portion.";ConfigureInputs();RefreshReadiness();
    }
    void ApplySampleProperties()
    {
        if(IsBoiling){((BoilingSimulation)Simulation).liquidFraction=preparation.liquidFraction;return;}
        Simulation.sampleColumnMm=preparation.sampleHeightMm;
        Simulation.packingFraction=preparation.PackingFraction;
    }
    public void SetToolHeld(bool held)
    {
        toolHeld=held&&CanUseTool&&((At("grind")&&preparation.pestle&&preparation.grind<GrindingTarget)||(At("heat")&&preparation.stirrer));
    }
    public void SetHandsFree(bool value){toolHeld=false;HandsFreeStirring=value;saved.handsFreeStirring=value;SaveCheckpoint();}
    public void ToggleStirring(){if(HandsFreeStirring&&At("heat"))SetToolHeld(!toolHeld);}
    public void OpenDocument()
    {if(InMenu||ShowingResults)return;StopTap();drag.Cancel();toolHeld=false;DocumentOpen=true;manager.paused=true;Time.timeScale=0;SaveCheckpoint();}
    public void CloseDocument()
    {DocumentOpen=false;toolHeld=false;Time.timeScale=IsPaused?0:1;manager.paused=IsPaused||InMenu||ShowingResults;}
    public void SaveDraft(){if(saved!=null)SaveCheckpoint();}
    public string SubmitWorkbook()
    {
        if(!At("notebook"))return "Finish heating and switch off the heat source before submitting.";
        if(TrialNumber<RequiredTrials)return RecordTrialAndRepeat();
        var d=saved.draft;
        if(string.IsNullOrWhiteSpace(d.initialAppearance)||string.IsNullOrWhiteSpace(d.onsetDescription)||(!IsBoiling&&string.IsNullOrWhiteSpace(d.completionDescription))||string.IsNullOrWhiteSpace(d.result)||string.IsNullOrWhiteSpace(d.conclusion)||string.IsNullOrWhiteSpace(d.reflection))
            return "Complete your observations, result, conclusion and technique reflection.";
        Submit(d.t1,d.t2,d.conclusion);return ShowingResults?"":Message;
    }
    void MoveTool(float degrees)
    {
        AddCircularMotion(degrees);float a=gestureAngle*Mathf.Deg2Rad;
        if(At("grind"))lookup["pestle"].item.transform.position=mortarCentre.position+new Vector3(Mathf.Cos(a)*.009f,pestleHeight,Mathf.Sin(a)*.009f);
        else lookup["stirrer"].item.transform.position=stirCentre.position+new Vector3(Mathf.Cos(a)*.019f,.02f,Mathf.Sin(a)*.019f);
    }
    public Vector3 ConstrainDrag(Draggable item,Vector3 target)
    {
        if(workspace!=null)target=workspace.Constrain(item,target);
        if(item.id=="pestle")
        {
            float distance=Vector2.Distance(new Vector2(target.x,target.z),new Vector2(mortarCentre.position.x,mortarCentre.position.z));
            if(distance<.09f)target.y=Mathf.Max(target.y,mortarRimY+pestleHeight);
        }
        return target;
    }
    public void InspectAfterDrop(string id){} // Compatibility: releases never change view or pack.
    public void InspectItem(string id)
    {
        if(!CanChangeView)return;
        if(workspace!=null)workspace.TakeOut(id);
        if(id=="tripod"||id=="gauze"){FrameBenchItem(id);return;}
        string anchor=id=="pestle"||id=="sample"||id=="mortar"?"Mortar":id=="capillary"?"Capillary":id=="fusion"?"Sample":id=="thermometer"?"Thermometer":id=="burner"?"Burner":id=="heater"?"Heating":id=="stirrer"?"Stirrer":id=="beaker"||id=="bath"?"Immersion":id=="packedSample"?"Sample":"Preparation";
        Focus(anchor);
    }
    public void Focus(string anchor)
    {
        if(!CanChangeView)return;
        if (At("water") && (anchor == "Tap" || anchor == "Immersion" || anchor == "Action" || anchor == "Overview"))
        {
            FrameWaterAction();
            return;
        }
        // Close-ups follow the apparatus, including its disassembled position.
        var shot=cameraDirector.anchors.Find(a=>a.name==anchor);
        if(shot!=null)
        {
            Vector3 centre,offset;
            if(anchor=="Thermometer")
            {
                if(At("heat")&&!IsBoiling&&Simulation.onset.captured)inspectedFirstLiquid=Simulation.onset;
                if(At("immerse")&&!preparation.clamped){FrameThermometerPlacement(shot);return;}
                if(!preparation.clamped){centre=(thermometer.position+destinations["ClampThermometer"].transform.position)*.5f+Vector3.up*.08f;offset=new Vector3(.02f,.46f,-.56f);shot.fov=48;}
                else {centre=thermometer.TransformPoint(new Vector3(0,.065f+.22f*Simulation.thermometer/100f,-.006f));offset=thermometer.rotation*new Vector3(0,.01f,-.23f);shot.fov=IsBoiling?28:24;}
            }
            else if(anchor=="Sample"&&IsBoiling)
            {centre=fusionTube.position+Vector3.up*(preparation.capillaryPlaced?.014f:.03f);offset=preparation.capillaryPlaced?thermometer.rotation*new Vector3(.015f,.014f,-.065f):new Vector3(.025f,.04f,-.15f);shot.fov=36;}
            else if(anchor=="Sample")
            {centre=packedSolid.parent.TransformPoint(new Vector3(0,.001f+preparation.sampleHeightMm*.0005f,0));offset=thermometer.rotation*new Vector3(.018f,.006f,-.05f);shot.fov=30;}
            else if(anchor=="Capillary"&&IsBoiling)
            {centre=(fusionTube.position+lookup["capillary"].item.transform.position)*.5f+Vector3.up*.02f;offset=new Vector3(.025f,.22f,-.25f);shot.fov=45;}
            else if(anchor=="Capillary")
            {
                if(!preparation.capillaryPlaced&&!preparation.sampleAccepted){centre=(mortarCentre.position+lookup["capillary"].item.transform.position)*.5f+Vector3.up*.035f;offset=new Vector3(.03f,.30f,-.30f);shot.fov=46;}
                else {centre=lookup["capillary"].item.transform.position+Vector3.up*.033f;offset=new Vector3(.035f,.10f,-.21f);shot.fov=34;}
            }
            else if(anchor=="Mortar")
            {
                if(!preparation.sample){centre=(samplePortion.position+mortarCentre.position)*.5f;offset=new Vector3(.02f,.40f,-.38f);shot.fov=48;}
                else if(!preparation.pestle){centre=(lookup["pestle"].item.transform.position+mortarCentre.position)*.5f+Vector3.up*.04f;offset=new Vector3(.025f,.30f,-.29f);shot.fov=48;}
                else {centre=mortarCentre.position+new Vector3(.005f,.04f,0);offset=new Vector3(.025f,.25f,-.23f);shot.fov=46;}
            }
            else if(anchor=="Attachment")
            {
                if(CanTieThread)StageLooseThread();
                centre=(bandAttachedTarget.position+rubberBand.position)*.5f;
                offset=new Vector3(.025f,.19f,-.21f);
                shot.fov=39;
                cameraDirector.ResetShot(anchor);
            }
            else if(anchor=="Stirrer")
            {centre=preparation.stirrer?stirCentre.position:(stirCentre.position+lookup["stirrer"].item.transform.position)*.5f;offset=new Vector3(.01f,.35f,-.40f);shot.fov=46;}
            else if(anchor=="Preparation")
            {centre=(waterContainer.position+lookup["beaker"].item.transform.position)*.5f+Vector3.up*.06f;offset=new Vector3(.01f,.39f,-.45f);shot.fov=48;}
            else if(anchor=="Immersion")
            {
                centre=lookup["beaker"].item.transform.position+Vector3.up*(At("water")?waterPosition.y+waterScale.y:.075f);
                offset=At("water")?new Vector3(.015f,.006f,-.29f):new Vector3(.035f,.012f,-.40f);shot.fov=At("water")?38:42;
            }
            else if(anchor=="Burner")
            {centre=flameVisual.position-Vector3.up*.015f;offset=new Vector3(.07f,.055f,-.32f);shot.fov=40;}
            else {cameraDirector.MoveTo(anchor,At("heat")||At("shutdown")?.2f:.35f);return;}
            shot.point.position=centre+offset;shot.point.rotation=Quaternion.LookRotation(centre-shot.point.position);
            cameraDirector.SetInspectionTarget(anchor,centre);
        }
        cameraDirector.MoveTo(anchor,At("heat")||At("shutdown")?.2f:.35f);
    }
    internal bool CanTieThread => At("attach")&&preparation.capillaryPlaced&&!preparation.band;

    void StageLooseThread()
    {
        var nearAttachment=bandAttachedTarget.position;
        rubberBand.SetPositionAndRotation(new Vector3(nearAttachment.x+.07f,.847f,nearAttachment.z-.075f),Quaternion.identity);
    }

    internal bool HitLooseThread(Vector2 pointer)
    {
        if(!CanTieThread||!rubberBand.gameObject.activeInHierarchy)return false;
        var camera=cameraDirector.cam;
        var screen=camera.WorldToScreenPoint(rubberBand.position);
        return screen.z>0&&Vector2.Distance(pointer,screen)<=Mathf.Max(18f,camera.pixelHeight*.03f);
    }

    IEnumerator SecureBand()
    {
        Busy = true; var start = rubberBand.position; var rotation = rubberBand.rotation;
        for (float t=0;t<.65f;t+=Time.deltaTime)
        {
            float lift=Mathf.Sin(Mathf.PI*t/.65f)*.022f;
            rubberBand.position = Vector3.Lerp(start,bandAttachedTarget.position,Mathf.SmoothStep(0,1,t/.65f))+Vector3.up*lift;
            rubberBand.rotation = Quaternion.Slerp(rotation,bandAttachedTarget.rotation,t/.65f);
            yield return null;
        }
        rubberBand.SetPositionAndRotation(bandAttachedTarget.position,bandAttachedTarget.rotation);
        rubberBand.SetParent(thermometer,true);
        preparation.band = true; Busy = false; Message = "Thread secured above the sample."; RefreshReadiness();
    }
    void SetWater(float fraction)
    {
        fraction=Mathf.Clamp(fraction,0,WaterSettings.capacityMl/WaterSettings.targetMl);
        waterVisual.gameObject.SetActive(fraction > 0f);
        waterVisual.localScale = new Vector3(waterScale.x, waterScale.y * Mathf.Max(.001f,fraction),waterScale.z);
        waterVisual.localPosition = waterPosition - Vector3.up * (waterScale.y * (1f-fraction));
        waterSurfaceY=waterVisual.TransformPoint(Vector3.up).y;
    }
    public void Mark(bool onset)
    {
        if (!CanRecord) return;
        if(IsBoiling)
        {
            var boiling=Simulation as BoilingSimulation;
            if(!onset)return;
            if(steps.ShowHints&&!boiling.ContinuousBubbles){Message="Wait for a continuous bubble stream; isolated air bubbles are not the endpoint.";return;}
            var observation=boiling.Capture(true);ui.CaptureScale(true,observation);
            if(AudioAvailable)sounds.Record();
            SaveCheckpoint();Message="Continuous-stream reading saved: "+observation.temperature.ToString("F1")+" °C.";RefreshReadiness();return;
        }
        if(steps.ShowHints&&((onset&&Simulation.MeltFraction<=0)||(!onset&&(Simulation.MeltFraction<.999f||!Simulation.onset.captured))))
        {Message=onset?"Still solid: mark first liquid when the sample first looks wet.":"Mark the start first, then wait until no solid remains before marking the end.";return;}
        var o = Simulation.Capture(onset);
        if(onset&&cameraDirector.CurrentAnchorName=="Thermometer"&&!cameraDirector.IsMoving)inspectedFirstLiquid=o;
        Message = onset ? "First-liquid scale saved in your workbook." : "Fully liquid scale saved in your workbook.";
        if (steps.ShowHints && !Simulation.IsTimely(o)) Message += " Check the sample: this mark may be early or late. You can replace it.";
        ui.CaptureScale(onset,o);if(AudioAvailable)sounds.Record();SaveCheckpoint();
        RefreshReadiness();
    }
    public void AddCircularMotion(float degrees)
    {
        if(Mathf.Abs(degrees)>.001f)toolSoundUntil=Time.unscaledTime+.16f;
        if (At("grind") && preparation.pestle)
        {
            bool wasComplete=preparation.grind>=GrindingTarget;
            preparation.grind = Mathf.Min(GrindingTarget,preparation.grind + Mathf.Abs(degrees)/360f);
            samplePortion.GetComponent<Renderer>().enabled=false;
            if(!wasComplete&&preparation.grind>=GrindingTarget)Message="Fine powder ready.";
        }
        else if (At("heat") && preparation.stirrer) Simulation.Stir();
        gestureAngle += degrees;
        RefreshReadiness();
    }
    void Gesture()
    {
        bool grinding = At("grind") && preparation.pestle && preparation.grind < GrindingTarget;
        bool stirring = At("heat") && preparation.stirrer;
        if ((!grinding && !stirring) || drag.IsHolding) { circling = false; return; }
        Vector3 centre = grinding ? mortarCentre.position : stirCentre.position;
        Vector2 screen = cameraDirector.cam.WorldToScreenPoint(centre);
        float radius = Vector2.Distance(screen,cameraDirector.cam.WorldToScreenPoint(centre + cameraDirector.cam.transform.right * (grinding ? .07f : .045f)));
        if (PointerInput.Down && !PointerInput.OverUI() && Vector2.Distance(PointerInput.Position,screen) < Mathf.Max(60f,radius * 2.2f))
        {
            circling = true; circle.minRadius = Mathf.Max(8f,radius * .18f); circle.Begin(screen);
        }
        if (!PointerInput.Held) { circling = false; return; }
        if (!circling || PointerInput.OverUI()) return;
        if (Vector2.Distance(PointerInput.Position,screen) > Mathf.Max(100f,radius*2.8f)) { circling = false; return; }
        float previousAngle=circle.SignedDegrees;
        circle.Feed(PointerInput.Position,Time.deltaTime);
        float delta = circle.SignedDegrees-previousAngle;
        if (Mathf.Abs(delta) < .5f) return;
        AddCircularMotion(delta);
        float a = gestureAngle * Mathf.Deg2Rad;
        var tool = lookup[grinding ? "pestle" : "stirrer"].item.transform;
        if (grinding) tool.position = centre + new Vector3(Mathf.Cos(a)*.02f,.025f,Mathf.Sin(a)*.02f);
        else tool.position = centre + new Vector3(Mathf.Cos(a)*.02f,.02f,Mathf.Sin(a)*.02f);
    }
    void Update()
    {
        if (ui == null || saved == null || leavingForLauncher) return;
        TickTap();
        manager.paused = InMenu || IsPaused || DocumentOpen || ShowingResults;
        drag.SetEnabled(CanInteract);
        if(!CanUseTool)toolHeld=false;
        if(toolHeld&&CanUseTool){MoveTool((At("grind")?180f:140f)*Time.deltaTime);if(At("grind")&&preparation.grind>=GrindingTarget)toolHeld=false;}
        UpdateVisuals(); RefreshReadiness();
        TickActionView();
        // Let snapping/animations finish and release held controls before entering the next view.
        if (Step >= 0 && !At("notebook") && steps.Ready && CanChangeView && !circling)
        {
            completedDwell += Time.deltaTime;
            if (completedDwell >= .35f) Next();
        }
        else completedDwell = 0;
        ui.Refresh();
        bool audible=AudioAvailable;
        bool active=audible&&!InMenu&&!IsPaused&&!DocumentOpen&&!ShowingResults;
        bool toolMoving=active&&(toolHeld||circling&&Time.unscaledTime<toolSoundUntil);
        sounds.Refresh(audible&&InMenu&&!ui.PageVisible,active&&(waterFill.tapOn||emptyingBath),
            active&&(Sealing||!IsBoiling&&Simulation.lit),toolMoving&&At("grind"),toolMoving&&At("heat"),
            active&&Simulation.lit&&Simulation is BoilingSimulation boiling&&boiling.ContinuousBubbles);
    }
    public bool IsInspectable(Transform target)
    {
        return InspectionId(target)!=null;
    }
    string InspectionId(Transform target)
    {
        if(target==null)return null;
        if(tapRoot!=null&&target.IsChildOf(tapRoot))return "tap";
        if(IsBoiling&&fusionTube!=null&&target.IsChildOf(fusionTube))return "fusion";
        if(IsBoiling&&electricHeaterBody!=null&&target.IsChildOf(electricHeaterBody))return "heater";
        foreach(var binding in items)if(target.IsChildOf(binding.item.transform))return binding.id;
        if(target.IsChildOf(waterContainer))return "water";
        if(target.IsChildOf(rubberBand))return "band";
        for(var t=target;t!=null;t=t.parent)
        {
            if(t.name.Contains("Mortar"))return "mortar";
            if(t.name.Contains("Burner"))return "burner";
        }
        return null;
    }
    public void InspectObject(Transform target)
    {
        if(!CanChangeView)return;
        var id=InspectionId(target);
        if(id==null)return;
        if(id=="tap"){ToggleTap();return;}
        if(id=="beaker"&&At("water")){if(CanCarryWaterBeaker)CarryWaterBeaker();return;}
        if(id=="beaker"&&CanRefillWater&&preparation.water){ui.ToggleBeakerOptions();return;}
        ui.CloseBeakerOptions();
        if(id=="capillary"&&At("pack")){PackTube();return;}
        if(id=="band") { if(CanTieThread)Action();return; }
        InspectItem(id);
    }
    void UpdateVisuals()
    {
        if(powderBed!=null)
        {
            powderBed.gameObject.SetActive(preparation.sample&&preparation.grind>0);
            powderBed.localScale=new Vector3(.022f+.025f*(preparation.grind/GrindingTarget),.0014f,.022f+.025f*(preparation.grind/GrindingTarget));
        }
        if(sampleChunks!=null)
        {
            sampleChunks.gameObject.SetActive(preparation.sample&&preparation.grind>0&&preparation.grind<GrindingTarget);
            sampleChunks.localScale=Vector3.one*Mathf.Lerp(1,.12f,preparation.grind/GrindingTarget);
        }
        samplePortion.GetComponent<Renderer>().enabled=preparation.grind<=0;
        flameVisual.gameObject.SetActive(Simulation.lit||Sealing);
        if (Simulation.lit||Sealing)
        {
            float strength=Sealing?1:.65f+Simulation.heatSetting;
            flameVisual.localScale=new Vector3(.8f,strength,.8f)*(1f+.03f*Mathf.Sin(Time.time*19f));
        }
        if (mercury != null)
        {
            // The real model's graduations run from y=.065 (0 °C) to .285 (100 °C).
            float length = .048f+.22f*Simulation.thermometer/100f;
            mercury.localScale = new Vector3(mercuryScale.x, length/2f,mercuryScale.z);
            mercury.localPosition = new Vector3(0,.017f+length/2f,-.006f);
        }
        packedSolid.gameObject.SetActive(preparation.collected && Simulation.MeltFraction < .999f);
        packedLiquid.gameObject.SetActive(preparation.collected && Simulation.MeltFraction > .001f);
        if (preparation.collected)
        {
            float f=Simulation.MeltFraction;
            float halfHeight=preparation.VisibleColumnMm*.0005f;
            Vector3 centre=new Vector3(powderStart.x,.001f+halfHeight,powderStart.z);
            packedSolid.localScale=new Vector3(packedScale.x,halfHeight*Mathf.Max(.001f,1-f),packedScale.z);
            packedSolid.localPosition=centre+Vector3.up*(halfHeight*f);
            packedLiquid.localScale=new Vector3(packedScale.x,halfHeight*Mathf.Max(.001f,f),packedScale.z);
            packedLiquid.localPosition=centre-Vector3.up*(halfHeight*(1-f));
        }
        if (solidMaterial != null)
        {
            float wetting=Mathf.SmoothStep(0,1,Mathf.Clamp01(Simulation.MeltFraction*5));
            solidMaterial.color=Color.Lerp(Color.white,new Color(.68f,.81f,.87f),wetting);
            solidMaterial.SetFloat("_Smoothness",wetting*.75f);
        }
        if(IsBoiling)UpdateBoilingVisuals();
        if (Simulation.overheated && At("heat")) Message = Simulation.settings.maximumBathTemperature.ToString("0.#")+" °C limit: heating stopped. Complete your observations and review the result.";
    }
    void RefreshReadiness()
    {
        bool wasReady=steps.Ready;
        bool ready = false;
        switch (StepId)
        {
            case "setup": ready=preparation.tripod&&preparation.gauze&&preparation.beaker; break;
            case "water": ready=preparation.water; break;
            case "seal": ready=preparation.capillarySealed;break;
            case "invert": ready=preparation.capillaryInverted;break;
            case "sample": ready=preparation.sample; break;
            case "grind": ready=preparation.grind>=GrindingTarget; break;
            case "pack":
                if(steps.ShowHints&&SampleSettings.Accepts(preparation.sampleHeightMm,preparation.packedHeightMm))
                {preparation.sampleAccepted=true;ApplySampleProperties();}
                ready=preparation.sampleAccepted; break;
            case "attach": ready=preparation.band; break;
            case "immerse": ready=preparation.immersed; break;
            case "ignite": ready=Simulation.lit; break;
            case "heat": ready=Simulation.HasRequiredObservations; break;
            case "shutdown": ready=preparation.shutdownConfirmed&&!Simulation.lit; break;
        }
        if (!At("notebook"))
        {
            steps.SetReady(ready);
            if(ready&&!wasReady&&!Busy)SaveCheckpoint(steps.ShowHints);
        }
    }
    public void Submit(string first, string second, string conclusion)
    {
        if (InMenu||IsPaused||ShowingResults||Busy||!At("notebook")) return;
        float t1,t2;
        if (!float.TryParse(first,NumberStyles.Float,CultureInfo.InvariantCulture,out t1) ||
            !float.TryParse(IsBoiling?first:second,NumberStyles.Float,CultureInfo.InvariantCulture,out t2) ||
            float.IsNaN(t1)||float.IsNaN(t2)||float.IsInfinity(t1)||float.IsInfinity(t2)||t1<0||t2>100||t1>t2||string.IsNullOrWhiteSpace(conclusion))
        { Message="Enter finite readings from 0–100 °C, with T2 ≥ T1, and a conclusion."; return; }
        if(TrialNumber<RequiredTrials){Message="Record this trial and perform the second measurement before submitting.";return;}
        var techniqueFeedback=Simulation.Feedback(t1,t2);
        saved.draft.t1=first;saved.draft.t2=second;saved.draft.conclusion=conclusion.Trim();
        if(preparation.QualityScore(SampleSettings)<10)techniqueFeedback.RemoveAll(line=>line.StartsWith("Careful technique:"));
        var allTrials=new List<PracticalTrial>(completedTrials){CurrentTrial(true)};float mean;
        if(!PracticalTrialStatistics.Mean(allTrials,RequiredTrials,out mean)){Message="Both separate trial readings are required for the mean.";return;}
        Draft.trial2Reading=TrialNumber==2?first:"";
        LastNotebook = new PracticalNotebook {
            workflow=Definition.workflow,experimentId=Definition.experimentId,procedureRevision=Definition.procedureRevision,trials=allTrials,meanPoint=mean,observationFormat=Practice?"practice-single-trial":IsBoiling?"two-trial-continuous-bubbles-v1":"two-trial-melting-v1",
            id=Guid.NewGuid().ToString("N"),date=DateTime.UtcNow.ToString("O"),mode=steps.mode.ToString(),
            practice=Practice,t1=t1,t2=t2,width=t2-t1,conclusion=conclusion.Trim(),
            score=Simulation.Score(t1,t2,preparation.IsPrepared(Definition))-(preparation.IsPrepared(Definition)?10-preparation.QualityScore(SampleSettings):0),onset=Simulation.onset,completion=Simulation.completion,
            preparationScore=preparation.IsPrepared(Definition)?40+preparation.QualityScore(SampleSettings):0,heatingScore=Simulation.HeatingScore,stirringScore=Simulation.StirringScore,
            sampleHeightMm=preparation.sampleHeightMm,packingFraction=preparation.PackingFraction,
            workbook=JsonUtility.FromJson<WorkbookDraft>(JsonUtility.ToJson(saved.draft)),
            timingScore=(Simulation.IsTimely(Simulation.onset)?5:0)+(Simulation.IsTimely(Simulation.completion)?5:0),
            readingScore=(Mathf.Abs(t1-Simulation.onset.temperature)<=Simulation.settings.readingTolerance?5:0)+(Mathf.Abs(t2-Simulation.completion.temperature)<=Simulation.settings.readingTolerance?5:0),
            feedback=SampleFeedback()+string.Join("\n\n",techniqueFeedback) };
        int prep=0,heating=0,stirring=0,timing=0,reading=0;string combined="";
        foreach(var trial in allTrials){prep+=trial.preparationScore;heating+=trial.heatingScore;stirring+=trial.stirringScore;timing+=trial.timingScore;reading+=trial.readingScore;combined+="Trial "+trial.number+"\n"+trial.feedback+"\n\n";}
        LastNotebook.preparationScore=Mathf.RoundToInt(prep/(float)RequiredTrials);LastNotebook.heatingScore=Mathf.RoundToInt(heating/(float)RequiredTrials);
        LastNotebook.stirringScore=Mathf.RoundToInt(stirring/(float)RequiredTrials);LastNotebook.timingScore=Mathf.RoundToInt(timing/(float)RequiredTrials);LastNotebook.readingScore=Mathf.RoundToInt(reading/(float)RequiredTrials);
        LastNotebook.score=LastNotebook.preparationScore+LastNotebook.heatingScore+LastNotebook.stirringScore+LastNotebook.timingScore+LastNotebook.readingScore;LastNotebook.feedback=combined;
        Finish();
    }
    string SampleFeedback()
    {
        if(IsBoiling)return "Liquid volume: "+Mathf.RoundToInt(preparation.liquidFraction*100)+"% of fusion tube; recommended about one third.\n\n";
        string feedback="Sample preparation: "+preparation.sampleHeightMm.ToString("F1")+" mm; "+Mathf.RoundToInt(preparation.PackingFraction*100)+"% settled. Target: a compact "+SampleTarget+" column.\n\n";
        if(preparation.sampleHeightMm<SampleSettings.minimumColumnMm)feedback+="Too little sample makes the first and final melting changes harder to observe.\n\n";
        if(preparation.sampleHeightMm>SampleSettings.maximumColumnMm)feedback+="Too much sample heats less evenly and responds more slowly. Use a smaller column next time.\n\n";
        if(preparation.PackingFraction<.99f)feedback+="Loose powder contains gaps and heats less evenly. Settle it fully before attachment.\n\n";
        return feedback;
    }
    void InstallSampleGrains()
    {
        solidRenderer.enabled=false;
        for(int i=0;i<24;i++)
        {
            var grain=GameObject.CreatePrimitive(PrimitiveType.Sphere);grain.name="PackedGrain"+i;
            Destroy(grain.GetComponent<Collider>());grain.transform.SetParent(packedSolid,false);
            float a=i*2.39996f;grain.transform.localPosition=new Vector3(Mathf.Cos(a)*.28f,Mathf.Lerp(-.85f,.85f,i/23f),Mathf.Sin(a)*.28f);
            grain.transform.localScale=new Vector3(.45f,.07f,.45f);grain.GetComponent<Renderer>().sharedMaterial=solidMaterial;
        }
    }
    PracticalTrial CurrentTrial(bool completed)
    {
        return ScoreTrial(new PracticalTrial{number=saved.currentTrial,completed=completed,
            onset=JsonUtility.FromJson<MeltingObservation>(JsonUtility.ToJson(Simulation.onset)),
            completion=JsonUtility.FromJson<MeltingObservation>(JsonUtility.ToJson(Simulation.completion)),
            enteredOnset=saved.draft.t1,enteredCompletion=saved.draft.t2});
    }
    void Finish()
    {
        if (LastNotebook == null) return;
        manager.StopHeating();CloseDocument(); ShowingResults = true; saved.resumable = false;
        saved.trials=new List<PracticalTrial>(LastNotebook.trials);
        saved.notebooks.Add(LastNotebook); if(!PracticalProgressStore.Save(saved))Message=PracticalProgressStore.LastError;
        ui.ShowResults(LastNotebook);
    }
    public void RetryHeating()
    {
        StopAllCoroutines(); Busy=false; Time.timeScale=1; IsPaused=false; ShowingResults=false; InMenu=false; Practice=true;
        DocumentOpen=false;saved.currentTrial=1;completedTrials.Clear();saved.trials.Clear();saved.draft=new WorkbookDraft{date=DateTime.Now.ToString("yyyy-MM-dd")};cameraDirector.ClearZoom();
        manager.ResetBath(); manager.paused=false; preparation.stirrer=false;
        preparation=new PreparationState{capillarySealed=true,tripod=true,gauze=true,beaker=true,water=true,sample=true,pestle=true,grind=GrindingTarget,collected=true,packing=Mathf.CeilToInt(SampleSettings.maximumColumnMm/SampleSettings.packingIncrementMm),sampleHeightMm=SampleSettings.maximumColumnMm,packedHeightMm=SampleSettings.maximumColumnMm,sampleAccepted=true,capillaryPlaced=true,band=true,clamped=true,immersed=true};
        if(IsBoiling){preparation.liquidFraction=1f/3;preparation.capillaryInverted=true;preparation.sampleHeightMm=preparation.packedHeightMm=0;}
        ApplySampleProperties();
        ResetVisuals();RestoreCompleted();ui.ShowRun();steps.GoTo("ignite");
    }
    public void ShowLastNotebook()
    {
        if (!HasNotebooks) return;
        LastNotebook=saved.notebooks[saved.notebooks.Count-1];
        steps.mode=LastNotebook.mode=="Assessment"?PracticalMode.Assessment:PracticalMode.Guided;
        ShowingResults=true;InMenu=false;ui.ShowResults(LastNotebook);
    }
    public void Pause(bool value)
    {
        if (InMenu || ShowingResults) return;
        IsPaused=value; circling=false; toolHeld=false;StopTap(); drag.Cancel(); manager.paused=value; Time.timeScale=value?0:1;
        SaveCheckpoint(); ui.ShowPause(value);
    }
    public void Menu()
    {
        if (!InMenu && !ShowingResults) SaveCheckpoint();
        StopAllCoroutines(); Busy=false; drag.Cancel(); Time.timeScale=1; IsPaused=false;
        DocumentOpen=false;toolHeld=false;
        manager.StopHeating(); manager.paused=true; InMenu=true; ShowingResults=false;
        steps.ShowMenu(); ui.ShowMenu(saved.resumable);
    }
    void OnApplicationPause(bool paused)
    {
        audioBackgrounded=paused;
        if(paused)sounds?.Stop();
        if (saved == null) return;
        if (paused && !InMenu && !ShowingResults) Pause(true);
    }
    void OnApplicationFocus(bool focused)
    {
        audioFocused=focused;
        if(!focused)sounds?.Stop();
    }
    public void ReturnToLauncher()
    {
        if(leavingForLauncher||!PracticalLauncher.Available)return;
        bool savedForExit=ShowingResults&&LastNotebook!=null&&PracticalProgressStore.LastError!=null
            ? PracticalProgressStore.Save(saved)
            : SaveCheckpoint();
        if(!savedForExit)
        {
            ui.ShowLauncherSaveError();
            return;
        }
        leavingForLauncher=true;
        StopTap();
        drag.Cancel();
        toolHeld=circling=false;
        manager.StopHeating();
        sounds?.Stop();
        PracticalLauncher.ReturnToMenu();
    }
    void OnApplicationQuit() { if (saved != null && !InMenu && !ShowingResults) SaveCheckpoint(); }
    bool SaveCheckpoint(bool completed=false)
    {
        if (saved == null || InMenu || ShowingResults) return true;
        saved.mode=steps.mode; saved.practice=Practice; saved.resumable=true;
        saved.step=Mathf.Clamp(Step+(completed?1:0),0,steps.steps.Count-1);
        saved.simulation=steps.steps[saved.step].id=="notebook"?ThermalSnapshot.Capture(Simulation):null;
        // Completed preparation only. Restore derives geometry from these flags.
        saved.preparation=CheckpointPreparation(saved.step);
        saved.experimentId=Definition.experimentId;saved.procedureRevision=Definition.procedureRevision;saved.stepId=steps.steps[saved.step].id;
        saved.trials=new List<PracticalTrial>(completedTrials);saved.trials.Add(CurrentTrial(false));
        if(PracticalProgressStore.Save(saved))return true;
        Message=PracticalProgressStore.LastError;
        return false;
    }
    PreparationState CheckpointPreparation(int step)
    {
        var p=JsonUtility.FromJson<PreparationState>(JsonUtility.ToJson(preparation));
        if(!string.IsNullOrEmpty(p.waterReturnStep))return p;
        if(!steps.ShowHints)
        {
            if(!p.water)p.waterVolumeMl=0;
            if(p.grind<GrindingTarget){p.grind=0;p.pestle=false;}
            if(!p.sampleAccepted){p.collected=false;p.packing=0;p.sampleHeightMm=p.packedHeightMm=0;}
            if(!p.band)p.capillaryPlaced=false;
            if(!p.immersed)p.clamped=false;
            p.stirrer=false;return p;
        }
        if(step<=steps.IndexOf("setup")){p.tripod=false;p.gauze=false;p.beaker=false;}
        if(step<=steps.IndexOf("water")){p.water=false;p.waterVolumeMl=0;}
        if(step<=steps.IndexOf("seal"))p.capillarySealed=false;
        if(step<=steps.IndexOf("sample"))p.sample=false;
        if(step<=steps.IndexOf("grind")){p.pestle=false;p.grind=0;}
        if(step<=steps.IndexOf("pack")){p.collected=false;p.packing=0;p.sampleHeightMm=p.packedHeightMm=0;p.sampleAccepted=false;}
        if(step<=steps.IndexOf("attach")){p.capillaryPlaced=false;p.band=false;}
        if(step<=steps.IndexOf("immerse")){p.clamped=false;p.immersed=false;}
        p.stirrer=false;
        // Live observations are deliberately not resumable: rewind heating before ignition.
        if(IsHeatingStep(steps.steps[step].id)) saved.step=steps.IndexOf("ignite");
        return p;
    }
    void ResetVisuals()
    {
        StopTap();emptyingBath=false;beakerAtSinkStation=beakerBackAtBench=false;waterFill=new WaterBathFill();Sealing=false;if(closedCapillaryTip!=null)closedCapillaryTip.gameObject.SetActive(false);
        foreach(var z in zones)z.Clear();
        lookup["capillary"].item.transform.SetParent(transform.parent,true);
        if(IsBoiling){fusionTube.SetParent(transform.parent,true);preparation.tripod=preparation.gauze=true;}
        rubberBand.SetParent(transform.parent,true);
        foreach(var b in items)
        {
            b.item.CancelMotion(); b.item.movement=DragMovement.FacingCamera;
            b.item.transform.SetPositionAndRotation(b.homePosition,b.homeRotation); b.item.RememberHome();
        }
        samplePortion.localScale=portionScale;
        rubberBand.SetPositionAndRotation(new Vector3(thermometer.position.x+.08f,.838f,thermometer.position.z-.08f),Quaternion.identity);
        SetWater(0); flameVisual.gameObject.SetActive(false); pouringStream.gameObject.SetActive(false);
        packedSolid.gameObject.SetActive(false); packedLiquid.gameObject.SetActive(false); WaterSelected=false;
        preparation.stirrer=false; circling=false; toolHeld=false;samplePortion.gameObject.SetActive(true);samplePortion.GetComponent<Renderer>().enabled=true; ui.ClearCaptures(); LastNotebook=null;
        if(workspace!=null)workspace.ResetTray();
    }
    void RestoreCompleted()
    {
        if(closedCapillaryTip!=null)closedCapillaryTip.gameObject.SetActive(preparation.capillarySealed);
        if(!IsBoiling){Restore("tripod","PlaceTripod",preparation.tripod);Restore("gauze","PlaceGauze",preparation.gauze);}
        Restore("beaker","PlaceBeaker",preparation.beaker);
        waterFill.volumeMl=preparation.water?(preparation.waterVolumeMl>0?preparation.waterVolumeMl:WaterSettings.targetMl):0;
        waterFill.visitedSink=waterFill.returnedToBath=preparation.water;UpdateBathVolume();
        if(!IsBoiling){Restore("sample","LoadSample",preparation.sample);Restore("pestle","PlacePestle",preparation.pestle);}else RestoreBoilingApparatus();
        if(preparation.grind>=GrindingTarget)samplePortion.localScale=new Vector3(portionScale.x*1.3f,.004f,portionScale.z*1.3f);
        if(preparation.collected)packedSolid.gameObject.SetActive(true);
        if(preparation.collected&&workspace!=null)workspace.TakeOut("capillary");
        if((preparation.capillaryPlaced||preparation.sampleAccepted)&&workspace!=null)workspace.TakeOut("thermometer");
        if(preparation.capillaryPlaced&&!IsBoiling)
        {
            Restore("capillary","AttachCapillary",true);
            lookup["capillary"].item.transform.SetParent(thermometer,true);
        }
        if(preparation.band){rubberBand.SetPositionAndRotation(bandAttachedTarget.position,bandAttachedTarget.rotation);rubberBand.SetParent(thermometer,true);}
        Restore("thermometer","ClampThermometer",preparation.clamped);
        if(preparation.immersed)thermometer.position=new Vector3(thermometer.position.x,immersedRootY,thermometer.position.z);
    }
    void Restore(string id,string zone,bool yes)
    {
        if(!yes)return;
        if(workspace!=null)workspace.TakeOut(id);
        var target=destinations[zone].transform;
        lookup[id].item.transform.SetPositionAndRotation(target.position,target.rotation);
        if(id=="beaker")lookup[id].item.RememberHome();
    }
}
