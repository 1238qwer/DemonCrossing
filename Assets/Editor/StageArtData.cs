using System.Collections.Generic;

namespace Mawang.EditorTools
{
    // 포획 스테이지용 도트: 포획대원 3명, 새 괴물 몸체 8종, 스테이지 3~10 괴물(몸체 + 스테이지 팔레트).
    // 스테이지 괴물 스프라이트 이름 = 괴물 id (s{스테이지}_{슬롯}). 슬롯: 0~2 골드, 3~5 자재, 6~7 마초, 8 히든.
    public static class StageArtData
    {
        // ── 포획대원 (오른쪽을 보는 8×12) ────────────────────
        static readonly PixelSprite[] Hunters =
        {
            new PixelSprite { name = "hunter_imp", palette = "i=#e05030,I=#9a2a1a,n=#e8dcc0,t=#8a5a30", rows = new[] {
                ".K...K..",
                ".KKKKK..",
                ".KiiiK..",
                "KiYiYiK.",
                "KiiiiiK.",
                ".KiWiK.n",
                "..KiiKnt",
                ".KIiiKKt",
                "KiIiiiKt",
                ".KIIIK..",
                ".KiKiK..",
                ".KK.KK.." } },

            new PixelSprite { name = "hunter_orc", palette = "o=#6aa040,O=#3a6a24,a=#8a6a4a,A=#5a4030,c=#a0703a", rows = new[] {
                "..KKKK..",
                ".KooooK.",
                ".KoYoYKc",
                ".KooooKc",
                ".KWooWKc",
                "..KKKK.c",
                ".KaaaaKc",
                "KoaAAaoK",
                "KoaaaaKK",
                ".KAAAAK.",
                ".KOKKOK.",
                ".KK..KK." } },

            new PixelSprite { name = "hunter_lich", palette = "r=#4a2a6a,R=#2a1440,s=#6ae0ff,t=#8a6a4a", rows = new[] {
                "..KKKK.s",
                ".KrrrrKs",
                ".KrWWrKt",
                ".KWBWBKt",
                ".KWWWWKt",
                "..KKKK.t",
                ".KrrrrKt",
                "KrRrrRrt",
                "KWRrrRKt",
                ".KrRRrK.",
                ".KrrrrK.",
                ".KKKKKK." } },
        };

        // ── 새 괴물 몸체 ─────────────────────────────────────
        static readonly PixelSprite[] Bases =
        {
            new PixelSprite { name = "m_ghost", mirror = true, palette = "g=#d8e0f0,G=#9098b8", rows = new[] {
                "...KK",
                "..Kgg",
                ".Kggg",
                ".KgBg",
                "KggBg",
                "Kgggg",
                "KgggK",
                "KGggg",
                "KGGgg",
                "KGKGG",
                "K.KK." } },

            new PixelSprite { name = "m_snake", palette = "s=#5ab04a,S=#2a7a30,y=#e0d060", rows = new[] {
                "........KKK.",
                ".......KsBsK",
                "......KsssKE",
                "..KKK.KsyK..",
                ".KsssKsyK...",
                "KsyyssyK....",
                "KsKKyyK.....",
                ".K..KK......" } },

            new PixelSprite { name = "m_eye", mirror = true, palette = "w=#f0e8e8,p=#6a3a8a", rows = new[] {
                "...KK",
                "..Kww",
                "K.Kww",
                "pKwEE",
                "pKwEB",
                "pKwww",
                "K.Kww",
                "...KK" } },

            new PixelSprite { name = "m_scorpion", palette = "c=#c8903a,C=#8a5a20", rows = new[] {
                "..KK........",
                ".KcK........",
                ".KcK........",
                "..KcK..K..K.",
                "...KcKKcKKcK",
                "..KcccccccK.",
                ".KcCcCcCcBK.",
                "K.K.K.K.KK.." } },

            new PixelSprite { name = "m_mimic", mirror = true, palette = "b=#a86432,g=#f2c040,r=#e05060", rows = new[] {
                "KKKKK",
                "Kbbbb",
                "KbbEb",
                "KKKKK",
                "KWKWK",
                "KBrrr",
                "KWKWK",
                "KKKKK",
                "Kbbgb",
                "Kbbbb",
                "KKKKK" } },

            new PixelSprite { name = "m_wisp", mirror = true, palette = "f=#ffb040,F=#ff6020,y=#fff0a0", rows = new[] {
                "....K",
                "...KF",
                "..KFf",
                ".KFff",
                "KFfyy",
                "KfyBy",
                "Kfyyy",
                ".Kfyy",
                "..KKf",
                "....K" } },

            new PixelSprite { name = "p_crystal", mirror = true, palette = "c=#8af0ff,C=#3aa8d0,g=#3a5a4a", rows = new[] {
                "....K",
                "...Kc",
                "K..Kc",
                "cK.Kc",
                "CcKCc",
                "CcKCc",
                "gggKC",
                "ggggg" } },

            new PixelSprite { name = "p_coral", mirror = true, palette = "c=#ff7090,C=#c83a60,g=#3a4a6a", rows = new[] {
                "K...K",
                "cK.Kc",
                "cK.Kc",
                "KcKcK",
                "K.KcK",
                "cKcKc",
                ".KcKc",
                "..Kcc",
                "..KcC",
                "ggggg" } },
        };

        // ── 스테이지 괴물: (몸체, 팔레트 덮어쓰기) × 슬롯 0~8 ─
        static readonly (int stage, (string body, string pal)[] slots)[] Rosters =
        {
            (3, new[] {
                ("m_slime", "g=#bfe8ff,G=#5aa0d8,w=#ffffff"),
                ("m_bat", "p=#8ab8e8,P=#3a5a9a"),
                ("m_werewolf", "f=#e8eef8,F=#98a8c8,Y=#60c0ff"),
                ("m_skeleton", "W=#c8e8ff,B=#2a4a8a"),
                ("m_spider", "b=#4a6a9a,B=#9ad0ff,E=#60f0ff"),
                ("m_golem", "r=#7a98c0,R=#3a5080,o=#b0e8ff,O=#ffffff"),
                ("p_firemoss", "o=#8ad8ff,O=#e0f8ff"),
                ("p_crystal", "c=#d0f4ff,C=#5ab0e8,g=#e8f0ff"),
                ("m_drake", "d=#7ac0f0,D=#3a70b0,y=#e8f8ff,w=#a0e0ff") }),
            (4, new[] {
                ("m_slime", "g=#9ad040,G=#5a8a20,w=#e0ff90"),
                ("m_snake", "s=#5a9a3a,S=#2a5a20,y=#c8d060"),
                ("m_mushroom", "r=#9a4ac0,R=#5a2080,s=#d8e0a0,S=#9aa060"),
                ("m_treant", "t=#5a4a2a,T=#3a2a14,l=#6a8a3a,L=#3a5a20"),
                ("m_spider", "b=#3a5a2a,B=#8ac040,E=#e0ff40"),
                ("m_worm", "m=#6a5a3a,M=#3a3020,O=#c0e040"),
                ("p_toadstool", "m=#a0e040,s=#e0d0a0,g=#3a5a2a"),
                ("p_vine", "v=#4a8a3a,V=#8ad060,g=#3a4a2a"),
                ("m_salamander", "s=#4aa060,S=#1a6030,y=#c0e080") }),
            (5, new[] {
                ("m_ghost", "g=#c8d8f0,G=#7a88b0"),
                ("m_bat", "p=#6a6a7a,P=#2a2a3a,E=#60c0ff"),
                ("m_werewolf", "f=#7a9080,F=#4a5a50,Y=#e04040"),
                ("m_skeleton", "W=#b0b0b8,B=#6020a0"),
                ("m_worm", "m=#c8b8a0,M=#8a7a60,O=#6040a0"),
                ("m_golem", "r=#d8d0b8,R=#8a8070,o=#6aa0ff,O=#c0e0ff"),
                ("p_firemoss", "o=#6a9a7a,O=#a0e0c0"),
                ("p_vine", "v=#3a3a4a,V=#6a8ac0,g=#2a2a3a"),
                ("m_ghost", "g=#f0d870,G=#a07a20,B=#e03048") }),
            (6, new[] {
                ("m_slime", "g=#e8c880,G=#b08a40,w=#fff0c0"),
                ("m_scorpion", "c=#c8903a,C=#8a5a20"),
                ("m_mimic", "b=#a86432,g=#f2c040,r=#e05060"),
                ("m_skeleton", "W=#e0d0a8,B=#6a4a2a"),
                ("m_worm", "m=#d8b070,M=#a07a40,O=#6a4a20"),
                ("m_golem", "r=#c8a060,R=#8a6a30,o=#40c0c0,O=#a0fff0"),
                ("p_firemoss", "o=#e8b030,O=#fff080"),
                ("p_sulfur", "y=#e8b8a0,w=#fff0e0,Y=#c87860"),
                ("m_mimic", "b=#f2c040,g=#fff4a0,r=#c83a60") }),
            (7, new[] {
                ("m_slime", "g=#f0a0e0,G=#b060b0,w=#ffe8ff"),
                ("m_bat", "p=#40c0c0,P=#1a6a7a,E=#ff60a0"),
                ("m_spider", "b=#6a3a8a,B=#e090ff,E=#80ffff"),
                ("m_skeleton", "W=#e8e0d0,B=#f0a020"),
                ("m_golem", "r=#8a6ab0,R=#4a3070,o=#ff90e0,O=#ffffff"),
                ("m_worm", "m=#40b0a0,M=#1a6a60,O=#ff80c0"),
                ("p_firemoss", "o=#80f0ff,O=#ffffff"),
                ("p_crystal", "c=#ffb0f0,C=#b060d0,g=#3a3050"),
                ("m_drake", "d=#e070d0,D=#903090,y=#80ffff,w=#ffb0f0") }),
            (8, new[] {
                ("m_wisp", "f=#80c0ff,F=#3a60e0,y=#ffffa0"),
                ("m_bat", "p=#5a6a9a,P=#2a3060,E=#ffe040"),
                ("m_werewolf", "f=#6a7aa0,F=#3a4a70,Y=#ffff60"),
                ("m_imp", "i=#4a80e0,I=#2a40a0,Y=#ffff60"),
                ("m_scorpion", "c=#4a70c0,C=#2a3a80"),
                ("m_golem", "r=#7a7a88,R=#44444f,o=#ffe040,O=#ffffa0"),
                ("p_firemoss", "o=#e8e040,O=#ffffff"),
                ("p_sulfur", "y=#80a0ff,w=#e0f0ff,Y=#ffff60"),
                ("m_crowking", "c=#2a3a6a,C=#5a7ac0,Y=#ffff60") }),
            (9, new[] {
                ("m_slime", "g=#40b0c0,G=#1a6a80,w=#a0ffff"),
                ("m_eye", "w=#d0f0f0,p=#1a5a6a,E=#20c0a0"),
                ("m_snake", "s=#2a8a9a,S=#1a4a5a,y=#80f0e0"),
                ("m_skeleton", "W=#a8c8c0,B=#1a4a4a"),
                ("m_spider", "b=#8a3a3a,B=#e07050,E=#ffe060"),
                ("m_worm", "m=#3a5a8a,M=#1a2a50,O=#60ffe0"),
                ("p_coral", "c=#ff7090,C=#c83a60,g=#2a4a5a"),
                ("p_vine", "v=#1a6a6a,V=#60ffe0,g=#1a3a4a"),
                ("m_drake", "d=#2a6aa0,D=#1a3a6a,y=#a0f0ff,w=#40a0c0") }),
            (10, new[] {
                ("m_wisp", "f=#e060ff,F=#8020c0,y=#ffc0ff"),
                ("m_eye", "w=#e0d0f0,p=#3a1a5a,E=#ff40c0"),
                ("m_mimic", "b=#4a2a5a,g=#e040ff,r=#40ffe0"),
                ("m_skeleton", "W=#b8a0d8,B=#ff40c0"),
                ("m_hellhound", "h=#8a2a8a,H=#4a0e4a,f=#ff60ff,O=#ffb0ff"),
                ("m_golem", "r=#3a2a4a,R=#1a1024,o=#e040ff,O=#ffc0ff"),
                ("p_firemoss", "o=#c040ff,O=#ffa0ff"),
                ("p_crystal", "c=#b060ff,C=#6020a0,g=#2a1a2a"),
                ("m_drake", "d=#3a1a4a,D=#1a0a24,y=#ff60e0,w=#a040c0") }),
        };

        public static IEnumerable<PixelSprite> All()
        {
            foreach (var h in Hunters) yield return h;
            foreach (var b in Bases) yield return b;

            var bodies = new Dictionary<string, PixelSprite>();
            foreach (var p in PixelArtData.All) bodies[p.name] = p;
            foreach (var p in Bases) bodies[p.name] = p;

            foreach (var (stage, slots) in Rosters)
                for (int i = 0; i < slots.Length; i++)
                {
                    var body = bodies[slots[i].body];
                    yield return new PixelSprite
                    {
                        name = $"s{stage}_{i}",
                        mirror = body.mirror,
                        pivotBottom = body.pivotBottom,
                        palette = body.palette + "," + slots[i].pal,
                        rows = body.rows,
                    };
                }
        }
    }
}
