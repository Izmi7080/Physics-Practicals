using System.Collections;
using UnityEngine;

public partial class NaphthalenePracticalSession
{
    public Transform tapRoot,tapSpout,tapStream;
    public SnapZone sinkDestination;
    public WaterBathFill waterFill=new WaterBathFill();
    bool pickedBeakerFromSink;
    bool emptyingBath;
    bool beakerAtSinkStation, beakerBackAtBench;
    Transform tapHandle;
    Renderer tapHandleFace;
    Quaternion closedTapRotation;
    public bool BeakerAtSinkStation => beakerAtSinkStation;
    public bool BeakerBackAtBench => beakerBackAtBench;
    public Vector3 WaterControlPoint => tapHandleFace != null ? tapHandleFace.bounds.center : tapSpout.position;
    public WaterBathSettings WaterSettings { get { return Definition.water; } }
    public bool CanReturnWater { get { return waterFill.Transportable(WaterSettings,steps.ShowHints); } }
    public bool CanOperateTap => At("water") && waterFill.atSink && CanChangeView;
    public bool CanCarryWaterBeaker => At("water") && !preparation.water && CanChangeView && !waterFill.tapOn &&
        (!beakerAtSinkStation && !beakerBackAtBench || waterFill.atSink && CanReturnWater);
    void InitializeTap()
    {
        tapHandle = tapRoot == null ? null : tapRoot.Find("pCylinder21");
        if (tapHandle != null)
        {
            tapHandleFace = tapHandle.GetComponent<Renderer>();
            closedTapRotation = tapHandle.localRotation;
        }
        var beaker=lookup["beaker"].item;
        beaker.PickedUp+=_=>{pickedBeakerFromSink=waterFill.atSink;waterFill.atSink=false;};
        beaker.Dropped+=(_,zone)=>{
            if(zone!=null||!At("water")||!pickedBeakerFromSink)return;
            waterFill.atSink=true;
            ConfigureInputs();
        };
    }
    internal bool HitWaterTap(Vector2 screen)
    {
        if (!CanOperateTap) return false;
        var point = cameraDirector.cam.WorldToScreenPoint(WaterControlPoint);
        return point.z > 0 && Vector2.Distance(screen, point) <= Mathf.Max(25, Screen.height * .045f);
    }

    internal SnapZone WaterDropAt(Draggable item, Vector2 screen)
    {
        if (!At("water") || item.id != "beaker") return null;
        var zone = beakerAtSinkStation ? sinkDestination : destinations["PlaceBeaker"];
        if (!zone.Accepts(item)) return null;
        var centre = cameraDirector.cam.WorldToScreenPoint(zone.transform.position + Vector3.up * .04f);
        if (centre.z <= 0 || Vector2.Distance(centre, screen) > Mathf.Max(28, Screen.height * .065f)) return null;
        return zone;
    }

    public void CarryWaterBeaker()
    {
        if (CanCarryWaterBeaker) StartCoroutine(CarryBeaker(!beakerAtSinkStation));
    }

    IEnumerator CarryBeaker(bool toSink)
    {
        Busy = true;
        cameraDirector.StopView();
        pendingActionView = null;
        var beaker = lookup["beaker"].item;
        beaker.BeginDrag();
        yield return ui.FadeWaterTransfer(1);
        beakerAtSinkStation = toSink;
        beakerBackAtBench = !toSink;
        waterFill.atSink = false;
        var target = toSink ? sinkDestination.transform.position + new Vector3(-.18f, 0, -.025f)
            : destinations["PlaceBeaker"].transform.position + new Vector3(.19f, -.10f, -.085f);
        var rotation = destinations["PlaceBeaker"].transform.rotation;
        beaker.transform.SetPositionAndRotation(target, rotation);
        beaker.RememberHome();
        pickedBeakerFromSink = false;
        beaker.EndDrag(null);
        FrameWaterAction(true);
        beaker.transform.position = target + Vector3.up * .065f;
        beaker.GlideTo(target, rotation, .28f);
        yield return ui.FadeWaterTransfer(0);
        while (beaker.IsGliding) yield return null;
        Busy = false;
        Message = "";
        ConfigureInputs();
    }
    public bool CanRefillWater { get { return !preparation.clamped&&!preparation.burnerStarted&&!ShowingResults&&!InMenu; } }
    public void RefillWater()
    {
        if(!CanInteract||!CanRefillWater)return;
        if(!At("water")){preparation.waterReturnStep=StepId;preparation.water=false;beakerBackAtBench=false;steps.GoTo("water");}
        if(waterFill.atSink&&waterFill.volumeMl>0&&!waterFill.tapOn)EmptyBathWater();
        else {Message="Take the beaker to the sink to empty and refill it.";ConfigureInputs();QueueActionView("Action");}
    }
    public void ToggleTap()
    {
        if(!CanOperateTap)return;
        waterFill.tapOn=!waterFill.tapOn;
        UpdateTapHandle();
        Message="";
        ConfigureInputs();
    }
    public void EmptyBathWater()
    {
        if(!CanInteract||!At("water")||!waterFill.atSink||waterFill.tapOn)return;
        StartCoroutine(EmptyBathMotion());
    }
    IEnumerator EmptyBathMotion()
    {
        Busy=emptyingBath=true;var beaker=lookup["beaker"].item.transform;var rotation=beaker.rotation;float amount=waterFill.volumeMl;
        for(float t=0;t<.65f;t+=Time.deltaTime)
        {
            float k=Mathf.Clamp01(t/.65f);beaker.rotation=rotation*Quaternion.Euler(0,0,Mathf.Sin(k*Mathf.PI)*48);
            waterFill.volumeMl=amount*(1-k);UpdateBathVolume();yield return null;
        }
        beaker.rotation=rotation;waterFill.Empty();Busy=emptyingBath=false;Message="Beaker emptied into the sink. Refill when ready.";UpdateBathVolume();ConfigureInputs();
    }
    void UpdateTapHandle()
    {
        if (tapHandle != null) tapHandle.localRotation = closedTapRotation * Quaternion.Euler(waterFill.tapOn ? 48 : 0, 0, 0);
    }
    void StopTap() { if(waterFill.tapOn){ waterFill.tapOn=false;UpdateTapHandle(); } if(tapStream!=null)tapStream.gameObject.SetActive(false); }
    void TickTap()
    {
        UpdateFillGuideVisibility();
        bool running=At("water")&&!InMenu&&!IsPaused&&!DocumentOpen&&!ShowingResults;
        if(!running)StopTap();
        if(running&&!Busy)waterFill.Tick(Time.deltaTime,WaterSettings,waterFill.atSink&&!ItemMoving&&!drag.IsHolding);
        if(tapStream!=null)tapStream.gameObject.SetActive(running&&waterFill.tapOn);
        if(tapStream!=null&&tapSpout!=null)
        {
            float bottom=waterFill.atSink?Mathf.Max(sinkDestination.transform.position.y+.015f,waterSurfaceY):sinkDestination.transform.position.y;
            float length=Mathf.Max(.005f,tapSpout.position.y-bottom);
            tapStream.position=new Vector3(tapSpout.position.x,tapSpout.position.y-length/2,tapSpout.position.z);tapStream.localScale=new Vector3(.004f,length/2,.004f);
        }
        if(At("water"))UpdateBathVolume();
    }
    void UpdateBathVolume()
    {
        SetWater(waterFill.volumeMl/WaterSettings.targetMl);
        preparation.waterVolumeMl=waterFill.volumeMl;
    }
    void WaterPlaced(string zoneName)
    {
        var beaker=lookup["beaker"].item;
        if(zoneName=="FillAtSink")
        {
            beakerAtSinkStation=true;
            waterFill.atSink=waterFill.visitedSink=true;waterFill.returnedToBath=false;
            StartCoroutine(RememberBeakerDestination());
            Message="Beaker below the tap. Watch the water reach the marked line.";
        }
        else if(zoneName=="PlaceBeaker"&&CanReturnWater)
        {
            beakerAtSinkStation=false;
            beakerBackAtBench=false;
            waterFill.atSink=false;waterFill.returnedToBath=true;preparation.beaker=true;preparation.water=true;
            StartCoroutine(RememberBeakerDestination());
            preparation.waterVolumeMl=waterFill.volumeMl;StopTap();Message="Water bath ready.";
        }
        ConfigureInputs();RefreshReadiness();
        if(waterFill.atSink)QueueActionView("Tap");
    }
    IEnumerator RememberBeakerDestination()
    {var d=lookup["beaker"].item;while(d.IsGliding)yield return null;d.RememberHome();}
    void ConfigureWaterInputs()
    {
        var d=lookup["beaker"].item;
        d.canDrag=!waterFill.tapOn;
        sinkDestination.canSnap=!waterFill.atSink;
        sinkDestination.SetHighlight(steps.ShowHints&&!waterFill.atSink);
        destinations["PlaceBeaker"].canSnap=CanReturnWater;
        destinations["PlaceBeaker"].SetHighlight(steps.ShowHints&&CanReturnWater);
    }
}
