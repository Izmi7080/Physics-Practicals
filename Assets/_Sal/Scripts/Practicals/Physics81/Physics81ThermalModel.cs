using System;

namespace VirtualLab.Physics81
{
    public readonly struct ThermalInputs
    {
        public readonly bool LampOn, Stirring;
        public readonly double BobInBath, BobInCup, LidOpening, CupProbeWet, BathProbeWet, BurnerOutput;

        public ThermalInputs(bool lampOn, double bobInBath, double bobInCup, double lidOpening,
            bool stirring, double cupProbeWet, double bathProbeWet, double burnerOutput = 1)
        {
            LampOn = lampOn; BobInBath = bobInBath; BobInCup = bobInCup; LidOpening = lidOpening;
            Stirring = stirring; CupProbeWet = cupProbeWet; BathProbeWet = bathProbeWet;
            BurnerOutput = Math.Clamp(burnerOutput, 0, 1);
        }
    }

    public readonly struct TemperatureCapture
    {
        public const double Division = .1;
        public readonly double Celsius, LabSeconds;
        public readonly bool TechniqueReady;
        public readonly double? StudentEntry;

        public TemperatureCapture(double value, double labSeconds, bool ready, double? studentEntry = null)
        {
            Celsius = ScaleValue(value);
            LabSeconds = labSeconds;
            TechniqueReady = ready;
            StudentEntry = studentEntry;
        }

        public static double ScaleValue(double value) => Math.Round(value / Division, MidpointRounding.AwayFromZero) * Division;
    }

    // All masses are kg, temperatures °C, capacities J/K and time seconds.
    public sealed class Physics81ThermalModel
    {
        public const double WaterSpecificHeat = 4200, BrassSpecificHeat = 380;
        public const double LatentHeat = 2_260_000, RequiredSoak = 600, HeatingClock = 32;
        public const double BathReserve = .055;
        public const float SoakWaterMarginMl = 18f;
        const double StepSize = .05, NearFraction = .28, LampWatts = 205, SimmerWatts = 84;
        const double ClosedWaterLoss = .018, CupWallLoss = .030;
        readonly double ambient, bobCapacity, cupCapacity, airLossMultiplier;
        readonly bool roomLosses;
        double bath, bob, cup, near, bulk, cupProbe, bathProbe, remaining;
        double cupContact, peak = double.NegativeInfinity, previousProbe;
        bool bobTouchedBath, bathReachedBoil;

        public double CupKg { get; private set; }
        public double BathKg { get; private set; }
        public double Seconds { get; private set; }
        public double SoakSeconds { get; private set; }
        public double StirSeconds { get; private set; }
        public double AirTransferSeconds { get; private set; }
        public double? EntryBobCelsius { get; private set; }
        public double EvaporatedKg { get; private set; }
        public double SpilledKg { get; private set; }
        public double LampEnergy { get; private set; }
        public double RoomEnergy { get; private set; }
        public double VaporEnergy { get; private set; }
        public double SpillEnergy { get; private set; }
        public double AddedBathEnergy { get; private set; }
        public double Bath => bath;
        public double Bob => bob;
        public double Cup => cup;
        public double Near => near;
        public double Bulk => bulk;
        public double CupProbe => cupProbe;
        public double BathProbe => bathProbe;
        public double PeakProbe => peak;
        public double PeakLabSeconds { get; private set; }
        public double BoilingPoint { get; }
        public bool Boiling => bath >= BoilingPoint - .05 && BathKg > BathReserve;
        public bool BathLow => BathKg > 0 && BathKg <= BathReserve;
        public bool InitialReady => Math.Abs(cupProbe - bulk) <= .14;
        public bool HotReady => SoakSeconds >= RequiredSoak && Math.Abs(bathProbe - bath) <= .35;
        double VisibleDrop => Math.Clamp((peak - ambient) * .5, .05, .20);
        public bool PastPeak => cupContact >= 5 && peak - cupProbe >= VisibleDrop;
        internal bool TurningDown => double.IsFinite(peak) && Seconds - PeakLabSeconds >= .5;
        internal double MixtureCoolingClock
        {
            get
            {
                double excess = peak - ambient;
                if (excess <= VisibleDrop || !MixtureReady || !TurningDown) return 1;
                // Use the same heat-loss constants to make a small fall visible after the peak.
                double capacity = NearCapacity + BulkCapacity + cupCapacity + bobCapacity;
                double labSeconds = -Math.Log(1 - VisibleDrop / excess) * capacity / (ClosedWaterLoss + CupWallLoss);
                return Math.Clamp(labSeconds / 10.5, 1, PastPeak ? 24 : 96);
            }
        }
        public bool MixtureReady => cupContact >= 5 && (PastPeak ||
            Math.Abs(bob - bulk) <= 1.3 && Math.Abs(near - bulk) <= .3 &&
            Math.Abs(cupProbe - bulk) <= .24 && Math.Abs(cupProbe - previousProbe) / StepSize <= .09);

        double NearCapacity => CupKg * NearFraction * WaterSpecificHeat;
        double BulkCapacity => CupKg * (1 - NearFraction) * WaterSpecificHeat;
        public double StoredEnergy => BathKg * WaterSpecificHeat * bath + bobCapacity * bob + cupCapacity * cup
            + NearCapacity * near + BulkCapacity * bulk;

        public Physics81ThermalModel(double cupKg, double bobKg, double waterTemperature = 23, double ambientTemperature = 25,
            double? bobTemperature = null, bool roomLosses = true, double cupHeatCapacity = 6.5, double airLossMultiplier = 1,
            double boilingPoint = 100)
        {
            CupKg = cupKg;
            ambient = ambientTemperature;
            near = bulk = bath = waterTemperature;
            cup = cupProbe = bathProbe = ambient;
            bob = bobTemperature ?? ambient;
            bobCapacity = bobKg * BrassSpecificHeat;
            cupCapacity = cupHeatCapacity;
            this.roomLosses = roomLosses;
            this.airLossMultiplier = airLossMultiplier;
            BoilingPoint = boilingPoint;
        }

        internal void RestoreCup(Physics81Preparation preparation)
        {
            cup = preparation.cup;
            near = preparation.near;
            bulk = preparation.bulk;
            cupProbe = preparation.probe;
            Seconds = preparation.seconds;
        }

        public void FillBath(double kg, double celsius = 23)
        {
            if (BathKg > 0 || kg <= 0) return;
            BathKg = kg;
            bath = celsius;
            AddedBathEnergy = kg * WaterSpecificHeat * celsius;
        }

        public void Advance(double labSeconds, ThermalInputs input)
        {
            remaining += labSeconds;
            while (remaining + 1e-9 >= StepSize)
            {
                remaining -= StepSize;
                Integrate(input);
            }
        }

        public double Spill(double kg)
        {
            // Water leaves both modeled regions in proportion to their volume.
            double amount = Math.Min(Math.Max(0, kg), Math.Max(0, CupKg - .005));
            SpillEnergy += amount * WaterSpecificHeat * (NearFraction * near + (1 - NearFraction) * bulk);
            CupKg -= amount;
            SpilledKg += amount;
            return amount;
        }

        void Integrate(ThermalInputs input)
        {
            double inBath = Math.Clamp(input.BobInBath, 0, 1);
            double inCup = Math.Clamp(input.BobInCup, 0, 1 - inBath);
            double open = Math.Clamp(input.LidOpening, 0, 1);
            double air = 1 - inBath - inCup;
            double bathCapacity = BathKg * WaterSpecificHeat;
            Seconds += StepSize;

            if (inBath > 1e-4) bobTouchedBath = true;
            if (inCup > 0 && EntryBobCelsius == null) EntryBobCelsius = bob;
            if (bobTouchedBath && EntryBobCelsius == null && inBath <= 1e-4) AirTransferSeconds += StepSize;
            Exchange(ref bath, bathCapacity, ref bob, bobCapacity, 2.4 * inBath);
            Exchange(ref bob, bobCapacity, ref near, NearCapacity, 2.0 * inCup);
            Exchange(ref near, NearCapacity, ref bulk, BulkCapacity, input.Stirring ? 8.0 : .82);
            Exchange(ref bulk, BulkCapacity, ref cup, cupCapacity, .72);

            if (roomLosses)
            {
                Lose(ref bath, bathCapacity, .58);
                Lose(ref bob, bobCapacity, .045 * air * airLossMultiplier);
                Lose(ref bulk, BulkCapacity, ClosedWaterLoss + .055 * open);
                Lose(ref cup, cupCapacity, CupWallLoss);
            }

            if (input.LampOn && BathKg > BathReserve)
            {
                double inputEnergy = (bathReachedBoil ? SimmerWatts : LampWatts) * input.BurnerOutput * StepSize;
                LampEnergy += inputEnergy;
                double warming = Math.Min(inputEnergy, Math.Max(0, (BoilingPoint - bath) * bathCapacity));
                bath += warming / bathCapacity;
                double vapor = (inputEnergy - warming) / LatentHeat;
                BathKg -= vapor;
                EvaporatedKg += vapor;
                VaporEnergy += vapor * (LatentHeat + WaterSpecificHeat * bath);
                if (Boiling) bathReachedBoil = true;
            }

            if (input.LampOn && Boiling && inBath >= .98) SoakSeconds += StepSize;
            if (input.Stirring) StirSeconds += StepSize;
            if (inCup > 0) cupContact += StepSize;
            previousProbe = cupProbe;
            Probe(ref cupProbe, bulk, input.CupProbeWet);
            Probe(ref bathProbe, bath, input.BathProbeWet);
            if (cupContact > 0 && input.CupProbeWet > .95 && cupProbe > peak)
            {
                peak = cupProbe;
                PeakLabSeconds = Seconds;
            }
        }

        // The exact two-body exchange cannot overshoot and adds the same Q that it removes.
        static void Exchange(ref double a, double ca, ref double b, double cb, double conductance)
        {
            if (ca <= 0 || cb <= 0 || conductance <= 0) return;
            double inverse = 1 / ca + 1 / cb;
            double energy = (a - b) * (1 - Math.Exp(-conductance * inverse * StepSize)) / inverse;
            a -= energy / ca;
            b += energy / cb;
        }

        void Lose(ref double node, double capacity, double conductance)
        {
            if (capacity <= 0) return;
            double change = (node - ambient) * (1 - Math.Exp(-conductance / capacity * StepSize));
            node -= change;
            RoomEnergy += change * capacity;
        }

        void Probe(ref double probe, double liquid, double wet)
        {
            double target = ambient + (liquid - ambient) * wet;
            probe += (target - probe) * (1 - Math.Exp(-StepSize / (wet > .5 ? 2.3 : 7.0)));
        }
    }
}
