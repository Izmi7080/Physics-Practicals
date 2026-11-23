using System;
using System.Collections.Generic;
using System.Globalization;

namespace VirtualLab.Physics81
{
    [Serializable]
    public sealed class Physics81Conditions
    {
        public int seed;
        public bool variation;
        public double room = 25, tap = 23, airLoss = 1, pressureKPa;
        public double BoilingPoint
        {
            get
            {
                if (pressureKPa <= 0) return 100;
                // Invert IAPWS SR1-86(1992), equation 1. Pressure is kPa and temperature is kelvin.
                const double criticalTemperature = 647.096, criticalPressure = 22064;
                double low = 360, high = 385;
                for (int i = 0; i < 24; i++)
                {
                    double temperature = (low + high) * .5;
                    double tau = 1 - temperature / criticalTemperature;
                    double exponent = criticalTemperature / temperature * (-7.85951783 * tau
                        + 1.84408259 * Math.Pow(tau, 1.5) - 11.7866497 * Math.Pow(tau, 3)
                        + 22.6807411 * Math.Pow(tau, 3.5) - 15.9618719 * Math.Pow(tau, 4)
                        + 1.80122502 * Math.Pow(tau, 7.5));
                    if (criticalPressure * Math.Exp(exponent) < pressureKPa) low = temperature;
                    else high = temperature;
                }
                return (low + high) * .5 - 273.15;
            }
        }

        internal float ScaleOffset(int weighing)
        {
            var random = new Random(unchecked(seed ^ (weighing * 73856093)));
            return random.Next(101) / 100f;
        }

        public static Physics81Conditions Create(int seed, bool variation)
        {
            var conditions = new Physics81Conditions { seed = seed, variation = variation };
            var random = new Random(seed);
            conditions.pressureKPa = 95 + random.NextDouble() * 9;
            conditions.room = 23.5 + random.NextDouble() * 3;
            // The practical starts with a room-conditioned water supply.
            conditions.tap = conditions.room + (random.NextDouble() - .5) * 1.4;
            if (!variation) return conditions;
            conditions.airLoss += (random.NextDouble() - .5) * .08;
            return conditions;
        }
    }

    [Serializable]
    public sealed class Physics81SavedReading
    {
        public double value, seconds, entered;
        public bool ready, valid, hasEntry;

        public static Physics81SavedReading From(TemperatureCapture? capture) => capture.HasValue ? new Physics81SavedReading
        {
            value = capture.Value.Celsius, seconds = capture.Value.LabSeconds, ready = capture.Value.TechniqueReady, valid = true,
            entered = capture.Value.StudentEntry.GetValueOrDefault(), hasEntry = capture.Value.StudentEntry.HasValue
        } : null;

        public TemperatureCapture ToCapture() => new(value, seconds, ready, hasEntry ? entered : null);
    }

    [Serializable]
    public sealed class Physics81Preparation
    {
        public float jugMl, cupMl, bathMl, suppliedMl, emptyMass, filledMass, bobMass, cupDepth, bathDepth;
        public double cup, near, bulk, probe, seconds;
        public Physics81SavedReading initial;
    }

    [Serializable]
    public sealed class Physics81Evidence
    {
        public Physics81SavedReading bath, mixture;
        public double soakSeconds, airSeconds, stirSeconds, spilledMl;
        public bool lowWater, mixtureWasFalling;
        public bool Complete => bath != null && mixture != null;
    }

    [Serializable]
    public sealed class Physics81Draft
    {
        public string[] readings = new string[6];
        public string[] calculations = new string[7];
        public string aim = "", apparatus = "", method = "", variables = "";
        public string observations = "", conclusion = "", reflection = "";
        public bool submitted;
    }

    [Serializable]
    public sealed class Physics81Trial
    {
        public const int CurrentVersion = 2;
        public int version = CurrentVersion;
        public bool guided, heatingStarted, finished;
        public Physics81Conditions conditions = new();
        public Physics81Preparation preparation;
        public Physics81Evidence evidence;
        public Physics81Draft draft = new();
    }

    public readonly struct Physics81Calculation
    {
        public readonly double WaterGrams, WaterRise, BobDrop, JoulesPerGramKelvin;
        public double JoulesPerKilogramKelvin => JoulesPerGramKelvin * 1000;

        Physics81Calculation(double water, double rise, double drop, double result)
        { WaterGrams = water; WaterRise = rise; BobDrop = drop; JoulesPerGramKelvin = result; }

        public static bool TryRead(string text, out double value)
        {
            const NumberStyles style = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            return (double.TryParse(text, style, CultureInfo.InvariantCulture, out value) ||
                double.TryParse(text, style, CultureInfo.CurrentCulture, out value)) && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static bool TryCalculate(string[] entries, out Physics81Calculation result, out string error)
        {
            result = default;
            var values = new double[6];
            for (int i = 0; i < values.Length; i++)
            {
                if (TryRead(entries[i], out values[i])) continue;
                error = "Enter all six readings as numbers. The units are already shown.";
                return false;
            }

            double water = values[1] - values[0], rise = values[5] - values[3], drop = values[4] - values[5];
            if (values[0] <= 0 || values[2] <= 0 || water <= 0)
                error = "Masses must be positive, and the filled cup must weigh more than the empty cup.";
            else if (rise <= 0 || drop <= 0)
                error = "The mixture must be warmer than the initial cup and cooler than the hot bath. Check your entries or review the trial.";
            else
            {
                double specificHeat = water * (Physics81ThermalModel.WaterSpecificHeat / 1000) * rise / (values[2] * drop);
                if (specificHeat > 0 && !double.IsInfinity(specificHeat) && !double.IsNaN(specificHeat))
                {
                    result = new Physics81Calculation(water, rise, drop, specificHeat);
                    error = "";
                    return true;
                }
                error = "These entries do not give a finite positive result. Check the readings and units.";
            }
            return false;
        }

        public static double?[] Captured(SetupLedger data) => new double?[]
        {
            data.EmptyReading > 0 ? data.EmptyReading : null, data.FullReading > 0 ? data.FullReading : null,
            data.BobReading > 0 ? data.BobReading : null, data.InitialMark?.Celsius, data.BathMark?.Celsius, data.MixtureMark?.Celsius
        };

        public static int Matched(SetupLedger data, Physics81Draft draft)
        {
            var values = Captured(data);
            var studentEntries = new double?[] { null, null, null, data.InitialMark?.StudentEntry, data.BathMark?.StudentEntry, data.MixtureMark?.StudentEntry };
            int matches = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!values[i].HasValue) continue;
                double entry;
                if (studentEntries[i].HasValue) entry = studentEntries[i].Value;
                else if (!TryRead(draft.readings[i], out entry)) continue;
                if (Math.Abs(values[i].Value - entry) <= (i < 3 ? .005 : .55)) matches++;
            }
            return matches;
        }

        public static string Technique(Physics81Trial trial)
        {
            var e = trial.evidence;
            if (e == null) return "Technique feedback becomes available after the run.";
            var issues = new List<string>();
            int passed = 0, measured = 0;
            void Check(bool present, bool good, string advice)
            {
                if (!present) return;
                measured++;
                if (good) passed++; else issues.Add(advice);
            }

            Check(trial.preparation?.initial != null, trial.preparation?.initial?.ready == true, "The initial cup reading was taken before the probe settled.");
            Check(e.bath != null, e.bath?.ready == true, e.soakSeconds < Physics81ThermalModel.RequiredSoak ?
                $"The bob soaked at the boil for {e.soakSeconds / 60:F1} of the required 10 minutes before the hot reading." :
                "The bath probe was still settling when you recorded the hot reading.");
            Check(e.Complete, e.airSeconds <= 5, $"The bob spent {e.airSeconds:F1} s out of the bath before reaching the cup, which allowed it to cool.");
            Check(e.Complete, e.spilledMl <= .05, $"The cup lost {e.spilledMl:F1} mL during transfer. The recorded mass still describes the water before the spill.");
            Check(e.Complete, e.stirSeconds >= 3, $"You stirred for {e.stirSeconds:F1} s before recording. Brief stirring can leave warmer and cooler regions in the cup.");
            Check(e.mixture != null, e.mixture?.ready == true, e.mixtureWasFalling ?
                "This saved mixture reading was taken during cooling. Use the highest reading you observed for the calculation." :
                "You recorded the mixture before confirming its peak. Watch for a small drop, then enter the highest reading you saw.");
            if (e.lowWater) issues.Add("The lamp switched off because the bath ran low. The trial ended before all readings were available.");
            return $"Technique  {passed} / {measured} observed checks\n" + (issues.Count == 0 ?
                "The observed actions met the technique checks." : "• " + string.Join("\n• ", issues));
        }
    }
}
