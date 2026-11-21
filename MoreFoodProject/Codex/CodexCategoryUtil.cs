// 百科分类页共用工具：等比合成封面、查找大图标 Grid、判定真实图标。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class CodexCategoryUtil
    {
        // 与原版 codexIconFood（856x929）保持同一长宽比例，卡片内不会二次形变
        internal const int CoverW = 236;
        internal const int CoverH = 256;
        internal const string UnknownIconName = "unknown";

        // 单款图案只给中心、目标高度与倾角；宽度由精灵自身宽高比推导，绝不拉伸
        internal sealed class CoverPart
        {
            internal readonly string Id;
            internal readonly float X;
            internal readonly float Y;
            internal readonly float Height;
            internal readonly float Angle;

            internal CoverPart(string id, float x, float y, float height, float angle)
            {
                Id = id;
                X = x;
                Y = y;
                Height = height;
                Angle = angle;
            }
        }

        private sealed class CoverLayer
        {
            internal readonly CoverPart Part;
            internal readonly Color[] Pixels;
            internal readonly int W;
            internal readonly int H;

            internal CoverLayer(CoverPart part, Color[] pixels, int w, int h)
            {
                Part = part;
                Pixels = pixels;
                W = w;
                H = h;
            }
        }

        // 每个 scope 只合成一次；scope 同时也是日志 key 前缀
        private static readonly Dictionary<string, Sprite> Covers = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        internal static Sprite UnknownIcon()
        {
            return Assets.GetSprite(UnknownIconName);
        }

        internal static bool IsRealIcon(Sprite icon, Sprite unknown)
        {
            return icon != null && icon != unknown;
        }

        // 取图路径由调用方注入：物品走 Def.GetUISprite，怪物走 kanim 的 ui 帧
        internal static Sprite BuildCompositeCover(string scope, IList<CoverPart> parts, Func<CoverPart, Sprite> resolve)
        {
            Sprite cached;
            if (Covers.TryGetValue(scope, out cached) && cached != null)
            {
                return cached;
            }
            try
            {
                Sprite unknown = UnknownIcon();
                var layers = new List<CoverLayer>();
                for (int i = 0; i < parts.Count; i++)
                {
                    CoverPart part = parts[i];
                    Sprite icon = null;
                    try { icon = resolve(part); }
                    catch (System.Exception e)
                    {
                        MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 封面 {0} 图标读取失败", part.Id);
                    }
                    if (!IsRealIcon(icon, unknown))
                    {
                        MFLog.Warn("codex:" + scope + ":coverMissing", LogCat.Codex,
                            "[MoreFood/Codex] 封面缺少 {0} 的 ui 图标，退回单图", part.Id);
                        return null;
                    }

                    Texture2D tex = icon.texture;
                    Rect rect = icon.textureRect;
                    if (tex == null || rect.width < 1f || rect.height < 1f)
                    {
                        MFLog.Warn("codex:" + scope + ":coverRect", LogCat.Codex,
                            "[MoreFood/Codex] 封面 {0} 图标纹理无效，退回单图", part.Id);
                        return null;
                    }
                    int px = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, Mathf.Max(0, tex.width - 1));
                    int py = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, Mathf.Max(0, tex.height - 1));
                    int pw = Mathf.Clamp(Mathf.RoundToInt(rect.width), 1, tex.width - px);
                    int ph = Mathf.Clamp(Mathf.RoundToInt(rect.height), 1, tex.height - py);
                    Color[] pixels;
                    try { pixels = tex.GetPixels(px, py, pw, ph); }
                    catch (System.Exception e)
                    {
                        MFLog.Exc("codex:" + scope + ":coverPixels", LogCat.Codex, e,
                            "[MoreFood/Codex] 封面 {0} 纹理不可读，退回单图", part.Id);
                        return null;
                    }
                    CoverLayer layer = TrimLayer(part, pixels, pw, ph);
                    if (layer == null)
                    {
                        MFLog.Warn("codex:" + scope + ":coverEmpty", LogCat.Codex,
                            "[MoreFood/Codex] 封面 {0} 图标全透明，退回单图", part.Id);
                        return null;
                    }
                    layers.Add(layer);
                }

                Color[] canvas = new Color[CoverW * CoverH];
                for (int i = 0; i < canvas.Length; i++) canvas[i] = new Color(1f, 1f, 1f, 0f);
                for (int i = 0; i < layers.Count; i++) DrawPart(canvas, layers[i]);

                Texture2D outTex = new Texture2D(CoverW, CoverH, TextureFormat.RGBA32, false);
                outTex.hideFlags = HideFlags.HideAndDontSave;
                outTex.filterMode = FilterMode.Bilinear;
                outTex.wrapMode = TextureWrapMode.Clamp;
                outTex.SetPixels(canvas);
                outTex.Apply();
                Sprite cover = Sprite.Create(outTex, new Rect(0f, 0f, CoverW, CoverH),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                cover.hideFlags = HideFlags.HideAndDontSave;
                Covers[scope] = cover;
                return cover;
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] {0} 封面合成失败，退回单图", scope);
                return null;
            }
        }

        // 取分类页里承载大图标链接的 Grid 容器
        internal static ContentContainer FindLargeIconGrid(List<ContentContainer> containers)
        {
            if (containers == null) return null;
            for (int i = 0; i < containers.Count; i++)
            {
                ContentContainer c = containers[i];
                if (c == null || c.content == null
                    || c.contentLayout != ContentContainer.ContentLayout.Grid) continue;
                for (int j = 0; j < c.content.Count; j++)
                {
                    if (c.content[j] is CodexLabelWithLargeIcon) return c;
                }
            }
            return null;
        }

        // 裁掉 ui 精灵四周的透明边，后续按有效图案的宽高比等比缩放
        private static CoverLayer TrimLayer(CoverPart part, Color[] pixels, int w, int h)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a <= 0.02f) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < minX || maxY < minY) return null;

            int tw = maxX - minX + 1;
            int th = maxY - minY + 1;
            var trimmed = new Color[tw * th];
            for (int y = 0; y < th; y++)
            {
                Array.Copy(pixels, (minY + y) * w + minX, trimmed, y * tw, tw);
            }
            return new CoverLayer(part, trimmed, tw, th);
        }

        // 按给定中心、高度与倾角叠到画布上，宽度按精灵比例推导
        private static void DrawPart(Color[] canvas, CoverLayer layer)
        {
            CoverPart part = layer.Part;
            float cx = part.X * CoverW;
            float cy = part.Y * CoverH;
            float hh = part.Height * CoverH * 0.5f;
            float hw = hh * layer.W / layer.H;
            float rad = part.Angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float boundX = hw * Mathf.Abs(cos) + hh * Mathf.Abs(sin);
            float boundY = hw * Mathf.Abs(sin) + hh * Mathf.Abs(cos);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - boundX));
            int x1 = Mathf.Min(CoverW - 1, Mathf.CeilToInt(cx + boundX));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - boundY));
            int y1 = Mathf.Min(CoverH - 1, Mathf.CeilToInt(cy + boundY));

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float rx = x + 0.5f - cx;
                    float ry = y + 0.5f - cy;
                    float lx = (rx * cos + ry * sin) / hw;
                    float ly = (-rx * sin + ry * cos) / hh;
                    if (lx < -1f || lx > 1f || ly < -1f || ly > 1f) continue;

                    Color src = Sample(layer.Pixels, layer.W, layer.H,
                                       lx * 0.5f + 0.5f, ly * 0.5f + 0.5f);
                    if (src.a <= 0.001f) continue;

                    int idx = y * CoverW + x;
                    Color dst = canvas[idx];
                    float a = src.a;
                    float inv = 1f - a;
                    float outA = a + dst.a * inv;
                    if (outA <= 0f) continue;
                    float wsrc = a / outA;
                    float wdst = dst.a * inv / outA;
                    canvas[idx] = new Color(src.r * wsrc + dst.r * wdst,
                                            src.g * wsrc + dst.g * wdst,
                                            src.b * wsrc + dst.b * wdst,
                                            outA);
                }
            }
        }

        // 双线性取样，等比放大缩小时边缘不会出锯齿
        private static Color Sample(Color[] pixels, int w, int h, float u, float v)
        {
            float fx = Mathf.Clamp(u, 0f, 1f) * (w - 1);
            float fy = Mathf.Clamp(v, 0f, 1f) * (h - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, w - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, h - 1);
            int x1 = Mathf.Min(x0 + 1, w - 1);
            int y1 = Mathf.Min(y0 + 1, h - 1);
            float tx = fx - x0;
            float ty = fy - y0;
            Color a = Color.Lerp(pixels[y0 * w + x0], pixels[y0 * w + x1], tx);
            Color b = Color.Lerp(pixels[y1 * w + x0], pixels[y1 * w + x1], tx);
            return Color.Lerp(a, b, ty);
        }
    }
}
