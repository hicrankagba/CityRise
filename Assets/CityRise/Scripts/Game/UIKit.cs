using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CityRise.Game
{
    /// <summary>
    /// Arayüzü koddan kurmak için küçük yardımcılar (prefab gerektirmez).
    /// Yuvarlak köşeli kutular ve daireler çalışma anında üretilen sprite'larla çizilir.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Panel = new Color(0.12f, 0.14f, 0.18f, 0.92f);
        public static readonly Color PanelLight = new Color(0.97f, 0.95f, 0.9f, 1f);
        public static readonly Color Accent = new Color(0.98f, 0.73f, 0.16f);
        public static readonly Color Green = new Color(0.3f, 0.72f, 0.36f);
        public static readonly Color Grey = new Color(0.62f, 0.62f, 0.62f);
        public static readonly Color TextDark = new Color(0.16f, 0.15f, 0.14f);

        static Font font;
        static Sprite rounded, circle;

        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeSprite(64, 22, false));
        public static Sprite Circle => circle != null ? circle : (circle = MakeSprite(64, 32, true));

        static Sprite MakeSprite(int size, int radius, bool isCircle)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            var border = isCircle ? Vector4.zero : new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(this RectTransform r, float inset = 0)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
            return r;
        }

        public static Image Box(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite ?? Rounded;
            img.type = img.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var r = Rect(name, parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, Color color, UnityAction onClick, int fontSize = 30)
        {
            var img = Box(name, parent, color);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.85f);
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(onClick);
            var label = Label("Yazi", img.transform, text, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.Stretch(6);
            return b;
        }

        public static void SetText(this Button b, string text) => b.GetComponentInChildren<Text>().text = text;
    }
}
