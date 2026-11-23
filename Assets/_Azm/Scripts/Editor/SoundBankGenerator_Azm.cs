using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.EditorTools
{
    /*
     * Layer: Editor tooling (development only).
     * Purpose: "VirtualLab_Azm > Generate UI Sounds" synthesises the app's feedback sounds as small
     * WAV files (16-bit mono, 44.1 kHz) in Assets/_Azm/Art/Audio and fills SoundBank_Azm.asset with
     * them. Synthesised rather than downloaded: the CC0 packs we wanted (Kenney) block automated
     * downloads, and our own sounds need no licence or credit. Each sound is built from a few simple
     * ingredients - sine partials with exponential decay ("bell"), filtered noise ("click",
     * "whoosh"), pitch sweeps ("pop") - and is short and quiet on purpose: feedback, not music.
     * Re-running it overwrites the WAVs (the generated files only) and keeps the bank's volumes.
     * Any clip can be replaced later with a recorded sound by assigning it in the bank asset.
     * Talks to: AssetDatabase; writes Art/Audio/*.wav and Data/SoundBank_Azm.asset.
     */
    public static class SoundBankGenerator_Azm
    {
        private const string AudioFolder = "Assets/_Azm/Art/Audio";
        private const string BankPath = "Assets/_Azm/Data/SoundBank_Azm.asset";
        private const int Rate = 44100;

        [MenuItem("VirtualLab_Azm/Generate UI Sounds")]
        public static void Generate()
        {
            Directory.CreateDirectory(AudioFolder);
            var random = new System.Random(12345);   // fixed seed: the same sounds every time

            // id, samples, default volume, pitch variation
            Make(SoundId_Azm.Click, Click(random), 0.35f, 0.06f);
            Make(SoundId_Azm.Confirm, Confirm(), 0.45f, 0.0f);
            Make(SoundId_Azm.Wrong, Wrong(), 0.40f, 0.0f);
            Make(SoundId_Azm.Tick, Tick(random), 0.30f, 0.08f);
            Make(SoundId_Azm.Tock, Tock(), 0.55f, 0.04f);
            Make(SoundId_Azm.Ratchet, Ratchet(random), 0.55f, 0.05f);
            Make(SoundId_Azm.Chime, Chime(), 0.40f, 0.0f);
            Make(SoundId_Azm.Whoosh, Whoosh(random), 0.30f, 0.08f);
            Make(SoundId_Azm.Success, Success(), 0.50f, 0.0f);
            Make(SoundId_Azm.Pop, Pop(), 0.35f, 0.06f);
            Make(SoundId_Azm.Lock, LockSnap(random), 0.50f, 0.04f);

            AssetDatabase.Refresh();
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank_Azm>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank_Azm>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            var ids = (SoundId_Azm[])Enum.GetValues(typeof(SoundId_Azm));
            var entries = new SoundBank_Azm.Entry[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                var existing = bank.Get(ids[i]);
                entries[i] = new SoundBank_Azm.Entry
                {
                    id = ids[i],
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/Sfx_{ids[i]}_Azm.wav"),
                    volume = existing != null ? existing.volume : pendingVolume[(int)ids[i]],
                    pitchVariation = existing != null ? existing.pitchVariation : pendingPitch[(int)ids[i]]
                };
            }
            bank.entries = entries;
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            Debug.Log("[SoundBankGenerator_Azm] Generated " + ids.Length + " sounds into " + AudioFolder);
        }

        private static readonly float[] pendingVolume = new float[32];
        private static readonly float[] pendingPitch = new float[32];

        private static void Make(SoundId_Azm id, float[] samples, float volume, float pitch)
        {
            pendingVolume[(int)id] = volume;
            pendingPitch[(int)id] = pitch;
            Normalise(samples, 0.9f);
            WriteWav($"{AudioFolder}/Sfx_{id}_Azm.wav", samples);
        }

        // ---------- ingredients ----------

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        // A sine partial with exponential decay added into the buffer, starting at startSeconds.
        private static void Partial(float[] b, float freq, float amp, float decayPerSecond, float startSeconds = 0f, float attackSeconds = 0.002f)
        {
            int start = Mathf.RoundToInt(startSeconds * Rate);
            for (int i = start; i < b.Length; i++)
            {
                float t = (i - start) / (float)Rate;
                float env = Mathf.Exp(-t * decayPerSecond) * Mathf.Clamp01(t / attackSeconds);
                b[i] += Mathf.Sin(2f * Mathf.PI * freq * t) * amp * env;
            }
        }

        // A bell-like note: fundamental plus soft inharmonic partials (small bells / glass).
        private static void Bell(float[] b, float freq, float amp, float startSeconds, float decay)
        {
            Partial(b, freq, amp, decay, startSeconds, 0.003f);
            Partial(b, freq * 2.0f, amp * 0.35f, decay * 1.6f, startSeconds, 0.003f);
            Partial(b, freq * 3.01f, amp * 0.12f, decay * 2.4f, startSeconds, 0.003f);
        }

        // White noise burst through a one-pole high-pass (crisp) with exponential decay.
        private static void NoiseBurst(float[] b, System.Random r, float amp, float decayPerSecond, float startSeconds, float highPass = 0.85f)
        {
            int start = Mathf.RoundToInt(startSeconds * Rate);
            float prevIn = 0f, prevOut = 0f;
            for (int i = start; i < b.Length; i++)
            {
                float t = (i - start) / (float)Rate;
                float env = Mathf.Exp(-t * decayPerSecond);
                if (env < 0.0005f) break;
                float x = (float)(r.NextDouble() * 2.0 - 1.0);
                float y = highPass * (prevOut + x - prevIn);   // one-pole high-pass
                prevIn = x; prevOut = y;
                b[i] += y * amp * env;
            }
        }

        // ---------- the sounds ----------

        private static float[] Click(System.Random r)
        {
            var b = Buffer(0.05f);
            NoiseBurst(b, r, 0.6f, 180f, 0f, 0.9f);
            Partial(b, 2600f, 0.5f, 140f);
            return b;
        }

        // Soft rising two-note chime (C6 -> G6).
        private static float[] Confirm()
        {
            var b = Buffer(0.45f);
            Bell(b, 1046.5f, 0.6f, 0f, 9f);
            Bell(b, 1568f, 0.7f, 0.09f, 7f);
            return b;
        }

        // Soft falling "hmm" (E4 -> C4), slow attack so it never sounds like an alarm.
        private static float[] Wrong()
        {
            var b = Buffer(0.42f);
            Partial(b, 329.6f, 0.6f, 9f, 0f, 0.02f);
            Partial(b, 659.3f, 0.12f, 14f, 0f, 0.02f);
            Partial(b, 261.6f, 0.6f, 8f, 0.14f, 0.02f);
            Partial(b, 523.3f, 0.10f, 12f, 0.14f, 0.02f);
            return b;
        }

        private static float[] Tick(System.Random r)
        {
            var b = Buffer(0.03f);
            Partial(b, 3200f, 0.6f, 260f, 0f, 0.0005f);
            NoiseBurst(b, r, 0.25f, 400f, 0f, 0.95f);
            return b;
        }

        // Wooden "tock": a low partial whose pitch drops slightly, plus a short knock.
        private static float[] Tock()
        {
            var b = Buffer(0.14f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 520f - 140f * Mathf.Clamp01(t / 0.05f);
                b[i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 45f) * 0.8f;
            }
            Partial(b, 1650f, 0.25f, 90f);
            return b;
        }

        // "trrr-click": a quick run of small clicks, then a firmer one (the ratchet slipping).
        private static float[] Ratchet(System.Random r)
        {
            var b = Buffer(0.16f);
            for (int k = 0; k < 4; k++)
            {
                NoiseBurst(b, r, 0.35f - k * 0.04f, 300f, k * 0.018f, 0.92f);
                Partial(b, 2300f, 0.2f, 260f, k * 0.018f, 0.0005f);
            }
            NoiseBurst(b, r, 0.8f, 160f, 0.085f, 0.88f);
            Partial(b, 1800f, 0.5f, 120f, 0.085f, 0.0005f);
            return b;
        }

        // Step complete: a short three-note arpeggio (C6 E6 G6).
        private static float[] Chime()
        {
            var b = Buffer(0.7f);
            Bell(b, 1046.5f, 0.5f, 0f, 6f);
            Bell(b, 1318.5f, 0.5f, 0.07f, 6f);
            Bell(b, 1568f, 0.6f, 0.14f, 5f);
            return b;
        }

        // Soft air movement: noise through a low-pass whose cutoff rises then falls.
        private static float[] Whoosh(System.Random r)
        {
            var b = Buffer(0.28f);
            float y = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float shape = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.28f));
                float cutoff = 0.02f + 0.10f * shape;          // one-pole low-pass coefficient
                float x = (float)(r.NextDouble() * 2.0 - 1.0);
                y += cutoff * (x - y);
                b[i] = y * shape;
            }
            return b;
        }

        // The final result: a brighter four-note arpeggio (C5 E5 G5 C6).
        private static float[] Success()
        {
            var b = Buffer(1.1f);
            Bell(b, 523.3f, 0.45f, 0f, 4.5f);
            Bell(b, 659.3f, 0.45f, 0.09f, 4.5f);
            Bell(b, 784f, 0.5f, 0.18f, 4f);
            Bell(b, 1046.5f, 0.6f, 0.27f, 3f);
            return b;
        }

        // A quick upward blip.
        private static float[] Pop()
        {
            var b = Buffer(0.09f);
            float phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(380f, 980f, Mathf.Clamp01(t / 0.04f));
                phase += 2f * Mathf.PI * f / Rate;
                b[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 40f) * Mathf.Clamp01(t / 0.002f);
            }
            return b;
        }

        // A lever snapping: two short metallic clicks.
        private static float[] LockSnap(System.Random r)
        {
            var b = Buffer(0.12f);
            NoiseBurst(b, r, 0.5f, 220f, 0f, 0.9f);
            Partial(b, 3100f, 0.35f, 110f, 0f, 0.0005f);
            NoiseBurst(b, r, 0.7f, 200f, 0.035f, 0.9f);
            Partial(b, 2400f, 0.45f, 90f, 0.035f, 0.0005f);
            return b;
        }

        // ---------- output ----------

        private static void Normalise(float[] b, float peak)
        {
            float max = 0f;
            foreach (float s in b) max = Mathf.Max(max, Mathf.Abs(s));
            if (max < 1e-6f) return;
            float k = peak / max;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
            // 3 ms fade-out so no sound ends with a click.
            int fade = Mathf.Min(b.Length, Mathf.RoundToInt(0.003f * Rate));
            for (int i = 0; i < fade; i++) b[b.Length - 1 - i] *= i / (float)fade;
        }

        private static void WriteWav(string assetPath, float[] samples)
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", assetPath);
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);              // fmt chunk size
                w.Write((short)1);        // PCM
                w.Write((short)1);        // mono
                w.Write(Rate);
                w.Write(Rate * 2);        // byte rate
                w.Write((short)2);        // block align
                w.Write((short)16);       // bits per sample
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                foreach (float s in samples)
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
            }
        }
    }
}
