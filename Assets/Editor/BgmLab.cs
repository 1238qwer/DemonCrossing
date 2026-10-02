using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Mawang.EditorTools
{
    // 배경음 후보 만들기 (에디터 전용). 음원 없이 합성해 WAV(44.1kHz 스테레오)로 내보낸다.
    // BgmLab.Export(폴더) → 01_8bit_current.wav ~ 05_*.wav
    public static class BgmLab
    {
        const int Rate = 44100;
        static System.Random rng = new System.Random(11);
        static float Noise() => (float)(rng.NextDouble() * 2 - 1);
        static float Midi(int n) => 440f * Mathf.Pow(2f, (n - 69) / 12f);
        const float TAU = Mathf.PI * 2f;

        public static string Export(string dir)
        {
            Directory.CreateDirectory(dir);
            var log = "";
            // 1. 지금 쓰는 8비트 (22050Hz 모노 그대로)
            var clip = (AudioClip)typeof(Sound).GetMethod("MakeBgm", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var d = new float[clip.samples]; clip.GetData(d, 0);
            WriteWav(Path.Combine(dir, "01_8bit_current.wav"), d, d, clip.frequency);
            log += $"01 {clip.length:0.0}s\n";
            log += Save(dir, "02_musicbox_waltz", Waltz());
            log += Save(dir, "03_cozy_tavern", Tavern());
            log += Save(dir, "04_bright_tycoon", Tycoon());
            log += Save(dir, "05_monster_carnival", Carnival());
            return log;
        }

        static string Save(string dir, string name, Track t)
        {
            t.Finish();
            WriteWav(Path.Combine(dir, name + ".wav"), t.L, t.R, Rate);
            return $"{name} {t.L.Length / (float)Rate:0.0}s\n";
        }

        // ── 믹서 ───────────────────────────────────────────────
        class Track
        {
            public float[] L, R, send;
            readonly int n, tail;
            public Track(float seconds)
            {
                n = Mathf.CeilToInt(seconds * Rate); tail = 3 * Rate;
                L = new float[n + tail]; R = new float[n + tail]; send = new float[n + tail];
            }

            public void Add(float start, float[] src, float vol, float pan = 0f, float rev = 0.2f)
            {
                float a = (pan + 1f) * Mathf.PI / 4f, gl = Mathf.Cos(a) * 1.414f, gr = Mathf.Sin(a) * 1.414f;
                int i0 = Mathf.RoundToInt(start * Rate);
                for (int i = 0; i < src.Length; i++)
                {
                    int k = i0 + i;
                    if (k < 0 || k >= L.Length) continue;
                    float s = src[i] * vol;
                    L[k] += s * gl; R[k] += s * gr; send[k] += s * rev;
                }
            }

            // 리버브(슈뢰더) → 꼬리를 앞에 겹쳐 이음매 없는 루프로 → 음량 맞추기
            public void Finish()
            {
                Reverb(send, L, 0); Reverb(send, R, 23);
                for (int i = 0; i < tail; i++) { L[i] += L[n + i]; R[i] += R[n + i]; }
                Array.Resize(ref L, n); Array.Resize(ref R, n);
                double sum = 0; for (int i = 0; i < n; i++) sum += L[i] * L[i] + R[i] * R[i];
                float rms = Mathf.Sqrt((float)(sum / (2 * n)));
                float g = 0.14f / Mathf.Max(1e-5f, rms);
                for (int i = 0; i < n; i++) { L[i] = Limit(L[i] * g); R[i] = Limit(R[i] * g); }
            }

            static float Limit(float x) => Mathf.Abs(x) < 0.7f ? x : Mathf.Sign(x) * (0.7f + 0.28f * (float)Math.Tanh((Mathf.Abs(x) - 0.7f) / 0.28f));

            static void Reverb(float[] input, float[] output, int spread)
            {
                int[] combs = { 1116, 1188, 1277, 1356, 1422, 1491 };
                int[] aps = { 556, 441, 341 };
                int len = input.Length;
                var wet = new float[len];
                foreach (int c0 in combs)
                {
                    int c = c0 + spread;
                    var buf = new float[c]; int idx = 0; float lp = 0;
                    for (int i = 0; i < len; i++)
                    {
                        float y = buf[idx];
                        lp = y * 0.75f + lp * 0.25f;
                        buf[idx] = input[i] + lp * 0.83f;
                        idx = (idx + 1) % c;
                        wet[i] += y / combs.Length;
                    }
                }
                foreach (int a0 in aps)
                {
                    int a = a0 + spread;
                    var buf = new float[a]; int idx = 0;
                    for (int i = 0; i < len; i++)
                    {
                        float b = buf[idx];
                        float y = -wet[i] + b;
                        buf[idx] = wet[i] + b * 0.5f;
                        idx = (idx + 1) % a;
                        wet[i] = y;
                    }
                }
                for (int i = 0; i < len; i++) output[i] += wet[i];
            }
        }

        // ── 악기 ───────────────────────────────────────────────
        static float[] Buf(float sec) => new float[Mathf.CeilToInt(sec * Rate)];
        static float Gate(float t, float dur, float rel) => t < dur ? 1f : Mathf.Max(0f, 1f - (t - dur) / rel);

        // 뜯는 현 (카플러스-스트롱): 류트·피치카토·우쿨렐레
        static float[] Pluck(float f, float dur, float bright = 0.5f, float decay = 0.996f)
        {
            var o = Buf(dur + 0.08f);
            int N = Mathf.Max(2, Mathf.RoundToInt(Rate / f));
            var line = new float[N];
            float prev = 0;
            for (int i = 0; i < N; i++) { float x = Noise(); prev = prev + bright * (x - prev); line[i] = prev; }
            int idx = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                float y = line[idx];
                int nx = (idx + 1) % N;
                line[idx] = decay * 0.5f * (line[idx] + line[nx]);
                idx = nx;
                o[i] = y * Gate(t, dur, 0.08f);
            }
            return o;
        }

        // 일렉트릭 피아노 (FM)
        static float[] Rhodes(float f, float dur)
        {
            var o = Buf(dur + 0.3f);
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                float idx = 1.3f * Mathf.Exp(-t * 5f) + 0.25f;
                float s = Mathf.Sin(TAU * f * t + idx * Mathf.Sin(TAU * f * t)) * Mathf.Exp(-t * 1.4f)
                        + 0.06f * Mathf.Sin(TAU * f * 15f * t) * Mathf.Exp(-t * 40f);
                o[i] = s * Mathf.Min(1f, t / 0.003f) * Gate(t, dur, 0.3f);
            }
            return o;
        }

        // 오르골
        static float[] MusicBox(float f, float dur)
        {
            var o = Buf(Mathf.Max(dur, 1.6f));
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                o[i] = (Mathf.Sin(TAU * f * t) * Mathf.Exp(-t * 2.4f)
                      + 0.18f * Mathf.Sin(TAU * f * 2f * t) * Mathf.Exp(-t * 5f)
                      + 0.10f * Mathf.Sin(TAU * f * 5.4f * t) * Mathf.Exp(-t * 14f)) * Mathf.Min(1f, t / 0.002f);
            }
            return o;
        }

        // 마림바
        static float[] Marimba(float f, float dur)
        {
            var o = Buf(Mathf.Max(dur, 0.7f));
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                o[i] = (Mathf.Sin(TAU * f * t) * Mathf.Exp(-t * 5.5f)
                      + 0.22f * Mathf.Sin(TAU * f * 4f * t) * Mathf.Exp(-t * 20f)
                      + 0.06f * Mathf.Sin(TAU * f * 9.9f * t) * Mathf.Exp(-t * 45f)) * Mathf.Min(1f, t / 0.0015f);
            }
            return o;
        }

        // 글로켄슈필 (종)
        static float[] Bell(float f, float dur)
        {
            var o = Buf(Mathf.Max(dur, 1.2f));
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                o[i] = (Mathf.Sin(TAU * f * t) * Mathf.Exp(-t * 2f)
                      + 0.3f * Mathf.Sin(TAU * f * 2.76f * t) * Mathf.Exp(-t * 5f)
                      + 0.12f * Mathf.Sin(TAU * f * 5.4f * t) * Mathf.Exp(-t * 10f)) * Mathf.Min(1f, t / 0.001f);
            }
            return o;
        }

        // 아코디언·칼리오페: 살짝 어긋난 펄스 두 개 + 비브라토 + 저역 통과
        static float[] Accordion(float f, float dur, float vib = 0.004f)
        {
            var o = Buf(dur + 0.06f);
            float p1 = 0, p2 = 0, p3 = 0, lp = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                float v = 1f + vib * Mathf.Sin(TAU * 5.5f * t) * Mathf.Min(1f, t / 0.25f);
                p1 = (p1 + f * 1.003f * v / Rate) % 1f; p2 = (p2 + f * 0.997f * v / Rate) % 1f; p3 = (p3 + f * 0.5f * v / Rate) % 1f;
                float x = (p1 < 0.35f ? 1f : -1f) + (p2 < 0.4f ? 1f : -1f) + 0.5f * (p3 < 0.5f ? 1f : -1f);
                lp += 0.18f * (x - lp);
                o[i] = lp * 0.4f * Mathf.Min(1f, t / 0.02f) * Gate(t, dur, 0.06f);
            }
            return o;
        }

        // 부드러운 패드 (톱니 셋 + 저역 통과, 느린 어택)
        static float[] Pad(float f, float dur)
        {
            var o = Buf(dur + 0.5f);
            float p1 = 0, p2 = 0, p3 = 0, lp = 0, lp2 = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                p1 = (p1 + f * 1.004f / Rate) % 1f; p2 = (p2 + f * 0.996f / Rate) % 1f; p3 = (p3 + f * 2.001f / Rate) % 1f;
                float x = (p1 * 2 - 1) + (p2 * 2 - 1) + 0.4f * (p3 * 2 - 1);
                lp += 0.05f * (x - lp); lp2 += 0.05f * (lp - lp2);
                o[i] = lp2 * 0.5f * Mathf.Min(1f, t / 0.35f) * Gate(t, dur, 0.5f);
            }
            return o;
        }

        // 베이스: 둥근 사인 + 살짝 찌그러뜨림
        static float[] Bass(float f, float dur, float drive = 1.5f)
        {
            var o = Buf(dur + 0.05f);
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                float x = Mathf.Sin(TAU * f * t) + 0.25f * Mathf.Sin(TAU * f * 2f * t);
                o[i] = (float)Math.Tanh(x * drive) * Mathf.Min(1f, t / 0.004f) * Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(t / 0.6f)) * Gate(t, dur, 0.05f);
            }
            return o;
        }

        // 튜바: 톱니를 필터로 둥글게
        static float[] Tuba(float f, float dur)
        {
            var o = Buf(dur + 0.06f);
            float p = 0, lp = 0, lp2 = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                p = (p + f / Rate) % 1f;
                float cut = 0.03f + 0.07f * Mathf.Exp(-t * 8f);
                lp += cut * ((p * 2 - 1) - lp); lp2 += cut * (lp - lp2);
                o[i] = lp2 * 1.6f * Mathf.Min(1f, t / 0.025f) * Gate(t, dur, 0.06f);
            }
            return o;
        }

        // ── 드럼 ───────────────────────────────────────────────
        static float[] Kick(float punch = 1f)
        {
            var o = Buf(0.35f); float ph = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                ph += (48f + 110f * Mathf.Exp(-t * 28f) * punch) / Rate;
                o[i] = Mathf.Sin(TAU * ph) * Mathf.Exp(-t * 9f) + Noise() * 0.15f * Mathf.Exp(-t * 300f);
            }
            return o;
        }

        static float[] Snare(float tone = 190f, float noiseDecay = 16f)
        {
            var o = Buf(0.3f); float prev = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                float nz = Noise(); float hp = nz - prev * 0.6f; prev = nz;
                o[i] = hp * 0.6f * Mathf.Exp(-t * noiseDecay) + Mathf.Sin(TAU * tone * t) * 0.5f * Mathf.Exp(-t * 25f);
            }
            return o;
        }

        static float[] Brush()
        {
            var o = Buf(0.25f); float lp = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                lp += 0.35f * (Noise() - lp);
                o[i] = lp * Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 11f);
            }
            return o;
        }

        static float[] Hat(float decay = 55f)
        {
            var o = Buf(Mathf.Min(0.5f, 6f / decay)); float prev = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                float nz = Noise(); float hp = nz - prev; prev = nz;
                o[i] = hp * 0.5f * Mathf.Exp(-t * decay);
            }
            return o;
        }

        static float[] Shaker()
        {
            var o = Buf(0.09f); float prev = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                float nz = Noise(); float hp = nz - prev; prev = nz;
                o[i] = hp * 0.4f * Mathf.Min(1f, t / 0.015f) * Mathf.Exp(-t * 35f);
            }
            return o;
        }

        static float[] Clap()
        {
            var o = Buf(0.3f); float prev = 0, bp = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                float nz = Noise(); float hp = nz - prev; prev = nz; bp += 0.5f * (hp - bp);
                float env = t < 0.03f ? Mathf.Exp(-(t % 0.01f) * 250f) : Mathf.Exp(-(t - 0.02f) * 16f);
                o[i] = bp * env * 0.8f;
            }
            return o;
        }

        static float[] Crash()
        {
            var o = Buf(1.8f); float prev = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                float nz = Noise(); float hp = nz - prev; prev = nz;
                o[i] = hp * 0.4f * Mathf.Exp(-t * 2.2f);
            }
            return o;
        }

        static float[] Crackle(float sec)
        {
            var o = Buf(sec); float lp = 0;
            for (int i = 0; i < o.Length; i++)
            {
                lp += 0.02f * (Noise() - lp);
                o[i] = lp * 0.15f + (rng.NextDouble() < 0.0004 ? Noise() * 0.6f : 0f);
            }
            return o;
        }

        // ── 악보 ───────────────────────────────────────────────
        static int Note(string s)
        {
            int[] semi = { 9, 11, 0, 2, 4, 5, 7 }; // A B C D E F G
            int k = 0;
            int n = semi[char.ToUpper(s[k++]) - 'A'];
            if (k < s.Length && s[k] == '#') { n++; k++; } else if (k < s.Length && s[k] == 'b') { n--; k++; }
            int oct = int.Parse(s.Substring(k));
            return 12 * (oct + 1) + n;
        }

        static int[] Chord(string s) { var p = s.Split(' '); var r = new int[p.Length]; for (int i = 0; i < p.Length; i++) r[i] = Note(p[i]); return r; }

        // "E5:2 r:1 C5:1 | ..." 를 칸 단위로 연주. time(칸) → 초
        static int Seq(string s, int step0, Func<float, float> time, Action<float, float, int> play)
        {
            int step = step0;
            foreach (var tok in s.Split(new[] { ' ', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = tok.Split(':');
                int len = p.Length > 1 ? int.Parse(p[1]) : 1;
                if (p[0] != "r") { float a = time(step), b = time(step + len); play(a, b - a, Note(p[0])); }
                step += len;
            }
            return step;
        }

        // ── 02 오르골 왈츠 (D단조, 3/4, 168) ────────────────────
        static Track Waltz()
        {
            const float bpm = 168f; float beat = 60f / bpm, e = beat / 2f; const int bars = 32;
            var t = new Track(bars * 3 * beat);
            Func<float, float> time = s => s * e;
            string mel =
                "A4:2 D5:2 F5:2 | E5:2 D5:2 A4:2 | C#5:2 E5:2 G5:2 | G5:2 F5:1 E5:1 C#5:2 |" +
                "D5:2 G5:2 Bb5:2 | A5:3 G5:1 F5:2 | E5:2 G5:2 C#5:2 | D5:4 r:2 |" +
                "F5:2 Bb5:2 D6:2 | C6:2 A5:2 F5:2 | Bb5:2 G5:2 D5:2 | C#5:2 E5:2 A5:2 |" +
                "F5:2 E5:1 F5:1 A5:2 | G5:2 F5:1 G5:1 Bb5:2 | A5:2 G5:2 C#5:2 | D5:4 r:2";
            string[] bass = { "D3", "A2", "A2", "E3", "G2", "D3", "A2", "D3", "Bb2", "F2", "G2", "A2", "D3", "G2", "A2", "D3" };
            string[] chords = { "A3 D4 F4", "A3 D4 F4", "G3 C#4 E4", "G3 C#4 E4", "G3 Bb3 D4", "A3 D4 F4", "G3 C#4 E4", "A3 D4 F4",
                                "Bb3 D4 F4", "A3 C4 F4", "G3 Bb3 D4", "A3 C#4 E4", "A3 D4 F4", "G3 Bb3 D4", "G3 C#4 E4", "F3 A3 D4" };
            for (int pass = 0; pass < 2; pass++)
            {
                int s0 = pass * 16 * 6;
                Seq(mel, s0, time, (a, d, n) =>
                {
                    t.Add(a, MusicBox(Midi(n), d), 0.30f, 0.1f, 0.35f);
                    if (pass == 1) t.Add(a, Bell(Midi(n + 12), d), 0.07f, 0.4f, 0.4f);
                });
                for (int b = 0; b < 16; b++)
                {
                    float b0 = (s0 + b * 6) * e;
                    t.Add(b0, Pluck(Midi(Note(bass[b])), beat * 0.9f, 0.45f, 0.995f), 0.55f, -0.15f, 0.2f);
                    foreach (int n in Chord(chords[b]))
                        for (int k = 1; k < 3; k++)
                            t.Add(b0 + k * beat, Pluck(Midi(n), beat * 0.5f, 0.6f, 0.99f), 0.16f, -0.35f + 0.1f * k, 0.25f);
                    if (pass == 1)
                    {
                        foreach (int n in Chord(chords[b])) t.Add(b0, Pad(Midi(n), 3 * beat), 0.05f, 0.3f, 0.5f);
                        t.Add(b0 + beat, Shaker(), 0.12f, 0.5f, 0.1f); t.Add(b0 + 2 * beat, Shaker(), 0.12f, 0.5f, 0.1f);
                    }
                }
            }
            return t;
        }

        // ── 03 아늑한 선술집 로파이 (C장조 재즈, 100, 스윙) ──────
        static Track Tavern()
        {
            const float bpm = 100f; float beat = 60f / bpm, e = beat / 2f; const int bars = 12;
            var t = new Track(bars * 4 * beat);
            Func<float, float> time = s => { int pair = (int)s / 2; float frac = s - pair * 2; return (pair * 2 + (frac >= 1f ? 1.24f + (frac - 1f) * 0.76f : frac * 1.24f)) * e; };
            string A = "E5:2 C5:1 A4:1 C5:2 E5:2 | D5:3 B4:1 G4:2 r:2 | F5:2 E5:1 D5:1 C5:2 A4:2 | B4:3 D5:1 G5:2 F5:2";
            string B = "A5:2 G5:1 E5:1 C5:2 r:1 C5:1 | B4:2 D5:1 E5:1 G5:3 r:1 | F5:1 E5:1 D5:1 C5:1 A4:2 C5:2 | D5:4 B4:2 G4:2";
            int st = 0;
            foreach (var part in new[] { A, B, A })
                st = Seq(part, st, time, (a, d, n) => t.Add(a, Pluck(Midi(n), d * 0.95f, 0.35f, 0.997f), 0.42f, 0.3f, 0.3f));
            string[] bass = { "F2", "E2", "D2", "G2" };
            string[] chords = { "F3 A3 C4 E4", "E3 G3 B3 D4", "D3 F3 A3 C4", "F3 G3 B3 D4" };
            for (int b = 0; b < bars; b++)
            {
                int s0 = b * 8; int r = Note(bass[b % 4]);
                foreach (int n in Chord(chords[b % 4]))
                {
                    t.Add(time(s0), Rhodes(Midi(n), beat * 1.4f), 0.07f, -0.3f, 0.25f);
                    t.Add(time(s0 + 3), Rhodes(Midi(n), beat * 0.9f), 0.05f, -0.3f, 0.25f);
                    if (b % 2 == 1) t.Add(time(s0 + 6), Rhodes(Midi(n + 12), beat * 0.5f), 0.025f, -0.4f, 0.3f);
                }
                t.Add(time(s0), Bass(Midi(r), beat * 1.3f), 0.32f, 0f, 0.02f);
                t.Add(time(s0 + 3), Bass(Midi(r), beat * 0.4f), 0.24f, 0f, 0.02f);
                t.Add(time(s0 + 6), Bass(Midi(r + 7), beat * 0.8f), 0.26f, 0f, 0.02f);
                t.Add(time(s0), Kick(0.6f), 0.4f, 0f, 0.02f);
                t.Add(time(s0 + 5), Kick(0.6f), 0.3f, 0f, 0.02f);
                t.Add(time(s0 + 2), Brush(), 0.35f, 0.1f, 0.15f);
                t.Add(time(s0 + 6), Brush(), 0.35f, 0.1f, 0.15f);
                for (int k = 0; k < 8; k++) t.Add(time(s0 + k), Hat(70f), k % 2 == 0 ? 0.10f : 0.06f, 0.35f, 0.05f);
            }
            t.Add(0, Crackle(bars * 4 * beat), 0.25f, 0f, 0f);
            return t;
        }

        // ── 04 손님 만원! 밝은 경영 (F장조, 126) ────────────────
        static Track Tycoon()
        {
            const float bpm = 126f; float beat = 60f / bpm, e = beat / 2f; const int bars = 16;
            var t = new Track(bars * 4 * beat);
            Func<float, float> time = s => s * e;
            string m1 = "A4:1 C5:1 F5:2 E5:1 F5:1 G5:2 | E5:2 C5:2 G4:2 C5:2 | D5:1 F5:1 A5:2 G5:1 F5:1 E5:1 D5:1 | F5:4 D5:2 Bb4:2 |" +
                        "A4:1 C5:1 F5:2 E5:1 F5:1 A5:2 | G5:2 E5:1 C5:1 G5:2 C6:2 | Bb5:2 A5:1 G5:1 F5:2 D5:2 |";
            string end1 = "E5:2 G5:2 C5:4", end2 = "C5:2 E5:2 G5:2 Bb5:2";
            int st = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int p = pass;
                st = Seq(m1 + (pass == 0 ? end1 : end2), st, time, (a, d, n) =>
                {
                    t.Add(a, Marimba(Midi(n), d), 0.34f, 0.1f, 0.18f);
                    if (p == 1) t.Add(a, Bell(Midi(n + 12), d), 0.06f, 0.45f, 0.3f);
                });
            }
            string[] bass = { "F2", "C3", "D3", "Bb2", "F2", "C3", "Bb2", "C3" };
            string[] chords = { "F3 A3 C4", "E3 G3 C4", "D3 F3 A3", "D3 F3 Bb3", "F3 A3 C4", "E3 G3 C4", "D3 F3 Bb3", "E3 G3 C4" };
            for (int b = 0; b < bars; b++)
            {
                int s0 = b * 8; int r = Note(bass[b % 8]);
                int[] pat = { 0, 12, 0, 7 }; int[] at = { 0, 3, 4, 6 };
                for (int k = 0; k < 4; k++) t.Add(time(s0 + at[k]), Bass(Midi(r + pat[k]), e * (k == 1 ? 0.8f : 1.6f), 2f), 0.3f, 0f, 0.03f);
                foreach (int n in Chord(chords[b % 8]))
                    for (int k = 1; k < 8; k += 2) t.Add(time(s0 + k), Pluck(Midi(n + 12), e * 0.6f, 0.7f, 0.985f), 0.12f, -0.4f, 0.15f);
                t.Add(time(s0), Kick(), 0.45f, 0f, 0.02f); t.Add(time(s0 + 4), Kick(), 0.45f, 0f, 0.02f);
                if (b % 2 == 1) t.Add(time(s0 + 7), Kick(0.8f), 0.3f, 0f, 0.02f);
                t.Add(time(s0 + 2), Clap(), 0.32f, 0.05f, 0.2f); t.Add(time(s0 + 6), Clap(), 0.32f, 0.05f, 0.2f);
                for (int k = 0; k < 16; k++) t.Add(time(s0 + k * 0.5f), Shaker(), k % 2 == 1 ? 0.14f : 0.07f, 0.4f, 0.03f);
                if (b == 0 || b == 8) t.Add(time(s0), Crash(), 0.12f, -0.2f, 0.2f);
            }
            return t;
        }

        // ── 05 몬스터 서커스 (E단조, 132) ───────────────────────
        static Track Carnival()
        {
            const float bpm = 132f; float beat = 60f / bpm, e = beat / 2f; const int bars = 16;
            var t = new Track(bars * 4 * beat);
            Func<float, float> time = s => s * e;
            string mel = "B4:1 A#4:1 B4:1 C5:1 B4:2 G4:2 | E4:1 F#4:1 G4:1 A4:1 B4:4 | A4:1 G#4:1 A4:1 B4:1 A4:2 F#4:2 | D#4:1 E4:1 F#4:1 G4:1 A4:4 |" +
                         "G4:1 F#4:1 G4:1 A4:1 B4:2 E5:2 | D5:1 C5:1 B4:1 A4:1 G4:2 E4:2 | C5:1 B4:1 A4:1 G4:1 F#4:2 A4:2 | B4:2 D#5:2 F#5:2 r:2";
            int st = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int p = pass;
                st = Seq(mel, st, time, (a, d, n) =>
                {
                    t.Add(a, Accordion(Midi(n + (p == 1 ? 12 : 0)), d * 0.85f, p == 1 ? 0.006f : 0.004f), 0.30f, 0.15f, 0.2f);
                    if (p == 1) t.Add(a, Bell(Midi(n + 12), d), 0.08f, -0.4f, 0.3f);
                });
            }
            string[] roots = { "E2", "E2", "B1", "B1", "E2", "E2", "A2", "B1" };
            string[] fifths = { "B2", "B2", "F#2", "F#2", "B2", "B2", "E2", "F#2" };
            string[] chords = { "E3 G3 B3", "E3 G3 B3", "D#3 A3 B3", "D#3 A3 B3", "E3 G3 B3", "E3 G3 B3", "E3 A3 C4", "D#3 A3 B3" };
            for (int b = 0; b < bars; b++)
            {
                int s0 = b * 8;
                t.Add(time(s0), Tuba(Midi(Note(roots[b % 8]) + 12), beat * 0.8f), 0.55f, 0f, 0.1f);
                t.Add(time(s0 + 4), Tuba(Midi(Note(fifths[b % 8]) + 12), beat * 0.8f), 0.55f, 0f, 0.1f);
                foreach (int n in Chord(chords[b % 8]))
                {
                    t.Add(time(s0 + 2), Accordion(Midi(n), beat * 0.35f, 0.002f), 0.10f, -0.35f, 0.15f);
                    t.Add(time(s0 + 6), Accordion(Midi(n), beat * 0.35f, 0.002f), 0.10f, -0.35f, 0.15f);
                }
                t.Add(time(s0), Kick(0.7f), 0.35f, 0f, 0.02f); t.Add(time(s0 + 4), Kick(0.7f), 0.35f, 0f, 0.02f);
                t.Add(time(s0 + 2), Snare(220f, 20f), 0.22f, 0.1f, 0.15f); t.Add(time(s0 + 6), Snare(220f, 20f), 0.22f, 0.1f, 0.15f);
                for (int k = 0; k < 8; k++) t.Add(time(s0 + k), Hat(60f), k % 2 == 1 ? 0.08f : 0.05f, 0.4f, 0.03f);
                if (b % 8 == 7) for (int k = 0; k < 8; k++) t.Add(time(s0 + 4 + k * 0.5f), Snare(220f, 30f), 0.08f + 0.02f * k, 0.1f, 0.15f);
                if (b == 0 || b == 8) t.Add(time(s0), Crash(), 0.18f, 0.3f, 0.2f);
            }
            return t;
        }

        // ── WAV ────────────────────────────────────────────────
        static void WriteWav(string path, float[] L, float[] R, int rate)
        {
            int n = L.Length;
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 4);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)2);
                w.Write(rate); w.Write(rate * 4); w.Write((short)4); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(n * 4);
                for (int i = 0; i < n; i++)
                {
                    w.Write((short)Mathf.Clamp(L[i] * 32767f, -32768f, 32767f));
                    w.Write((short)Mathf.Clamp(R[i] * 32767f, -32768f, 32767f));
                }
            }
        }
    }
}
