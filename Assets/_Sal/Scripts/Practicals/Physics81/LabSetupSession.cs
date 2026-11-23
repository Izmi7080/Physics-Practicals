using System;
using System.Globalization;
using UnityEngine;

namespace VirtualLab.Physics81
{
    [Serializable]
    public sealed class SetupVessel
    {
        public Transform Body, Liquid;
        public float Capacity, FillHeight, Bottom;
        public Vector2 Centre { get; private set; }
        VesselLiquidMesh fitted;
        public float Surface(float volume) => fitted == null ? Bottom + FillHeight * Mathf.Clamp01(volume / Capacity) : fitted.Surface(volume);
        public Vector3 SurfaceCentre(float volume) => Body.TransformPoint(new Vector3(Centre.x, Surface(volume), Centre.y));
        public void Fit(float bottom, float max, Vector2 centre, float bottomRadius, float topRadius)
        {
            Bottom = bottom; FillHeight = max - bottom; Centre = centre;
            fitted = new VesselLiquidMesh(this, bottomRadius, topRadius);
        }
        public void Draw(float volume)
        {
            Liquid.gameObject.SetActive(volume > .025f);
            if (volume > .025f) fitted?.Draw(volume);
        }
    }

    [Serializable]
    public sealed class SetupFixtures
    {
        public LabWalkRig Rig;
        public LabBeamBalance Balance;
        public SetupVessel Jug, Cylinder, Cup, Bath;
        public Transform Table, Sink, Tap, SinkView, BenchView, BalanceView, MeasureView, CupView, BathView;
        public Transform JugRest, JugSink, JugCarry, JugPour, CylinderRest, CupRest, CupOnPan, CupLid, Lid;
        public Transform CupProbe, CupProbeSlot, Stirrer, StirrerSlot, BathProbe, BathProbeSlot, BeakerRest, BeakerTripod;
        public Transform Bob, BobRest, BobOnPan, Stream;
    }

    public sealed partial class LabSetupSession : MonoBehaviour
    {
        [SerializeField] SetupFixtures lab;
        public SetupLedger Data { get; private set; } = new();
        public bool Started { get; private set; }
        public SetupStep Step => Data.Step;
        public LabWalkRig Rig => lab.Rig;
        public LabBeamBalance Balance => lab.Balance;
        public bool Weighing => Step is SetupStep.WeighBob or SetupStep.WeighEmptyCup or SetupStep.WeighFilledCup;
        public bool AdjustingDepth => Step is SetupStep.SetCupDepth or SetupStep.SetBathDepth;
        bool AtSinkStep => Step is SetupStep.DockCylinder or SetupStep.MeasureCup or SetupStep.CarryCylinder
            or SetupStep.DockBeaker or SetupStep.MeasureBath or SetupStep.CarryBeaker;
        public bool IsAtSink => Near(lab.Sink);
        public bool IsAtTable => Near(lab.Table);
        public bool Paused => userPaused || pageOpen;
        bool AtTaskStation => AtSinkStep ? IsAtSink : IsAtTable;
        public bool CanInteract => Started && !Paused && AtTaskStation;
        public float DepthMillimetres => depth * 1000f;
        public bool CanInspect => CanInteract && !PlacementBusy && Step != SetupStep.Review;
        public bool CanMix => CanInteract && Step is (SetupStep.MixCup or SetupStep.RecordMixture);
        public bool CanTransfer => CanInteract && Step == SetupStep.TransferBob && !Rig.IsTransitioning;
        public bool RecordingTemperature => Step is SetupStep.RecordInitial or SetupStep.RecordBath or SetupStep.RecordMixture;
        public bool ShowsBathScale => Step is SetupStep.WatchBoil or SetupStep.SoakBob or SetupStep.RecordBath;
        public double LiveTemperature => ShowsBathScale ? Data.Thermal.BathProbe : Data.Thermal.CupProbe;
        public bool CanTare => CanInteract && Balance.Powered && Balance.LoadGrams == 0 && CheckingScaleZero;
        public bool CanReweigh => CanInteract && Balance.Powered && Balance.LoadGrams > 0 &&
            Data.CanReweigh && !Rig.IsTransitioning && !PlacementBusy;
        bool CheckingScaleZero => Step is SetupStep.BobOnPan or SetupStep.EmptyCupOnPan or SetupStep.FilledCupOnPan;
        public bool CanDrain => CanInteract && Data.Measuring;
        public bool AtCupDepthView => Rig.IsViewing(cupDepthView);
        public bool CanUseFilled => CanInteract && Data.Measuring && !Data.TapOn && Data.PortionReady;
        float FillVolume => Step == SetupStep.MeasureCup ? Data.CylinderMl : Data.BathMl;
        public bool DepthOkay => GetDepthIssue(Step == SetupStep.SetBathDepth).Length == 0;
        string GetDepthIssue(bool bath)
        {
            var position = instruments.ProbeDepth(bath, bath ? Data.VisibleBathMl : Data.VisibleCupMl);
            if (position.clearance <= .001f) return "The bulb is touching the bottom. Raise it slightly.";
            if (position.clearance < .004f) return "Raise the bulb clear of the bottom.";
            if (position.cover < .001f || position.wet < .99f) return "Lower the whole glass bulb below the water line.";
            if (bath && instruments.ProbeDepth(true, Data.VisibleBathMl - Physics81ThermalModel.SoakWaterMarginMl).cover < .001f)
                return "Lower the bulb a little more so it stays covered while the bath boils.";
            if (!bath && Data.Guided && (position.clearance < .006f || position.clearance > .016f))
                return "Bring the bottom of the bulb near the 10 mm mark.";
            return "";
        }

        public bool PrimaryEnabled => CanRetryLowBath || !IsPlacement && ActionEnabled;
        bool CanRetryLowBath => Started && !Paused && Step == SetupStep.Review && Data.LowWaterStop && Trial?.preparation != null;
        bool ActionEnabled => CanInteract && !Rig.IsTransitioning && Data.ActionReady && (Step switch
        {
            SetupStep.WeighBob or SetupStep.WeighEmptyCup or SetupStep.WeighFilledCup => Balance.Settled && Balance.Reading > 0,
            SetupStep.SetCupDepth or SetupStep.SetBathDepth => DepthOkay,
            SetupStep.WatchBoil => instruments.ProbeDepth(true, Data.VisibleBathMl).wet >= .99f,
            SetupStep.SoakBob or SetupStep.RecordBath => instruments.BobImmersed && instruments.ProbeDepth(true, Data.VisibleBathMl).wet >= .99f,
            SetupStep.RecordInitial or SetupStep.RecordMixture => instruments.ProbeDepth(false, Data.VisibleCupMl).wet >= .99f,
            SetupStep.MixCup => mixedFor >= (Data.Guided ? 3f : .8f),
            _ => true
        });

        public string PrimaryTitle => Step switch
        {
            SetupStep.PowerBalance => "Turn scale on",
            SetupStep.WeighBob or SetupStep.WeighEmptyCup or SetupStep.WeighFilledCup => "Record mass",
            SetupStep.MeasureCup or SetupStep.MeasureBath => Data.TapOn ? "Turn water off" : "Turn water on",
            SetupStep.SetCupDepth or SetupStep.SetBathDepth => "Confirm depth",
            SetupStep.RecordInitial or SetupStep.RecordBath or SetupStep.RecordMixture => "Record temperature",
            SetupStep.LightLamp => "Light burner",
            SetupStep.WatchBoil => "Water is boiling",
            SetupStep.SoakBob => "Read bath temperature",
            SetupStep.MixCup => "Watch the reading",
            SetupStep.Review when Data.LowWaterStop => "Retry heating",
            _ => ""
        };

        public string Cue => Step switch
        {
            SetupStep.PowerBalance => "Switch on the scale and check the empty display.",
            SetupStep.BobOnPan => Data.Guided ? "Check for 0.00 g. Use Tare if needed, then drag the bob onto the platform." : "Drag the brass bob onto the scale platform.",
            SetupStep.WeighBob => "Wait for the display to settle, then record the bob’s mass.",
            SetupStep.ReturnBob => "Return the weighed bob to its rest.",
            SetupStep.EmptyCupOnPan => Data.Guided ? "Check the empty scale. Tap TARE if needed, then drag the empty cup onto it." : "Drag the empty polystyrene cup onto the scale.",
            SetupStep.WeighEmptyCup => "Record the settled mass of the empty cup.",
            SetupStep.RemoveEmptyCup => "Move the empty cup to the mixing station.",
            SetupStep.TakeCylinder => "Pick up the measuring cylinder and walk to the main tap.",
            SetupStep.DockCylinder => "Drag the cylinder from your hand into the basin below the tap.",
            SetupStep.MeasureCup => "Fill near the 30 mL line. Stop the tap, then tap Use this amount or empty and refill.",
            SetupStep.CarryCylinder => "Pick up the filled cylinder and walk back to the table.",
            SetupStep.ParkCylinder => "Put the cylinder on the bench beside the cup.",
            SetupStep.TipCup => "Drag the cylinder above the cup and release to pour.",
            SetupStep.PourCup => "Watch the measured water flow into the cup.",
            SetupStep.FilledCupOnPan => Data.Guided ? "Check for 0.00 g again. Tap TARE if needed, then put the filled cup on the scale." : "Put the cup of water back on the scale.",
            SetupStep.WeighFilledCup => "Record the settled mass of the cup and water together.",
            SetupStep.CupOnSlab => "Return the filled cup to the mixing station.",
            SetupStep.PlaceBathStand => "Place the clamp stand at the heating station.",
            SetupStep.PlaceTripod => "Set the tripod beside the clamp stand.",
            SetupStep.PlaceGauze => "Set the wire gauze flat on the tripod.",
            SetupStep.PlaceLamp => "Slide the burner under the tripod.",
            SetupStep.TakeBeaker => "Pick up the beaker and take it to the main tap.",
            SetupStep.DockBeaker => "Drag the beaker below the spout.",
            SetupStep.MeasureBath => "Fill near the 150 mL line. Stop the tap, then tap Use this amount or empty and refill.",
            SetupStep.CarryBeaker => "Pick up the filled beaker and walk back to the heating station.",
            SetupStep.BeakerOnTripod => "Set the filled beaker on the wire gauze.",
            SetupStep.FitBathProbe => "Drag the bath thermometer into the stand clamp.",
            SetupStep.SetBathDepth => "Slide the thermometer until its bulb is submerged and clear of the glass.",
            SetupStep.LightLamp => "Light the burner on low while you set up the cup.",
            SetupStep.PlaceCupStand => "Place the other clamp stand next to the cup.",
            SetupStep.FitCupProbe => "Drag the cup thermometer to the clamp above the open cup.",
            SetupStep.SetCupDepth => "Slide the bulb until its orange marker is near 10 mm. Use Side view or Look into cup to check the depth.",
            SetupStep.RecordInitial => Data.Guided ? "Wait for the red column to settle. Read its top. Each small tick is 1 °C." : "Read and enter the initial water temperature.",
            SetupStep.RaiseCupProbe => "Drag the thermometer upward to clear the cup opening.",
            SetupStep.WatchBoil => "The heat is now higher. Watch the red column rise and wait for continuous bubbles.",
            SetupStep.LowerBob => "Drag the brass bob from the pad on the left to just above the beaker.",
            SetupStep.SoakBob => "Keep the bob submerged for ten lab minutes. Use Look at thermometer to check that the bath reading stays steady.",
            SetupStep.RecordBath => Data.Guided ? "Read the top of the red column at eye level. Each small tick is 1 °C. Enter the bath temperature." :
                "Read and enter the steady bath temperature. Use it as the hot bob’s starting temperature.",
            SetupStep.TransferBob => "Grab the thread above the beaker. Lift, drag toward the cup, then lower gently.",
            SetupStep.LowerCupProbe => "Drag the thermometer down to its earlier mark.",
            SetupStep.FitLid => "Drag the lid onto the cup, aligning its hole with the thermometer.",
            SetupStep.MixCup => "Gently move the cup side to side to mix the water.",
            SetupStep.RecordMixture => "Watch the column rise and then start to fall. Enter the highest temperature you saw.",
            _ => Data.LowWaterStop ? "The bath ran low and the burner switched off. Retry heating with a fresh bath." :
                "Procedure complete. Review your measurements and calculations."
        };

        public string Context
        {
            get
            {
                if (!string.IsNullOrEmpty(RecoveryNote)) return RecoveryNote;
                if (!string.IsNullOrEmpty(readingError)) return readingError;
                if (CheckingScaleZero && Data.Guided && Balance.Settled && Balance.Reading != 0)
                    return "Empty scale is above zero · tap TARE on the scale";
                if (Weighing && Data.Guided && !Balance.Settled) return "Let the scale settle";
                if (AdjustingDepth)
                {
                    string issue = GetDepthIssue(Step == SetupStep.SetBathDepth);
                    return issue.Length == 0 ? "Bulb fully immersed and clear of the bottom" : issue;
                }
                if (Data.Measuring && Data.Guided && !Data.TapOn && FillVolume > .05f && !Data.PortionReady)
                    return FillVolume < Data.Goal ? "Below the line. Run the tap a little longer." : "Above the line. Empty the vessel and try again.";
                if (Step == SetupStep.WatchBoil) return Data.ActionReady ? "Continuous bubbles · bath is boiling" : $"Bath warming · {(watchingBath ? Data.ClockRate : 1):F0}× lab time";
                if (Step == SetupStep.SoakBob)
                {
                    int left = Mathf.CeilToInt((float)Math.Max(0, Physics81ThermalModel.RequiredSoak - Data.Thermal.SoakSeconds));
                    if (!instruments.BobImmersed) return "Lowering the bob…";
                    if (left == 0) return "Soak complete · check that the column is steady";
                    return $"Soak · {left / 60:00}:{left % 60:00} remaining · {Data.ClockRate:F0}× lab time";
                }
                if (Step == SetupStep.RecordInitial && Data.Guided) return Data.ActionReady ? "The reading has settled" : "Let the probe settle…";
                if (Step == SetupStep.TransferBob && Data.Thermal.SpilledKg > 0) return "Water spilled from the cup";
                if (Step == SetupStep.RecordMixture && Data.Guided)
                    return Data.Thermal.PastPeak ? "Cooling · enter the highest reading you saw" :
                        Data.CanRecordUnclearPeak ? "No clear drop yet. Enter the highest reading you saw." :
                        Data.Thermal.MixtureReady ? "Remember the highest reading. Watch for a small drop." : "Watch the red column as it rises";
                return "";
            }
        }

        public string Direction
        {
            get
            {
                if (Rig.IsInspecting || Paused || CanInteract) return "";
                Vector3 delta = Flat((AtSinkStep ? lab.Sink : lab.Table).position - Rig.transform.position);
                float angle = Vector3.SignedAngle(Rig.transform.forward, delta, Vector3.up);
                return $"{(AtSinkStep ? "Main sink" : "Centre table")} · {delta.magnitude:F1} m · {(Mathf.Abs(angle) > 150 ? "behind" : angle < -25 ? "left" : angle > 25 ? "right" : "ahead")}";
            }
        }

        bool userPaused, pageOpen, foreground = true, touchingCupBottom, watchingBath;
        float transferTarget, downwardTravel, downwardTime, mixedFor, mixMotion;
        float depth = .055f;
        string readingError = "";
        Transform pendingInspection;
        Physics81InstrumentRig instruments;
        Physics81Audio sounds;
        public void Configure(SetupFixtures fixtures) => lab = fixtures;
        public void ArrangeBench() => Physics81BenchLayout.Arrange(lab);
        public void PlaySound(string cue) => sounds?.Cue(cue);
        public void PlayTyping(bool active) => sounds?.Typing(active && !Paused && foreground);

        void Awake()
        {
            foreach (var camera in GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            ArrangeBench();
            lab.Balance.InstallDigitalFace(lab.CupOnPan, lab.BobOnPan);
            BuildProcedureFixtures();
            BuildCupLid();
            BuildVesselVisuals();
            instruments = new Physics81InstrumentRig(lab);
            sounds = new Physics81Audio(gameObject);
            InitializeTrialStorage();
            RefreshSuspension();
        }

        void Update()
        {
            if (draftWriteAt > 0 && Time.unscaledTime >= draftWriteAt) SaveProgress();
            bool active = Started && !Paused && foreground;
            float dt = active ? Mathf.Min(.07f, Time.deltaTime) : 0f;
            if (!AtTaskStation) ReleaseHands();
            if (Data.TapOn && !IsAtSink) Data.StopTap();
            var previous = Step;
            UpdatePourPose(dt);
            Data.Tick(dt, AtTaskStation ? transferTarget : Data.TransferProgress, cylinderTip >= .99f);
            bool mixing = CanMix && mixMotion > 0;
            if (mixing) mixedFor += dt;
            mixMotion = Mathf.Max(0, mixMotion - dt);
            instruments.MoveApparatus(Data, mixing, dt);
            if (Step == SetupStep.WatchBoil && Rig.IsViewing(bathReadingView)) watchingBath = true;
            bool normalClock = Step == SetupStep.WatchBoil && !watchingBath ||
                Step == SetupStep.SoakBob && !instruments.BobImmersed;
            float heatDt = active && Step is (SetupStep.WatchBoil or SetupStep.RecordMixture)
                ? Mathf.Min(.07f, Time.unscaledDeltaTime) : dt;
            Data.TickHeat(heatDt, instruments.HeatInputs(Data, mixing), normalClock);
            if (Step != previous) StepChanged();
            Balance.Animate(dt);
            instruments.DrawInstruments(Data, dt);
            DrawVessels();
            UpdatePlacement(dt);
            if (pendingInspection != null && CanInspect)
            {
                Rig.Inspect(pendingInspection);
                pendingInspection = null;
            }
            sounds.Update(Data, Started && !Paused && foreground, Step == SetupStep.PourCup && cylinderTip >= .99f, !Started && foreground);
        }

        public void Primary()
        {
            if (CanRetryLowBath) { RetryHeating(); return; }
            if (PrimaryEnabled && !RecordingTemperature) AdvanceAction();
        }

        public void RecordTemperature(string input)
        {
            if (!RecordingTemperature || !PrimaryEnabled) return;
            if (!Physics81Calculation.TryRead(input, out double value) || value < 0 || value > 110)
            {
                readingError = "Enter the temperature as a number between 0 and 110.";
                PlaySound("Wrong");
                return;
            }
            if (Data.Guided && Step != SetupStep.RecordMixture && Math.Abs(value - LiveTemperature) > .55)
            {
                readingError = "Check the top of the red column and try again.";
                PlaySound("Wrong");
                return;
            }
            int index = Step == SetupStep.RecordInitial ? 3 : Step == SetupStep.RecordBath ? 4 : 5;
            Trial.draft.readings[index] = value.ToString("0.##", CultureInfo.InvariantCulture);
            AdvanceAction(value);
        }

        public void UseFilledVessel()
        {
            if (!CanUseFilled || !Data.AcceptFilledVessel()) return;
            PlaySound("Correct");
            StepChanged();
        }

        public void DrainVessel() { if (CanDrain) Data.EmptyVessel(); }
        public void TareScale() { if (CanTare) Balance.Tare(); }

        public void TareAndReweigh()
        {
            if (!CanReweigh) return;
            int entry = Data.RestartWeighing();
            if (entry < 0) return;
            if (Step == SetupStep.BobOnPan) Place(lab.Bob, lab.BobRest);
            else Place(lab.Cup.Body, lab.CupRest);
            Balance.Load(0);
            Trial.draft.readings[entry] = "";
            StepChanged();
            Balance.Tare();
            RecoveryNote = "Scale tared. Put the item back on the scale and record again.";
            SaveProgress();
        }

        void AdvanceAction(double? enteredTemperature = null)
        {
            if (!ActionEnabled) return;
            var step = Step;
            bool mass = Weighing;
            if (!Data.Advance(mass ? Balance.Reading : 0, enteredTemperature)) return;
            if (Step == step) return;
            if (PlacementFor(step, out var item, out var seat) && step is not (SetupStep.LowerBob or SetupStep.TipCup))
                Place(item, seat);
            switch (step)
            {
                case SetupStep.PowerBalance: Balance.SwitchOn(); break;
                case SetupStep.BobOnPan: Balance.Load(SetupLedger.BobMass); break;
                case SetupStep.EmptyCupOnPan: Balance.Load(SetupLedger.CupMass); break;
                case SetupStep.FilledCupOnPan: Balance.Load(SetupLedger.CupMass + Data.CupMl); break;
                case SetupStep.ReturnBob:
                case SetupStep.RemoveEmptyCup:
                case SetupStep.CupOnSlab: Balance.Load(0); break;
                case SetupStep.WeighBob: SaveMass(2); break;
                case SetupStep.WeighEmptyCup: SaveMass(0); break;
                case SetupStep.WeighFilledCup: SaveMass(1); break;
                case SetupStep.FitBathProbe:
                case SetupStep.FitCupProbe:
                    depth = .055f;
                    DrawDepth();
                    break;
                case SetupStep.SetCupDepth:
                    cupDepth = depth;
                    cupProbeWetPose.localPosition = Vector3.up * depth;
                    break;
                case SetupStep.SetBathDepth:
                    Trial.preparation = Data.CapturePreparation(.010f, depth);
                    SaveProgress();
                    break;
                case SetupStep.RecordInitial:
                    Trial.preparation.cupDepth = cupDepth;
                    Trial.preparation.initial = Physics81SavedReading.From(Data.InitialMark);
                    SaveProgress();
                    break;
                case SetupStep.LightLamp:
                    Trial.heatingStarted = true;
                    SaveProgress();
                    break;
                case SetupStep.LowerBob: instruments.LowerBob(); break;
            }
            if (mass || step is (SetupStep.RecordInitial or SetupStep.RecordBath or SetupStep.RecordMixture)) PlaySound("Correct");
            StepChanged();
        }

        void SaveMass(int index)
        {
            Trial.draft.readings[index] = Balance.Reading.ToString("F2", CultureInfo.InvariantCulture);
            SaveProgress();
        }

        public void SetDepth(float millimetres)
        {
            if (!AdjustingDepth || !CanInteract) return;
            depth = Mathf.Clamp(millimetres, 0f, 85f) / 1000f;
            DrawDepth();
            if (Step != SetupStep.SetCupDepth) return;
            bool touching = instruments.ProbeDepth(false, Data.VisibleCupMl).clearance <= .001f;
            if (touching && !touchingCupBottom) PlaySound("Wrong");
            touchingCupBottom = touching;
        }

        public void ToggleCupDepthView()
        {
            if (CanInspect && Step == SetupStep.SetCupDepth)
                Rig.Inspect(AtCupDepthView ? lab.CupView : cupDepthView);
        }

        void DrawDepth()
        {
            Transform probe = Step == SetupStep.SetCupDepth ? lab.CupProbe : lab.BathProbe;
            probe.localPosition = Vector3.up * depth;
        }

        public void Pause()
        {
            userPaused = !userPaused;
            RefreshSuspension();
        }
        public void SetPageOpen(bool value) { pageOpen = value; RefreshSuspension(); }

        void RefreshSuspension()
        {
            if (Paused || !Started)
            {
                ReleaseHands();
                if (Started) sounds?.StopLoops();
            }
            Rig.Suspend(Paused || !Started);
        }

        void ReleaseHands()
        {
            CancelPlacement();
            mixMotion = 0;
            transferTarget = Data.TransferProgress;
        }

        public void Inspect()
        {
            if (Paused || PlacementBusy) return;
            if (Rig.IsInspecting) { Rig.LeaveInspection(); return; }
            if (CanInspect) Rig.Inspect(CurrentView());
        }

        public void InspectThermometer()
        {
            if (CanInspect && (RecordingTemperature || ShowsBathScale || Step == SetupStep.MixCup))
                Rig.Inspect(ThermometerView(ShowsBathScale));
        }

        public bool TryGetActionPosition(out Vector3 point)
        {
            Transform anchor = Step switch
            {
                SetupStep.PowerBalance => Balance.PowerButton,
                SetupStep.MeasureCup or SetupStep.MeasureBath => lab.Tap,
                SetupStep.LightLamp => instruments.FlameAnchor,
                _ => null
            };
            point = anchor == null ? Vector3.zero : anchor.position;
            return anchor != null;
        }

        Transform CurrentView() => Step switch
        {
            SetupStep.DockCylinder or SetupStep.CarryCylinder => SinkView(lab.Cylinder, SetupLedger.CupGoal, true),
            SetupStep.DockBeaker or SetupStep.CarryBeaker => SinkView(lab.Bath, SetupLedger.BathGoal, true),
            SetupStep.MeasureCup => SinkView(lab.Cylinder, SetupLedger.CupGoal, false),
            SetupStep.MeasureBath => SinkView(lab.Bath, SetupLedger.BathGoal, false),
            SetupStep.PowerBalance or SetupStep.BobOnPan or SetupStep.WeighBob or SetupStep.ReturnBob or SetupStep.WeighEmptyCup or SetupStep.WeighFilledCup => lab.BalanceView,
            SetupStep.EmptyCupOnPan or SetupStep.FilledCupOnPan or SetupStep.RemoveEmptyCup or SetupStep.CupOnSlab => cupPlacementView,
            SetupStep.PlaceBathStand or SetupStep.PlaceTripod or SetupStep.PlaceGauze or SetupStep.PlaceLamp or SetupStep.FitBathProbe => bathAssemblyView,
            SetupStep.PlaceCupStand or SetupStep.FitCupProbe or SetupStep.FitLid or SetupStep.RaiseCupProbe or SetupStep.TipCup or SetupStep.PourCup => cupSetupView,
            SetupStep.SetCupDepth => cupDepthView,
            SetupStep.LowerCupProbe or SetupStep.MixCup => lab.CupView,
            SetupStep.RecordInitial or SetupStep.RecordMixture => ThermometerView(false),
            SetupStep.WatchBoil or SetupStep.RecordBath => ThermometerView(true),
            SetupStep.SetBathDepth or SetupStep.SoakBob => lab.BathView,
            SetupStep.LightLamp => instruments.LampView,
            SetupStep.LowerBob => bobLoadingView,
            SetupStep.TransferBob => instruments.TransferView,
            _ => lab.BenchView
        };

        void StepChanged()
        {
            pendingInspection = null;
            readingError = RecoveryNote = "";
            touchingCupBottom = false;
            watchingBath = false;
            if (CheckingScaleZero) Balance.BeginWeighing(Data.Conditions.ScaleOffset((int)Step));
            RefreshApparatus();
            if (Step == SetupStep.Review)
            {
                Trial.finished = true;
                Trial.evidence = Data.CaptureEvidence();
                SaveProgress();
            }
            if (Step is SetupStep.DockCylinder or SetupStep.ParkCylinder or SetupStep.DockBeaker or SetupStep.BeakerOnTripod or SetupStep.Review)
                Rig.LeaveInspection();
            if (Step == SetupStep.Review) return;
            pendingInspection = CurrentView();
        }

        void Stream(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            lab.Stream.gameObject.SetActive(delta.sqrMagnitude > .00001f);
            lab.Stream.SetPositionAndRotation((from + to) * .5f, Quaternion.FromToRotation(Vector3.up, delta));
            lab.Stream.localScale = new Vector3(.0038f, delta.magnitude * .5f, .0038f);
        }

        static void Place(Transform item, Transform anchor)
        { item.SetParent(anchor, false); item.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); }
        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);
        bool Near(Transform station) => Flat(station.position - Rig.transform.position).sqrMagnitude <= 1f;
        void OnApplicationFocus(bool focused) { foreground = focused; if (!focused) Interrupt(); }
        void OnApplicationPause(bool paused) { foreground = !paused; if (paused) Interrupt(); }
        void OnApplicationQuit() => SaveProgress();
        void OnDisable() { ReleaseHands(); Data.StopTap(); sounds?.Stop(); }
        void Interrupt()
        {
            sounds?.Stop();
            if (!Started) return;
            Data.StopTap();
            if (!pageOpen) userPaused = true;
            RefreshSuspension();
            SaveProgress();
        }
    }
}
