using System.Collections.Generic;
using UnityEngine;

namespace VirtualLab.Physics81
{
    internal sealed class Physics81Audio
    {
        readonly AudioSource actions;
        readonly Dictionary<string, AudioClip> clips = new();
        readonly AudioSource water, burner, boiling, timer, typing, intro;

        public Physics81Audio(GameObject owner)
        {
            foreach (var clip in Resources.LoadAll<AudioClip>("Physics81Audio")) clips[clip.name] = clip;
            actions = Add(owner, null, .65f);
            water = Add(owner, "Water", .72f);
            burner = Add(owner, "Burner", .28f);
            boiling = Add(owner, "Boiling", .55f);
            timer = Add(owner, "Timer", .46f);
            typing = Add(owner, "Typing", .36f);
            intro = Add(owner, "Intro", .28f);
        }

        AudioSource Add(GameObject owner, string name, float volume)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = name != null;
            source.volume = volume;
            if (name != null && clips.TryGetValue(name, out var clip)) source.clip = clip;
            if (source.loop) source.enabled = false;
            return source;
        }

        public void Cue(string name)
        {
            if (clips.TryGetValue(name, out var clip)) actions.PlayOneShot(clip);
        }

        public void Update(SetupLedger data, bool active, bool cupPouring, bool showingIntro)
        {
            Play(water, active && (data.TapOn || cupPouring));
            Play(burner, active && data.LampOn);
            Play(boiling, active && data.LampOn && data.Thermal?.Boiling == true);
            Play(timer, active && data.Step == SetupStep.SoakBob && data.Thermal.SoakSeconds > 0 &&
                data.Thermal.SoakSeconds < Physics81ThermalModel.RequiredSoak);
            Play(intro, showingIntro);
        }

        public void Typing(bool active) => Play(typing, active);
        public void StopLoops()
        {
            Play(water, false);
            Play(burner, false);
            Play(boiling, false);
            Play(timer, false);
            Play(typing, false);
            Play(intro, false);
        }

        public void Stop() { actions.Stop(); StopLoops(); }

        static void Play(AudioSource source, bool active)
        {
            if (source.enabled == active || source.clip == null) return;
            if (!active) source.Stop();
            source.enabled = active;
            if (active) source.Play();
        }
    }
}
