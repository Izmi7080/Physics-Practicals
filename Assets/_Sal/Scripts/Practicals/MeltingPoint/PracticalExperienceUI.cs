using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

public partial class PracticalUIController
{
    class ContextControl
    {
        public Button button;public Func<bool> visible;public Func<bool> enabled;
    }
    readonly List<ContextControl> context=new List<ContextControl>();
    readonly Dictionary<string,InputField> draftFields=new Dictionary<string,InputField>();
    Button thermometerHandle;
    RectTransform smallHint,zoomStrip,heatingRail,document,heatPopup,notice;
    Text toast,liveReading,calculation,docHeading,packingOptionsLabel;
    Button stirring,stirMode,inspectionHeat,shutdownControl,sampleView,thermometerView,packingOptions;
    RectTransform[] workbookPages;
    string pageKind="";
    int pageIndex;
    bool loadingDraft, viewOptionsOpen, packingOptionsOpen;
    string lastMessage="";
    float messageUntil;
    Sprite circleSprite;
    public bool PageVisible { get { return document!=null&&document.gameObject.activeSelf||study!=null&&study.gameObject.activeSelf||readingsSheet!=null&&readingsSheet.gameObject.activeSelf; } }
    public void Initialize(NaphthalenePracticalSession owner)
    {
        InitializeCanvas(owner);
        circleSprite=CircleSprite();
        var input=EventSystem.current.GetComponent<InputSystemUIInputModule>();
        if(input!=null)input.pointerBehavior=UIPointerBehavior.AllPointersAsIs;
        BuildBenchGuide();
        BuildCornerNavigation();
        viewOptions=Button(run,"View",new Vector2(.025f,.045f),new Vector2(.105f,.108f),()=>viewOptionsOpen=!viewOptionsOpen,21);
        smallHint=Card("View options",run,new Vector2(.012f,.112f),new Vector2(BenchLeft-.012f,.188f),PracticalUiStyle.Document);
        notice=Card("Notice",run,new Vector2(.012f,.324f),new Vector2(BenchLeft-.012f,.386f),Navy);
        toast=Label(notice,"",new Vector2(.035f,.04f),new Vector2(.965f,.96f),20);
        toast.resizeTextForBestFit=true;toast.resizeTextMinSize=15;toast.resizeTextMaxSize=20;
        BuildCompactZoom();
        BuildContext();BuildHeating();BuildDocuments();if(session.workspace!=null)BuildTray();BuildWaterControls();BuildBoilingControls();BuildStudy();BuildActionZoom();BuildViewJoystick();BuildReadingsSheet();
        status=toast;
    }
    Button AddContext(string label,Func<bool> visible,UnityEngine.Events.UnityAction callback,Func<bool> enabled=null)
    {
        var button=Button(run,label,Vector2.zero,Vector2.zero,callback,22);
        context.Add(new ContextControl{button=button,visible=visible,enabled=enabled??(()=>session.CanChangeView)});
        return button;
    }
    void BuildContext()
    {
        AddContext("Use sample",()=>session.At("pack")&&!session.steps.ShowHints&&session.preparation.collected,()=>session.AcceptSample());
        packingOptions=AddContext("Options",()=>session.At("pack"),TogglePackingOptions);
        StyleSecondaryAction(packingOptions);
        packingOptionsLabel=packingOptions.GetComponentInChildren<Text>();
        foreach(var binding in session.items)
        {
            var b=binding;
            if(b.id=="tripod"||b.id=="gauze"||b.id=="beaker"||b.id=="pestle")continue;
            if(b.id=="thermometer"){BuildThermometerHandle(b.item);continue;}
            var handle=AddContext("Move "+PracticalWorkspace.Label(b.id).ToLowerInvariant(),()=>b.item.canDrag&&b.item.gameObject.activeInHierarchy&&(b.id!="capillary"||!session.At("pack")||packingOptionsOpen),
                ()=>{},()=>session.CanInteract||session.drag.IsHolding);
            handle.name="Move "+b.id;
            var dragHandle=handle.gameObject.AddComponent<PracticalDragHandle>();dragHandle.controller=session.drag;dragHandle.item=b.item;
            dragHandle.fromDock=true;
        }
        StyleSecondaryAction(AddContext("Settle powder",()=>session.At("pack")&&packingOptionsOpen&&session.HasLoosePowder,() =>
        {
            ClosePackingOptions();
            session.PackTube();
        }));
        StyleSecondaryAction(AddContext("Empty tube",()=>session.At("pack")&&packingOptionsOpen&&session.preparation.collected,() =>
        {
            ClosePackingOptions();
            session.EmptyCapillary();
        }));
        AddContext("Confirm",()=>session.At("immerse")&&session.preparation.clamped&&!session.preparation.immersed,()=>session.Action(),()=>session.CanInteract);
        AddContext(session.IsBoiling?"Power on":"Light",()=>session.At("ignite")&&!session.Simulation.lit,()=>session.Action(),()=>session.CanInteract)
            .GetComponent<Image>().color=new Color(.78f,.40f,.10f,.76f);
        AddContext("Hold Grind",()=>session.At("grind")&&session.preparation.pestle&&session.preparation.grind<session.GrindingTarget,
            ()=>{},()=>session.CanUseTool).gameObject.AddComponent<PracticalHoldControl>().session=session;
    }
    void BuildThermometerHandle(Draggable item)
    {
        var centre=Vector2.one*.5f;
        thermometerHandle=Button(run,"Move",centre,centre,()=>{},22);
        thermometerHandle.name="Move thermometer";
        var rect=(RectTransform)thermometerHandle.transform;
        rect.sizeDelta=new Vector2(100,56);
        var handle=thermometerHandle.gameObject.AddComponent<PracticalDragHandle>();
        handle.controller=session.drag;
        handle.item=item;
    }
    void PlaceThermometerHandle()
    {
        var item=session.BoundItem("thermometer");
        bool active=item!=null&&item.canDrag&&item.gameObject.activeInHierarchy&&!PageVisible&&!session.IsPaused;
        var rect=(RectTransform)thermometerHandle.transform;
        if(active)
        {
            var camera=session.cameraDirector.cam;
            var screen=camera.WorldToScreenPoint(session.ThermometerGripPoint);
            var held=session.drag.HeldItem==item;
            active=held||(screen.z>0&&camera.pixelRect.Contains(screen));
            if(screen.z>0&&RectTransformUtility.ScreenPointToLocalPointInRectangle(run,screen,null,out var local))
            {
                var margin=rect.sizeDelta*.5f;
                rect.anchoredPosition=new Vector2(
                    Mathf.Clamp(local.x,run.rect.xMin+margin.x,run.rect.xMax-margin.x),
                    Mathf.Clamp(local.y+47f,run.rect.yMin+margin.y,run.rect.yMax-margin.y));
            }
            thermometerHandle.interactable=session.CanInteract||held;
        }
        thermometerHandle.gameObject.SetActive(active);
    }
    void TogglePackingOptions()
    {
        CloseBeakerOptions();
        packingOptionsOpen=!packingOptionsOpen;
        packingOptionsLabel.text=packingOptionsOpen?"Close options":"Options";
    }
    void ClosePackingOptions()
    {
        if(!packingOptionsOpen)return;
        packingOptionsOpen=false;
        packingOptionsLabel.text="Options";
    }
    static void StyleSecondaryAction(Button button)
    {
        button.GetComponent<Image>().color=PracticalUiStyle.Panel;
        var sheen=button.transform.Find("Glass highlight");
        if(sheen!=null)sheen.gameObject.SetActive(false);
    }
    void BuildHeating()
    {
        heatingRail=Rect("ObservationControls",run,new Vector2(BenchRight+.015f,.245f),new Vector2(.985f,.75f));
        sampleView=Button(heatingRail,"Sample",new Vector2(0,.80f),new Vector2(1,.98f),()=>session.Focus("Sample"),23);
        thermometerView=Button(heatingRail,"Thermometer",new Vector2(0,.60f),new Vector2(1,.78f),()=>session.Focus("Thermometer"),21);
        markOnset=Button(heatingRail,"First liquid",new Vector2(0,.25f),new Vector2(1,.43f),()=>session.Mark(true),22);
        markEnd=Button(heatingRail,"Fully liquid",new Vector2(0,.05f),new Vector2(1,.23f),()=>session.Mark(false),22);
        if(session.IsBoiling)liveReading=Label(heatingRail,"",new Vector2(0,.44f),new Vector2(1,.60f),24);
        inspectionHeat=Button(run,"Heat",new Vector2(BenchRight+.015f,.038f),new Vector2(.985f,.108f),()=>heatPopup.gameObject.SetActive(!heatPopup.gameObject.activeSelf),22);
        inspectionHeat.GetComponent<Image>().color=new Color(.78f,.40f,.10f,.76f);
        shutdownControl=Button(run,"Switch off",new Vector2(BenchRight+.015f,.038f),new Vector2(.985f,.108f),()=>session.Action(),22);
        shutdownControl.GetComponent<Image>().color=inspectionHeat.GetComponent<Image>().color;
        stirring=Button(run,"Hold Stir",new Vector2(.012f,.478f),new Vector2(BenchLeft-.012f,.567f),()=>session.ToggleStirring(),23);
        stirring.gameObject.AddComponent<PracticalHoldControl>().session=session;
        stirMode=Button(run,"Hands-free: off",new Vector2(.012f,.392f),new Vector2(BenchLeft-.012f,.458f),()=>session.SetHandsFree(!session.HandsFreeStirring),20);
        heatPopup=Card("Heat adjustment",run,new Vector2(BenchRight+.015f,.115f),new Vector2(.985f,.232f),Navy);heat=SliderControl(heatPopup);
        heat.onValueChanged.AddListener(v=>{if(session.CanRecord)session.Simulation.SetHeat(v);});
        rate=Label(heatPopup,"",new Vector2(.05f,.55f),new Vector2(.95f,.97f),21);
        heatPopup.gameObject.SetActive(false);
    }
    public void Zoom(float direction){if(session.CanChangeView)session.cameraDirector.Zoom(direction);}
    public void Refresh()
    {
        if(session.InMenu||session.ShowingResults)return;
        string step=session.StepId;var s=session.Simulation;
        if(step!="pack"||PageVisible||session.IsPaused)ClosePackingOptions();
        if(beakerOptionsStep!=step||PageVisible||session.IsPaused)CloseBeakerOptions();
        bool canShowView = step != "water" && step != "notebook" && !PageVisible && !session.IsPaused;
        if (!canShowView) viewOptionsOpen = false;
        viewOptions.gameObject.SetActive(canShowView);
        smallHint.gameObject.SetActive(canShowView && viewOptionsOpen);
        RefreshBenchGuide();
        RefreshBenchNavigation();
        RefreshActionZoom();RefreshViewJoystick();RefreshReadingsSheet();
        zoomStrip.gameObject.SetActive(canShowView && viewOptionsOpen && session.cameraDirector.IsInspection);
        heatingRail.gameObject.SetActive(step=="heat"&&!PageVisible);
        shutdownControl.gameObject.SetActive(step=="shutdown"&&!session.preparation.shutdownConfirmed&&!PageVisible);
        shutdownControl.interactable=session.CanInteract;
        inspectionHeat.gameObject.SetActive(step=="heat"&&!PageVisible);
        inspectionHeat.interactable=session.CanRecord;
        sampleView.GetComponent<Image>().color=session.cameraDirector.CurrentAnchorName=="Sample"?new Color(.10f,.58f,.55f,.84f):Teal;
        thermometerView.GetComponent<Image>().color=session.cameraDirector.CurrentAnchorName=="Thermometer"?new Color(.10f,.58f,.55f,.84f):Teal;
        stirring.gameObject.SetActive(step=="heat"&&session.preparation.stirrer&&!PageVisible);
        stirring.interactable=session.CanUseTool;stirMode.gameObject.SetActive(stirring.gameObject.activeSelf);
        stirring.GetComponentInChildren<Text>().text=session.HandsFreeStirring?(session.ToolHeld?"Stop stirring":"Start stirring"):(session.ToolHeld?"Stirring…":"Hold Stir");
        stirMode.GetComponentInChildren<Text>().text="Hands-free: "+(session.HandsFreeStirring?"on":"off");
        markOnset.interactable=markEnd.interactable=session.CanRecord;
        markOnset.GetComponentInChildren<Text>().text=s.onset.captured?"Replace reading":session.IsBoiling?"Continuous bubbles":"First liquid";
        markEnd.gameObject.SetActive(!session.IsBoiling);sampleView.GetComponentInChildren<Text>().text=session.IsBoiling?"Fusion tube":"Sample";
        markEnd.GetComponentInChildren<Text>().text=s.completion.captured?"Replace end":"Fully liquid";
        if(liveReading!=null)liveReading.text=session.steps.ShowHints?s.thermometer.ToString("F1")+" °C":"Read the scale";
        heatPopup.gameObject.SetActive(heatPopup.gameObject.activeSelf&&step=="heat"&&!PageVisible);
        heat.SetValueWithoutNotify(s.heatSetting);heat.interactable=session.CanRecord&&s.lit;
        rate.text="Heat "+s.Rate.ToString("F1")+" °C/min • "+s.ActiveClockScale.ToString("0.#")+"× clock";
        string message=session.Message??"";
        if(message!=lastMessage){lastMessage=message;messageUntil=Time.unscaledTime+5;}
        if(step=="grind")message="Grinding "+Mathf.RoundToInt(session.preparation.grind/session.GrindingTarget*100)+"%";
        else if(step=="pack")
            message="Packed "+session.preparation.packedHeightMm.ToString("0.#")+" mm · Target "+session.SampleTarget
                +(session.HasLoosePowder? "\nLoose column: "+session.preparation.VisibleColumnMm.ToString("0.#")+" mm":"");
        else if(session.steps.ShowHints&&step=="heat"&&Time.unscaledTime>messageUntil)
            message=s.bath>=s.settings.slowHeatingTemperature&&s.Rate>s.settings.excessiveRate?"Near melting: reduce heat to about "+s.settings.gentleRate.ToString("0.#")+" °C/min.":!s.RecentlyStirred?"Keep stirring while observing the sample.":"";
        else if(Time.unscaledTime>messageUntil)message="";
        notice.anchorMin=new Vector2(.012f,step=="pack"?.503f:.324f);
        notice.anchorMax=new Vector2(BenchLeft-.012f,step=="pack"?.577f:.386f);
        toast.text=message;notice.gameObject.SetActive(message.Length>0&&!PageVisible&&step!="water");
        RefreshTrialControls();RefreshBoilingControls();PlaceContext();PlaceThermometerHandle();
        if(PageVisible&&pageKind=="Workbook"&&pageIndex==3)UpdateCalculation();
    }
    void PlaceContext()
    {
        int slot=0;
        foreach(var control in context)
        {
            bool visible=control.visible()&&!PageVisible;
            if(visible)
            {
                var r=(RectTransform)control.button.transform;
                float top=.862f-.088f*slot++;
                float left=control.button==packingOptions&&!packingOptionsOpen?.871f:BenchRight+.015f;
                r.anchorMin=new Vector2(left,top-.07f);
                r.anchorMax=new Vector2(.985f,top);
            }
            control.button.gameObject.SetActive(visible);control.button.interactable=control.enabled();
        }
    }
    internal static Sprite CircleSprite()
    {
        var tex=new Texture2D(96,96,TextureFormat.RGBA32,false);tex.wrapMode=TextureWrapMode.Clamp;
        for(int y=0;y<96;y++)for(int x=0;x<96;x++)tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(47.5f-Vector2.Distance(new Vector2(x,y),new Vector2(47.5f,47.5f)))));
        tex.Apply();return Sprite.Create(tex,new UnityEngine.Rect(0,0,96,96),new Vector2(.5f,.5f));
    }
}
