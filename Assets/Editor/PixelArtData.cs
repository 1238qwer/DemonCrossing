namespace Mawang.EditorTools
{
    // 손으로 찍은 작은 도트. 한 글자 = 한 픽셀, '.' = 투명.
    // mirror = true 이면 각 행은 왼쪽 절반만 적고 좌우 반전한다 (폭 = 절반 × 2).
    // 팔레트: "문자=#RRGGBB[AA]" 쉼표 구분. K(외곽선)/W/B/E/Y 는 공통.
    // 방(건물 내부) 그림은 RoomPainter 가 코드로 그린다.
    public class PixelSprite
    {
        public string name;
        public bool mirror;
        public bool pivotBottom = true;
        public string palette = "";
        public string[] rows;
    }

    public static class PixelArtData
    {
        public const string Common = "K=#1b1022,W=#f4f0e8,B=#101018,E=#ff3048,Y=#ffd040";

        public static readonly PixelSprite[] All =
        {
            // ── 용사 손님 (오른쪽을 보는 8×12) ────────────────
            new PixelSprite { name = "hero_warrior", palette = "m=#a8a8b8,M=#6a6a80,s=#f0c8a0,r=#c83838,R=#8a2020,b=#5a4030", rows = new[] {
                "..KKKK..",
                ".KmmmmK.",
                ".KMmmMK.",
                ".KssBsK.",
                "..KKKK..",
                ".KrmmrKW",
                "KrRmmRKW",
                "KsRmmRKW",
                ".KRRRRKY",
                ".KbbbbK.",
                ".KbKKbK.",
                ".KK..KK." } },

            new PixelSprite { name = "hero_mage", palette = "p=#4a58d0,P=#2a3490,s=#f0c8a0,t=#8a5a30,g=#60e0ff", rows = new[] {
                "...KK..g",
                "..KppK.t",
                ".KpppK.t",
                "KppppppK",
                ".KssBsKt",
                "..KssK.t",
                ".KppppKt",
                "KpPpppKt",
                "KspPppst",
                ".KpPppKt",
                ".KppppK.",
                ".KK..KK." } },

            new PixelSprite { name = "hero_archer", palette = "g=#3a9a4a,G=#206030,s=#f0c8a0,b=#8a5a30,n=#5a4a3a", rows = new[] {
                "..KKKK..",
                ".KggggK.",
                ".KgssBKb",
                ".KgsssKb",
                "..KKKK.b",
                ".KggggKb",
                "KgGGGGsb",
                "KsGGGGKb",
                ".KGGGGKb",
                ".KnnnnK.",
                ".KnKKnK.",
                ".KK..KK." } },

            new PixelSprite { name = "hero_cleric", palette = "w=#e8e4f0,v=#b0a8c8,s=#f0c8a0", rows = new[] {
                "..KKKK..",
                ".KwwwwK.",
                ".KwssBK.",
                ".KwsssK.",
                "..KKKK..",
                ".KwwwwK.",
                "KwwYwwK.",
                "KsYYYwsY",
                ".KwYwwKY",
                ".KwvwwK.",
                ".KvvvvK.",
                ".KKKKKK." } },

            // ── 마왕성 관리자 ────────────────────────────────
            new PixelSprite { name = "castle_skel", palette = "n=#2a2a3a", rows = new[] {
                "..KKKK..",
                ".KWWWWK.",
                ".KBWBWK.",
                ".KWWWWK.",
                "..KWKK..",
                "..KKKK..",
                ".KnEEnK.",
                "KnnWWnnK",
                "KWnnnnWK",
                ".KnnnnK.",
                ".KnKKnK.",
                ".KK..KK." } },

            new PixelSprite { name = "castle_succ", palette = "H=#3a2a4a,a=#8a3aa0,k=#f0b0c0,r=#c02858,w=#5a2a6a", rows = new[] {
                ".H....H.",
                ".HKKKKH.",
                ".KaaaaK.",
                "KakkBkaK",
                "KakkkkaK",
                ".KaKKaK.",
                "w.KrrK.w",
                "wwKrrKww",
                ".KkrrkK.",
                ".KrrrrK.",
                ".KkKKkK.",
                ".KK..KK." } },

            new PixelSprite { name = "castle_garg", palette = "g=#8a8a9a,G=#5a5a6a", rows = new[] {
                ".K....K.",
                ".KKKKKK.",
                ".KgggGK.",
                ".KgEgGK.",
                ".KggggK.",
                "G.KWKK.G",
                "GGKggKGG",
                "GGKgggKG",
                ".KggggK.",
                ".KgGGgK.",
                ".KgKKgK.",
                ".KK..KK." } },

            // 마녀 연구원 (연구소 특화)
            new PixelSprite { name = "castle_witch", palette = "h=#3a8a4a,H=#1f5a2e,s=#c8f0b0,r=#6a3aa8,R=#44207a", rows = new[] {
                "...KK...",
                "..KhhK..",
                ".KhhhhK.",
                "KHHHHHHK",
                ".KssBsK.",
                ".KsssK..",
                ".KrrrrK.",
                "KrRrrRrK",
                "KsRrrRsK",
                ".KrRRrK.",
                ".KrrrrK.",
                ".KK..KK." } },

            // ── 괴물: 저주받은 숲 ────────────────────────────
            new PixelSprite { name = "m_slime", mirror = true, palette = "g=#6ee07a,G=#2f9e4a,w=#d8ffe0", rows = new[] {
                "...KK",
                ".KKgg",
                "KgwBg",
                "Kgggg",
                "KGggg",
                ".KKKK" } },

            new PixelSprite { name = "m_bat", mirror = true, palette = "p=#8a5aaa,P=#4a2a66", rows = new[] {
                "K...K",
                "KK.Kp",
                "KPKEp",
                "KPPpp",
                ".KKpp",
                "...KW",
                "....K" } },

            new PixelSprite { name = "m_mushroom", mirror = true, palette = "r=#e04848,R=#9a2a2a,s=#f0e0c8,S=#c8b090", rows = new[] {
                "..KKK",
                ".Krrr",
                "KrWrr",
                "Krrrr",
                "KRRRR",
                ".KKss",
                "..KBs",
                "..Kss",
                "..KSS",
                "...KK" } },

            new PixelSprite { name = "m_skeleton", mirror = true, rows = new[] {
                ".KKK",
                "KWWW",
                "KBWW",
                "KWWW",
                ".KWK",
                "..KK",
                "KWKW",
                "KWKW",
                "..KW",
                ".KW.",
                ".KK." } },

            new PixelSprite { name = "m_treant", mirror = true, palette = "t=#7a5230,T=#4a3018,l=#6cc04a,L=#3a8030", rows = new[] {
                "..KKK",
                ".Klll",
                "KlLll",
                "KLlll",
                ".KKLL",
                "..Ktt",
                "..KEt",
                "..Ktt",
                "KtKtt",
                "..Ktt",
                "..KTT" } },

            new PixelSprite { name = "m_werewolf", mirror = true, palette = "f=#8a8090,F=#5a5060", rows = new[] {
                "K....",
                "KK...",
                "KfKKK",
                "Kffff",
                "KfYff",
                "KffWW",
                ".KfWK",
                "..KFF",
                ".KfFf",
                ".KfKf",
                ".KK.K" } },

            new PixelSprite { name = "p_toadstool", mirror = true, palette = "m=#b060d0,s=#f0e0c8,g=#3a7a3a", rows = new[] {
                "...KK",
                "..Kmm",
                ".KmWm",
                ".Kmmm",
                "..KKK",
                "...Ks",
                "...Ks",
                "g.gKs",
                "ggggg" } },

            new PixelSprite { name = "p_vine", mirror = true, palette = "v=#8a4ab0,V=#c070e0,g=#3a6a3a", rows = new[] {
                "....K",
                "...Kv",
                "K.KvK",
                "vKvK.",
                "KvK..",
                ".Kv..",
                "..KvK",
                "..KvV",
                "...Kv",
                "ggggg" } },

            new PixelSprite { name = "m_crowking", mirror = true, palette = "c=#2a2238,C=#4a3e62", rows = new[] {
                "..YKY",
                "..KYY",
                ".Kccc",
                ".KcEc",
                ".KccY",
                "KCKcc",
                "KCCcc",
                "KCCKc",
                ".KKcc",
                "..KY.",
                "..KY." } },

            // ── 괴물: 용암 동굴 ──────────────────────────────
            new PixelSprite { name = "m_imp", mirror = true, palette = "i=#ff6a30,I=#b83a18", rows = new[] {
                "K....",
                "KK...",
                ".KKKK",
                "..Kii",
                "..KYi",
                "..KiW",
                "...KI",
                ".KiKi",
                "..KiK",
                "..KK." } },

            new PixelSprite { name = "m_golem", mirror = true, palette = "r=#6a4638,R=#3a2420,o=#ff8020,O=#ffd040", rows = new[] {
                "..KKK",
                ".KrrR",
                ".KOrr",
                ".KroO",
                "KrKrr",
                "KrKro",
                "KrKrr",
                "..Krr",
                ".KrrK",
                ".KKK." } },

            new PixelSprite { name = "m_salamander", palette = "s=#ff5020,S=#a02810,y=#ffc040", rows = new[] {
                "........KKK.",
                ".......KsYsK",
                "KK....KssssK",
                "KsKKKKsyyyK.",
                ".KssssyyyK..",
                "..KsK.KsK...",
                "..KK..KK...." } },

            new PixelSprite { name = "m_spider", mirror = true, palette = "b=#2a2a3a,B=#5a5a7a", rows = new[] {
                "K..KK",
                ".KKbb",
                "KKbEb",
                ".Kbbb",
                "K.KbB",
                ".K.KK",
                "K...." } },

            new PixelSprite { name = "m_worm", mirror = true, palette = "m=#e05030,M=#902818,O=#ffd040", rows = new[] {
                "..KKK",
                ".Kmmm",
                ".KOmm",
                ".KmKO",
                ".KmmK",
                "..KmM",
                "..KMm",
                ".KmmM",
                ".KMmm",
                "KKKKK" } },

            new PixelSprite { name = "m_hellhound", mirror = true, palette = "h=#8a2222,H=#4a0e0e,f=#ff7020,O=#ffb030", rows = new[] {
                "Kf.Kf",
                "KhKKh",
                ".Khhh",
                ".KOhh",
                ".KhHH",
                "..KWK",
                ".KKhh",
                "KhHhh",
                "KhKKh",
                "KK.KK" } },

            new PixelSprite { name = "p_firemoss", mirror = true, palette = "o=#ff9030,O=#ffd050", rows = new[] {
                "...K.",
                "..KoK",
                ".KoOK",
                "KoOoK",
                "KOoOo",
                "KKKKK" } },

            new PixelSprite { name = "p_sulfur", mirror = true, palette = "y=#f0e040,w=#fffbc0", rows = new[] {
                "....K",
                "...Kw",
                "...Ky",
                "K..Ky",
                "wK.Ky",
                "yK.Ky",
                "yYKyY",
                "yYYYY",
                "KKKKK" } },

            new PixelSprite { name = "m_drake", mirror = true, palette = "d=#c82838,D=#801828,y=#ffd080,w=#e05060", rows = new[] {
                "..KW..",
                "...KKK",
                "K..Kdd",
                "KK.KYd",
                "KwKKdd",
                "KwwKdW",
                "KwwKyy",
                ".KKdyy",
                "..KdDd",
                "..KdKK" } },

            // ── 구조물 ────────────────────────────────────────
            new PixelSprite { name = "slab", pivotBottom = false, palette = "L=#8a7080,s=#5a4050,S=#43303d", rows = new[] {
                "LLLLLLLLLLLLLLLL",
                "sssKssssssssKsss",
                "SSSKSSSSSSSSKSSS",
                "sssssssKssssssss",
                "SSSSSSSKSSSSSSSS",
                "KKKKKKKKKKKKKKKK" } },

            new PixelSprite { name = "battlement", pivotBottom = true, palette = "L=#8a7080,s=#5a4050,S=#43303d", rows = new[] {
                "KKKKKK..........",
                "KLLLLK..........",
                "KsssSK..........",
                "KsssSK..........",
                "KsssSKKKKKKKKKKK",
                "KSSSSKLLLLLLLLLK",
                "KsssssssKsssssSK",
                "KSSSSSSSKSSSSSSK" } },

            new PixelSprite { name = "pillar", pivotBottom = false, palette = "L=#8a7080,s=#5a4050,S=#43303d", rows = new[] {
                "KLssssSK",
                "KLssssSK",
                "KLssssSK",
                "KKKKKKKK",
                "KLsssSSK",
                "KLsssSSK",
                "KLsssSSK",
                "KKKKKKKK" } },

            new PixelSprite { name = "elevator", palette = "m=#5a5a70,M=#8a8aa0,g=#c8a040,G=#f0d070,d=#3a3a4a", rows = new[] {
                "KKKKKKKKKKKKKKKKKKKK",
                "KgGGGGGGGGGGGGGGGGgK",
                "KgKKKKKKKKKKKKKKKKgK",
                "KgKMMMMMMKKMMMMMMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmdmmmKKmmmdmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmGKKGmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMmdmmmKKmmmdmMKgK",
                "KgKMmmmmmKKmmmmmMKgK",
                "KgKMMMMMMKKMMMMMMKgK",
                "KgKKKKKKKKKKKKKKKKgK",
                "KggggggggggggggggggK",
                "KKKKKKKKKKKKKKKKKKKK" } },
        };
    }
}
