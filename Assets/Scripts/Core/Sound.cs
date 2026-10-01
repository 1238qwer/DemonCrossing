using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mawang
{
    // 배경음과 효과음. 음원 파일 없이 실행할 때 칩튠으로 합성한다.
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
            bgm.clip = MakeBgm();
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

        // 8마디 루프: 가단조의 으스스하지만 귀여운 멜로디 (Am F G E | Am Dm E Am)
        static AudioClip MakeBgm()
        {
            const float bpm = 108f;
            float eighth = 60f / bpm / 2f;
            // (음, 8분음표 길이). 0 = 쉼표
            int[,] mel =
            {
                {69,2},{72,1},{76,1},{74,2},{72,2},   {69,2},{65,2},{69,1},{72,1},{71,2},
                {67,2},{71,1},{74,1},{72,2},{71,2},   {68,3},{71,1},{76,4},
                {76,2},{74,1},{72,1},{71,2},{69,2},   {77,2},{76,1},{74,1},{69,2},{74,2},
                {76,1},{74,1},{72,1},{71,1},{68,2},{71,2}, {69,6},{0,2},
            };
            int[] roots = { 45, 41, 43, 40, 45, 38, 40, 45 };
            int[] fifths = { 52, 48, 50, 47, 52, 45, 47, 52 };
            float total = 8 * 8 * eighth;
            int n = Mathf.CeilToInt(total * Rate);
            var data = new float[n];

            // 멜로디
            float start = 0;
            for (int k = 0; k < mel.GetLength(0); k++)
            {
                int note = mel[k, 0];
                float len = mel[k, 1] * eighth;
                if (note > 0)
                {
                    float f = Midi(note);
                    int i0 = (int)(start * Rate), i1 = Mathf.Min(n, (int)((start + len) * Rate));
                    for (int i = i0; i < i1; i++)
                    {
                        float t = (i - i0) / (float)Rate;
                        float vib = 1f + 0.004f * Mathf.Sin(t * 30f) * Mathf.Clamp01(t * 4f);
                        float env = Mathf.Min(1f, t / 0.01f) * Mathf.Lerp(1f, 0.55f, t / len) * Mathf.Clamp01((len - t) / 0.03f);
                        data[i] += Sq(f * vib, t, 0.25f) * env * 0.09f + Sq(f * 2f, t, 0.5f) * env * 0.015f;
                    }
                }
                start += len;
            }

            // 베이스: 박마다 근음-5음
            float beat = eighth * 2f;
            for (int bar = 0; bar < 8; bar++)
                for (int b = 0; b < 4; b++)
                {
                    float f = Midi(b % 2 == 0 ? roots[bar] : fifths[bar]);
                    float s0 = (bar * 4 + b) * beat;
                    int i0 = (int)(s0 * Rate), i1 = Mathf.Min(n, (int)((s0 + beat * 0.9f) * Rate));
                    for (int i = i0; i < i1; i++)
                    {
                        float t = (i - i0) / (float)Rate;
                        data[i] += Tri(f, t) * Mathf.Clamp01(1f - t / (beat * 0.9f)) * 0.16f;
                    }
                    // 뒷박 하이햇
                    int h0 = (int)((s0 + eighth) * Rate), h1 = Mathf.Min(n, h0 + (int)(0.03f * Rate));
                    for (int i = h0; i < h1; i++) data[i] += Noise() * (1f - (i - h0) / (float)(h1 - h0)) * 0.035f;
                }

            // 아르페지오 패드 (16분음표, 아주 작게)
            for (int bar = 0; bar < 8; bar++)
            {
                int r = roots[bar] + 24;
                bool minor = bar != 1 && bar != 2 && bar != 3 && bar != 6; // F, G, E 는 장3화음
                int[] chord = { r, r + (minor ? 3 : 4), r + 7, r + 12 };
                for (int s = 0; s < 16; s++)
                {
                    float f = Midi(chord[s % 4]);
                    float s0 = bar * 8 * eighth + s * eighth / 2f;
                    int i0 = (int)(s0 * Rate), i1 = Mathf.Min(n, i0 + (int)(eighth / 2f * Rate));
                    for (int i = i0; i < i1; i++)
                    {
                        float t = (i - i0) / (float)Rate;
                        data[i] += Tri(f, t) * (1f - t / (eighth / 2f)) * 0.025f;
                    }
                }
            }

            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create("bgm", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
