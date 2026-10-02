using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mawang
{
    // 배경음과 효과음. 배경음은 구워 둔 음원(Resources/Audio/bgm)을 쓰고, 효과음은 실행할 때 칩튠으로 합성한다.
    // 음량은 설정 창에서 조절하고 PlayerPrefs 에 저장한다.
    public class Sound : MonoBehaviour
    {
        const int Rate = 22050;
        static Sound inst;
        AudioSource bgm, sfx;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

        public static float BgmVolume
        {
            get => PlayerPrefs.GetFloat("vol_bgm", 0.6f);
            set { PlayerPrefs.SetFloat("vol_bgm", Mathf.Clamp01(value)); if (inst != null) inst.bgm.volume = Mathf.Clamp01(value) * 0.55f; }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat("vol_sfx", 0.8f);
            set => PlayerPrefs.SetFloat("vol_sfx", Mathf.Clamp01(value));
        }

        public static void Init()
        {
            if (inst != null) return;
            var go = new GameObject("Sound");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<Sound>();
            inst.Build();
        }

        void Build()
        {
            bgm = gameObject.AddComponent<AudioSource>();
            bgm.loop = true;
            bgm.playOnAwake = false;
            bgm.volume = BgmVolume * 0.55f;
            // 배경음: Resources/Audio/bgm (몬스터 서커스, 에디터 BgmLab 으로 합성해 구움). 없으면 예전 칩튠을 합성한다
            var baked = Resources.Load<AudioClip>("Audio/bgm");
            bgm.clip = baked != null ? baked : MakeBgm();
            bgm.Play();
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;

            clips["click"] = Synth(0.05f, t => Sq(Lerp(880, 1250, t / 0.05f), t, 0.5f) * Env(t, 0.002f, 0.05f) * 0.35f);
            clips["open"] = Synth(0.09f, t => Tri(Lerp(520, 820, t / 0.09f), t) * Env(t, 0.004f, 0.09f) * 0.5f);
            clips["coin"] = Synth(0.2f, t => Sq(t < 0.06f ? 988 : 1319, t, 0.5f) * Env(t, 0.002f, 0.2f) * 0.28f);
            clips["sale"] = Synth(0.12f, t => Sq(t < 0.04f ? 1319 : 1760, t, 0.25f) * Env(t, 0.002f, 0.12f) * 0.16f);
            clips["gem"] = Synth(0.35f, t => (Tri(t < 0.08f ? 1568 : t < 0.16f ? 2093 : 2637, t) * 0.6f + Sq(2637, t, 0.125f) * 0.1f) * Env(t, 0.003f, 0.35f) * 0.5f);
            clips["pop"] = Synth(0.09f, t => Tri(Lerp(380, 950, t / 0.09f), t) * Env(t, 0.002f, 0.09f) * 0.55f);
            clips["build"] = Synth(0.32f, t => (Tri(Lerp(160, 55, t / 0.2f), t) * Env(t, 0.002f, 0.22f) * 0.7f
                                              + Noise() * Env(t, 0.001f, 0.08f) * 0.25f
                                              + Sq(1568, t, 0.25f) * (t > 0.16f ? Env(t - 0.16f, 0.002f, 0.16f) : 0f) * 0.12f));
            clips["error"] = Synth(0.2f, t => Sq(t < 0.09f ? 196 : 147, t, 0.5f) * Env(t, 0.002f, 0.2f) * 0.25f);
            clips["upgrade"] = Synth(0.42f, t =>
            {
                int step = Mathf.Min(3, (int)(t / 0.07f));
                float f = new[] { 523f, 659f, 784f, 1047f }[step];
                return Sq(f, t, 0.25f) * Env(t - step * 0.07f, 0.002f, step == 3 ? 0.21f : 0.07f) * 0.25f;
            });
            clips["catch"] = Synth(0.25f, t => (Tri(Lerp(300, 1200, Mathf.Clamp01(t / 0.12f)), t) * 0.6f + Noise() * Env(t, 0.001f, 0.04f) * 0.2f) * Env(t, 0.002f, 0.25f) * 0.5f);
        }

        // 너무 잦은 소리(판매 등)는 간격을 둔다
        public static void Play(string name, float volume = 1f)
        {
            if (inst == null || !inst.clips.TryGetValue(name, out var clip)) return;
            float now = Time.unscaledTime;
            float gap = name == "sale" || name == "coin" ? 0.09f : 0.03f;
            if (inst.lastPlayed.TryGetValue(name, out var last) && now - last < gap) return;
            inst.lastPlayed[name] = now;
            inst.sfx.PlayOneShot(clip, SfxVolume * volume);
        }

        // ── 합성 ───────────────────────────────────────────────
        static readonly System.Random rng = new System.Random(7);
        static float Noise() => (float)(rng.NextDouble() * 2 - 1);
        static float Lerp(float a, float b, float t) => a + (b - a) * Mathf.Clamp01(t);
        static float Sq(float f, float t, float duty) => (f * t) % 1f < duty ? 1f : -1f;
        static float Tri(float f, float t) { float p = (f * t) % 1f; return 4f * Mathf.Abs(p - 0.5f) - 1f; }
        // 짧은 어택 + 직선 감쇠
        static float Env(float t, float attack, float len) => t < 0 || t > len ? 0f : t < attack ? t / attack : 1f - (t - attack) / Mathf.Max(0.0001f, len - attack);

        static AudioClip Synth(float len, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create("sfx", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Midi(int n) => 440f * Mathf.Pow(2f, (n - 69) / 12f);

        // 16마디 루프 (144 BPM): 가단조의 으스스하지만 귀여운 분위기는 그대로, 통통 튀는 스타카토 멜로디 + 8분음표 베이스 + 드럼
        // A: Am F G E | Am Dm E Am   B: F G Em Am | Dm Am E E
        static AudioClip MakeBgm()
        {
            const float bpm = 144f;
            float eighth = 60f / bpm / 2f;
            // (음, 8분음표 길이). 0 = 쉼표. 마디당 8칸
            int[,] mel =
            {
                {76,1},{69,1},{72,1},{76,1},{74,1},{72,1},{71,1},{72,1},
                {69,2},{65,1},{69,1},{72,2},{69,1},{72,1},
                {71,1},{74,1},{79,2},{77,1},{76,1},{74,2},
                {76,2},{68,1},{71,1},{76,3},{0,1},
                {81,1},{79,1},{76,1},{72,1},{76,2},{69,2},
                {77,1},{76,1},{74,1},{69,1},{74,2},{77,2},
                {76,1},{74,1},{72,1},{71,1},{68,2},{71,2},
                {69,2},{76,2},{81,2},{0,2},
                {72,1},{77,1},{81,1},{77,1},{72,1},{77,1},{81,2},
                {71,1},{74,1},{79,1},{74,1},{71,1},{74,1},{79,2},
                {71,1},{76,1},{79,1},{83,1},{81,1},{79,1},{76,2},
                {81,2},{76,1},{72,1},{69,2},{72,2},
                {74,1},{77,1},{81,2},{79,1},{77,1},{76,1},{74,1},
                {72,1},{76,1},{81,2},{79,1},{76,1},{72,2},
                {71,1},{72,1},{74,1},{76,1},{77,1},{76,1},{74,1},{71,1},
                {68,2},{71,2},{76,2},{0,2},
            };
            int[] roots = { 45, 41, 43, 40, 45, 38, 40, 45, 41, 43, 40, 45, 38, 45, 40, 40 };
            bool[] minor = { true, false, false, false, true, true, false, true, false, false, true, true, true, true, false, false };
            const int bars = 16;
            float total = bars * 8 * eighth;
            int n = Mathf.CeilToInt(total * Rate);
            var data = new float[n];

            void Add(float start, float len, Func<float, float> f)
            {
                int i0 = (int)(start * Rate), i1 = Mathf.Min(n, (int)((start + len) * Rate));
                for (int i = i0; i < i1; i++) data[i] += f((i - i0) / (float)Rate);
            }

            // 멜로디: 짧게 끊어 치는 사각파 (음 길이의 75%만 울린다)
            float t0 = 0;
            for (int k = 0; k < mel.GetLength(0); k++)
            {
                int note = mel[k, 0];
                float len = mel[k, 1] * eighth;
                if (note > 0)
                {
                    float f = Midi(note), on = len * 0.75f;
                    Add(t0, on, t =>
                    {
                        float env = Mathf.Min(1f, t / 0.005f) * Mathf.Lerp(1f, 0.6f, t / on) * Mathf.Clamp01((on - t) / 0.02f);
                        return Sq(f, t, 0.25f) * env * 0.085f + Sq(f * 2f, t, 0.5f) * env * 0.012f;
                    });
                }
                t0 += len;
            }

            float beat = eighth * 2f;
            for (int bar = 0; bar < bars; bar++)
            {
                float b0 = bar * 4 * beat;
                // 베이스: 8분음표로 근음-옥타브를 번갈아 퉁긴다
                for (int e = 0; e < 8; e++)
                {
                    float f = Midi(roots[bar] + (e % 2 == 1 ? 12 : 0));
                    float len = eighth * 0.7f;
                    Add(b0 + e * eighth, len, t => Tri(f, t) * Mathf.Clamp01(1f - t / len) * 0.17f);
                }
                for (int b = 0; b < 4; b++)
                {
                    float s0 = b0 + b * beat;
                    // 킥: 1·3박 (+ 마디 끝 당김음)
                    if (b % 2 == 0 || (b == 3 && bar % 2 == 1))
                    {
                        float ks = b == 3 ? s0 + eighth : s0;
                        Add(ks, 0.12f, t => Mathf.Sin(2f * Mathf.PI * Lerp(150f, 45f, t / 0.08f) * t) * (1f - t / 0.12f) * 0.32f);
                    }
                    // 스네어: 2·4박
                    if (b % 2 == 1)
                        Add(s0, 0.1f, t => (Noise() * 0.7f + Tri(190f, t) * 0.3f) * (1f - t / 0.1f) * 0.12f);
                    // 하이햇: 8분음표마다 (뒷박은 조금 크게)
                    Add(s0, 0.02f, t => Noise() * (1f - t / 0.02f) * 0.025f);
                    Add(s0 + eighth, 0.03f, t => Noise() * (1f - t / 0.03f) * 0.04f);
                }
                // 아르페지오: 16분음표로 반짝이게 (아주 작게)
                int r = roots[bar] + 24;
                int[] chord = { r, r + (minor[bar] ? 3 : 4), r + 7, r + 12 };
                for (int s = 0; s < 16; s++)
                {
                    float f = Midi(chord[(s % 8) < 4 ? s % 4 : 3 - s % 4]);
                    float len = eighth / 2f;
                    Add(b0 + s * len, len, t => Tri(f, t) * (1f - t / len) * 0.022f);
                }
            }

            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create("bgm", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
