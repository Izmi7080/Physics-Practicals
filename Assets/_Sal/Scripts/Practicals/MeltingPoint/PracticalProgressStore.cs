using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class PreparationState
{
    public bool tripod, gauze, beaker, water, sample, pestle, collected, capillaryPlaced, band, clamped, immersed, stirrer;
    public bool capillarySealed,burnerStarted,shutdownConfirmed;
    public float grind;
    public float waterVolumeMl,liquidFraction;
    public string waterReturnStep="";
    public bool capillaryInverted;
    public int packing;
    public float sampleHeightMm, packedHeightMm;
    public bool sampleAccepted;
    public float PackingFraction { get { return sampleHeightMm>0 ? Mathf.Clamp01(packedHeightMm/sampleHeightMm) : 0; } }
    public float VisibleColumnMm => sampleHeightMm*(2-PackingFraction);
    public int SampleQualityScore { get { return QualityScore(new SamplePreparationSettings()); } }
    public int QualityScore(SamplePreparationSettings target) { return target.Accepts(sampleHeightMm,packedHeightMm)?10:PackingFraction>=.99f?5:0; }
    public bool Prepared { get { return IsPrepared(new SamplePreparationSettings()); } }
    public bool IsPrepared(PracticalDefinition definition) { return definition.workflow==PracticalWorkflow.Melting?IsPrepared(definition.sample):capillarySealed&&tripod&&gauze&&beaker&&water&&sample&&liquidFraction>0&&capillaryInverted&&band&&immersed; }
    public bool IsPrepared(SamplePreparationSettings target) { return capillarySealed && tripod && gauze && beaker && water && sample && grind >= target.grindingRevolutions && sampleAccepted && band && immersed; }
}
[Serializable]
public class WorkbookDraft
{
    public string calculatedMean="";
    public string trial1Reading="",trial2Reading="",postLab1="",postLab2="",postLab3="",postLab4="",postLab5="",postLab6="";
    public string studentName="",date="",initialAppearance="",groundAppearance="",onsetDescription="",completionDescription="",t1="",t2="",result="",conclusion="",reflection="";
}
[Serializable]
public class PracticalNotebook
{
    public string experimentId,procedureRevision,observationFormat="melting-range-v1";
    public List<PracticalTrial> trials=new List<PracticalTrial>();
    public string id, date, mode, conclusion, feedback;
    public bool practice;
    public float meanPoint;
    public PracticalWorkflow workflow;
    public float t1, t2, width;
    public int score;
    public int preparationScore, heatingScore, stirringScore, timingScore, readingScore;
    public float sampleHeightMm, packingFraction;
    public WorkbookDraft workbook;
    public MeltingObservation onset, completion;
}
[Serializable]
public class PracticalTrial
{
    public int number=1;
    public bool completed;
    public int preparationScore,heatingScore,stirringScore,timingScore,readingScore,score;
    public string feedback="",initialAppearance="",groundAppearance="",onsetDescription="",completionDescription="";
    // Two observations inside ONE trial, not two trial measurements.
    public string observationFormat="melting-range-v1";
    public MeltingObservation onset=new MeltingObservation(),completion=new MeltingObservation();
    public string enteredOnset="",enteredCompletion="";
}
[Serializable]
public class PracticalSave
{
    public int version = 2, step;
    public string experimentId=PracticalDefinitions.DefaultId,procedureRevision,stepId;
    public int currentTrial=1;
    public List<PracticalTrial> trials=new List<PracticalTrial>();
    public PracticalMode mode;
    public bool resumable, practice;
    public PreparationState preparation = new PreparationState();
    public ThermalSnapshot simulation;
    public List<PracticalNotebook> notebooks = new List<PracticalNotebook>();
    public WorkbookDraft draft = new WorkbookDraft();
    public bool handsFreeStirring;
}
public static class PracticalProgressStore
{
    public static string OverrideDirectory;
    public static string DataDirectory { get { return OverrideDirectory ?? Application.persistentDataPath; } }
    public static string PathName { get { return PathFor(PracticalDefinitions.DefaultId); } }
    public static string LegacyPathName { get { return Path.Combine(DataDirectory,"naphthalene-progress-v1.json"); } }
    public static string PathFor(string experimentId)
    {
        if(string.IsNullOrEmpty(experimentId)||!System.Text.RegularExpressions.Regex.IsMatch(experimentId,"^[a-z][a-z0-9-]*$"))throw new ArgumentException("Invalid experiment ID.");
        return Path.Combine(DataDirectory,"Practicals",experimentId,"progress-v2.json");
    }
    public static string LastError { get; private set; }
    public static PracticalSave Load() { return Load(PracticalDefinitions.Load()); }
    public static PracticalSave Load(PracticalDefinition definition)
    {
        try
        {
            string path=PathFor(definition.experimentId);
            bool legacy=!File.Exists(path)&&definition.experimentId==PracticalDefinitions.DefaultId&&File.Exists(LegacyPathName);
            LastError=null;
            if(!File.Exists(path)&&!legacy)return Empty(definition);
            var save = JsonUtility.FromJson<PracticalSave>(File.ReadAllText(legacy?LegacyPathName:path));
            if(save==null||(legacy?save.version!=1:save.version!=2)||save.preparation==null||save.notebooks==null||(!legacy&&save.experimentId!=definition.experimentId))
                throw new InvalidDataException("Unsupported or incomplete save.");
            save.version=2;save.experimentId=definition.experimentId;
            if(save.draft==null)save.draft=new WorkbookDraft();
            if(save.trials==null)save.trials=new List<PracticalTrial>();
            if(save.currentTrial<1)save.currentTrial=1;
            if(legacy)
            {
                if(save.step<0||save.step>=PracticalStepIds.Melting.Length)throw new InvalidDataException("Invalid legacy step.");
                save.stepId=PracticalStepIds.Melting[save.step];save.procedureRevision="legacy-v1";
            }
            foreach(var notebook in save.notebooks)
            {
                if(notebook==null)throw new InvalidDataException("Invalid notebook.");
                if(string.IsNullOrEmpty(notebook.experimentId))notebook.experimentId=definition.experimentId;
                if(notebook.experimentId!=definition.experimentId)throw new InvalidDataException("A notebook belongs to another experiment.");
                if(string.IsNullOrEmpty(notebook.procedureRevision))notebook.procedureRevision=legacy?"legacy-v1":definition.procedureRevision;
                if(notebook.trials==null)notebook.trials=new List<PracticalTrial>();
                if(notebook.trials.Count==0)notebook.trials.Add(new PracticalTrial{number=1,completed=true,onset=notebook.onset??new MeltingObservation(),completion=notebook.completion??new MeltingObservation(),enteredOnset=notebook.t1.ToString("F1",System.Globalization.CultureInfo.InvariantCulture),enteredCompletion=notebook.t2.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)});
            }
            if(save.simulation!=null&&save.simulation.settings==null)save.simulation.settings=definition.thermal.Copy();
            if(legacy&&save.simulation!=null&&save.trials.Count==0)
                save.trials.Add(new PracticalTrial{number=1,onset=save.simulation.onset,completion=save.simulation.completion,enteredOnset=save.draft.t1,enteredCompletion=save.draft.t2});
            if(save.resumable)
            {
                int index=definition.steps.FindIndex(s=>s.id==save.stepId);
                if(save.procedureRevision!=definition.procedureRevision||index<0)
                { save.resumable=false;LastError="The procedure has changed. Saved notebooks are retained; start a new preparation."; }
                else save.step=index;
            }
            if(legacy)Save(save); // New path only; the original v1 file is never changed.
            return save;
        }
        catch (Exception e) { LastError = "Saved progress could not be read. You can start a new run. " + e.Message; return Empty(definition); }
    }
    static PracticalSave Empty(PracticalDefinition definition) { return new PracticalSave{experimentId=definition.experimentId,procedureRevision=definition.procedureRevision}; }
    public static bool Save(PracticalSave save)
    {
        try
        {
            if(save==null||save.version!=2)throw new InvalidDataException("Invalid save version.");
            string path=PathFor(save.experimentId);Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(save, true));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            LastError = null; return true;
        }
        catch (Exception e) { LastError = "Progress could not be saved: " + e.Message; Debug.LogWarning(LastError); return false; }
    }
}
