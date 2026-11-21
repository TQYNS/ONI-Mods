using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EventFramework
{
    public static class UiSkin
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        // 取圆角矩形 Sprite（按半径缓存，没有则生成）
        public static Sprite Rounded(int radius)
        {
            return GetSprite("r" + radius, radius, 0f);
        }

        // 取描边圆角 Sprite（按半径 + 线宽缓存）
        public static Sprite RoundedOutline(int radius, int thickness)
        {
            return GetSprite("o" + radius + "_" + thickness, radius, thickness);
        }

        // 建一个纯色圆角图片节点（Sliced 拉伸，可直接当背景）
        public static Image RoundSolid(string name, Transform parent, int radius, Color color)
        {
            GameObject go = EventUiKit.MakeNode(name, parent);
            Image img = go.AddComponent<Image>();
            img.sprite = Rounded(radius);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // 建一个描边圆角图片节点（用于边框 / 选中态）
        public static Image RoundOutline(string name, Transform parent, int radius, int thickness, Color color)
        {
            GameObject go = EventUiKit.MakeNode(name, parent);
            Image img = go.AddComponent<Image>();
            img.sprite = RoundedOutline(radius, thickness);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Sprite GetSprite(string key, int radius, float thickness)
        {
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null)
            {
                return s;
            }
            s = Build(radius, thickness);
            cache[key] = s;
            return s;
        }

        private static Sprite Build(int radius, float thickness)
        {
            if (radius < 1) radius = 1;
            int size = radius * 2 + 2;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "UiSkin_" + radius + "_" + thickness.ToString("0.##");
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float sdf = RoundedSdf(px, py, half, radius);
                    float a;
                    if (thickness <= 0f)
                    {
                        a = Mathf.Clamp01(0.5f - sdf);
                    }
                    else
                    {
                        float outer = Mathf.Clamp01(0.5f - sdf);
                        float inner = Mathf.Clamp01(0.5f - (sdf + thickness));
                        a = Mathf.Clamp01(outer - inner);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply(false, false);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            sprite.name = tex.name;
            return sprite;
        }

        private static float RoundedSdf(float x, float y, float half, float radius)
        {
            float qx = Mathf.Abs(x) - (half - radius);
            float qy = Mathf.Abs(y) - (half - radius);
            float mx = Mathf.Max(qx, 0f);
            float my = Mathf.Max(qy, 0f);
            float outside = Mathf.Sqrt(mx * mx + my * my);
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - radius;
        }
    }
}
