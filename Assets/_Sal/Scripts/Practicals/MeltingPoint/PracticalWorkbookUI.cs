using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public partial class PracticalUIController
{
    readonly string[] bookTitles={"Details","Reference","Observations","Calculations","Conclusion"};
    RectTransform tasksBody,referenceBody,documentSheet;
    Text referenceText;
    Text capturedNumbers;
    Text trialReview;
    Button workbookSubmit,repeatTrial;
    readonly Dictionary<string,Text> fieldPrompts=new Dictionary<string,Text>();
    readonly List<Button> workbookTabs=new List<Button>();
    float cursor;
    void BuildDocuments()
    {
        document=PaperDocument("Practical pages", out documentSheet);
        taskList=document;
        docHeading=Label(documentSheet,"",new Vector2(.045f,.90f),new Vector2(.48f,.97f),30);
        docHeading.fontStyle=FontStyle.Bold;
        AddLauncherButton(documentSheet,new Vector2(.69f,.9f),new Vector2(.835f,.969f));
        Button(documentSheet,"Close",new Vector2(.85f,.9f),new Vector2(.952f,.969f),()=>ClosePage(),24);
        Button(documentSheet,"Readings + calculator",new Vector2(.49f,.9f),new Vector2(.675f,.969f),ShowReadings,21);
        trialReview=Label(documentSheet,"",new Vector2(.04f,.12f),new Vector2(.58f,.195f),21);
        repeatTrial=Button(documentSheet,"Save Trial 1 and start Trial 2",new Vector2(.60f,.12f),new Vector2(.965f,.195f),()=>{
            var error=session.RecordTrialAndRepeat();
            if(error.Length>0)trialReview.text=error;
        },23);
        tasksBody=ScrollPage("Tasks page");cursor=0;
        foreach(var step in session.steps.steps)
        {
            var row=Row(tasksBody,90);
            var task=step.id;
            Button(row,step.title,new Vector2(0,.08f),new Vector2(.95f,.92f),()=>session.ChooseAssessmentTask(task),24);
        }
        LabelBlock(tasksBody,"These are procedure steps, not camera views. Tap apparatus to inspect it without changing or skipping your current step. Completed actions advance automatically.",100);FinishContent(tasksBody);
        referenceBody=ScrollPage("Reference page");cursor=0;
        referenceText=LabelBlock(referenceBody,"",2100);BuildDiagram(referenceBody);FinishContent(referenceBody);
        workbookPages=new RectTransform[5];
        for(int i=0;i<5;i++)workbookPages[i]=ScrollPage("Workbook "+bookTitles[i]);
        cursor=0;
        LabelBlock(workbookPages[0],session.Definition.title,85);
        DraftInput(workbookPages[0],"Student name (optional)","studentName",false,100);
        DraftInput(workbookPages[0],"Date", "date",false,100);
        LabelBlock(workbookPages[0],"Aim\n"+session.Definition.aim,180);
        LabelBlock(workbookPages[0],"Use the tabs below to read the method, record your own observations and calculate your result. Your writing saves when you finish editing a field.",130);FinishContent(workbookPages[0]);
        cursor=0;LabelBlock(workbookPages[1],PracticalReference(false),1200);BuildDiagram(workbookPages[1]);FinishContent(workbookPages[1]);
        cursor=0;
        var captures=Row(workbookPages[2],300);
        firstScale=Raw(captures,"First liquid scale",new Vector2(.025f,.05f),new Vector2(.23f,.95f));
        if(!session.IsBoiling)secondScale=Raw(captures,"Fully liquid scale",new Vector2(.27f,.05f),new Vector2(.475f,.95f));
        capturedNumbers=Label(captures,"",new Vector2(.53f,.06f),new Vector2(.97f,.94f),26);
        RefreshCapturedNumbers();
        t1=DraftInput(workbookPages[2],session.IsBoiling?"Current trial · continuous-bubble temperature (°C)":"Current trial · first liquid temperature (°C)","t1",false,120,true,"Copy the saved numeric temperature beside the scale. Enter only the number.");
        if(!session.IsBoiling)t2=DraftInput(workbookPages[2],"Current trial · fully liquid temperature (°C)","t2",false,120,true,"Use the second capture. The final reading must be at least the first.");
        DraftInput(workbookPages[2],"Initial sample appearance","initialAppearance",true,170,false,session.IsBoiling?"Describe its colour and whether the liquid was clear or cloudy.":"Describe its colour and form. Example: white crystalline solid.");
        if(!session.IsBoiling)DraftInput(workbookPages[2],"Appearance after grinding (optional)","groundAppearance",true,170,false,"Describe how the grain size and appearance changed.");
        DraftInput(workbookPages[2],session.IsBoiling?"Describe the bubble stream at your observation":"Appearance at first liquid","onsetDescription",true,170,false,session.IsBoiling?"Describe where the bubbles came from and whether they formed a continuous stream.":"Look for a wet or clear region and describe any solid grains that remained.");
        if(!session.IsBoiling)DraftInput(workbookPages[2],"Appearance when fully melted","completionDescription",true,170,false,"Describe the liquid and whether you could still see any solid.");
        LabelBlock(workbookPages[2],session.IsBoiling?"Record the current continuous-bubble temperature, then perform a separate second heating run. Average the two trial readings.":"These readings describe the current trial. Record it, then perform a separate second trial. The mean uses the two first-liquid readings, not the ends of one melting interval.",130);
        FinishContent(workbookPages[2]);
        cursor=0;calculation=LabelBlock(workbookPages[3],"",440);
        LabelBlock(workbookPages[3],(session.IsBoiling?"The boiling point is the mean of the continuous-bubble readings from two separate trials. The app assumes approximately atmospheric pressure.\n\n":"The board result is the mean of the first-liquid readings from trial 1 and trial 2. The additional interval within each trial is first liquid to fully liquid. Do not swap an inverted interval: check both saved temperatures instead.\n\nElapsed time is recorded by the app for technique review. It is not the melting point, and you do not need a heating graph for this capillary record."),270);FinishContent(workbookPages[3]);
        cursor=0;
        DraftInput(workbookPages[4],"Your result","result",true,150);
        conclusion=DraftInput(workbookPages[4],"Your conclusion","conclusion",true,200);
        DraftInput(workbookPages[4],"Technique / possible errors / improvements","reflection",true,200,false,session.IsBoiling?"Consider your heating rate, stirring and how you identified a continuous stream.":"Consider heating rate, stirring, packing and when you marked each change.");
        DraftInput(workbookPages[4],session.IsBoiling?"Post-lab: why does boiling point change with altitude?":"Post-lab: what could a reading below the reference suggest?","postLab1",true,180);
        DraftInput(workbookPages[4],session.IsBoiling?"Post-lab: why do acetone and ethanol have different boiling points?":"Post-lab: why would benzoic acid melt before sodium chloride under similar heating?","postLab2",true,180);
        if(session.IsBoiling)
        {
            DraftInput(workbookPages[4],"Post-lab: how could the boiling point of water at altitude be raised?","postLab3",true,180);
            DraftInput(workbookPages[4],"Post-lab: which property allows petroleum fractions to be separated?","postLab4",true,180);
            DraftInput(workbookPages[4],"Post-lab: why is coloured alcohol used in a thermometer?","postLab5",true,180);
            DraftInput(workbookPages[4],"Post-lab: why may alcohol be preferred to mercury in some thermometers?","postLab6",true,180);
        }
        var submit=Row(workbookPages[4],100);workbookSubmit=Button(submit,"Submit practical",new Vector2(.60f,.08f),new Vector2(.97f,.92f),()=>{var error=session.SubmitWorkbook();if(error.Length>0)docHeading.text=error;},25);
        FinishContent(workbookPages[4]);
        var tabs=Rect("Workbook tabs",documentSheet,new Vector2(.04f,.035f),new Vector2(.96f,.11f));
        for(int i=0;i<5;i++){int n=i;workbookTabs.Add(Button(tabs,bookTitles[i],new Vector2(i*.20f,0),new Vector2(i*.20f+.19f,1),()=>SelectWorkbookPage(n),22));}
        StylePaperContents(documentSheet);
        SelectPaperTab(workbookSubmit,true);
        SelectPaperTab(repeatTrial,true);
        document.gameObject.SetActive(false);
    }
    RectTransform ScrollPage(string name)
    {
        var root=Rect(name,documentSheet,new Vector2(.035f,.14f),new Vector2(.963f,.884f));
        var viewport=Panel("Viewport",root,Vector2.zero,Vector2.one,Color.clear);viewport.GetComponent<Image>().raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
        var content=Rect("Content",viewport,new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);
        PracticalUiStyle.VerticalScroll(root,viewport,content);
        return content;
    }
    RectTransform Row(RectTransform parent,float height)
    {
        var row=Rect("Row",parent,new Vector2(0,1),Vector2.one);row.pivot=new Vector2(.5f,1);row.sizeDelta=new Vector2(-40,height);row.anchoredPosition=new Vector2(0,-cursor);cursor+=height+18;return row;
    }
    Text LabelBlock(RectTransform parent,string text,float height)
    {var label=Label(Row(parent,height),text,new Vector2(.015f,0),new Vector2(.985f,1),26);label.alignment=TextAnchor.UpperLeft;return label;}
    void FinishContent(RectTransform content){content.sizeDelta=new Vector2(0,cursor+25);}
    InputField DraftInput(RectTransform parent,string label,string key,bool multi,float height,bool numeric=false,string guidance=null)
    {
        var row=Row(parent,height);Label(row,label,new Vector2(.015f,guidance==null?.70f:.77f),new Vector2(.97f,1),23);
        if(guidance!=null)fieldPrompts[key]=Label(row,guidance,new Vector2(.015f,.57f),new Vector2(.97f,.77f),20);
        var field=Input(row,key,new Vector2(.015f,.03f),new Vector2(.97f,guidance==null?.68f:.56f),multi);
        if(!numeric){field.contentType=InputField.ContentType.Standard;field.keyboardType=TouchScreenKeyboardType.Default;field.characterLimit=multi?500:80;}
        draftFields[key]=field;
        field.onValueChanged.AddListener(value=>{if(!loadingDraft&&session.Draft!=null)typeof(WorkbookDraft).GetField(key).SetValue(session.Draft,value);});
        field.onEndEdit.AddListener(value=>session.SaveDraft());return field;
    }
    void BuildDiagram(RectTransform parent)
    {
        var diagram=Row(parent,480);
        Label(diagram,"Labelled water-bath setup (diagram)",new Vector2(.02f,.88f),new Vector2(.95f,1),26);
        Panel("Beaker",diagram,new Vector2(.33f,.25f),new Vector2(.60f,.72f),new Color(.45f,.70f,.78f,.35f));
        Panel("Water",diagram,new Vector2(.34f,.26f),new Vector2(.59f,.60f),new Color(.25f,.62f,.78f,.65f));
        Panel("Thermometer",diagram,new Vector2(.435f,.33f),new Vector2(.447f,.87f),White);
        if(session.IsBoiling){Panel("Fusion tube",diagram,new Vector2(.456f,.33f),new Vector2(.50f,.70f),new Color(.80f,.93f,1,.65f));Panel("Solvent",diagram,new Vector2(.46f,.34f),new Vector2(.496f,.46f),new Color(.33f,.76f,.9f));Panel("Inverted capillary",diagram,new Vector2(.476f,.35f),new Vector2(.482f,.65f),White);}
        else Panel("SampleCapillary",diagram,new Vector2(.455f,.33f),new Vector2(.46f,.49f),White);
        Panel("Thread",diagram,new Vector2(.428f,.65f),new Vector2(.466f,.67f),new Color(.90f,.55f,.20f));
        Panel("Stirrer",diagram,new Vector2(.54f,.34f),new Vector2(.55f,.78f),White);
        Panel("Gauze",diagram,new Vector2(.30f,.22f),new Vector2(.63f,.235f),new Color(.65f,.7f,.7f));
        Panel("TripodLeg",diagram,new Vector2(.31f,.07f),new Vector2(.32f,.22f),White);Panel("TripodLeg",diagram,new Vector2(.61f,.07f),new Vector2(.62f,.22f),White);
        if(session.IsBoiling)Panel("Electric bath heater",diagram,new Vector2(.29f,.15f),new Vector2(.64f,.23f),new Color(.30f,.37f,.40f));
        else Panel("Burner",diagram,new Vector2(.445f,.02f),new Vector2(.48f,.17f),new Color(.2f,.4f,.8f));
        Panel("Stand",diagram,new Vector2(.69f,.04f),new Vector2(.70f,.88f),White);Panel("Clamp",diagram,new Vector2(.44f,.81f),new Vector2(.70f,.825f),White);
        Label(diagram,session.IsBoiling?"Thermometer →\n\nThread above water →\n\nFusion tube / liquid →":"Thermometer →\n\nThread above water →\n\nSample beside bulb →",new Vector2(.025f,.35f),new Vector2(.32f,.83f),23);
        Label(diagram,session.IsBoiling?"← Clamp and stand\n\n← Glass stirrer\n\n← Beaker / water\n\n← Electric heater":"← Clamp and stand\n\n← Glass stirrer\n\n← Beaker / water\n\n← Gauze and tripod\n\n← Burner",new Vector2(.73f,.02f),new Vector2(.98f,.88f),23);
    }
    public void OpenPage(string kind)
    {
        if(session.InMenu||session.ShowingResults||session.drag.IsHolding)return;
        PresentDocument(document);pageKind=kind;pageIndex=0;
        tasksBody.parent.parent.gameObject.SetActive(kind=="Tasks");
        referenceBody.parent.parent.gameObject.SetActive(kind=="Reference");
        documentSheet.Find("Workbook tabs").gameObject.SetActive(kind=="Workbook");
        foreach(var page in workbookPages)page.parent.parent.gameObject.SetActive(false);
        docHeading.text=kind=="Workbook"?"Practical workbook":kind=="Tasks"?"Procedure and inspection":"Guided reference";
        referenceText.text=session.steps.ShowHints?PracticalReference(true):"Controls\n\nTop-right icons: clipboard = Tasks; open book = Reference; notebook and pencil = Workbook; two bars = Pause.\n\nTap apparatus to open its close-up without changing the procedure. Drag an available tool in its close-up or on the bench; the camera stays still while dragging. Tap the house marked Bench to return to the bench.\n\nDuring heating, hold Stir with your left finger. Use your right finger to inspect the sample or thermometer, adjust heat, and mark your observations.\n\nHands-free stirring is optional. The assessed run does not show technique hints or example answers. Worked answers remain hidden during assessment.\n\nThis page pauses the practical. Close it to resume; hold Stir again when ready.";
        if(kind=="Workbook")SelectWorkbookPage(session.At("notebook")?2:0);
        else RefreshTrialControls();
        LoadDraft();
    }
    public void ClosePage()
    {if(document!=null)EndDocument();}
    public void CloseForTrialTransition(){EndDocument();}
    public void SelectWorkbookPage(int index)
    {
        pageIndex=Mathf.Clamp(index,0,4);
        if(pageIndex==1&&!session.steps.ShowHints)pageIndex=0;
        for(int i=0;i<5;i++)
        {
            workbookPages[i].parent.parent.gameObject.SetActive(i==pageIndex);
            workbookTabs[i].interactable=(i!=1||session.steps.ShowHints)&&(!IsTrialReview||i<3);
            SelectPaperTab(workbookTabs[i],i==pageIndex);
        }
        docHeading.text=IsTrialReview&&pageIndex==2?"Trial "+session.TrialNumber+" complete · Review your observations":"Workbook • "+bookTitles[pageIndex];
        trialReview.text="Enter your readings and descriptions below the captures, then save this trial.";
        if(IsTrialReview&&pageIndex==2)
        {
            Canvas.ForceUpdateCanvases();
            var scroll=workbookPages[2].GetComponentInParent<ScrollRect>();scroll.StopMovement();scroll.verticalNormalizedPosition=1;
        }
        LoadDraft();UpdateCalculation();RefreshTrialControls();
    }
    void LoadDraft()
    {
        if(session.Draft==null)return;loadingDraft=true;
        foreach(var pair in draftFields)pair.Value.text=(string)typeof(WorkbookDraft).GetField(pair.Key).GetValue(session.Draft)??"";
        foreach(var prompt in fieldPrompts.Values)prompt.gameObject.SetActive(session.steps.ShowHints);
        loadingDraft=false;
        if(firstScale!=null)firstScale.texture=firstTexture;if(secondScale!=null)secondScale.texture=secondTexture;
        RefreshCapturedNumbers();
        if(t1!=null)t1.interactable=session.At("notebook");if(t2!=null)t2.interactable=session.At("notebook");
    }
    void RefreshCapturedNumbers()
    {
        if(capturedNumbers==null)return;
        var first=session.Simulation.onset;var second=session.Simulation.completion;
        string previous="";foreach(var trial in session.CompletedTrials)previous+="Trial "+trial.number+": "+trial.enteredOnset+" °C\n";
        capturedNumbers.text="Saved observations\n"+previous+"\nCurrent trial "+session.TrialNumber+" / "+session.RequiredTrials+"\n\n"+(session.IsBoiling?"Continuous bubbles: ":"First liquid: ")+(first.captured?first.temperature.ToString("F1",CultureInfo.InvariantCulture)+" °C":"not marked")+
            (session.IsBoiling?"":"\n\nFully liquid: "+(second.captured?second.temperature.ToString("F1",CultureInfo.InvariantCulture)+" °C":"not marked"))+"\n\nUse these saved values in your record. Captures stay fixed.";
    }
    void UpdateCalculation()
    {
        float first,second;
        bool valid=PracticalTrialStatistics.Reading(t1.text,out first);
        if(session.IsBoiling)calculation.text=valid?"This trial boiling-point reading: "+first.ToString("F1")+" °C":"Enter this trial's saved continuous-bubble temperature.";
        else if(valid&&PracticalTrialStatistics.Reading(t2.text,out second))calculation.text=second<first?"The final-liquid reading is lower than the first. Check the captures; do not swap them.":"Additional within-trial interval: "+first.ToString("F1")+"–"+second.ToString("F1")+" °C\nWidth: "+(second-first).ToString("F1")+" °C";
        else calculation.text="Enter this trial's first and final readings from its saved temperatures.";
        if(session.RequiredTrials>1)
        {
            float previous;
            if(session.CompletedTrials.Count>0&&PracticalTrialStatistics.Reading(session.CompletedTrials[0].enteredOnset,out previous)&&valid)
                calculation.text="Separate trials\nTrial 1: "+previous.ToString("F1")+" °C\nTrial 2: "+first.ToString("F1")+" °C\n\nMean = ("+previous.ToString("F1")+" + "+first.ToString("F1")+") / 2 = "+((previous+first)/2).ToString("F1")+" °C\n\n"+calculation.text;
            else calculation.text="Record two separate measurements before calculating their mean.\n\n"+calculation.text;
        }
    }
    bool IsTrialReview => session.At("notebook")&&session.TrialNumber<session.RequiredTrials;
    void RefreshTrialControls()
    {
        bool review=pageKind=="Workbook"&&pageIndex==2&&IsTrialReview;
        repeatTrial.gameObject.SetActive(review);
        trialReview.gameObject.SetActive(review);
        ((RectTransform)workbookPages[2].parent.parent).anchorMin=new Vector2(.025f,review?.205f:.14f);
        workbookSubmit.interactable=session.At("notebook")&&!IsTrialReview;
    }
    string PracticalReference(bool examples)
    {
        if(session.IsBoiling)return BoilingReference(examples);
        string text="Controls\nTop-right icons: clipboard = Tasks; open book = Reference; notebook and pencil = Workbook; two bars = Pause. Tap apparatus to open its close-up independently of the procedure. Drag the pestle itself; thin tools keep their small Move controls. Tap the house marked Bench to return.\n\n"+
        "Aim and principle\nDetermine the melting interval of naphthalene. A solid melts into a liquid when heated; record the first wetting and the disappearance of the last solid.\n\n"+
        "Apparatus and materials\nBeaker, water, thermometer, capillary sealed virtually at one end, naphthalene, mortar and pestle, thread, clamp stand, tripod, wire gauze, Bunsen burner and glass stirrer.\n\n"+
        "Method\n1. Assemble the water bath. Use the tap to put about 100 mL into the 250 mL beaker.\n2. Grind a small sample into fine powder. Seal one end of the capillary, let it cool, then drag its open end into the powder. Tap the tube to settle the grains. Repeat until the packed column is 10 mm.\n3. Attach it beside the thermometer bulb. Keep the thread above water.\n4. Clamp the thermometer. Submerge the bulb and sample without touching glass.\n5. Light the blue flame. Warm gently and keep stirring. Near 75 °C reduce heating to about 1 °C/min.\n6. Watch the sample and mark First liquid (T1). Check the thermometer, then return to the sample and mark Fully liquid (T2) when the last solid disappears. The workbook retains both scales at the moment you marked them.\n7. Switch off, record trial 1, and repeat with a fresh sample. Calculate the mean of the two first-liquid readings.\n\n"+
        "Identifying melting\nFirst liquid: the powder first looks wet or a small clear liquid region appears. Fully liquid: no solid grains remain. Moving or settling powder alone is not melting. Look at the sample, then the thermometer.\n\n"+
        "Reading the thermometer\nRead the top of the mercury column against the numbered graduations. Tap Thermometer to centre the current reading, then tap Sample to return. Each observation also saves a numeric temperature to one decimal place, since the scale alone cannot show decimals exactly. Avoid reading from a steep angle.\n\n"+
        "Precautions\nUse a water bath, not direct flame on naphthalene. Heat slowly near melting, stir regularly, use fine compact powder, keep glass clear of the bulb and sample, and do not inhale vapour. Turn off the burner when finished.\n\n"+
        (examples?"Worked examples — these are not your answers\nTrial 1: 80.2 °C; trial 2: 80.4 °C. Mean = 80.3 °C. Each is a separate heating run.\n80.2–81.0 °C: width 0.8 °C; a narrow interval consistent with the expected region when technique is sound.\nA high or wide interval after fast heating: potentially unreliable; repeat with slow heat and stirring.\nA mark while the sample is still solid: an early observation, not evidence of a lower melting point.\n\nGuided mode explains early and late marks. Assessment accepts your decisions and reviews technique afterward.\n\n":"")+
        "Interpretation\nReport T1–T2 and T2 − T1. Compare with about 80–82 °C, but do not replace your readings with this value. A narrow interval alone does not prove purity: heating, stirring, packing and reading technique also matter.\n\n"+
        "Simulation and timing\nThe app gives each chemical a modelled onset and completion temperature, then changes the visible sample between them. The thermometer can lag behind the sample. The observed interval depends on heat, stirring, packing and when you record. A one-degree interval is not a rule for real samples.\n\nThe app uses an 8× clock. The three-real-second timing allowance is an app scoring setting, not a laboratory rule. In a real practical, record the visible first and final melting events as promptly as possible.\n\n"+
        "Source: University of Wisconsin, Observing the Melting Point. Method adapted to the Punjab practical notebook; boiling uses the assigned national-curriculum chemical list. The workbook records two actual trials and calculates their mean.";
        var thermal=session.Definition.thermal;
        return text.Replace("naphthalene",session.Definition.chemicalName.ToLowerInvariant())
            .Replace("10 mm",session.SampleTarget)
            .Replace("75 °C",thermal.slowHeatingTemperature.ToString("0.#")+" °C")
            .Replace("about 1 °C/min","about "+thermal.gentleRate.ToString("0.#")+" °C/min")
            .Replace("80–82 °C",thermal.referenceMinimum.ToString("0.#")+"–"+thermal.referenceMaximum.ToString("0.#")+" °C")
            .Replace("Trial 1: 80.2 °C; trial 2: 80.4 °C. Mean = 80.3 °C.",
                $"Trial 1: {thermal.onsetTemperature+.2f:F1} °C; trial 2: {thermal.onsetTemperature+.4f:F1} °C. Mean = {thermal.onsetTemperature+.3f:F1} °C.")
            .Replace("80.2–81.0 °C: width 0.8 °C",(thermal.onsetTemperature+.2f).ToString("F1")+"–"+thermal.completionTemperature.ToString("F1")+" °C: width "+(thermal.completionTemperature-thermal.onsetTemperature-.2f).ToString("F1")+" °C")
            .Replace("an 8× clock",thermal.transitionClockScale>0
                ? $"a {thermal.clockScale:0.#}× warm-up clock that slows to {thermal.transitionClockScale:0.#}× near melting"
                : $"a {thermal.clockScale:0.#}× clock")
            .Replace("three-real-second timing allowance",thermal.observationAllowanceSeconds.ToString("0.#")+"-real-second timing allowance");
    }
}
