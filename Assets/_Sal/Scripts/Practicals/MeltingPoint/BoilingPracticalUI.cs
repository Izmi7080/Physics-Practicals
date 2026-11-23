using UnityEngine;
using UnityEngine.UI;

public partial class PracticalUIController
{
    Text liquidAmount;
    RectTransform liquidCard;
    void BuildBoilingControls()
    {
        liquidCard=Card("Fusion liquid controls",run,new Vector2(.012f,.325f),new Vector2(BenchLeft-.012f,.567f),Navy);
        liquidAmount=Label(liquidCard,"",new Vector2(.06f,.70f),new Vector2(.94f,.96f),23);
        Button(liquidCard,"Add drops",new Vector2(.05f,.40f),new Vector2(.95f,.66f),()=>session.AddLiquidDrop(),23);
        Button(liquidCard,"Replace",new Vector2(.05f,.07f),new Vector2(.47f,.33f),()=>session.EmptyLiquid(),21);
        var accept=Button(liquidCard,"Use liquid",new Vector2(.52f,.07f),new Vector2(.95f,.33f),()=>session.AcceptLiquid(),21);accept.name="Accept liquid";
    }
    void RefreshBoilingControls()
    {
        liquidCard.gameObject.SetActive(session.IsBoiling&&session.At("sample")&&!PageVisible);
        liquidAmount.text="Fusion tube: "+Mathf.RoundToInt(session.preparation.liquidFraction*100)+"%";
        liquidCard.Find("Accept liquid").gameObject.SetActive(!session.steps.ShowHints);
        foreach(var button in liquidCard.GetComponentsInChildren<Button>())button.interactable=session.CanInteract;
    }
    string BoilingReference(bool examples)
    {
        var t=session.Definition.thermal;
        return "Aim\nDetermine the boiling point of "+session.Definition.chemicalName+" using two separate trials.\n\n"+
            "Apparatus\n250 mL beaker, water, thermometer, fusion tube, capillary, dropper, thread, clamp stand, stirrer and electric bath heater.\n\n"+
            "Method\nPlace the beaker on the fixed heater. Fill about 100 mL from the tap and return it. Seal one end of the EMPTY capillary at the separate glass-preparation burner; let it cool. Use the dropper to fill about one third of the fusion tube. Insert the capillary CLOSED END UP, with its open end in the liquid. Attach the fusion tube beside the thermometer bulb using thread. Clamp and immerse the bulb and sample, keeping the fusion-tube opening above the bath and avoiding glass contact.\n\n"+
            "Heat and observe\nSwitch on the electric heater. Stir continuously. Warm at about "+t.warmingRate.ToString("0.#")+" °C/min, reducing to about "+t.gentleRate.ToString("0.#")+" °C/min near "+t.slowHeatingTemperature.ToString("0.#")+" °C. Occasional bubbles may be expanding air. Record when a CONTINUOUS stream comes from the capillary's open end. Switch off, record the reading, and perform a separate second trial with a fresh sample.\n\n"+
            "Workbook\nThe two trial readings are averaged: (trial 1 + trial 2) / 2. They are separate heating runs, not two points on one run. Captures save the scale, numeric temperature and sample condition.\n\n"+
            (examples?"Guidance\nExpect approximately "+t.referenceMinimum.ToString("0.#")+"–"+t.referenceMaximum.ToString("0.#")+" °C at near atmospheric pressure. Example only: readings "+(t.onsetTemperature+.2f).ToString("F1")+" and "+(t.onsetTemperature+.4f).ToString("F1")+" °C average "+(t.onsetTemperature+.3f).ToString("F1")+" °C. An early isolated bubble is not the endpoint; rapid heating can give a high reading.\n\n":"")+
            "Precautions and scope\nUse an electric water bath for flammable solvents. The sink is for bath water; replace solvent samples through the approved solvent-waste procedure. The separate flame is only for an empty capillary before solvent is dispensed. Benzene is a virtual-only exercise because of its health hazards. Real laboratory work needs the school's approved method and supervision.\n\n"+
            "This follows the assigned book's continuous-bubbles endpoint. Some modern micro-methods use liquid entry during cooling; that is a different observation. Pressure affects boiling point. The app assumes approximately atmospheric pressure. Its accelerated clock and scoring tolerance are simulation settings.\n\n"+
            "Curriculum note: the assigned 2006 list names benzene at 5.4; the Punjab notebook page names toluene. This app retains the assigned benzene practical. Ask your teacher which assigned list your class follows.";
    }
}
