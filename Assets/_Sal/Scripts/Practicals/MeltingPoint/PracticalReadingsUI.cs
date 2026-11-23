using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VirtualLab.Physics141;

public partial class PracticalUIController
{
    RectTransform readingsSheet;
    Text[,] readingCells=new Text[2,2];
    InputField[,] enteredCells=new InputField[2,2];
    Text readingSummary,calculatorPrompt,readingBadge;
    Text calculatorResult,calculatorOperation;
    Button assignCalculator;
    InputField calculatedMean,calculatorTarget;
    Tajurbah_Gah.calc borrowedCalculator;
    bool refreshingReadings;
    void BuildReadingsSheet()
    {
        readingsSheet=PaperDocument("Readings and calculator",out var sheet);
        Label(sheet,"Observations and calculations",new Vector2(.045f,.90f),new Vector2(.65f,.97f),34).fontStyle=FontStyle.Bold;
        AddLauncherButton(sheet,new Vector2(.68f,.9f),new Vector2(.84f,.969f));
        Button(sheet,"Close",new Vector2(.86f,.9f),new Vector2(.952f,.969f),()=>CloseReadings(),24);
        var table=Card("Trial table",sheet,new Vector2(.04f,.27f),new Vector2(.675f,.86f),OhmSheet.GraphPaper);
        Card("Trial header",table,new Vector2(0,.84f),Vector2.one,OhmSheet.Note);
        if(!session.IsBoiling)Panel("Observation divider",table,new Vector2(.02f,.562f),new Vector2(.98f,.566f),OhmSheet.Rule);
        Label(table,"Reading (°C)",new Vector2(.025f,.86f),new Vector2(.32f,.98f),27).fontStyle=FontStyle.Bold;
        for(int t=0;t<2;t++)
        {
            int trial=t;float x=.34f+t*.33f;
            Label(table,"Trial "+(t+1),new Vector2(x,.86f),new Vector2(x+.30f,.98f),27).alignment=TextAnchor.MiddleCenter;
            for(int e=0;e<2;e++)
            {
                int endpoint=e;float y=.61f-e*.29f;
                if(e==0)Label(table,session.IsBoiling?"Continuous bubbles":"First liquid",new Vector2(.025f,y+.09f),new Vector2(.32f,y+.23f),24);
                else if(!session.IsBoiling)Label(table,"Fully liquid",new Vector2(.025f,y+.09f),new Vector2(.32f,y+.23f),24);
                readingCells[t,e]=Label(table,"Not recorded",new Vector2(x,y+.11f),new Vector2(x+.30f,y+.23f),24);readingCells[t,e].alignment=TextAnchor.MiddleCenter;
                enteredCells[t,e]=Input(table,"Your reading",new Vector2(x,y-.02f),new Vector2(x+.30f,y+.10f),false);
                var field=enteredCells[t,e];field.contentType=InputField.ContentType.DecimalNumber;
                var target=field.gameObject.AddComponent<PracticalCalculationTarget>();target.ui=this;target.field=field;
                target.fieldLabel="Trial "+(t+1)+" · "+(session.IsBoiling?"Continuous bubbles":e==0?"First liquid":"Fully liquid");
                field.onValueChanged.AddListener(v=>{if(refreshingReadings||session.Draft==null||trial+1!=session.TrialNumber)return;if(endpoint==0)session.Draft.t1=v;else session.Draft.t2=v;});
                field.onEndEdit.AddListener(v=>session.SaveDraft());
                if(session.IsBoiling&&e==1){readingCells[t,e].gameObject.SetActive(false);field.gameObject.SetActive(false);}
            }
        }
        Label(table,"Captured temperature above; your entered reading below. Unrecorded observations stay blank.",new Vector2(.025f,.025f),new Vector2(.975f,.15f),21);
        readingSummary=Label(sheet,"",new Vector2(.04f,.15f),new Vector2(.45f,.25f),22);
        Label(sheet,"Your mean (optional)",new Vector2(.46f,.22f),new Vector2(.67f,.27f),21);
        calculatedMean=Input(sheet,"Mean",new Vector2(.46f,.145f),new Vector2(.67f,.215f),false);calculatedMean.contentType=InputField.ContentType.DecimalNumber;
        var meanTarget=calculatedMean.gameObject.AddComponent<PracticalCalculationTarget>();meanTarget.ui=this;meanTarget.field=calculatedMean;
        calculatedMean.onValueChanged.AddListener(v=>{if(!refreshingReadings&&session.Draft!=null)session.Draft.calculatedMean=v;});calculatedMean.onEndEdit.AddListener(v=>session.SaveDraft());
        var calculatorPanel=Card("Calculator panel",sheet,new Vector2(.696f,.14f),new Vector2(.952f,.86f),OhmSheet.Note);
        var calculator=Rect("Calculator",calculatorPanel,new Vector2(.05f,.04f),new Vector2(.95f,.96f));
        (borrowedCalculator,calculatorResult,calculatorOperation,calculatorPrompt,assignCalculator)=
            PracticalUiStyle.Calculator(calculator,AssignCalculator,RefreshCalculatorSelection,session.PlayUiSound);
        var written=Button(sheet,"Open written workbook",new Vector2(.04f,.035f),new Vector2(.33f,.115f),()=>OpenPage("Workbook"),25);
        Button(sheet,"Study observations",new Vector2(.36f,.035f),new Vector2(.65f,.115f),()=>{ShowStudy(false);SelectStudy(4);},24);
        StylePaperContents(sheet);
        SelectPaperTab(written,true);
        SelectPaperTab(assignCalculator,true);
        readingsSheet.gameObject.SetActive(false);
        var readingsButton=run.Find("CornerNavigation/Readings");
        if(readingsButton!=null)
        {
            var badge=Card("Saved count",readingsButton,new Vector2(.72f,.65f),new Vector2(1.12f,1.05f),new Color(.85f,.35f,.16f,.95f));readingBadge=Label(badge,"0",Vector2.zero,Vector2.one,19);readingBadge.alignment=TextAnchor.MiddleCenter;
        }
    }
    public void ShowReadings()
    {
        if(session.InMenu||session.ShowingResults||session.drag.IsHolding)return;
        PresentDocument(readingsSheet);
        SelectCalculatorTarget(null);RefreshReadingsSheet();
    }
    public void CloseReadings(){EndDocument();}
    public void SelectCalculatorTarget(InputField field)
    {
        if(field!=null&&!field.interactable)return;
        if(calculatorTarget!=null)calculatorTarget.targetGraphic.color=PracticalUiStyle.Entry;
        calculatorTarget=field;
        if(field!=null)field.targetGraphic.color=PracticalUiStyle.EntryHighlight;
        RefreshCalculatorSelection();
    }
    public void AssignCalculator()
    {
        if(!CanAssignCalculation())return;
        calculatorTarget.text=borrowedCalculator.GetOutputValue();calculatorTarget.ForceLabelUpdate();
        session.SaveDraft();calculatorPrompt.text="Result copied to your selected entry";
    }
    bool CanAssignCalculation()
    {
        return calculatorTarget!=null&&calculatorTarget.interactable&&calculatorTarget.gameObject.activeInHierarchy&&
            calculatorOperation.text.Length==0&&float.TryParse(calculatorResult.text,NumberStyles.Float,CultureInfo.InvariantCulture,out float result)&&float.IsFinite(result);
    }
    void RefreshCalculatorSelection()
    {
        calculatorPrompt.text=calculatorTarget==null?"Tap a number field to select it":"Selected · "+calculatorTarget.GetComponent<PracticalCalculationTarget>().fieldLabel;
        assignCalculator.interactable=CanAssignCalculation();
    }
    void RefreshReadingsSheet()
    {
        int count=session.CompletedTrials.Sum(t=>(t.onset.captured?1:0)+(session.IsBoiling?0:t.completion.captured?1:0))+(session.Simulation.onset.captured?1:0)+(session.IsBoiling?0:session.Simulation.completion.captured?1:0);
        if(readingBadge!=null){readingBadge.text=count.ToString();readingBadge.transform.parent.gameObject.SetActive(count>0);}
        if(readingsSheet==null||!readingsSheet.gameObject.activeSelf)return;
        refreshingReadings=true;
        for(int t=0;t<2;t++)
        {
            bool current=t+1==session.TrialNumber;var archived=session.CompletedTrials.FirstOrDefault(x=>x.number==t+1);
            for(int e=0;e<2;e++)
            {
                var captured=current?(e==0?session.Simulation.onset:session.Simulation.completion):archived==null?null:e==0?archived.onset:archived.completion;
                readingCells[t,e].text=captured!=null&&captured.captured?captured.temperature.ToString("F1")+" °C":"Not recorded";
                var field=enteredCells[t,e];field.interactable=current&&captured!=null&&captured.captured;
                string value=current?(e==0?session.Draft.t1:session.Draft.t2):archived==null?"":e==0?archived.enteredOnset:archived.enteredCompletion;
                if(!field.isFocused)field.SetTextWithoutNotify(value??"");
            }
        }
        if(!calculatedMean.isFocused)calculatedMean.SetTextWithoutNotify(session.Draft.calculatedMean??"");
        readingSummary.text=session.CompletedTrials.Count==0?"Trial 1: make your first complete measurement.\nTrial 2: repeat with a fresh sample.":"Trial 1 is saved. Complete the separate second trial.\nMean = (trial 1 + trial 2) ÷ 2.";
        refreshingReadings=false;
    }
}
public class PracticalCalculationTarget : MonoBehaviour,ISelectHandler,IPointerDownHandler
{
    public PracticalUIController ui;public InputField field;
    public string fieldLabel="Mean";
    public void OnSelect(BaseEventData e){ui.SelectCalculatorTarget(field);}
    public void OnPointerDown(PointerEventData e){ui.SelectCalculatorTarget(field);}
}
