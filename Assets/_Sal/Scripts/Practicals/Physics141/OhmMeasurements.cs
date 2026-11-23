using System;
using System.Globalization;
using UnityEngine;

namespace VirtualLab.Physics141
{
    [Serializable]
    public struct Observation
    {
        public bool saved;
        public string current, voltage;
        public float rheostat;
    }

    public sealed class OhmMeasurements
    {
        internal const float SupplyDegreesPerVolt = 19;
        // Start midway, cover the low-current end, then fill in the range with distinct readings at 6 V.
        static readonly int[] GuidedPositions = { 25, 50, 35, 15, 5 };
        readonly OhmBench bench;
        readonly OhmWiring wiring;
        readonly AudioSource audio;
        readonly AudioClip click, record;
        CircuitReading reading;
        float changedAt;
        int retake = -1;
        bool tripped;
        internal string Feedback { get; private set; }
        public bool Powered { get; private set; }
        public bool Closed { get; private set; }
        public float Rheostat { get; private set; } = 25;
        public float Supply { get; private set; }
        public bool ZeroChecked { get; private set; }
        public Observation[] Pairs { get; private set; } = new Observation[5];
        internal bool Retaking => retake >= 0;
        internal int Count { get; private set; }
        internal bool ReadyToCapture => CanCapture(out _);
        internal bool ReadingValid => !tripped && reading.Status == CircuitStatus.Ready;
        internal bool DifferentReading => ReadingValid && !IsDuplicate();
        internal int SuggestedResistance
        {
            get
            {
                foreach (int setting in GuidedPositions)
                {
                    var sample = OhmCircuit.Read(wiring.Leads, wiring.CurrentRange, wiring.VoltageRange,
                        Powered, Closed, Supply, setting);
                    if (sample.Status == CircuitStatus.Ready && !IsDuplicate(sample)) return setting;
                }
                return (int)Rheostat;
            }
        }

        public event Action Changed;

        internal OhmMeasurements(OhmBench owner, OhmWiring input, AudioSource source)
        {
            bench = owner;
            wiring = input;
            audio = source;
            click = Resources.Load<AudioClip>("Practicals/ChemistryAudio/Click");
            record = Resources.Load<AudioClip>("Practicals/ChemistryAudio/Record");
            wiring.CanMoveWiper = () => Powered && Closed;
            wiring.WiperMoved = fraction => SetRheostat(Mathf.Round(fraction * (float)OhmCircuit.MaximumRheostat));
            wiring.Changed += Recalculate;
            Recalculate();
        }

        internal void Restore(float supply, float rheostat, bool zeroChecked, Observation[] pairs)
        {
            Supply = Mathf.Clamp(supply, 0, (float)OhmCircuit.MaximumSupply);
            Rheostat = Mathf.Clamp(rheostat, 0, (float)OhmCircuit.MaximumRheostat);
            ZeroChecked = zeroChecked;
            Pairs = pairs;
            Count = 0;
            foreach (var pair in Pairs) if (pair.saved) Count++;
            Closed = Powered = tripped = false;
            retake = -1;
            Recalculate();
        }

        internal void Retake(int row)
        {
            if (row < 0 || row >= Pairs.Length || !Pairs[row].saved) return;
            retake = row;
            Recalculate();
            Changed?.Invoke();
        }

        internal void CheckZero()
        {
            if (Closed || wiring.CurrentRange != MeterRange.Dc10A || wiring.VoltageRange != MeterRange.Dc20V)
                return;
            ZeroChecked = true;
            Cue(click);
            Changed?.Invoke();
        }

        internal void TogglePower()
        {
            if (Closed) return;
            Powered = !Powered;
            tripped = false;
            Commit();
        }

        internal void ToggleKey()
        {
            if (!Closed && (!Powered || tripped)) return;
            Closed = !Closed;
            Commit();
        }

        internal void SetSupply(float value)
        {
            if (!Powered || Closed) return;
            float setting = Mathf.Clamp(Mathf.Round(value * 2) * .5f, 0, (float)OhmCircuit.MaximumSupply);
            if (setting == Supply) return;
            Supply = setting;
            Commit();
        }

        internal void SetRheostat(float value)
        {
            value = Mathf.Clamp(value, 0, (float)OhmCircuit.MaximumRheostat);
            if (Rheostat == value) return;
            Rheostat = value;
            Recalculate();
            Changed?.Invoke();
        }

        void Commit() { Cue(click); Recalculate(); Changed?.Invoke(); }

        void Recalculate()
        {
            reading = OhmCircuit.Read(wiring.Leads, wiring.CurrentRange, wiring.VoltageRange, Powered, Closed, Supply, Rheostat);
            if (reading.Status == CircuitStatus.Short) tripped = true;
            changedAt = Time.unscaledTime;
            wiring.Editable = !Powered && !Closed;
            var wiper = bench.Wiper;
            wiper.localPosition = new Vector3(WiperX(Rheostat), wiper.localPosition.y, wiper.localPosition.z);
            bench.Key.localRotation = Quaternion.Euler(0, 0, Closed ? 0 : 24);
            bench.SupplyKnob.localRotation = Quaternion.Euler(0, 0, -Supply * SupplyDegreesPerVolt);
            bench.SupplyDisplay.text = tripped ? "TRIP" : Powered ? Supply.ToString("0.0", CultureInfo.InvariantCulture) : "OFF";
            bench.CurrentDisplay.text = MeterText(wiring.CurrentRange, reading.CurrentText);
            bench.VoltageDisplay.text = MeterText(wiring.VoltageRange, reading.VoltageText);
            Feedback = tripped ? "The supply tripped. Open the key and check your wires."
                : reading.Status switch
                {
                    CircuitStatus.OverRange => "The reading is outside the allowed range.",
                    CircuitStatus.Reverse => "One reading is negative. Check the red and black leads.",
                    CircuitStatus.WrongCircuit => "The voltage meter must measure across the sample wire.",
                    CircuitStatus.Incomplete => "No complete circuit. Check for any loose or missing wire.",
                    CircuitStatus.MeterSetup => "Check the dial setting and jacks on both meters.",
                    _ => ""
                };
        }

        internal static float WiperX(float resistance) =>
            Mathf.Lerp(-.107f, .107f, resistance / (float)OhmCircuit.MaximumRheostat);

        string MeterText(MeterRange range, string number) =>
            range == MeterRange.Off ? "—" : reading.Status == CircuitStatus.OverRange ? "OL" : tripped ? "0.00" : number;

        bool CanCapture(out int row)
        {
            row = retake >= 0 ? retake : Array.FindIndex(Pairs, pair => !pair.saved);
            return row >= 0 && ReadingValid && Supply == OhmCircuit.ObservationSupply
                && (row != 0 || retake >= 0 || Rheostat == 25) && wiring.Prepared && ZeroChecked
                && Time.unscaledTime - changedAt >= .4f && !IsDuplicate();
        }

        bool IsDuplicate() => IsDuplicate(reading);

        bool IsDuplicate(CircuitReading candidate)
        {
            double value = double.Parse(candidate.CurrentText, CultureInfo.InvariantCulture);
            for (int i = 0; i < Pairs.Length; i++)
                if (i != retake && Pairs[i].saved && Math.Abs(double.Parse(Pairs[i].current, CultureInfo.InvariantCulture) - value)
                    < OhmCircuit.PairSeparation - .0001) return true;
            return false;
        }

        internal bool Capture()
        {
            if (!CanCapture(out int row)) return false;
            if (!Pairs[row].saved) Count++;
            Pairs[row] = new Observation
                { saved = true, current = bench.CurrentDisplay.text, voltage = bench.VoltageDisplay.text, rheostat = Rheostat };
            retake = -1;
            Cue(record);
            Changed?.Invoke();
            return true;
        }

        void Cue(AudioClip cue) { if (cue) audio.PlayOneShot(cue, .48f); }
    }
}
