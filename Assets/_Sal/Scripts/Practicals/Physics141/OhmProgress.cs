using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace VirtualLab.Physics141
{
    [Serializable]
    public sealed class OhmProgress
    {
        public int version = 1, circuitTiles, inspectedApparatus, reviewedChecks;
        public bool guided = true, prepared, zeroChecked, sampleMounted;
        public List<Lead> leads = new(), diagram = new();
        public MeterRange currentRange, voltageRange;
        public float supply, rheostat = 25;
        public Observation[] pairs = new Observation[5];
        public string[] entries = new string[15];
        public string mean, conclusion;
        public Vector2[] points = new Vector2[5];
        public bool[] plotted = new bool[5];
        public Vector2 lineStart, lineEnd;
        public bool hasLine, submitted;

        internal bool HasWork => circuitTiles != 0 || inspectedApparatus != 0 || prepared || zeroChecked || sampleMounted
            || leads.Count > 0 || diagram.Count > 0 || currentRange != MeterRange.Off || voltageRange != MeterRange.Off
            || supply != 0 || rheostat != 25 || pairs.Any(pair => pair.saved) || entries.Any(value => !string.IsNullOrEmpty(value))
            || !string.IsNullOrEmpty(mean) || !string.IsNullOrEmpty(conclusion) || plotted.Any(point => point) || hasLine || submitted;
    }

    internal sealed class OhmProgressStore
    {
        readonly string path;
        internal OhmProgressStore(string file) => path = file;
        internal OhmProgress Load() => Read(path) ?? Read(path + ".bak") ?? new OhmProgress();

        static OhmProgress Read(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                var data = JsonUtility.FromJson<OhmProgress>(File.ReadAllText(file));
                if (data?.version != 1 || data.leads == null || data.diagram == null || data.pairs?.Length != 5
                    || data.entries?.Length != 15 || data.points?.Length != 5 || data.plotted?.Length != 5) return null;
                return data;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { return null; }
        }

        internal bool Save(OhmProgress progress)
        {
            try
            {
                string pending = path + ".tmp";
                File.WriteAllText(pending, JsonUtility.ToJson(progress));
                if (File.Exists(path))
                {
                    try { File.Replace(pending, path, path + ".bak"); }
                    catch (Exception ex) when (ex is PlatformNotSupportedException or IOException)
                    {
                        File.Copy(path, path + ".bak", true);
                        File.Delete(path);
                        File.Move(pending, path);
                    }
                }
                else File.Move(pending, path);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            { return false; }
        }
    }
}
