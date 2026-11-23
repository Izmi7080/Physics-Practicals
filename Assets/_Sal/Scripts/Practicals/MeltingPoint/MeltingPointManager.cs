using System;
using UnityEngine;

public class MeltingPointManager : MonoBehaviour
{
    public static MeltingPointManager Instance;
    public enum ExperimentState { NotStarted, Crushing, Packing, Attaching, Immersing, Heating, Melting, Melted, Recorded }
    // Compatibility for legacy components; these no longer drive procedure progression.
    public ExperimentState currentState;
    public bool naphthaleneWellCrushed, capillaryWellPacked, thermometerAttachedCorrectly,
        thermometerTouchingGlass, wasStirredEnough, overshotMeltingPoint;
    public float currentTemperature = 25f, recordedTemperature, heatingRate, meltingPoint = 80f, plateauDuration;
    [NonSerialized] public ThermalSimulation simulation = new MeltingSimulation();
    public bool paused;
    public PracticalDefinition definition;
    public event Action Changed;
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }
    void Update()
    {
        if (paused) return;
        simulation.Tick(Mathf.Min(Time.deltaTime, .1f));
        currentTemperature = simulation.thermometer;
        wasStirredEnough = simulation.StirCoverage >= .9f;
        overshotMeltingPoint = simulation.fastSeconds > 3f;
        Changed?.Invoke();
    }
    public void ResetBath() { simulation = (definition!=null?definition:PracticalDefinitions.Load()).CreateSimulation(); currentTemperature = simulation.thermometer; meltingPoint=simulation.settings.onsetTemperature; currentState = ExperimentState.NotStarted; }
    public void StartHeating() { simulation.lit = true; currentState = ExperimentState.Heating; }
    public void StopHeating() { simulation.lit = false; }
    public void RecordReading() { recordedTemperature = simulation.thermometer; currentState = ExperimentState.Recorded; }
    public float GetPercentError() { return Mathf.Abs(recordedTemperature - meltingPoint) / meltingPoint * 100f; }
}
