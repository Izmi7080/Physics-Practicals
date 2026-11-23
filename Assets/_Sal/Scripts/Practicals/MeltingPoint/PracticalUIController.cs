using System;
using System.IO;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public partial class PracticalUIController : MonoBehaviour
{
    NaphthalenePracticalSession session;
    RectTransform safe, run, menu, pause, results;
    Text status,rate,menuStatus,resultsText;
    Text launcherSaveError;
    Coroutine launcherSaveErrorTimer;
    Button markOnset,markEnd,resume,history;
    RectTransform taskList;
    Sprite rounded;
    Slider heat;
    InputField t1,t2,conclusion;
    RawImage firstScale,secondScale;
    Texture2D firstTexture,secondTexture;
    Font font;
    public RectTransform CanvasRoot { get; private set; }
    static readonly Color Navy=PracticalUiStyle.Panel,Teal=PracticalUiStyle.ButtonFace,White=new Color(.95f,.98f,.98f);
    void InitializeCanvas(NaphthalenePracticalSession owner)
    {
        session=owner;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");rounded=RoundedSprite();
        var go=new GameObject("PracticalHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
        CanvasRoot=go.GetComponent<RectTransform>();
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=PracticalUiStyle.ReferenceResolution;scaler.matchWidthOrHeight=.5f;
        safe=Rect("SafeArea",CanvasRoot,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<PracticalSafeArea>();
        run=Rect("Experiment",safe,Vector2.zero,Vector2.one);
        BuildMenu();BuildPause();BuildResults();
    }

    void AddLauncherButton(Transform parent,Vector2 min,Vector2 max)
    {
        if(PracticalLauncher.Available)Button(parent,"All practicals",min,max,session.ReturnToLauncher,21);
    }

    public void ShowLauncherSaveError()
    {
        if(launcherSaveError==null)
        {
            var card=Card("Launcher save status",safe,new Vector2(.21f,.17f),new Vector2(.79f,.26f),PracticalUiStyle.Document);
            launcherSaveError=Label(card,"",new Vector2(.03f,.04f),new Vector2(.97f,.96f),23);
            launcherSaveError.alignment=TextAnchor.MiddleCenter;
        }
        launcherSaveError.transform.parent.SetAsLastSibling();
        launcherSaveError.text="Could not save progress. Try All practicals again.";
        launcherSaveError.transform.parent.gameObject.SetActive(true);
        if(launcherSaveErrorTimer!=null)StopCoroutine(launcherSaveErrorTimer);
        launcherSaveErrorTimer=StartCoroutine(HideLauncherSaveError());
    }

    System.Collections.IEnumerator HideLauncherSaveError()
    {
        yield return new WaitForSecondsRealtime(4);
        if(launcherSaveError!=null)launcherSaveError.transform.parent.gameObject.SetActive(false);
        launcherSaveErrorTimer=null;
    }

    void BuildMenu()
    {
        menu=Panel("Introduction",safe,Vector2.zero,Vector2.one,new Color(.02f,.05f,.07f,.88f));
        for(int i=0;i<PracticalDefinitions.AssignedIds.Length;i++)
        {
            string id=PracticalDefinitions.AssignedIds[i];var profile=PracticalDefinitions.Load(id);
            var selector=Button(menu,profile.curriculumNumber+" "+profile.chemicalName,new Vector2(.08f+i*.17f,.91f),new Vector2(.24f+i*.17f,.975f),()=>session.SelectExperiment(id),21);
            selector.interactable=profile.playable;selector.GetComponent<Image>().color=id==session.Definition.experimentId?new Color(.10f,.58f,.55f,.90f):Teal;
        }
        Label(menu,session.Definition.title,new Vector2(.14f,.76f),new Vector2(.88f,.91f),43);
        var thermal=session.Definition.thermal;
        string clock=thermal.transitionClockScale>0
            ? $"The bath warms at {thermal.clockScale:0.#}× and slows to {thermal.transitionClockScale:0.#}× near melting."
            : $"Heating uses an accelerated {thermal.clockScale:0.#}× simulation clock.";
        Label(menu,"Aim: "+session.Definition.aim+"\n\nApparatus: "+session.Definition.apparatus+"\n\nUse a water bath. Avoid inhaling vapour. Keep the sample away from direct flame.\n\n"+clock,new Vector2(.20f,.33f),new Vector2(.80f,.74f),27);
        Button(menu,"Begin guided",new Vector2(.18f,.18f),new Vector2(.39f,.28f),()=>session.Begin(PracticalMode.Guided));
        Button(menu,"Begin assessment",new Vector2(.41f,.18f),new Vector2(.64f,.28f),()=>session.Begin(PracticalMode.Assessment));
        resume=Button(menu,"Resume",new Vector2(.66f,.18f),new Vector2(.82f,.28f),()=>session.Begin(session.steps.mode,true));
        history=Button(menu,"Last notebook",new Vector2(.66f,.06f),new Vector2(.82f,.15f),()=>session.ShowLastNotebook(),21);
        Button(menu,"Study this experiment",new Vector2(.18f,.065f),new Vector2(.43f,.14f),()=>ShowStudy(false),23);
        AddLauncherButton(menu,new Vector2(.835f,.05f),new Vector2(.975f,.13f));
        menuStatus=Label(menu,"",new Vector2(.45f,.03f),new Vector2(.64f,.15f),21);
    }
    void BuildPause()
    {
        pause=Panel("Pause",safe,Vector2.zero,Vector2.one,new Color(.02f,.04f,.06f,.90f));
        Label(pause,"Practical paused",new Vector2(.25f,.60f),new Vector2(.75f,.76f),45);
        Label(pause,"Heating and interactions are paused.\nLeaving during heating restarts it with a fresh sample.",new Vector2(.25f,.42f),new Vector2(.75f,.59f),27);
        Button(pause,"Resume",new Vector2(.18f,.26f),new Vector2(.38f,.37f),()=>session.Pause(false));
        Button(pause,"Save and menu",new Vector2(.40f,.26f),new Vector2(.60f,.37f),()=>session.Menu());
        AddLauncherButton(pause,new Vector2(.62f,.26f),new Vector2(.82f,.37f));
        pause.gameObject.SetActive(false);
    }

    void BuildResults()
    {
        results=Panel("Results",safe,Vector2.zero,Vector2.one,new Color(.02f,.05f,.07f,.97f));
        resultsText=Label(results,"Result",new Vector2(.12f,.22f),new Vector2(.88f,.94f),27);
        var scrollRoot=Rect("ResultScroll",results,new Vector2(.10f,.22f),new Vector2(.90f,.94f));
        var viewport=Panel("Viewport",scrollRoot,Vector2.zero,Vector2.one,Color.clear);
        viewport.GetComponent<Image>().raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
        resultsText.transform.SetParent(viewport,false);
        resultsText.rectTransform.anchorMin=new Vector2(0,1);resultsText.rectTransform.anchorMax=Vector2.one;
        resultsText.rectTransform.pivot=new Vector2(.5f,1);resultsText.rectTransform.offsetMin=resultsText.rectTransform.offsetMax=Vector2.zero;
        resultsText.alignment=TextAnchor.UpperLeft;
        var fitter=resultsText.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        PracticalUiStyle.VerticalScroll(scrollRoot,viewport,resultsText.rectTransform);
        Button(results,"Retry heating (practice)",new Vector2(.07f,.06f),new Vector2(.34f,.17f),()=>session.RetryHeating(),23);
        Button(results,"Restart practical",new Vector2(.36f,.06f),new Vector2(.58f,.17f),()=>session.Begin(session.steps.mode),23);
        Button(results,"Menu",new Vector2(.60f,.06f),new Vector2(.74f,.17f),()=>session.Menu(),23);
        AddLauncherButton(results,new Vector2(.76f,.06f),new Vector2(.94f,.17f));
        results.gameObject.SetActive(false);
    }
    public void ShowMenu(bool canResume)
    {
        ResetWaterTransferFade();
        ClosePackingOptions();CloseBeakerOptions();
        HideExtraPages();
        if(document!=null)document.gameObject.SetActive(false);
        menu.gameObject.SetActive(true);run.gameObject.SetActive(false);results.gameObject.SetActive(false);pause.gameObject.SetActive(false);
        resume.interactable=canResume;
        history.interactable=session.HasNotebooks;
        menuStatus.text=session.Message;
    }
    public void ShowRun()
    {
        ResetWaterTransferFade();
        ClosePackingOptions();CloseBeakerOptions();
        HideExtraPages();
        if(document!=null)document.gameObject.SetActive(false);
        menu.gameObject.SetActive(false);run.gameObject.SetActive(true);results.gameObject.SetActive(false);pause.gameObject.SetActive(false);
        taskList.gameObject.SetActive(false);
        LoadDraft();
    }
    public void ShowPause(bool value) { pause.gameObject.SetActive(value); }
    public void ShowResults(PracticalNotebook result)
    {
        HideExtraPages();
        if(document!=null)document.gameObject.SetActive(false);
        results.gameObject.SetActive(true);run.gameObject.SetActive(false);
        var s=session.Simulation;
        string score=result.mode=="Assessment" ? "\nScore: "+result.score+"/100\nAssembly / preparation / immersion "+result.preparationScore+"/50\nHeating "+result.heatingScore+"/20 • Stirring "+result.stirringScore+"/10\nObservation timing "+result.timingScore+"/10 • Reading "+result.readingScore+"/10\n" : "\n";
        string rows="";
        if(result.trials!=null)foreach(var trial in result.trials)rows+="Trial "+trial.number+": "+trial.enteredOnset+" °C"+(result.workflow==PracticalWorkflow.Melting?" (first liquid); fully liquid "+trial.enteredCompletion+" °C":" (continuous bubbles)")+"\n";
        bool repeated=result.trials!=null&&result.trials.Count>1;
        string point=result.workflow==PracticalWorkflow.Boiling?"boiling":"melting";
        resultsText.text=(result.practice?"HEATING PRACTICE — ":"")+result.mode+" result\n\n"+
            (repeated?"Mean "+point+" point: "+result.meanPoint.ToString("F1")+" °C\n":"Single-trial record\n")+rows+score+
            "\nConclusion: "+result.conclusion+"\n\nTechnique review\n"+result.feedback;
        if(result.workbook!=null)
        {
            var book=result.workbook;
            string descriptions=result.workflow==PracticalWorkflow.Boiling
                ?"\nBubble stream · "+book.onsetDescription
                :"\nFirst liquid · "+book.onsetDescription+"\nFully liquid · "+book.completionDescription;
            resultsText.text+="\n\nWritten practical\nStudent · "+book.studentName+" • "+book.date+
                "\nInitial appearance · "+book.initialAppearance+descriptions+"\nResult · "+book.result+"\nReflection · "+book.reflection;
        }
        if(PracticalProgressStore.LastError!=null)resultsText.text+="\n\n"+PracticalProgressStore.LastError;
    }

    public void CaptureScale(bool onset,MeltingObservation observation,bool writeImage=true)
    {
        var texture=ThermometerScaleGraphic.Snapshot(observation.temperature);
        if(onset){if(firstTexture!=null)Destroy(firstTexture);firstTexture=texture;if(firstScale!=null)firstScale.texture=texture;}
        else {if(secondTexture!=null)Destroy(secondTexture);secondTexture=texture;if(secondScale!=null)secondScale.texture=texture;}
        RefreshCapturedNumbers();
        if(!writeImage)return;
        try
        {
            string folder=Path.Combine(Path.GetDirectoryName(PracticalProgressStore.PathFor(session.Definition.experimentId)),"Scales");Directory.CreateDirectory(folder);
            observation.scalePath=Path.Combine(folder,Guid.NewGuid().ToString("N")+".png");
            File.WriteAllBytes(observation.scalePath,texture.EncodeToPNG());
        }
        catch(Exception e){Debug.LogWarning("Scale image could not be saved: "+e.Message);}
    }
    public void ClearCaptures()
    {
        if(firstTexture!=null)Destroy(firstTexture);if(secondTexture!=null)Destroy(secondTexture);
        firstTexture=secondTexture=null;if(firstScale!=null)firstScale.texture=null;if(secondScale!=null)secondScale.texture=null;
        RefreshCapturedNumbers();
    }
    void OnDestroy(){ClearCaptures();if(CanvasRoot!=null)Destroy(CanvasRoot.gameObject);if(rounded!=null){Destroy(rounded.texture);Destroy(rounded);}if(circleSprite!=null){Destroy(circleSprite.texture);Destroy(circleSprite);}foreach(var sprite in emojiSprites.Values){Destroy(sprite.texture);Destroy(sprite);}emojiSprites.Clear();}
    public void BeginPractical(){session.Begin(PracticalMode.Guided);}
    public void PerformAction(){session.Action();}
    RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var go=new GameObject(name,typeof(RectTransform));var rect=go.GetComponent<RectTransform>();
        rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }
    RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color)
    {var rect=Rect(name,parent,min,max);var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return rect;}
    RectTransform Card(string name,Transform parent,Vector2 min,Vector2 max,Color color)
    {
        var rect=Panel(name,parent,min,max,color);var image=rect.GetComponent<Image>();image.sprite=rounded;image.type=Image.Type.Sliced;return rect;
    }
    internal static Sprite RoundedSprite()
    {
        const int size=64,radius=14;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.wrapMode=TextureWrapMode.Clamp;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float dx=Mathf.Max(radius-x,0,x-(size-1-radius));float dy=Mathf.Max(radius-y,0,y-(size-1-radius));
            texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(radius-Mathf.Sqrt(dx*dx+dy*dy))));
        }
        texture.Apply();return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(radius,radius,radius,radius));
    }
    Text Label(Transform parent,string value,Vector2 min,Vector2 max,int size)
    {
        var rect=Rect(value.Length>30?"Text":value,parent,min,max);var text=rect.gameObject.AddComponent<Text>();
        text.font=font;text.text=value;text.fontSize=size;text.color=White;text.alignment=TextAnchor.MiddleLeft;text.supportRichText=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.raycastTarget=false;return text;
    }
    Button Button(Transform parent,string value,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction click,int size=25)
    {
        // Menu and action controls use their full layout area; Move handles are sized separately.
        var rect=Rect(value,parent,min,max);
        var image=rect.gameObject.AddComponent<Image>();PracticalUiStyle.StyleTextFace(image);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        button.onClick.AddListener(()=>{session.PlayUiSound();click();});
        var label=Label(rect,value,new Vector2(.03f,.02f),new Vector2(.97f,.98f),Mathf.Max(20,size));label.alignment=TextAnchor.MiddleCenter;
        PracticalUiStyle.ApplyButtonStates(button);return button;
    }
    InputField Input(Transform parent,string value,Vector2 min,Vector2 max,bool multi)
    {
        var rect=Panel(value,parent,min,max,PracticalUiStyle.Entry);rect.GetComponent<Image>().raycastTarget=true;
        var input=rect.gameObject.AddComponent<InputField>();
        var label=Label(rect,"",new Vector2(.04f,.04f),new Vector2(.96f,.96f),25);label.color=new Color(.1f,.15f,.18f);label.supportRichText=false;
        input.textComponent=label;input.targetGraphic=rect.GetComponent<Image>();input.characterLimit=multi?320:6;
        input.lineType=multi?InputField.LineType.MultiLineNewline:InputField.LineType.SingleLine;
        input.contentType=multi?InputField.ContentType.Standard:InputField.ContentType.DecimalNumber;
        input.keyboardType=multi?TouchScreenKeyboardType.Default:TouchScreenKeyboardType.DecimalPad;
        return input;
    }
    RawImage Raw(Transform parent,string name,Vector2 min,Vector2 max)
    {var rect=Rect(name,parent,min,max);var raw=rect.gameObject.AddComponent<RawImage>();raw.raycastTarget=false;return raw;}
    Slider SliderControl(Transform parent)
    {
        var rect=Rect("HeatSlider",parent,new Vector2(.06f,.14f),new Vector2(.94f,.44f));
        var background=Panel("Track",rect,new Vector2(0,.38f),new Vector2(1,.62f),new Color(.5f,.65f,.67f));background.GetComponent<Image>().raycastTarget=true;
        var fillArea=Rect("FillArea",rect,new Vector2(.02f,.38f),new Vector2(.98f,.62f));
        var fill=Panel("Fill",fillArea,Vector2.zero,Vector2.one,Teal);
        var handleArea=Rect("HandleArea",rect,new Vector2(.02f,0),new Vector2(.98f,1));
        var handle=Panel("Handle",handleArea,new Vector2(0,0),new Vector2(0,1),White);
        handle.sizeDelta=new Vector2(35,0);handle.GetComponent<Image>().raycastTarget=true;
        var slider=rect.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.value=1f/6f;
        return slider;
    }
}
