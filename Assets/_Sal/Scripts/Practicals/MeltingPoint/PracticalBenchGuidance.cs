using System.Collections;
using System.Linq;
using UnityEngine;

public partial class NaphthalenePracticalSession
{
    Material fillMarkMaterial;
    LineRenderer fillMarkLine;
    Renderer fillMarkLabel;
    Coroutine benchFraming;
    string pendingActionView;
    string pendingActionStep;
    MeltingObservation inspectedFirstLiquid;
    public Vector3 ThermometerGripPoint => thermometer.TransformPoint(Vector3.up*(preparation.clamped?.10f:.16f));
    public Vector3 ThermometerClampPoint => thermometerClamp!=null?thermometerClamp.position
        :destinations["ClampThermometer"].transform.position+Vector3.up*.22f;

    internal bool TryThermometerDrop(Draggable item,Vector2 pointer,Vector3 aim,out SnapZone snapped)
    {
        snapped=null;
        if(item.id!="thermometer"||preparation.clamped||!destinations["ClampThermometer"].Accepts(item))return false;

        var camera=cameraDirector.cam;
        var clamp=camera.WorldToScreenPoint(ThermometerClampPoint);
        var grip=camera.WorldToScreenPoint(aim+ThermometerGripPoint-item.transform.position);
        float radius=Mathf.Max(22f,camera.pixelHeight*.046f);
        if(clamp.z>0&&(Vector2.Distance(clamp,pointer)<=radius||(grip.z>0&&Vector2.Distance(clamp,grip)<=radius)))
        {
            var zone=destinations["ClampThermometer"];
            if(zone.TrySnap(item,zone.transform.position))snapped=zone;
        }
        return true;
    }
    public Transform Beaker { get { return lookup["beaker"].item.transform; } }
    public string ActionSubject
    {
        get
        {
            switch(StepId)
            {
                case "setup":return !preparation.tripod?"tripod":!preparation.gauze?"gauze":"beaker";
                case "water":case "immerse":return "beaker";
                case "sample":return IsBoiling?"fusion tube":"mortar";
                case "grind":return "mortar";
                case "seal":case "pack":case "invert":return "capillary";
                case "attach":return "attachment";
                case "ignite":case "shutdown":return IsBoiling?"heater":"burner";
                case "heat":return !preparation.stirrer?"stirrer":IsBoiling?"fusion tube":"sample";
                default:return "bench";
            }
        }
    }
    public string CurrentInstruction
    {
        get
        {
            switch(StepId)
            {
                case "setup":return !preparation.tripod?"The tripod is on the table. Drag it over the burner, onto its outline.":!preparation.gauze?"The wire gauze is now on the table. Drag it onto the top of the tripod.":"Drag the empty beaker from the table onto the gauze.";
                case "water":
                    if(preparation.water)return "Water bath ready.";
                    if(beakerBackAtBench)return "Drag the filled beaker onto the wire gauze.";
                    if(!beakerAtSinkStation)return "Tap the beaker to carry it to the sink.";
                    if(!waterFill.atSink)return "Drag the beaker under the spout onto the mint circle.";
                    if(waterFill.tapOn)return waterFill.volumeMl>WaterSettings.targetMl+WaterSettings.toleranceMl
                        ? "Tap the handle to stop the flow, then tap Empty to try again."
                        : "Tap the handle again when the surface reaches the marked line.";
                    if(CanReturnWater)return "Tap the filled beaker to carry it back to the tripod.";
                    if(waterFill.volumeMl>WaterSettings.targetMl+WaterSettings.toleranceMl)return "The water is above the line. Tap Empty and refill.";
                    return "Tap the handle to open the tap. Watch the marked line on the glass.";
                case "seal":return "The empty capillary is on the table. Drag one end into the small sealing flame. It returns to the table after sealing.";
                case "sample":return IsBoiling?"The fusion tube is on the table. Tap Add liquid four times to fill about one-third of the tube.":"Drag the white sample portion from its container into the mortar. It stays solid until you grind it.";
                case "grind":return !preparation.pestle?"The pestle is beside the mortar. Drag the pestle itself into the mortar.":"Hold Grind to crush the sample. Watch it become fine powder; release to stop.";
                case "pack":return PackingInstruction();
                case "invert":return "Drag the capillary into the fusion tube with its sealed end uppermost. The open end goes into the liquid.";
                case "attach":return !preparation.capillaryPlaced?(IsBoiling?"Drag the fusion tube beside the thermometer bulb, onto its outline.":"Drag the capillary beside the thermometer bulb. Align the sample with the bulb."):preparation.band?"Thread secured. Keep it above water during immersion.":"Tap the highlighted thread to tie the tubes together. Keep the thread above water during immersion.";
                case "immerse":return !preparation.clamped?"Drag the highlighted thermometer to the mint circle on the clamp."
                    : "Slide the bulb and sample below water. Keep them clear of the glass. Tap Confirm.";
                case "ignite":return IsBoiling?"Tap Power on beside the electric bath heater. Start gently; do not apply a flame to the solvent.":"Tap Light beside the burner. A blue flame heats the water bath.";
                case "heat":return !preparation.stirrer?"The glass stirrer is beside the bath. Drag it into the beaker. Then hold Stir while observing.":IsBoiling?"Keep stirring. Zoom into the fusion tube and record when a continuous bubble stream begins. Inspect the thermometer as needed.":MeltingInstruction();
                case "shutdown":return "Tap Switch off beside the heat source before writing up your readings.";
                default:return "Open Readings to compare your captures. Complete the workbook and repeat using a fresh sample.";
            }
        }
    }
    public string AssessmentControlInstruction
    {
        get
        {
            switch(StepId)
            {
                case "water":return preparation.water ? "Water bath ready." : beakerBackAtBench ? "Drag the beaker onto the gauze."
                    : !beakerAtSinkStation ? "Tap the beaker to carry it to the sink."
                    : !waterFill.atSink ? "Drag the beaker under the spout."
                    : CanReturnWater ? "Tap the beaker to carry it back to the bench."
                    : "Tap the handle to open or close the tap. Empty the beaker at the sink if needed.";
                case "grind":return "Place the pestle in the mortar, then hold Grind.";
                case "pack":return "Drag the tube to dip its open end into the powder. Tap the tube to settle it. Select Use sample when ready.";
                case "attach":return preparation.capillaryPlaced?"Tap the loose thread beside the glass to tie the tubes together.":"Drag the sample tube beside the thermometer bulb.";
                case "immerse":return preparation.clamped?"Slide the thermometer up or down, then tap Confirm.":"Drag the thermometer into the clamp.";
                case "heat":return preparation.stirrer?"Hold Stir while using the sample and thermometer views. Mark the required observations.":"Place the stirrer in the beaker.";
                default:return "Use the apparatus on the bench. Tap Zoom for a closer view.";
            }
        }
    }
    string MeltingInstruction()
    {
        if(Simulation.completion.captured)return "Both observations are saved. Read the marked scales in the workbook.";
        if(cameraDirector.CurrentAnchorName=="Thermometer")
            return Simulation.onset.captured
                ? "Read the top of the red column. Tap Sample to watch for the last solid to disappear."
                : "Read the top of the red column. Lower the heat near "+Simulation.settings.slowHeatingTemperature.ToString("0")+" °C, then tap Sample.";
        if(Simulation.onset.captured)
            return inspectedFirstLiquid==Simulation.onset
                ? "Keep watching the sample. Mark Fully liquid when no solid grains remain."
                : "Keep stirring. Tap Thermometer to check the scale, then return to Sample.";
        return "Keep stirring. Check the Thermometer and lower the heat near "+Simulation.settings.slowHeatingTemperature.ToString("0")+" °C. Watch the sample and mark First liquid when it looks wet.";
    }
    string PackingInstruction()
    {
        if(preparation.sampleAccepted)return "The packed sample is ready to attach to the thermometer.";
        if(HasLoosePowder)return "Tap the highlighted tube. Watch the loose grains settle toward the sealed end.";
        if(preparation.sampleHeightMm>SampleSettings.maximumColumnMm)return "The column is too tall. Open Options and empty the tube to try again.";
        return preparation.collected
            ?"Drag the tube to the highlighted powder again. Each dip adds a small amount."
            :"Drag the highlighted tube into the powder. Its open end picks up a small amount.";
    }
    public void InspectCurrentAction()
    {
        if(!CanChangeView)return;
        switch(StepId)
        {
            case "water":FrameWaterAction();break;
            case "immerse":if(preparation.clamped)Focus("Immersion");else FrameAction();break;
            case "setup":case "seal":FrameAction();break;
            case "sample":Focus(IsBoiling?"Sample":"Mortar");break;
            case "grind":Focus("Mortar");break;
            case "pack":case "invert":Focus("Capillary");break;
            case "attach":if(preparation.capillaryPlaced)Focus("Attachment");else FrameAction();break;
            case "ignite":case "shutdown":Focus(IsBoiling?"Heating":"Burner");break;
            case "heat":if(preparation.stirrer)Focus("Sample");else FrameAction();break;
            default:Focus("Overview");break;
        }
    }
    public void ReturnToBench()
    {
        if(!CanChangeView)return;
        if(At("water"))FrameAction();else Focus("Overview");
    }
    void FrameBenchItem(string id)
    {
        var item=lookup[id].item;var renderers=item.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
        foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        var anchor=cameraDirector.anchors.Find(a=>a.name=="Action");
        if(anchor==null){var point=new GameObject("Action inspection").transform;point.SetParent(transform.parent,false);anchor=new CameraDirector.Anchor{name="Action",point=point,fov=44};cameraDirector.anchors.Add(anchor);}
        float distance=Mathf.Max(.22f,bounds.size.magnitude*1.3f);anchor.point.position=bounds.center+new Vector3(.03f,distance*.6f,-distance);anchor.point.LookAt(bounds.center);
        cameraDirector.SetInspectionTarget("Action",bounds.center);cameraDirector.MoveTo("Action",.3f);
    }
    void FrameAction()
    {
        if (At("water")) { FrameWaterAction(); return; }
        var anchor=cameraDirector.anchors.Find(a=>a.name=="Action");
        if(anchor==null){var point=new GameObject("Action inspection").transform;point.SetParent(transform.parent,false);anchor=new CameraDirector.Anchor{name="Action",point=point,fov=44};cameraDirector.anchors.Add(anchor);}
        if(At("immerse")&&!preparation.clamped){FrameThermometerPlacement(anchor);return;}
        var moving=items.FirstOrDefault(b=>b.item.canDrag&&b.item.gameObject.activeInHierarchy);
        Vector3 centre=moving==null?stirCentre.position:moving.item.transform.position;
        var target=zones.FirstOrDefault(z=>z.canSnap&&(moving==null||z.acceptsId==moving.id));
        float span=.15f;if(target!=null){span=Vector3.Distance(centre,target.transform.position);centre=(centre+target.transform.position)*.5f;}
        anchor.point.position=centre+new Vector3(.04f,Mathf.Max(.25f,span*.65f),-Mathf.Max(.38f,span*1.3f));
        anchor.point.LookAt(centre);cameraDirector.SetInspectionTarget("Action",centre);cameraDirector.MoveTo("Action",.3f);
    }

    void FrameThermometerPlacement(CameraDirector.Anchor anchor)
    {
        var grip=ThermometerGripPoint;
        var clamp=ThermometerClampPoint;
        var centre=(grip+clamp)*.5f;
        var away=new Vector3(.10f,.25f,-1).normalized;
        var rotation=Quaternion.LookRotation(-away);
        var local=Quaternion.Inverse(rotation)*(clamp-centre);
        anchor.fov=44;
        float tangent=Mathf.Tan(anchor.fov*.5f*Mathf.Deg2Rad);
        float width=Mathf.Abs(local.x)/(tangent*cameraDirector.cam.aspect*.44f);
        float height=Mathf.Abs(local.y)/(tangent*.63f);
        float distance=Mathf.Max(.58f,Mathf.Max(width,height)+Mathf.Abs(local.z)+.05f);
        anchor.point.SetPositionAndRotation(centre+away*distance,rotation);
        cameraDirector.SetInspectionTarget(anchor.name,centre);
        cameraDirector.ResetShot(anchor.name);
        cameraDirector.MoveTo(anchor.name,.3f);
    }

    void FrameWaterAction(bool snap = false)
    {
        string name = waterFill.atSink ? "Tap" : beakerAtSinkStation ? "Sink placement" : "Bath bench";
        var anchor = cameraDirector.anchors.Find(a => a.name == name);
        if (anchor == null)
        {
            var point = new GameObject(name + " camera anchor").transform;
            point.SetParent(transform, false);
            anchor = new CameraDirector.Anchor { name = name, point = point, fov = 43 };
            cameraDirector.anchors.Add(anchor);
        }

        bool readingLevel = waterFill.atSink;
        var beakerBase = Beaker.position;
        var beakerRim = beakerBase + Vector3.up * .145f;
        var bench = destinations["PlaceBeaker"].transform.position;
        Vector3[] points;
        Vector3 centre;
        if (readingLevel)
        {
            var mark = fillMarkLine.transform.position;
            var handle = WaterControlPoint;
            centre = Vector3.Lerp(mark, handle, .25f);
            centre.y = mark.y;
            points = new[] { beakerBase, beakerRim, handle };
            anchor.fov = 38;
        }
        else
        {
            var destination = beakerAtSinkStation ? sinkDestination.transform.position : bench;
            var upperDetail = beakerAtSinkStation ? tapSpout.position + Vector3.up * .05f : bench + Vector3.up * .10f;
            points = new[] { beakerBase, beakerRim, destination, upperDetail };
            var bounds = new Bounds(beakerBase, Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            centre = bounds.center;
        }

        // Read the water line from its own height. The shallow side angle exposes the tap handle.
        var away = (readingLevel ? new Vector3(.33f, 0, -1) : new Vector3(.12f, .48f, -1)).normalized;
        var rotation = Quaternion.LookRotation(-away);
        var toCameraAxes = Quaternion.Inverse(rotation);
        float distance = readingLevel ? .33f : .45f;
        float tangent = Mathf.Tan(anchor.fov * .5f * Mathf.Deg2Rad);
        foreach (var point in points)
        {
            var local = toCameraAxes * (point - centre);
            float horizontal = Mathf.Abs(local.x) / (tangent * Mathf.Max(.65f, cameraDirector.cam.aspect) * .46f);
            float vertical = Mathf.Abs(local.y) / (tangent * (readingLevel ? .82f : .66f));
            distance = Mathf.Max(distance, Mathf.Max(horizontal, vertical) - local.z + (readingLevel ? .025f : .08f));
        }
        anchor.point.SetPositionAndRotation(centre + away * distance, rotation);
        cameraDirector.SetInspectionTarget(name, centre);
        cameraDirector.ResetShot(name);
        if (snap) cameraDirector.SnapTo(name); else cameraDirector.MoveTo(name, .29f);
    }
    void OfferBenchApparatus()
    {
        if(workspace!=null)workspace.OfferAvailable();
        if(benchFraming!=null)StopCoroutine(benchFraming);
        if(cameraDirector.IsInspection&&pendingActionView==null&&!At("water")&&!At("shutdown")&&!At("notebook"))
            benchFraming=StartCoroutine(KeepActionVisible());
    }
    IEnumerator KeepActionVisible()
    {
        while(Busy||ItemMoving||cameraDirector.IsMoving||DocumentOpen)yield return null;
        if(!InMenu&&!ShowingResults&&cameraDirector.IsInspection&&!drag.IsHolding)
        {
            var required=items.Where(b=>b.item.canDrag&&b.item.gameObject.activeInHierarchy).ToArray();
            if(required.Any(b=>OutsideActionFrame(b.item.transform.position))||
                steps.ShowHints&&zones.Any(z=>z.canSnap&&required.Any(b=>z.acceptsId==b.id)&&OutsideActionFrame(z.transform.position)))
                FrameAction();
        }
        benchFraming=null;
    }
    bool OutsideActionFrame(Vector3 point)
    {
        var viewport=cameraDirector.cam.WorldToViewportPoint(point);
        return viewport.z<=0||viewport.x<.13f||viewport.x>.84f||viewport.y<.15f||viewport.y>.80f;
    }
    void QueueActionView(string anchor)
    {
        pendingActionView=anchor;
        pendingActionStep=StepId;
    }
    void TickActionView()
    {
        if(pendingActionView==null)return;
        if(pendingActionStep!=StepId||InMenu||ShowingResults){pendingActionView=null;return;}
        if(!CanChangeView||circling||cameraDirector.IsMoving)return;
        string anchor=pendingActionView;
        pendingActionView=null;
        if(benchFraming!=null){StopCoroutine(benchFraming);benchFraming=null;}
        if(anchor=="Action")FrameAction();else Focus(anchor);
    }
    void InstallFillMark()
    {
        fillMarkMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));fillMarkMaterial.color=new Color(.12f,.22f,.23f);
        var line=new GameObject("Target water filling line").AddComponent<LineRenderer>();fillMarkLine=line;line.transform.SetParent(Beaker,false);
        line.useWorldSpace=false;line.loop=true;line.positionCount=48;line.widthMultiplier=.002f;line.sharedMaterial=fillMarkMaterial;
        float y=waterPosition.y+waterScale.y;line.transform.localPosition=Vector3.up*y;
        for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;line.SetPosition(i,new Vector3(Mathf.Cos(a)*.0505f,0,Mathf.Sin(a)*.049f));}
        var text=new GameObject("Filling line label").AddComponent<TextMesh>();text.transform.SetParent(Beaker,false);
        text.transform.localPosition=new Vector3(0,y+.010f,-.051f);text.characterSize=.00115f;text.fontSize=64;text.anchor=TextAnchor.MiddleCenter;
        text.text=WaterSettings.targetMl.ToString("0")+" mL";text.color=new Color(.10f,.20f,.21f);fillMarkLabel=text.GetComponent<Renderer>();
    }
    void UpdateFillGuideVisibility()
    {
        bool visible=!cameraDirector.IsInspection||cameraDirector.CurrentAnchorName=="Immersion"||cameraDirector.CurrentAnchorName=="Tap";
        if(fillMarkLine!=null)fillMarkLine.enabled=visible;
        if(fillMarkLabel!=null)fillMarkLabel.enabled=visible;
    }
    void ReleaseFillMark(){if(fillMarkMaterial!=null)Destroy(fillMarkMaterial);}
}
