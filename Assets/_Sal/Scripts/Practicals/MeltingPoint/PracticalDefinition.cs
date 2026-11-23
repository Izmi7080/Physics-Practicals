using System;
using System.Collections.Generic;
using UnityEngine;

public enum PracticalWorkflow { Melting, Boiling }

[Serializable]
public sealed class ThermalSettings
{
    public float ambientTemperature=25, onsetTemperature=80, completionTemperature=81;
    public float referenceMinimum=80, referenceMaximum=82, slowHeatingTemperature=75, maximumBathTemperature=95;
    public float clockScale=8, maximumHeatingRate=6, warmingRate=4, gentleRate=1, excessiveRate=1.5f;
    [Tooltip("Clock speed near the melting range. Zero keeps the normal clock speed.")]
    public float transitionClockScale;
    public float stirredResponseSeconds=2, unstirredResponseSeconds=6, thermometerResponseSeconds=.5f;
    public float stirringAllowanceSeconds=6, observationAllowanceSeconds=3, readingTolerance=.5f;
    public float coolingRate=.025f, nominalSampleColumnMm=3;
    public ThermalSettings Copy() { return (ThermalSettings)MemberwiseClone(); }
}

[Serializable]
public sealed class SamplePreparationSettings
{
    public float minimumColumnMm=2, maximumColumnMm=3, capacityMm=12;
    public float collectionIncrementMm=1, packingIncrementMm=1, grindingRevolutions=4;
    public bool Accepts(float column,float packed) { return column>=minimumColumnMm&&column<=maximumColumnMm&&column>0&&packed/column>=.99f; }
    public string ColumnDescription { get { return minimumColumnMm==maximumColumnMm?minimumColumnMm.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+" mm":minimumColumnMm.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"–"+maximumColumnMm.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+" mm"; } }
}

// A reusable Inspector-editable description. Planned definitions are deliberately
// not launchable until their apparatus, procedure and observations are implemented.
[CreateAssetMenu(menuName="Virtual Lab/Practical definition")]
public sealed class PracticalDefinition : ScriptableObject
{
    public string experimentId="naphthalene", curriculumNumber="5.1", chemicalName="Naphthalene";
    public PracticalWorkflow workflow;
    public bool playable;
    public int requiredTrials=2;
    public string procedureRevision="melting-foundation-v1";
    public string title="Determine the Melting Point of Naphthalene";
    [TextArea] public string aim="Measure the temperature interval from first liquid to completely melted sample.";
    [TextArea] public string apparatus="Beaker, thermometer, burner, tripod, gauze, stirrer, capillary, clamp stand, mortar and pestle.";
    [TextArea] public string sourceNotes;
    public ThermalSettings thermal=new ThermalSettings();
    public WaterBathSettings water=new WaterBathSettings();
    public SamplePreparationSettings sample=new SamplePreparationSettings();
    public List<PracticalStep> steps=new List<PracticalStep>();

    public MeltingSimulation CreateMeltingSimulation()
    {
        if(workflow!=PracticalWorkflow.Melting)throw new InvalidOperationException("A boiling practical needs its boiling workflow; it cannot run the melting simulation.");
        return new MeltingSimulation { settings=thermal.Copy(),bath=thermal.ambientTemperature,
            sample=thermal.ambientTemperature,thermometer=thermal.ambientTemperature,
            sampleColumnMm=thermal.nominalSampleColumnMm,heatSetting=thermal.gentleRate/thermal.maximumHeatingRate };
    }
    public ThermalSimulation CreateSimulation()
    {return workflow==PracticalWorkflow.Melting?(ThermalSimulation)CreateMeltingSimulation():CreateBoilingSimulation();}
    public BoilingSimulation CreateBoilingSimulation()
    {
        if(workflow!=PracticalWorkflow.Boiling)throw new InvalidOperationException("A melting definition cannot use the boiling model.");
        return new BoilingSimulation{settings=thermal.Copy(),bath=thermal.ambientTemperature,sample=thermal.ambientTemperature,thermometer=thermal.ambientTemperature,heatSetting=thermal.gentleRate/thermal.maximumHeatingRate};
    }
    public void ValidateForLaunch()
    {
        if(!playable)throw new InvalidOperationException(title+" is registered but not yet playable.");
        if(steps==null||steps.Count==0)throw new InvalidOperationException("The practical has no procedure.");
        var ids=new HashSet<string>();
        foreach(var step in steps)if(step==null||string.IsNullOrEmpty(step.id)||!ids.Add(step.id))throw new InvalidOperationException("Procedure step IDs must be non-empty and unique.");
        foreach(var required in workflow==PracticalWorkflow.Melting?PracticalStepIds.Melting:PracticalStepIds.Boiling)if(!ids.Contains(required))throw new InvalidOperationException("The melting workflow requires step "+required+".");
        if(requiredTrials<1||requiredTrials>2||water==null||water.targetMl<=0||water.capacityMl<=water.targetMl||water.flowMlPerSecond<=0||water.toleranceMl<0||thermal==null||sample==null||(workflow==PracticalWorkflow.Melting&&thermal.completionTemperature<=thermal.onsetTemperature)||thermal.maximumBathTemperature<=thermal.completionTemperature||
            thermal.clockScale<=0||thermal.transitionClockScale<0||thermal.transitionClockScale>thermal.clockScale||thermal.maximumHeatingRate<=0||thermal.stirredResponseSeconds<=0||thermal.unstirredResponseSeconds<=0||thermal.thermometerResponseSeconds<=0||
            thermal.gentleRate<0||thermal.warmingRate>thermal.maximumHeatingRate||sample.minimumColumnMm<=0||sample.maximumColumnMm<sample.minimumColumnMm||
            sample.capacityMm<sample.maximumColumnMm||sample.collectionIncrementMm<=0||sample.packingIncrementMm<=0||sample.grindingRevolutions<=0)
            throw new InvalidOperationException("The practical contains invalid thermal or sample settings.");
    }
}

public static class PracticalDefinitions
{
    public const string DefaultId="naphthalene";
    public static readonly string[] AssignedIds={DefaultId,"biphenyl","acetone","benzene","ethanol"};
    public static PracticalDefinition Load(string id=DefaultId)
    {
        if(string.IsNullOrEmpty(id)||!System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-z][a-z0-9-]*$"))throw new ArgumentException("Invalid experiment ID.","id");
        var definition=Resources.Load<PracticalDefinition>("Practicals/Definitions/"+id);
        if(definition==null)throw new InvalidOperationException("Missing practical definition: "+id);
        return definition;
    }
}

public static class PracticalStepIds
{
    public const string Setup="setup",Water="water",Sample="sample",Grind="grind",Pack="pack",Attach="attach",Immerse="immerse",Ignite="ignite",Heat="heat",Shutdown="shutdown",Notebook="notebook";
    public static readonly string[] Boiling={Setup,Water,"seal",Sample,"invert",Attach,Immerse,Ignite,Heat,Shutdown,Notebook};
    public static readonly string[] Melting={Setup,Water,Sample,Grind,Pack,Attach,Immerse,Ignite,Heat,Shutdown,Notebook};
}
