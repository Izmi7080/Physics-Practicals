using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class PracticalUIController
{
    RectTransform study,studyDiagram;
    Text studyText,studyFooter,actionZoomLabel;
    Button actionZoom,zoomIn,zoomOut;
    ScrollRect studyScroll;
    readonly string[] studySections={"Concept","Procedure","Controls","Precautions","Observations","Apparatus"};
    Button[] studyTabs;
    void BuildActionZoom()
    {
        actionZoom=Button(smallHint,"Zoom",new Vector2(.04f,.09f),new Vector2(.68f,.91f),()=>session.InspectCurrentAction(),21);
        actionZoomLabel=actionZoom.GetComponentInChildren<Text>();actionZoomLabel.rectTransform.anchorMin=new Vector2(.19f,0);
        var image=Rect("Magnifier",actionZoom.transform,new Vector2(.025f,.13f),new Vector2(.17f,.87f)).gameObject.AddComponent<Image>();image.sprite=EmojiSprite("inspect");image.preserveAspect=true;image.raycastTarget=false;
        zoomOut=Button(smallHint,"−",new Vector2(.71f,.09f),new Vector2(.83f,.91f),()=>Zoom(1),25);
        zoomIn=Button(smallHint,"+",new Vector2(.85f,.09f),new Vector2(.97f,.91f),()=>Zoom(-1),25);
    }
    void RefreshActionZoom()
    {
        actionZoomLabel.text="Zoom "+session.ActionSubject;actionZoom.interactable=session.CanChangeView;
        zoomIn.gameObject.SetActive(session.cameraDirector.IsInspection);zoomOut.gameObject.SetActive(session.cameraDirector.IsInspection);
        zoomIn.interactable=zoomOut.interactable=session.CanChangeView;
    }
    void BuildStudy()
    {
        study=PaperDocument("Study and getting started", out var sheet);
        Label(sheet,"Study • "+session.Definition.chemicalName,new Vector2(.045f,.90f),new Vector2(.65f,.975f),34).fontStyle=FontStyle.Bold;
        AddLauncherButton(sheet,new Vector2(.68f,.9f),new Vector2(.84f,.969f));
        Button(sheet,"Close",new Vector2(.85f,.9f),new Vector2(.952f,.969f),()=>CloseStudy(),24);
        studyTabs=new Button[studySections.Length];
        for(int i=0;i<studySections.Length;i++)
        {
            int n=i;
            studyTabs[i]=Button(sheet,studySections[i],new Vector2(.039f+i*.155f,.80f),new Vector2(.181f+i*.155f,.873f),()=>SelectStudy(n),22);
        }
        var root=Rect("Study scroll",sheet,new Vector2(.045f,.16f),new Vector2(.952f,.77f));
        var viewport=Panel("Viewport",root,Vector2.zero,Vector2.one,Color.clear);viewport.GetComponent<Image>().raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
        studyText=Label(viewport,"",new Vector2(0,1),Vector2.one,27);studyText.alignment=TextAnchor.UpperLeft;studyText.rectTransform.pivot=new Vector2(.5f,1);
        studyText.verticalOverflow=VerticalWrapMode.Overflow;
        studyScroll=PracticalUiStyle.VerticalScroll(root,viewport,studyText.rectTransform);
        studyDiagram=Rect("Setup diagram",sheet,new Vector2(.63f,.17f),new Vector2(.95f,.76f));cursor=0;BuildDiagram(studyDiagram);
        var footer=Button(sheet,"Start at the bench",new Vector2(.61f,.035f),new Vector2(.952f,.12f),()=>CloseStudy(),26);studyFooter=footer.GetComponentInChildren<Text>();
        Label(sheet,"Opening Study pauses the practical.",new Vector2(.045f,.035f),new Vector2(.59f,.12f),22);
        StylePaperContents(sheet);
        SelectPaperTab(footer, true);
        study.gameObject.SetActive(false);
    }
    public void ShowStudy(bool beforeStart)
    {
        if(session.drag.IsHolding||session.ShowingResults)return;
        PresentDocument(study);SelectStudy(beforeStart ? 1 : 0);
        studyFooter.text=session.InMenu?"Back to introduction":beforeStart?"Start at the bench":"Return to experiment";
    }
    public void CloseStudy(){EndDocument();}
    public void SelectStudy(int section)
    {
        bool guide=session.InMenu||session.steps.ShowHints;
        if(!guide)section=2;
        for(int i=0;i<studyTabs.Length;i++)
        {
            studyTabs[i].gameObject.SetActive(guide||i==2);
            SelectPaperTab(studyTabs[i],i==section);
        }
        studyDiagram.gameObject.SetActive(section==0&&guide);
        studyText.rectTransform.anchorMax=new Vector2(section==0&&guide?.61f:1,1);
        var d=session.Definition;string value;
        if(!guide)value="Controls\n\nDrag the real apparatus on the bench. Small Move handles assist with thin tools.\n\nTap the beaker to carry it between stations. Drag it under the tap, then tap the handle to start and stop the flow.\n\nOpen View to inspect or adjust the camera. Readings and Workbook contain your saved observations.";
        else if(section==0)value="Aim\n"+d.aim+"\n\nPrinciple\n"+(session.IsBoiling?"Heating increases the liquid's temperature. In this assigned method, record the thermometer when a continuous stream of bubbles leaves the inverted capillary.":"A solid changes into liquid near its melting point. Watch for the first liquid and then the disappearance of the last solid.")+"\n\nApparatus and materials\n"+d.apparatus+"\n\nTwo trials\nMake one complete measurement, then repeat with a fresh sample. The workbook compares both trials and calculates their mean.";
        else if(section==1)value="Procedure\n\n"+string.Join("\n\n",session.steps.steps.Select((s,i)=>(i+1)+". "+s.title+"\n"+
            (s.id=="water" ? "Tap the beaker to carry it to the sink. Drag it under the spout, then use the tap handle to fill to the mark. Tap the beaker to carry it back and drag it onto the gauze."
                : s.guidedHint.Replace("from the tray","from the table").Replace("Take out","Use").Replace("Take ","Use "))))+
            "\n\nActions advance automatically. The next required apparatus appears on the table.";
        else if(section==2)value="Getting around\n\n1. Drag the apparatus from the gold circle to the mint circle. Small Move handles assist with thin tools.\n\n2. Tap the beaker to carry it between stations. Drag it under the spout, then use the tap handle itself.\n\n3. Open View for close-ups, zoom and an optional view joystick.\n\n4. Hold Grind or Stir to move the tool. During heating you can hold Stir with one finger while using views or recording with another.\n\n5. Readings stores each observation beside the other trial. Workbook opens your written practical.";
        else if(section==3)value="Precautions\n\n• Use the main tap to fill the beaker to its marked line. Compare the surface with the line at eye level. Stop the tap and empty the beaker into the sink if you overfill it.\n\n• Keep the bulb and sample submerged, clear of the bottom and sides. Keep the thread above water.\n\n• Stir regularly to reduce temperature differences in the bath.\n\n• Heat gently near the expected transition. Fast heating makes the reading less reliable.\n\n• Avoid inhaling vapour. Never place the sample directly in the flame.\n\n"+(session.IsBoiling?"• The solvent bath uses an electric heater. The separate flame is only for sealing an EMPTY capillary before adding solvent.":"• Use finely ground, compact powder and a sealed capillary. Turn off the burner after recording.");
        else value=section==5 ? EquipmentNotes() : PracticalReference(true);
        studyText.text=value;
        Canvas.ForceUpdateCanvases();
        studyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(studyScroll.viewport.rect.height,studyText.preferredHeight+24));
        studyScroll.StopMovement();
        studyScroll.verticalNormalizedPosition=1;
    }
    string EquipmentNotes() => "Tripod and wire gauze\nThe tripod holds the beaker above the heat source. The gauze supports the glass and spreads the heat.\n\n" +
        "Beaker, tap and water bath\nFill the beaker to the mark. The heated water surrounds the sample and thermometer bulb so the sample is heated indirectly.\n\n" +
        (session.IsBoiling ? "Fusion tube and capillary\nThe fusion tube holds the test liquid. An inverted capillary makes a stream of bubbles visible.\n\n"
            : "Mortar, pestle and capillary\nThe mortar holds the crystals while the pestle crushes them. The thin, sealed-end capillary holds a short column of powder so you can see it melt.\n\n") +
        "Thermometer, thread and clamp\nThe thermometer measures temperature near the sample. The thread holds the sample alongside the bulb. The clamp keeps both clear of the hot glass.\n\n" +
        "Stirrer and heat source\nThe stirrer distributes heat through the water. The heat source warms the bath from below.";
    void HideExtraPages()
    {if(study!=null)study.gameObject.SetActive(false);if(readingsSheet!=null)readingsSheet.gameObject.SetActive(false);}

    void PresentDocument(RectTransform active)
    {
        if (!session.DocumentOpen) session.OpenDocument();
        HideExtraPages();
        document.gameObject.SetActive(false);
        menu.gameObject.SetActive(false);
        run.gameObject.SetActive(false);
        pause.gameObject.SetActive(false);
        results.gameObject.SetActive(false);
        active.gameObject.SetActive(true);
    }

    void EndDocument()
    {
        session.SaveDraft();
        HideExtraPages();
        document.gameObject.SetActive(false);
        session.CloseDocument();
        menu.gameObject.SetActive(session.InMenu);
        run.gameObject.SetActive(!session.InMenu&&!session.ShowingResults);
        pause.gameObject.SetActive(session.IsPaused&&!session.InMenu&&!session.ShowingResults);
        results.gameObject.SetActive(session.ShowingResults);
    }
}
