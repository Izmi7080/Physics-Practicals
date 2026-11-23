using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum PracticalMode { Guided, Assessment }
[Serializable]
public class PracticalStep
{
    public string id, title, cameraAnchor;
    [TextArea] public string guidedHint;
}
// Only this component advances the procedure. Interactions report readiness.
public class StepController : MonoBehaviour
{
    public static StepController Instance { get; private set; }
    public PracticalMode mode;
    public CameraDirector director;
    public List<PracticalStep> steps = new List<PracticalStep>();
    public int currentIndex = -1;
    public bool autoStart;
    public Button viewToggleButton;
    public bool Ready { get; private set; }
    public bool IsOverview { get; private set; }
    public bool ShowHints { get { return mode == PracticalMode.Guided; } }
    public PracticalStep Current { get { return currentIndex >= 0 && currentIndex < steps.Count ? steps[currentIndex] : null; } }
    public event Action<int, PracticalStep> StepChanged;
    public event Action PracticalFinished;
    void Awake() { Instance = this; if (steps.Count == 0) BuildDefaultSteps(); }
    public int IndexOf(string id) { return steps.FindIndex(step=>step.id==id); }
    public bool IsCurrent(string id) { return Current!=null&&Current.id==id; }
    public void Configure(PracticalDefinition definition)
    {
        definition.ValidateForLaunch();
        steps=new List<PracticalStep>();
        foreach(var step in definition.steps)steps.Add(new PracticalStep{id=step.id,title=step.title,cameraAnchor=step.cameraAnchor,guidedHint=step.guidedHint});
    }
    public void GoTo(string id)
    {
        int index=IndexOf(id);if(index<0)throw new ArgumentException("Unknown procedure step: "+id);GoTo(index);
    }
    void OnDestroy() { if (Instance == this) Instance = null; }
    public void SetReady(bool value) { Ready = value; }
    public void StartPractical() { GoTo(0); }
    public void CompleteCurrentStep()
    {
        if (!Ready || director != null && director.IsMoving) return;
        if (currentIndex == steps.Count - 1) { PracticalFinished?.Invoke(); return; }
        GoTo(currentIndex + 1);
    }
    public void GoTo(int index)
    {
        currentIndex = Mathf.Clamp(index, 0, steps.Count - 1); Ready = false; IsOverview = false;
        // Procedure selection does not take control of the student's view.
        StepChanged?.Invoke(currentIndex, Current);
    }
    public void ShowMenu() { currentIndex = -1; Ready = false; IsOverview = true; if (director != null) director.SnapTo("Overview"); }
    public void Inspect(string anchor)
    {
        if(Current==null||director==null||director.IsMoving)return;
        IsOverview=anchor=="Overview";director.MoveTo(anchor,.45f);
    }
    public void ToggleWideView()
    {
        if (Current == null || director == null || director.IsMoving) return;
        IsOverview = !IsOverview;
        director.MoveTo(IsOverview ? "Overview" : Current.cameraAnchor);
    }
    [ContextMenu("Reset steps to defaults")]
    public void BuildDefaultSteps()
    {
        Configure(PracticalDefinitions.Load());
    }
}
