using System;
using System.IO;
using UnityEngine;

namespace VirtualLab.Physics81
{
    public sealed class Physics81TrialStore
    {
        readonly string path;
        public Physics81TrialStore(string path) => this.path = path;

        public Physics81Trial Load() => Read(path) ?? Read(path + ".bak");

        static Physics81Trial Read(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                var trial = JsonUtility.FromJson<Physics81Trial>(File.ReadAllText(file));
                if (trial == null || trial.version < 1 || trial.version > Physics81Trial.CurrentVersion ||
                    trial.conditions == null || trial.draft?.readings?.Length != 6) return null;
                // Unity may deserialize null inline objects as objects with default fields.
                if (trial.preparation == null || trial.preparation.cupMl <= 0 || trial.preparation.bathMl <= 0) trial.preparation = null;
                else if (trial.preparation.initial?.valid != true) trial.preparation.initial = null;
                if (!trial.finished) trial.evidence = null;
                else if (trial.evidence != null)
                {
                    if (trial.evidence.bath?.valid != true) trial.evidence.bath = null;
                    if (trial.evidence.mixture?.valid != true) trial.evidence.mixture = null;
                }
                if (trial.finished && (trial.preparation == null || trial.evidence == null)) return null;
                if (trial.draft.calculations?.Length != 7) trial.draft.calculations = new string[7];
                trial.version = Physics81Trial.CurrentVersion;
                return trial;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { return null; }
        }

        public bool Save(Physics81Trial trial)
        {
            try
            {
                string pending = path + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(pending, JsonUtility.ToJson(trial));
                if (!File.Exists(path)) File.Move(pending, path);
                else
                {
                    try { File.Replace(pending, path, path + ".bak"); }
                    catch (Exception ex) when (ex is PlatformNotSupportedException or IOException)
                    {
                        File.Copy(path, path + ".bak", true);
                        File.Delete(path);
                        File.Move(pending, path);
                    }
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { return false; }
        }
    }
}
