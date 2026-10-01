using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mawang.EditorTools
{
    // PixelArtData + RoomPainter → Assets/Resources/Sprites/*.png (Point 필터, 16 PPU)
    public static class PixelArtBaker
    {
        const string OutDir = "Assets/Resources/Sprites";
        public const int PPU = 16;

        [MenuItem("Mawang/Bake Pixel Sprites")]
        public static void Bake()
        {
            Directory.CreateDirectory(OutDir);
            Resources.UnloadUnusedAssets();
            // 이전 버전에서 쓰던 스프라이트 정리
            foreach (var old in new[] { "frame", "bars", "prop_pot", "prop_gift", "prop_flask", "prop_bed", "prop_bench", "visitor_0", "visitor_1", "visitor_2" })
                AssetDatabase.DeleteAsset($"{OutDir}/{old}.png");

            var written = new List<(string path, SpriteAlignment pivot)>();

            var sprites = new List<PixelSprite>(PixelArtData.All);
            sprites.AddRange(StageArtData.All());
            foreach (var def in sprites)
            {
                var tex = Render(def);
                string path = $"{OutDir}/{def.name}.png";
                WriteSafe(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                written.Add((path, def.pivotBottom ? SpriteAlignment.BottomCenter : SpriteAlignment.Center));
            }

            var canvases = new List<(string, Canvas)>(RoomPainter.All());
            canvases.AddRange(StagePainter.All());
            foreach (var (name, canvas) in canvases)
            {
                var tex = canvas.ToTexture();
                string path = $"{OutDir}/{name}.png";
                WriteSafe(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                written.Add((path, SpriteAlignment.BottomLeft));
            }

            // UI 틀(9-slice) + 아이콘
            var uiSprites = new List<UISprite>();
            uiSprites.AddRange(UIPainter.Frames());
            uiSprites.AddRange(UIPainter.Icons());
            foreach (var ui in uiSprites)
            {
                WriteSafe($"{OutDir}/{ui.name}.png", ui.tex.EncodeToPNG());
            }
            WritePreview(uiSprites);
            foreach (var ui in uiSprites) Object.DestroyImmediate(ui.tex);

            var white = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            white.SetPixels32(px);
            WriteSafe($"{OutDir}/white.png", white.EncodeToPNG());
            Object.DestroyImmediate(white);

            AssetDatabase.Refresh();
            foreach (var (path, pivot) in written) ConfigureImporter(path, pivot, PPU);
            ConfigureImporter($"{OutDir}/white.png", SpriteAlignment.Center, 4); // 1유닛 정사각형
            foreach (var ui in uiSprites) ConfigureImporter($"{OutDir}/{ui.name}.png", SpriteAlignment.Center, PPU, ui.border);
            ConfigureFonts();
            ConfigureBackgrounds();
            AssetDatabase.Refresh();
            Debug.Log($"[PixelArtBaker] {written.Count + uiSprites.Count + 1}개 스프라이트 생성 → {OutDir}");
        }

        // 에디터가 PNG를 메모리 매핑하고 있으면 덮어쓰기가 실패한다 → 에셋을 지우고 다시 쓴다
        static void WriteSafe(string path, byte[] bytes)
        {
            try { File.WriteAllBytes(path, bytes); return; }
            catch (IOException) { }
            Resources.UnloadUnusedAssets();
            AssetDatabase.DeleteAsset(path);
            File.WriteAllBytes(path, bytes);
        }

        // AI 생성 배경: 해상도를 낮추고 Point 필터로 도트 느낌을 맞춘다
        [MenuItem("Mawang/Configure Backgrounds")]
        public static void ConfigureBackgrounds()
        {
            ConfigureBackground("Assets/Resources/Backgrounds/sky.png", 1024);
            ConfigureBackground("Assets/Resources/Backgrounds/wall.png", 512);
        }

        static void ConfigureBackground(string path, int maxSize)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.maxTextureSize = maxSize;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spritePixelsPerUnit = 100;
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteAlignment = (int)SpriteAlignment.Center;
            imp.SetTextureSettings(st);
            imp.SaveAndReimport();
        }

        static Texture2D Render(PixelSprite def)
        {
            var pal = Canvas.ParsePalette(PixelArtData.Common + "," + def.palette);
            int half = 0;
            foreach (var r in def.rows) half = Mathf.Max(half, r.Length);
            int w = def.mirror ? half * 2 : half;
            int h = def.rows.Length;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
            {
                string row = def.rows[y];
                for (int x = 0; x < w; x++)
                {
                    int sx = def.mirror ? (x < half ? x : w - 1 - x) : x;
                    char c = sx < row.Length ? row[sx] : '.';
                    Color32 col = clear;
                    if (c != '.' && !pal.TryGetValue(c, out col))
                    {
                        Debug.LogWarning($"[PixelArtBaker] {def.name}: 팔레트에 없는 문자 '{c}'");
                        col = new Color32(255, 0, 255, 255);
                    }
                    tex.SetPixel(x, h - 1 - y, col); // 텍스처는 아래에서 위로
                }
            }
            tex.Apply();
            return tex;
        }

        // 아이콘/UI 확인용 8배 확대 시트 (Temp/ui_preview.png, 에셋 아님)
        static void WritePreview(List<UISprite> list)
        {
            const int S = 8, Cell = 26;
            int cols = 10, rows = (list.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * Cell * S, rows * Cell * S, TextureFormat.RGBA32, false);
            var bg = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < bg.Length; i++) bg[i] = new Color32(70, 60, 80, 255);
            sheet.SetPixels32(bg);
            for (int n = 0; n < list.Count; n++)
            {
                var t = list[n].tex;
                int ox = (n % cols) * Cell * S + S, oy = (rows - 1 - n / cols) * Cell * S + S;
                for (int y = 0; y < t.height; y++)
                    for (int x = 0; x < t.width; x++)
                    {
                        var c = t.GetPixel(x, y);
                        if (c.a <= 0) continue;
                        for (int j = 0; j < S; j++) for (int i = 0; i < S; i++) sheet.SetPixel(ox + x * S + i, oy + y * S + j, c);
                    }
            }
            sheet.Apply();
            File.WriteAllBytes("Temp/ui_preview.png", sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        // 도트 폰트: 안티앨리어싱 없이 래스터
        static void ConfigureFonts()
        {
            foreach (var f in new[] { "Galmuri11", "Galmuri11-Bold", "Galmuri9" })
            {
                var imp = AssetImporter.GetAtPath($"Assets/Resources/Fonts/{f}.ttf") as TrueTypeFontImporter;
                if (imp == null) continue;
                imp.fontRenderingMode = FontRenderingMode.HintedRaster;
                imp.fontTextureCase = FontTextureCase.Dynamic;
                imp.SaveAndReimport();
            }
        }

        static void ConfigureImporter(string path, SpriteAlignment pivot, int ppu) => ConfigureImporter(path, pivot, ppu, Vector4.zero);

        static void ConfigureImporter(string path, SpriteAlignment pivot, int ppu, Vector4 border)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;

            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spritePixelsPerUnit = ppu;
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteAlignment = (int)pivot;
            st.spriteBorder = border;
            st.spriteGenerateFallbackPhysicsShape = false;
            imp.SetTextureSettings(st);
            imp.SaveAndReimport();
        }
    }
}
