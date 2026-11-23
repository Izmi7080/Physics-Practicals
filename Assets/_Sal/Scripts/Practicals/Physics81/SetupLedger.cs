using System;

namespace VirtualLab.Physics81
{
    public enum SetupStep
    {
        PowerBalance, BobOnPan, WeighBob, ReturnBob,
        EmptyCupOnPan, WeighEmptyCup, RemoveEmptyCup,
        TakeCylinder, DockCylinder, MeasureCup, CarryCylinder, ParkCylinder, TipCup, PourCup,
        FilledCupOnPan, WeighFilledCup, CupOnSlab,
        PlaceBathStand, PlaceTripod, PlaceGauze, PlaceLamp,
        TakeBeaker, DockBeaker, MeasureBath, CarryBeaker, BeakerOnTripod,
        FitBathProbe, SetBathDepth, LightLamp, PlaceCupStand, FitCupProbe,
        SetCupDepth, RecordInitial, RaiseCupProbe, WatchBoil, LowerBob, SoakBob, RecordBath,
        TransferBob, LowerCupProbe, FitLid, MixCup, RecordMixture, Review
    }

    public sealed class SetupLedger
    {
        public const float CupGoal = 30f, BathGoal = 150f;
        public const float CupMass = 2.24f, BobMass = 12.57f;
        public SetupStep Step { get; private set; }
        public float CylinderMl { get; private set; }
        public float CupMl { get; private set; }
        public float BathMl { get; private set; }
        public float SuppliedMl { get; private set; }
        public float EmptyReading { get; private set; }
        public float FullReading { get; private set; }
        public float BobReading { get; private set; }
        public bool TapOn { get; private set; }
        public bool LampOn { get; private set; }
        // A low flame warms the bath during setup without taking it to a boil off-screen.
        internal double BurnerOutput => Step < SetupStep.WatchBoil ? .07 : 1;
        public bool Guided { get; }
        public Physics81Conditions Conditions { get; }
        public float TransferProgress { get; private set; }
        public TemperatureCapture? InitialMark { get; private set; }
        public TemperatureCapture? BathMark { get; private set; }
        public TemperatureCapture? MixtureMark { get; private set; }
        public Physics81ThermalModel Thermal { get; private set; }
        public bool LowWaterStop { get; private set; }
        double hotSoakSeconds, mixtureObservationSeconds;
        public bool CanRecordUnclearPeak => mixtureObservationSeconds >= 35 && Thermal.MixtureReady;
        public double ClockRate => Step switch
        {
            SetupStep.WatchBoil when LampOn && !Thermal.Boiling => 9,
            SetupStep.SoakBob when LampOn && Thermal.SoakSeconds < Physics81ThermalModel.RequiredSoak => Physics81ThermalModel.HeatingClock,
            SetupStep.RecordMixture when mixtureObservationSeconds >= 3 && !Thermal.TurningDown => 12,
            SetupStep.RecordMixture => Thermal.MixtureCoolingClock,
            _ => 1
        };
        public float VisibleBathMl => Thermal?.BathKg > 0 ? (float)(Thermal.BathKg * 1000) : BathMl;
        public float VisibleCupMl => Thermal != null ? (float)(Thermal.CupKg * 1000) : CupMl;

        public SetupLedger(bool guided = true, Physics81Conditions conditions = null)
        {
            Guided = guided;
            Conditions = conditions ?? new Physics81Conditions();
        }

        public bool ActionReady => Step switch
        {
            SetupStep.RecordInitial => !Guided || Thermal.InitialReady,
            SetupStep.WatchBoil => Thermal.Boiling,
            SetupStep.SoakBob => !Guided || Thermal.HotReady,
            SetupStep.RecordMixture => !Guided || Thermal.PastPeak || CanRecordUnclearPeak,
            SetupStep.PourCup or SetupStep.TransferBob or SetupStep.Review => false,
            _ => true
        };

        public bool Measuring => Step is SetupStep.MeasureCup or SetupStep.MeasureBath;
        public float Goal => Step == SetupStep.MeasureCup ? CupGoal : BathGoal;
        public bool PortionReady
        {
            get
            {
                float volume = Step == SetupStep.MeasureCup ? CylinderMl : BathMl;
                return Guided ? Math.Abs(volume - Goal) <= (Goal == CupGoal ? 2f : 7f) :
                    volume >= (Goal == CupGoal ? 15f : 95f) && volume <= (Goal == CupGoal ? 60f : 230f);
            }
        }

        public void Tick(float seconds, float transferTarget, bool portionTipped)
        {
            if (seconds <= 0) return;
            if (Step == SetupStep.TransferBob)
            {
                TransferProgress = Math.Min(Math.Max(TransferProgress, transferTarget), TransferProgress + seconds * .85f);
                if (TransferProgress >= 1f) { LampOn = false; Next(); }
            }
            if (TapOn && Measuring)
            {
                float current = Goal == CupGoal ? CylinderMl : BathMl;
                float capacity = Goal == CupGoal ? 200f : 250f;
                float rate = Guided ? Math.Min(45f, 6f + Math.Abs(Goal - current) * 2f) : Goal == CupGoal ? 18f : 36f;
                float added = Math.Min(capacity - current, seconds * rate);
                if (Goal == CupGoal) CylinderMl += added; else BathMl += added;
                SuppliedMl += added;
            }
            if (portionTipped && Step == SetupStep.PourCup)
            {
                float amount = Math.Min(CylinderMl, seconds * 58f);
                CylinderMl -= amount;
                CupMl += amount;
                if (CylinderMl <= 0f) Next();
            }
        }

        public void EmptyVessel()
        {
            if (!Measuring) return;
            TapOn = false;
            if (Step == SetupStep.MeasureCup) CylinderMl = 0;
            if (Step == SetupStep.MeasureBath) BathMl = 0;
        }

        public void StopTap() => TapOn = false;

        // The scene validates geometry, pointer ownership and instrument stability.
        public bool Advance(float reading = 0f, double? enteredTemperature = null)
        {
            if (!ActionReady) return false;
            switch (Step)
            {
                case SetupStep.MeasureCup:
                case SetupStep.MeasureBath:
                    TapOn = !TapOn;
                    return true;
                case SetupStep.WeighEmptyCup: EmptyReading = reading; break;
                case SetupStep.WeighFilledCup: FullReading = reading; break;
                case SetupStep.WeighBob: BobReading = reading; break;
                case SetupStep.SetBathDepth: PrepareThermal(); break;
                case SetupStep.LightLamp: LampOn = true; break;
                case SetupStep.RecordInitial: InitialMark = new TemperatureCapture(Thermal.CupProbe, Thermal.Seconds, Thermal.InitialReady, enteredTemperature); break;
                case SetupStep.RecordBath:
                    BathMark = new TemperatureCapture(Thermal.BathProbe, Thermal.Seconds, Thermal.HotReady, enteredTemperature);
                    hotSoakSeconds = Thermal.SoakSeconds;
                    LampOn = false;
                    break;
                case SetupStep.RecordMixture:
                    if (double.IsNegativeInfinity(Thermal.PeakProbe)) return false;
                    MixtureMark = new TemperatureCapture(Thermal.PeakProbe, Thermal.PeakLabSeconds, Thermal.PastPeak, enteredTemperature);
                    break;
            }
            Next();
            return true;
        }

        public bool AcceptFilledVessel()
        {
            if (!Measuring || TapOn || !PortionReady) return false;
            Next();
            return true;
        }

        SetupStep? WeighingStart => Step switch
        {
            SetupStep.WeighBob or SetupStep.ReturnBob => SetupStep.BobOnPan,
            SetupStep.WeighEmptyCup or SetupStep.RemoveEmptyCup => SetupStep.EmptyCupOnPan,
            SetupStep.WeighFilledCup or SetupStep.CupOnSlab => SetupStep.FilledCupOnPan,
            _ => null
        };

        internal bool CanReweigh => WeighingStart.HasValue;

        internal int RestartWeighing()
        {
            if (WeighingStart is not { } start) return -1;
            Step = start;
            switch (start)
            {
                case SetupStep.BobOnPan: BobReading = 0; return 2;
                case SetupStep.EmptyCupOnPan: EmptyReading = 0; return 0;
                default: FullReading = 0; return 1;
            }
        }

        public void TickHeat(float seconds, ThermalInputs input, bool useNormalClock)
        {
            if (seconds <= 0 || Thermal == null || Step == SetupStep.Review) return;
            if (Step == SetupStep.RecordMixture) mixtureObservationSeconds += seconds;
            Thermal.Advance(seconds * (useNormalClock ? 1 : ClockRate), input);
            if (!LampOn || !Thermal.BathLow) return;
            LampOn = false;
            LowWaterStop = true;
            Step = SetupStep.Review;
        }

        void Next() => Step = (SetupStep)((int)Step + 1);

        internal void RecheckBathDepth()
        {
            if (Step == SetupStep.LightLamp) Step = SetupStep.SetBathDepth;
        }

        void PrepareThermal()
        {
            Thermal = new Physics81ThermalModel(CupMl / 1000, BobMass / 1000, Conditions.tap, Conditions.room,
                cupHeatCapacity: 2.9, airLossMultiplier: Conditions.airLoss, boilingPoint: Conditions.BoilingPoint);
            Thermal.FillBath(BathMl / 1000, Conditions.tap);
        }

        public Physics81Preparation CapturePreparation(float cupDepth, float bathDepth) => new()
        {
            cupMl = CupMl, bathMl = BathMl, suppliedMl = SuppliedMl,
            emptyMass = EmptyReading, filledMass = FullReading, bobMass = BobReading,
            cupDepth = cupDepth, bathDepth = bathDepth, initial = Physics81SavedReading.From(InitialMark),
            cup = Thermal.Cup, near = Thermal.Near, bulk = Thermal.Bulk, probe = Thermal.CupProbe, seconds = Thermal.Seconds
        };

        public Physics81Evidence CaptureEvidence() => new()
        {
            bath = Physics81SavedReading.From(BathMark), mixture = Physics81SavedReading.From(MixtureMark),
            soakSeconds = BathMark.HasValue ? hotSoakSeconds : Thermal.SoakSeconds,
            airSeconds = Thermal.AirTransferSeconds, stirSeconds = Thermal.StirSeconds,
            spilledMl = Thermal.SpilledKg * 1000, lowWater = LowWaterStop, mixtureWasFalling = Thermal.PastPeak
        };

        public static SetupLedger Restore(Physics81Trial trial)
        {
            var data = new SetupLedger(trial.guided, trial.conditions);
            var prep = trial.preparation;
            if (prep == null) return data;
            data.Step = trial.finished ? SetupStep.Review : SetupStep.LightLamp;
            data.CupMl = prep.cupMl;
            data.BathMl = prep.bathMl;
            data.SuppliedMl = prep.suppliedMl;
            data.EmptyReading = prep.emptyMass;
            data.FullReading = prep.filledMass;
            data.BobReading = prep.bobMass;
            data.PrepareThermal();
            if (trial.finished && trial.evidence != null)
            {
                data.InitialMark = prep.initial?.ToCapture();
                data.BathMark = trial.evidence.bath?.ToCapture();
                data.MixtureMark = trial.evidence.mixture?.ToCapture();
                data.LowWaterStop = trial.evidence.lowWater;
                data.Thermal.RestoreCup(prep);
            }
            return data;
        }
    }
}
