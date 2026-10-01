using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Mawang
{
    // 포획 스테이지 장면 (원작: 낚시 탭).
    // 배경 도트 좌표계(276×108)를 그대로 쓰는 world 판을 정수배로 키워 그린다.
    // 게임 상태(포획대원 타이머, 미리 굴린 괴물, 바닥 쓰레기)를 매 프레임 읽어 연출만 한다 → 언제 열어도 상태와 일치.
    public class HuntScene : MonoBehaviour
    {
        const int W = GameData.StageW, H = GameData.StageH;
        const float WalkEnd = 0.55f, FightStart = 0.62f;
        // 캐릭터 크기: CharacterSettings (포획장 배율 × 개별 배율). 기본은 배경 도트의 2배.
        static float HunterScale(string id) => CharacterSettings.I.huntHunterScale * CharacterSettings.I.Scale(id);
        static float MonsterScale(string id) => CharacterSettings.I.huntMonsterScale * CharacterSettings.I.Scale(id);
        static readonly float[] HitAt = { 0.68f, 0.8f, 0.92f };

        Game g;
        RectTransform host, world, bgLayer, ambientLayer, trashLayer, actorLayer, fxLayer, overlay;
        Image bg;
        float scale = 4f;
        RegionDef region;

        class Particle { public RectTransform rt; public Vector2 pos, vel; public float phase; public Image img; }
        readonly List<Particle> particles = new List<Particle>();

        class MonsterActor
        {
            public string id;
            public MonsterDef def;
            public RectTransform rt;
            public Image img;
            public float hitFlash, knock;
            public float A => MonsterScale(id);
            public int lastHit = -1;
            public RectTransform aura;
        }

        class HunterActor
        {
            public HunterState h;
            public StaffDef def;
            public RectTransform rt, bait;
            public Image img, baitImg;
            public Text tag;
            public RectTransform barFill;
            public float lunge, zTimer;
            public float A => HunterScale(def.id);
            public readonly List<MonsterActor> monsters = new List<MonsterActor>();
            public Vector2 home;
        }
        readonly Dictionary<HunterState, HunterActor> hunters = new Dictionary<HunterState, HunterActor>();

        // 단발성 연출: 매 프레임 t(0→1)로 갱신, 끝나면 정리
        class Fx { public float t, dur; public Action<float> step; public Action done; }
        readonly List<Fx> fxs = new List<Fx>();

        readonly Dictionary<int, (TrashItem item, RectTransform rt)> trash = new Dictionary<int, (TrashItem, RectTransform)>();
        float shake;

        public RegionDef Region => region;

        // ── 초기화 ─────────────────────────────────────────────
        public void Init(RectTransform hostRect)
        {
            g = Game.I;
            host = hostRect;
            host.gameObject.AddComponent<RectMask2D>();

            world = UIKit.Rect("World", host);
            world.anchorMin = world.anchorMax = world.pivot = Vector2.zero;
            world.sizeDelta = new Vector2(W, H);
            bgLayer = Layer("Bg"); ambientLayer = Layer("Ambient"); trashLayer = Layer("Trash"); actorLayer = Layer("Actors"); fxLayer = Layer("Fx");

            bg = bgLayer.gameObject.AddComponent<Image>();
            bg.raycastTarget = false;

            overlay = UIKit.Rect("Overlay", host);
            UIKit.Stretch(overlay);

            g.HuntCaught += OnCaught;
            g.TrashCollected += OnTrashCollected;
        }

        void OnDestroy()
        {
            if (g == null) return;
            g.HuntCaught -= OnCaught;
            g.TrashCollected -= OnTrashCollected;
        }

        RectTransform Layer(string name)
        {
            var r = UIKit.Rect(name, world);
            r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
            r.sizeDelta = new Vector2(W, H);
            return r;
        }

        public void SetRegion(RegionDef r)
        {
            if (region == r) return;
            region = r;
            bg.sprite = Art.Get(r.bg);
            foreach (var a in hunters.Values) Destroy(a.rt.gameObject);
            hunters.Clear();
            foreach (var t in trash.Values) Destroy(t.rt.gameObject);
            trash.Clear();
            // 끝 콜백이 새 연출을 추가할 수 있으므로 복사본으로 돈다
            var pending = fxs.ToArray();
            fxs.Clear();
            foreach (var f in pending) f.done?.Invoke();
            fxs.Clear();
            ClearChildren(fxLayer); ClearChildren(overlay); ClearChildren(actorLayer);
            foreach (var p in particles) Destroy(p.rt.gameObject);
            particles.Clear();
            for (int i = 0; i < 36; i++) particles.Add(NewParticle(true));
        }

        static void ClearChildren(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject); }

        // ── 도트 좌표계 헬퍼 ───────────────────────────────────
        static Image Spr(Transform parent, string sprite, Vector2 pos, float s = 1f, Vector2? pivot = null)
        {
            var img = UIKit.Rect(sprite, parent).gameObject.AddComponent<Image>();
            img.sprite = Art.Get(sprite);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = pivot ?? new Vector2(0.5f, 0f);
            rt.sizeDelta = img.sprite != null ? img.sprite.rect.size : new Vector2(8, 8);
            rt.localScale = new Vector3(s, s, 1);
            rt.anchoredPosition = pos;
            return img;
        }

        Vector2 ToOverlay(Vector2 dot) => dot * scale;

        Text FloatText(Vector2 dot, string text, Color color, int size = UIKit.TS, float rise = 60f, float dur = 1.1f)
        {
            var t = UIKit.Label(overlay, text, size, TextAnchor.LowerCenter, color);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(300, 30);
            Vector2 start = ToOverlay(dot);
            Play(dur, k =>
            {
                if (t == null) return;
                rt.anchoredPosition = start + new Vector2(0, rise * (1f - (1f - k) * (1f - k)));
                var c = t.color; c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f; t.color = c;
            }, () => { if (t != null) Destroy(t.gameObject); });
            return t;
        }

        void Play(float dur, Action<float> step, Action done = null)
        {
            fxs.Add(new Fx { dur = dur, step = step, done = done });
            step(0);
        }

        // 날아가는 이펙트 이미지
        void FxSprite(string sprite, Vector2 from, Vector2 to, float dur, Color color, float s0, float s1, float spin = 0, float arc = 0)
        {
            var img = Spr(fxLayer, sprite, from, s0, new Vector2(0.5f, 0.5f));
            img.color = color;
            var rt = img.rectTransform;
            Play(dur, k =>
            {
                if (rt == null) return;
                var p = Vector2.Lerp(from, to, k) + new Vector2(0, arc * 4f * k * (1f - k));
                rt.anchoredPosition = p;
                float s = Mathf.Lerp(s0, s1, k);
                rt.localScale = new Vector3(s, s, 1);
                rt.localEulerAngles = new Vector3(0, 0, spin * k);
                var c = color; c.a = color.a * (k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f); img.color = c;
            }, () => { if (rt != null) Destroy(rt.gameObject); });
        }

        void Sparkles(Vector2 at, Color color, int n = 6, float radius = 16f)
        {
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + Random.value * 0.5f;
                FxSprite("fx_spark", at, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * Random.Range(0.6f, 1f), 0.5f, color, 0.4f, 0.1f);
            }
        }

        // ── 매 프레임 ──────────────────────────────────────────
        void Update()
        {
            if (region == null) return;
            float dt = Time.deltaTime;

            // 호스트 크기에 맞춰 정수 배율
            float sx = Mathf.Min(host.rect.width / W, host.rect.height / H);
            scale = sx >= 1f ? Mathf.Max(1f, Mathf.Floor(sx * 4f) / 4f) : sx;
            shake = Mathf.Max(0, shake - dt);
            Vector2 jitter = shake > 0 ? new Vector2(Random.Range(-1, 2), Random.Range(-1, 2)) : Vector2.zero;
            world.localScale = new Vector3(scale, scale, 1);
            world.anchoredPosition = new Vector2((host.rect.width - W * scale) / 2f, (host.rect.height - H * scale) / 2f) + jitter * scale;
            overlay.anchoredPosition = world.anchoredPosition;

            UpdateParticles(dt);
            SyncHunters(dt);
            SyncTrash();

            for (int i = fxs.Count - 1; i >= 0; i--)
            {
                var f = fxs[i];
                f.t += dt;
                float k = Mathf.Clamp01(f.t / f.dur);
                f.step(k);
                if (k >= 1f) { fxs.RemoveAt(i); f.done?.Invoke(); }
            }
        }

        // ── 떠다니는 입자(눈, 불티, 거품…) ─────────────────────
        Particle NewParticle(bool anywhere)
        {
            var img = UIKit.Rect("P", ambientLayer).gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var p = new Particle { rt = img.rectTransform, img = img, phase = Random.value * 10f };
            p.rt.anchorMin = p.rt.anchorMax = p.rt.pivot = Vector2.zero;
            float sz = Random.value < 0.25f ? 2 : 1;
            p.rt.sizeDelta = new Vector2(sz, sz);
            ResetParticle(p, anywhere);
            return p;
        }

        void ResetParticle(Particle p, bool anywhere)
        {
            var v = region.ambientVel;
            p.vel = v * Random.Range(0.6f, 1.4f);
            if (anywhere) p.pos = new Vector2(Random.Range(0, W), Random.Range(0, H));
            else if (Mathf.Abs(v.y) >= Mathf.Abs(v.x) && v.y != 0) p.pos = new Vector2(Random.Range(0, W), v.y > 0 ? -2 : H + 2);
            else if (v.x != 0) p.pos = new Vector2(v.x > 0 ? -2 : W + 2, Random.Range(0, H));
            else p.pos = new Vector2(Random.Range(0, W), Random.Range(0, H));
            var c = region.ambient; c.a = Random.Range(0.35f, 0.9f);
            p.img.color = c;
        }

        void UpdateParticles(float dt)
        {
            float time = Time.time;
            foreach (var p in particles)
            {
                p.pos += (p.vel + new Vector2(Mathf.Sin(time * 1.3f + p.phase) * 2f, Mathf.Cos(time + p.phase) * 1f)) * dt;
                if (p.pos.x < -4 || p.pos.x > W + 4 || p.pos.y < -4 || p.pos.y > H + 4) ResetParticle(p, false);
                // 정지 입자(수정 광산)는 반짝임
                if (region.ambientVel == Vector2.zero)
                {
                    var c = p.img.color; c.a = Mathf.Abs(Mathf.Sin(time * 2f + p.phase)); p.img.color = c;
                }
                p.rt.anchoredPosition = new Vector2(Mathf.Round(p.pos.x), Mathf.Round(p.pos.y));
            }
        }

        // ── 포획대원 ───────────────────────────────────────────
        static bool Present(HunterState h, RegionDef r) =>
            h.regionId == r.id && (h.dispatched || (h.restTimer > 0 && !h.penalty));

        public int PresentCount()
        {
            int n = 0;
            foreach (var h in g.S.hunters) if (Present(h, region)) n++;
            return n;
        }

        void SyncHunters(float dt)
        {
            // 떠난 포획대원 정리
            var gone = new List<HunterState>();
            foreach (var kv in hunters) if (!Present(kv.Key, region)) gone.Add(kv.Key);
            foreach (var h in gone) { RemoveHunter(hunters[h]); hunters.Remove(h); }

            int lane = 0;
            foreach (var h in g.S.hunters)
            {
                if (!Present(h, region)) continue;
                if (!hunters.TryGetValue(h, out var a)) hunters[h] = a = NewHunter(h);
                a.home = new Vector2(20 + lane * 16, 24 - lane * 11); // 줄마다 깊이만 다르게(앞줄이 아래)
                UpdateHunter(a, dt);
                lane++;
            }

            // 아래(앞)에 있는 것이 위에 그려지도록
            var all = new List<(float y, Transform t)>();
            foreach (Transform t in actorLayer) all.Add((((RectTransform)t).anchoredPosition.y, t));
            all.Sort((x, y) => y.y.CompareTo(x.y));
            for (int i = 0; i < all.Count; i++) all[i].t.SetSiblingIndex(i);
        }

        HunterActor NewHunter(HunterState h)
        {
            var def = g.Def(h);
            var a = new HunterActor { h = h, def = def };
            a.img = Spr(actorLayer, def.id, Vector2.zero, a.A);
            a.rt = a.img.rectTransform;
            a.baitImg = Spr(actorLayer, GameData.Baits[h.bait].id, Vector2.zero, 0.75f);
            a.bait = a.baitImg.rectTransform;

            // 이름표 + 진행 막대 (오버레이: 글자는 도트 배율과 무관한 크기)
            a.tag = UIKit.Label(overlay, def.Name, UIKit.TS, TextAnchor.LowerCenter, UIKit.TextMain);
            a.tag.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = a.tag.rectTransform;
            trt.anchorMin = trt.anchorMax = Vector2.zero;
            trt.pivot = new Vector2(0.5f, 0);
            trt.sizeDelta = new Vector2(200, 24);
            var bar = UIKit.Box(a.tag.transform, new Color(0.08f, 0.05f, 0.1f, 0.9f), "Bar").rectTransform;
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(64, 8);
            bar.anchoredPosition = new Vector2(0, -2);
            a.barFill = UIKit.Box(bar, new Color(0.45f, 0.9f, 0.5f), "Fill").rectTransform;
            a.barFill.anchorMin = Vector2.zero; a.barFill.anchorMax = new Vector2(0, 1); a.barFill.pivot = new Vector2(0, 0.5f);
            a.barFill.offsetMin = new Vector2(2, 2); a.barFill.offsetMax = new Vector2(2, -2);
            return a;
        }

        void RemoveHunter(HunterActor a)
        {
            foreach (var m in a.monsters) RunAway(m);
            a.monsters.Clear();
            if (a.rt != null) Destroy(a.rt.gameObject);
            if (a.bait != null) Destroy(a.bait.gameObject);
            if (a.tag != null) Destroy(a.tag.gameObject);
        }

        void UpdateHunter(HunterActor a, float dt)
        {
            var h = a.h;
            float time = Time.time;
            bool resting = !h.dispatched;
            float interval = g.HuntInterval(h);
            float p = resting ? 0f : Mathf.Clamp01(h.timer / interval);

            // 몸: 숨쉬기 + 공격 시 앞으로 찌르기
            a.lunge = Mathf.Max(0, a.lunge - dt * 5f);
            float breathe = resting ? 0 : (Mathf.Sin(time * 3f + a.home.x) > 0.6f ? 1 : 0);
            a.rt.anchoredPosition = a.home + new Vector2(Mathf.Round(a.lunge * 4f), breathe);
            a.img.color = resting ? new Color(0.55f, 0.55f, 0.65f) : Color.white;
            float A = a.A;
            a.rt.localScale = resting ? new Vector3(A, A * 0.85f, 1) : new Vector3(A, A, 1);
            if (a.tag.text != a.def.Name) a.tag.text = a.def.Name;

            // 이름표
            a.tag.rectTransform.anchoredPosition = ToOverlay(a.home + new Vector2(0, 12 * A + 4));
            float barV = resting ? 1f - h.restTimer / g.Mods.restTime : p;
            a.barFill.sizeDelta = new Vector2(60f * Mathf.Clamp01(barV), -4);
            a.barFill.GetComponent<Image>().color = resting ? new Color(0.6f, 0.6f, 1f) : new Color(0.45f, 0.9f, 0.5f);

            // 휴식: Z Z Z
            if (resting)
            {
                a.bait.gameObject.SetActive(false);
                a.zTimer -= dt;
                if (a.zTimer <= 0) { a.zTimer = 0.9f; FloatText(a.home + new Vector2(8, 22), "z", new Color(0.8f, 0.85f, 1f), UIKit.TS, 40f, 1.4f); }
                foreach (var m in a.monsters) RunAway(m);
                a.monsters.Clear();
                return;
            }

            // 미끼: 괴물이 다가오는 곳
            bool hasBait = h.pending.Count > 0;
            a.bait.gameObject.SetActive(hasBait);
            if (hasBait)
            {
                int bi = Mathf.Max(0, h.pendingBait);
                var baitSprite = Art.Get(GameData.Baits[bi].id);
                if (a.baitImg.sprite != baitSprite) a.baitImg.sprite = baitSprite;
                a.bait.anchoredPosition = a.home + new Vector2(30, -2);
            }

            // 이번 사이클 괴물 등장
            if (a.monsters.Count == 0 && h.pending.Count > 0)
                for (int j = 0; j < h.pending.Count; j++) a.monsters.Add(NewMonster(h.pending[j]));
            else if (h.pending.Count == 0 && a.monsters.Count > 0)
            {
                foreach (var m in a.monsters) RunAway(m);
                a.monsters.Clear();
            }

            for (int j = 0; j < a.monsters.Count; j++) UpdateMonster(a, a.monsters[j], j, p, dt);
        }

        MonsterActor NewMonster(string id)
        {
            var def = GameData.MonsterById[id];
            var m = new MonsterActor { id = id, def = def };
            m.img = Spr(actorLayer, id, new Vector2(W + 30, 0), m.A);
            m.rt = m.img.rectTransform;
            if (def.hidden)
            {
                // 히든: 반짝이는 기운
                var aura = Spr(m.rt, "fx_orb", new Vector2(m.rt.sizeDelta.x / 2f, m.rt.sizeDelta.y / 2f), 1.3f, new Vector2(0.5f, 0.5f));
                aura.color = new Color(1f, 0.9f, 0.4f, 0.35f);
                m.aura = aura.rectTransform;
                aura.rectTransform.SetAsFirstSibling();
                aura.rectTransform.anchorMin = aura.rectTransform.anchorMax = Vector2.zero;
            }
            return m;
        }

        void UpdateMonster(HunterActor a, MonsterActor m, int j, float p, float dt)
        {
            float time = Time.time;
            float A = m.A;
            var engage = a.home + new Vector2(44 + j * 24, -j * 3);
            Vector2 pos;
            if (m.def.isPlant)
            {
                // 마초는 걸어오지 않고 땅에서 자라난다
                pos = engage + new Vector2(4, 0);
                float grow = Mathf.Clamp01(p / 0.35f);
                m.rt.localScale = new Vector3(A, A * Mathf.Round(grow * 8f) / 8f, 1);
            }
            else
            {
                float w = Mathf.Clamp01(p / WalkEnd);
                w = 1f - (1f - w) * (1f - w);
                pos = Vector2.Lerp(new Vector2(W + 24 + j * 24, engage.y), engage, w);
                bool walking = p < WalkEnd;
                float bob = walking ? ((int)(time * 8f + j) % 2) : (Mathf.Sin(time * 2.5f + j) > 0.7f ? 1 : 0);
                pos.y += bob;
                m.rt.localScale = new Vector3(-A, A, 1); // 포획대원(왼쪽)을 바라본다
            }

            // 공격 구간: 정해진 시점마다 타격
            if (p >= FightStart)
            {
                int hit = -1;
                for (int k = 0; k < HitAt.Length; k++) if (p >= HitAt[k]) hit = k;
                if (hit > m.lastHit)
                {
                    m.lastHit = hit;
                    if (j == 0) a.lunge = 1f;
                    Attack(a, m, pos);
                }
            }

            m.hitFlash = Mathf.Max(0, m.hitFlash - dt * 6f);
            m.knock = Mathf.Max(0, m.knock - dt * 12f);
            pos.x += Mathf.Round(m.knock * 3f);
            m.img.color = m.hitFlash > 0 ? Color.Lerp(Color.white, new Color(1f, 0.35f, 0.35f), m.hitFlash) : Color.white;
            m.rt.anchoredPosition = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
            if (m.aura != null)
            {
                float s = 1.2f + Mathf.Sin(time * 4f) * 0.15f;
                m.aura.localScale = new Vector3(s, s, 1);
                if (Random.value < dt * 3f) Sparkles(pos + new Vector2(0, m.rt.sizeDelta.y * A / 2f), new Color(1f, 0.9f, 0.4f), 1, 14f);
            }
        }

        // 포획대원마다 다른 공격, 힘(약/중/강)에 따라 크기·색·흔들림
        void Attack(HunterActor a, MonsterActor m, Vector2 target)
        {
            int power = (int)a.h.power;
            float s = (0.55f + power * 0.25f) * 1.6f;
            Color col = power == 0 ? Color.white : power == 1 ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.5f, 0.3f);
            var from = a.home + new Vector2(10, 14);
            var hitPos = target + new Vector2(0, m.rt.sizeDelta.y * m.A * 0.5f);

            switch (a.def.id)
            {
                case "hunter_lich": // 마법 구슬이 날아간다
                    FxSprite("fx_orb", from + new Vector2(2, 4), hitPos, 0.22f, new Color(0.8f, 0.5f, 1f), s * 0.6f, s * 0.8f, 0, 6);
                    Delay(0.2f, () => Impact(m, hitPos, col, s, power));
                    break;
                case "hunter_orc": // 몽둥이 크게 휘두르기
                    FxSprite("fx_slash", hitPos + new Vector2(-4, 2), hitPos + new Vector2(2, -2), 0.18f, col, s * 1.1f, s * 1.2f, -70);
                    Impact(m, hitPos, col, s, power);
                    break;
                default: // 임프: 찌르기
                    FxSprite("fx_slash", hitPos + new Vector2(-6, 0), hitPos, 0.14f, col, s * 0.7f, s * 0.8f, -30);
                    Impact(m, hitPos, col, s, power);
                    break;
            }
        }

        void Delay(float t, Action a) => Play(t, _ => { }, a);

        void Impact(MonsterActor m, Vector2 at, Color col, float s, int power)
        {
            if (m.rt == null) return;
            m.hitFlash = 1f;
            m.knock = 1f;
            FxSprite("fx_hit", at, at, 0.16f, col, s * 0.5f, s * 0.9f);
            if (power == 2) shake = 0.12f;
        }

        void RunAway(MonsterActor m)
        {
            if (m.rt == null) return;
            var rt = m.rt;
            var img = m.img;
            Vector2 from = rt.anchoredPosition;
            Play(0.5f, k =>
            {
                if (rt == null) return;
                rt.anchoredPosition = from + new Vector2(60f * k, 0);
                var c = img.color; c.a = 1f - k; img.color = c;
            }, () => { if (rt != null) Destroy(rt.gameObject); });
        }

        // ── 포획 성공: 우리가 떨어져 가두고, 포획대원에게 날아간다 ─
        void OnCaught(HunterState h, List<string> ids)
        {
            if (!hunters.TryGetValue(h, out var a)) return;
            Sound.Play("catch");
            for (int j = 0; j < a.monsters.Count; j++)
            {
                var m = a.monsters[j];
                Capture(a, m, j);
            }
            a.monsters.Clear();
        }

        void Capture(HunterActor a, MonsterActor m, int j)
        {
            var rt = m.rt;
            var img = m.img;
            if (rt == null) return;
            Vector2 at = rt.anchoredPosition;
            float A = m.A;
            float hgt = rt.sizeDelta.y * A;
            var cage = Spr(fxLayer, "fx_cage", at + new Vector2(0, 40), Mathf.Max(0.8f, hgt / 14f), new Vector2(0.5f, 0f));
            var crt = cage.rectTransform;
            Vector2 dest = a.home + new Vector2(0, 22);

            // 1) 우리 낙하
            Play(0.16f, k => { if (crt != null) crt.anchoredPosition = at + new Vector2(0, 40f * (1f - k * k)); }, () =>
            {
                FxSprite("fx_poof", at + new Vector2(-6, 2), at + new Vector2(-10, 4), 0.35f, Color.white, 0.5f, 0.7f);
                FxSprite("fx_poof", at + new Vector2(6, 2), at + new Vector2(10, 4), 0.35f, Color.white, 0.5f, 0.7f);
                shake = Mathf.Max(shake, 0.06f);
                // 2) 잠깐 흔들린 뒤 포획대원에게 날아간다
                Play(0.5f, k =>
                {
                    if (crt == null || rt == null) return;
                    float wob = k < 0.3f ? Mathf.Sin(k * 60f) * 1.5f : 0;
                    float f = Mathf.Clamp01((k - 0.3f) / 0.7f);
                    var p = Vector2.Lerp(at, dest, f) + new Vector2(wob, 18f * 4f * f * (1f - f));
                    float s = 1f - f * 0.7f;
                    crt.anchoredPosition = p;
                    rt.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
                    crt.localScale = Vector3.one * Mathf.Max(0.8f, hgt / 14f) * s;
                    rt.localScale = new Vector3(-s * A, s * A, 1);
                }, () =>
                {
                    Sparkles(dest, m.def.hidden ? new Color(1f, 0.85f, 0.3f) : Color.white, m.def.hidden ? 12 : 6, m.def.hidden ? 24f : 14f);
                    if (crt != null) Destroy(crt.gameObject);
                    if (rt != null) Destroy(rt.gameObject);
                });
            });

            string label = m.def.hidden ? L.T($"★ 히든! {m.def.Name}", $"★ Hidden! {m.def.Name}") : $"+1 {m.def.Name}";
            Color lc = m.def.hidden ? UIKit.Gold : m.def.isPlant ? UIKit.GoodText : UIKit.TextMain;
            Delay(0.2f + j * 0.12f, () => FloatText(at + new Vector2(0, hgt + 4), label, lc, m.def.hidden ? UIKit.TM : UIKit.TS));
        }

        // ── 쓰레기 ─────────────────────────────────────────────
        void SyncTrash()
        {
            var alive = new HashSet<int>();
            foreach (var t in g.S.trashItems)
            {
                if (t.regionId != region.id) continue;
                alive.Add(t.uid);
                if (!trash.ContainsKey(t.uid)) trash[t.uid] = (t, NewTrash(t));
            }
            var gone = new List<int>();
            foreach (var kv in trash) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) { if (trash[id].rt != null) Destroy(trash[id].rt.gameObject); trash.Remove(id); }

            // 가끔 반짝여서 눈에 띄게
            if (trash.Count > 0 && Random.value < Time.deltaTime * 0.8f)
            {
                int k = Random.Range(0, trash.Count), i = 0;
                foreach (var kv in trash) { if (i++ != k) continue; Sparkles(kv.Value.rt.anchoredPosition + new Vector2(0, 6), Color.white, 2, 6f); break; }
            }
        }

        RectTransform NewTrash(TrashItem t)
        {
            var img = Spr(trashLayer, t.can ? "trash_can" : "trash_bottle", new Vector2(t.x, t.y), 0.75f);
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.localEulerAngles = new Vector3(0, 0, t.can ? 0 : (t.uid % 2 == 0 ? 80 : -80)); // 병은 누워 있다
            var ph = img.gameObject.AddComponent<PointerHandler>();
            var item = t;
            ph.onLeft = () => g.CollectTrash(item);
            ph.onEnter = () => { if (rt != null) rt.localScale = Vector3.one * 1f; };
            ph.onExit = () => { if (rt != null) rt.localScale = Vector3.one * 0.75f; };
            img.Tip(() => L.T($"{(item.can ? "빈 캔" : "페트병")}\n탭해서 줍기: {L.Cur(item.can ? Currency.Gold : Currency.Material)} +{Mathf.RoundToInt(GameData.TrashReward * g.Mods.trashMul)}",
                              $"{(item.can ? "Empty can" : "Plastic bottle")}\nTap to pick up: +{Mathf.RoundToInt(GameData.TrashReward * g.Mods.trashMul)} {L.Cur(item.can ? Currency.Gold : Currency.Material)}"));

            // 떨어지며 통통 튄다
            Vector2 land = new Vector2(t.x, t.y);
            Play(0.45f, k =>
            {
                if (rt == null) return;
                float y = k < 0.6f ? 30f * (1f - (k / 0.6f) * (k / 0.6f)) : 3f * Mathf.Sin((k - 0.6f) / 0.4f * Mathf.PI);
                rt.anchoredPosition = land + new Vector2(0, Mathf.Round(y));
            });
            return rt;
        }

        void OnTrashCollected(TrashItem t, Currency cur, int amt)
        {
            if (region == null || t.regionId != region.id) return;
            var at = new Vector2(t.x, t.y + 4);
            FxSprite("fx_poof", at, at + new Vector2(0, 4), 0.3f, Color.white, 0.4f, 0.6f);
            Sparkles(at, cur == Currency.Gold ? UIKit.Gold : UIKit.MatText, 5, 12f);
            FloatText(at + new Vector2(0, 6), $"+{amt} {L.Cur(cur)}", cur == Currency.Gold ? UIKit.Gold : UIKit.MatText);
        }
    }
}
