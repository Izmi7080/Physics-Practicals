using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VirtualLab.Physics141
{
    public enum CircuitStatus { Ready, PowerOff, OpenKey, Incomplete, MeterSetup, Short, OverRange, Reverse, WrongCircuit }

    public readonly struct CircuitReading
    {
        public readonly CircuitStatus Status;
        public readonly double Current, Voltage;
        public CircuitReading(CircuitStatus status, double current = 0, double voltage = 0)
        { Status = status; Current = current; Voltage = voltage; }
        public string CurrentText => Format(Current);
        public string VoltageText => Format(Voltage);
        public static string Format(double value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
    }

    public static class OhmCircuit
    {
        public const double Nichrome = 15;
        public const double MaximumRheostat = 50;
        public const double ObservationSupply = 6;
        public const double MaximumSupply = 12;
        public const double SupplyLimit = 1;
        public const double ExperimentCurrentLimit = .60;
        public const double PairSeparation = .02;

        public static CircuitReading Read(IReadOnlyList<Lead> leads, MeterRange current, MeterRange voltage,
            bool power, bool closed, double supply, double rheostat)
        {
            var node = new Nodes();
            node.Join(Terminal.WireLeft, Terminal.WireLeftSense);
            node.Join(Terminal.WireRight, Terminal.WireRightSense);
            foreach (var lead in leads) node.Join(lead.from, lead.to);
            if (!power) return new(CircuitStatus.PowerOff);
            if (!closed) return new(CircuitStatus.OpenKey);
            node.Join(Terminal.KeyIn, Terminal.KeyOut);

            Terminal? currentInput = AmpInput(current, Terminal.CurrentAmps, Terminal.CurrentVolts);
            Terminal? voltageInput = AmpInput(voltage, Terminal.VoltageAmps, Terminal.VoltageVolts);
            int positive = node[Terminal.SupplyPlus], negative = node[Terminal.SupplyMinus];
            if (positive == negative) return new(CircuitStatus.Short);

            var resistors = new List<Branch>
            {
                new(node[Terminal.RheostatFixed], node[Terminal.RheostatWiper], Math.Clamp(rheostat, 0, MaximumRheostat)),
                new(node[Terminal.WireLeft], node[Terminal.WireRight], Nichrome)
            };
            int currentBranch = AddAmmeter(resistors, node, currentInput, Terminal.CurrentCom);
            AddAmmeter(resistors, node, voltageInput, Terminal.VoltageCom);

            var paths = new List<List<(int branch, int direction)>>();
            FindPaths(positive, negative, resistors, new HashSet<int>(), new List<(int, int)>(), paths);
            if (paths.Count == 0) return new(CircuitStatus.Incomplete);
            double minimum = paths.Min(path => path.Sum(segment => resistors[segment.branch].Resistance));
            if (minimum < 1e-8 || supply / minimum > SupplyLimit + 1e-8) return new(CircuitStatus.Short);
            if (currentBranch < 0 || voltage != MeterRange.Dc20V) return new(CircuitStatus.MeterSetup);

            var path = paths[0];
            if (paths.Count != 1 || path.Count != 3 || !path.Any(segment => segment.branch == currentBranch)
                || !path.Any(segment => segment.branch == 0) || !path.Any(segment => segment.branch == 1))
                return new(CircuitStatus.WrongCircuit);

            double amps = supply / minimum;
            var potential = new Dictionary<int, double> { [positive] = supply };
            double level = supply;
            foreach (var (branch, direction) in path)
            {
                var resistor = resistors[branch];
                level -= amps * resistor.Resistance;
                potential[direction == 1 ? resistor.To : resistor.From] = level;
            }
            var vPlus = node[Terminal.VoltageVolts];
            var vMinus = node[Terminal.VoltageCom];
            if (!potential.TryGetValue(vPlus, out double high) || !potential.TryGetValue(vMinus, out double low))
                return new(CircuitStatus.Incomplete);
            double volts = high - low;
            var wire = resistors[1];
            bool across = vPlus == wire.From && vMinus == wire.To || vPlus == wire.To && vMinus == wire.From;
            double signedAmps = amps * path.Find(segment => segment.branch == currentBranch).direction;
            if (amps > ExperimentCurrentLimit + 1e-8 || current == MeterRange.Dc200mA && amps >= .200 || Math.Abs(volts) >= 20)
                return new(CircuitStatus.OverRange, signedAmps, volts);
            if (!across) return new(CircuitStatus.WrongCircuit, signedAmps, volts);
            return new(signedAmps < 0 || volts < 0 ? CircuitStatus.Reverse : CircuitStatus.Ready, signedAmps, volts);
        }

        static Terminal? AmpInput(MeterRange range, Terminal amps, Terminal milliamps) => range switch
        {
            MeterRange.Dc10A => amps,
            MeterRange.Dc200mA => milliamps,
            _ => null
        };

        static int AddAmmeter(List<Branch> branches, Nodes nodes, Terminal? input, Terminal common)
        {
            if (!input.HasValue) return -1;
            branches.Add(new Branch(nodes[input.Value], nodes[common], 0));
            return branches.Count - 1;
        }

        static void FindPaths(int at, int end, List<Branch> branches, HashSet<int> visited,
            List<(int branch, int direction)> path, List<List<(int branch, int direction)>> result)
        {
            if (at == end) { result.Add(new List<(int, int)>(path)); return; }
            visited.Add(at);
            for (int i = 0; i < branches.Count; i++)
            {
                var branch = branches[i];
                int next = branch.From == at ? branch.To : branch.To == at ? branch.From : -1;
                if (next < 0 || visited.Contains(next)) continue;
                path.Add((i, branch.From == at ? 1 : -1));
                FindPaths(next, end, branches, visited, path, result);
                path.RemoveAt(path.Count - 1);
            }
            visited.Remove(at);
        }

        readonly struct Branch
        {
            public readonly int From, To;
            public readonly double Resistance;
            public Branch(int from, int to, double resistance) { From = from; To = to; Resistance = resistance; }
        }

        sealed class Nodes
        {
            readonly int[] parents = Enumerable.Range(0, Enum.GetValues(typeof(Terminal)).Length).ToArray();
            public int this[Terminal terminal]
            {
                get
                {
                    int index = (int)terminal;
                    while (parents[index] != index) index = parents[index];
                    return index;
                }
            }
            public void Join(Terminal a, Terminal b) => parents[this[a]] = this[b];
        }
    }
}
