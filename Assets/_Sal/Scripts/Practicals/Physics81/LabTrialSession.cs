using System;
using System.IO;
using UnityEngine;

namespace VirtualLab.Physics81
{
    public sealed partial class LabSetupSession
    {
        public Physics81Trial Trial { get; private set; }
        public Physics81Trial SavedTrial { get; private set; }
        public string RecoveryNote { get; private set; } = "";
        public bool SaveFailed { get; private set; }
        Physics81TrialStore trialStore;
        HomePose[] homePoses;
        float cupDepth, draftWriteAt;

        void InitializeTrialStorage()
        {
            homePoses = Array.ConvertAll(new[] { lab.Jug.Body, lab.Cylinder.Body, lab.Cup.Body, lab.Bath.Body,
                lab.CupProbe, lab.BathProbe, lab.Lid, lab.Stirrer, lab.Bob }, item => new HomePose(item));
            UseTrialStorage(new Physics81TrialStore(Path.Combine(Application.persistentDataPath, "physics.8.1.json")));
        }

        public void UseTrialStorage(Physics81TrialStore storage)
        {
            if (Started) throw new InvalidOperationException("Select trial storage before beginning the practical.");
            trialStore = storage;
            SavedTrial = trialStore.Load();
        }

        public void Begin(bool guided, bool variation = false)
        {
            if (Started) return;
            NewTrial(guided, variation);
        }

        public void RepeatTrial() => NewTrial(Trial.guided, Trial.conditions.variation);

        void NewTrial(bool guided, bool variation)
        {
            var conditions = Physics81Conditions.Create(Guid.NewGuid().GetHashCode(), variation && !guided);
            ActivateTrial(new Physics81Trial { guided = guided, conditions = conditions });
            RecoveryNote = "";
            SaveProgress();
        }

        public void ResumeTrial()
        {
            if (Started || SavedTrial == null) return;
            ActivateTrial(SavedTrial);
            RecoveryNote = Trial.finished ? "Saved workbook restored." : Trial.preparation == null ?
                "Preparation restarted. Your workbook draft was kept." : Trial.heatingStarted ?
                "Heating restarted safely. Earlier readings were kept." : "Prepared apparatus restored. The lamp is off.";
        }

        public void RetryHeating()
        {
            if (Trial?.preparation == null) return;
            Trial.finished = Trial.heatingStarted = false;
            Trial.evidence = null;
            Trial.draft.readings[3] = Trial.draft.readings[4] = Trial.draft.readings[5] = "";
            Trial.draft.calculations = new string[7];
            Trial.draft.observations = Trial.draft.conclusion = Trial.draft.reflection = "";
            Trial.draft.submitted = false;
            ActivateTrial(Trial);
            RecoveryNote = "Fresh bath ready. Earlier readings were kept.";
            SaveProgress();
        }

        void ActivateTrial(Physics81Trial trial)
        {
            foreach (var pose in homePoses) pose.Restore();
            Rig.ResetStation();
            instruments.Reset();
            cylinderTip = 0f;
            mixedFor = 0f;
            readingError = "";
            Balance.ResetScale();
            Data = SetupLedger.Restore(trial);
            Trial = trial;
            Started = true;
            userPaused = false;
            pendingInspection = null;
            depth = .055f;
            ReleaseHands();
            ResetStations(trial.preparation != null, trial.finished);
            if (trial.preparation != null)
            {
                var prep = trial.preparation;
                cupDepth = prep.cupDepth;
                depth = prep.bathDepth;
                cupProbeWetPose.localPosition = Vector3.up * cupDepth;
                Balance.SwitchOn();
                if (trial.finished)
                {
                    Place(lab.Lid, lab.CupLid);
                    Place(lab.CupProbe, cupProbeWetPose);
                }
                else trial.draft.readings[3] = trial.draft.readings[4] = trial.draft.readings[5] = "";
                Place(lab.Bath.Body, lab.BeakerTripod);
                Place(lab.BathProbe, lab.BathProbeSlot);
                lab.BathProbe.localPosition = Vector3.up * prep.bathDepth;
            }
            instruments.DrawInstruments(Data, 0);
            DrawVessels();
            RefreshApparatus();
            if (Step == SetupStep.LightLamp && GetDepthIssue(true).Length > 0) Data.RecheckBathDepth();
            if (Step != SetupStep.Review) pendingInspection = CurrentView();
            RefreshSuspension();
        }

        public void MarkDraftChanged()
        {
            Trial.draft.submitted = false;
            draftWriteAt = Time.unscaledTime + .4f;
        }

        public void SaveProgress()
        {
            draftWriteAt = 0;
            if (Trial == null) return;
            SaveFailed = !trialStore.Save(Trial);
            if (!SaveFailed) SavedTrial = Trial;
        }

        readonly struct HomePose
        {
            readonly Transform item, parent;
            readonly Vector3 position;
            readonly Quaternion rotation;
            public HomePose(Transform item)
            {
                this.item = item;
                parent = item.parent;
                position = item.localPosition;
                rotation = item.localRotation;
            }
            public void Restore()
            {
                item.SetParent(parent, false);
                item.SetLocalPositionAndRotation(position, rotation);
            }
        }
    }
}
