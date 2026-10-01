using System.Collections.Generic;
using UnityEngine;

namespace Mawang
{
    // Resources 에서 스프라이트를 읽어 캐시한다. 스프라이트는 메뉴 Mawang/Bake Pixel Sprites 로 생성.
    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>("Sprites/" + name);
            if (s == null) Debug.LogWarning($"스프라이트 없음: {name} (메뉴 Mawang/Bake Pixel Sprites 실행)");
            cache[name] = s;
            return s;
        }

        public static Sprite Background(string name) => Resources.Load<Sprite>("Backgrounds/" + name);

        public static Sprite White => Get("white");


        public static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // 월드 공간 텍스트(그림자 포함). UI가 아니라 장면의 일부로 그려진다.
        public static WorldText Text(Transform parent, string text, Vector2 pos, float height, Color color, int order, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var wt = go.AddComponent<WorldText>();
            wt.Init(text, height, color, order, anchor);
            return wt;
        }
    }

    public class WorldText : MonoBehaviour
    {
        const int FontSize = 60; // 도트 폰트 12px의 배수
        TextMesh main, shadow;

        public void Init(string text, float height, Color color, int order, TextAnchor anchor)
        {
            shadow = Make("Shadow", new Color(0, 0, 0, 0.85f), order, anchor, height);
            shadow.transform.localPosition = new Vector3(height * 0.08f, -height * 0.08f, 0);
            main = Make("Main", color, order + 1, anchor, height);
            Set(text);
        }

        TextMesh Make(string n, Color c, int order, TextAnchor anchor, float height)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = UIKit.Font;
            tm.fontSize = FontSize;
            tm.characterSize = height * 10f / FontSize; // 한 줄 높이 ≈ height
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.UpperLeft || anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.LowerLeft ? TextAlignment.Left : TextAlignment.Center;
            tm.color = c;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = UIKit.Font.material;
            mr.sortingOrder = order;
            return tm;
        }

        public void Set(string text)
        {
            if (main.text == text) return;
            main.text = text;
            shadow.text = text;
        }

        public void SetAlpha(float a)
        {
            var c = main.color; c.a = a; main.color = c;
            var s = shadow.color; s.a = a * 0.85f; shadow.color = s;
        }
    }
}
