using System;

namespace VirtualLab.Physics141
{
    public enum Terminal
    {
        SupplyPlus, SupplyMinus, KeyIn, KeyOut, RheostatFixed, RheostatWiper,
        CurrentCom, CurrentVolts, CurrentAmps, VoltageCom, VoltageVolts, VoltageAmps,
        WireLeft, WireRight, WireLeftSense, WireRightSense
    }

    public enum MeterRange { Off, Dc20V, Dc10A, Dc200mA, Ac200V }

    [Serializable]
    public struct Lead
    {
        public Terminal from, to;
        public Lead(Terminal from, Terminal to) { this.from = from; this.to = to; }
        public bool Joins(Terminal a, Terminal b) => from == a && to == b || from == b && to == a;
        public bool Uses(Terminal terminal) => from == terminal || to == terminal;
    }
}
