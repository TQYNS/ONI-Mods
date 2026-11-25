using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    // 战斗动画总入口（§15 + K5/K6）

    internal static class CombatFx
    {
        private static readonly HashSet<long> playedThisFrame = new HashSet<long>();
        private static int activeCount;

        internal static void BeginFrame()
        {
            playedThisFrame.Clear();
        }

        // 两个入口

        internal static void PlayRocket(Clustercraft craft, SpaceMonster monster, AmmoDef ammo)
        {
            try
            {
                if (craft == null || monster == null || ammo == null)
                {
                    return;
                }
                if (!Visible())
                {
                    return;
                }
                if (!playedThisFrame.Add(Key(craft.GetInstanceID(), monster.GetInstanceID())))
                {
                    return;
                }
                ClusterMapScreen screen = ClusterMapScreen.Instance;
                Sprite sprite = ResolveAmmoSprite(ammo);
                float baseSize = StarMapVisuals.GetEntityWidth(screen, craft);
                float hexW = StarMapVisuals.GetHexWidth(screen, craft.Location);
                float size;
                if (baseSize > 0.5f)
                {
                    size = baseSize * CombatDb.Shared.rocketProjectileRatio;
                }
                else
                {
                    size = Mathf.Max(1f, hexW) * CombatDb.Shared.fallbackHexRatio;   // 兜底（漏洞3）
                }
                PlayInternal(craft.Location, monster.Location, sprite, ammo.fxColor, size, ammo.pellets, true);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 战斗特效(火箭)失败");
            }
        }

        internal static void PlayMonster(SpaceMonster monster, Clustercraft craft, CombatMonsterDef def)
        {
            try
            {
                if (monster == null || craft == null || def == null)
                {
                    return;
                }
                if (!Visible())
                {
                    return;
                }
                if (!playedThisFrame.Add(Key(monster.GetInstanceID(), craft.GetInstanceID())))
                {
                    return;
                }
                ClusterMapScreen screen = ClusterMapScreen.Instance;
                float hexW = StarMapVisuals.GetHexWidth(screen, monster.Location);
                float size = Mathf.Max(1f, hexW) * CombatDb.Shared.monsterProjectileRatio;
                PlayInternal(monster.Location, craft.Location, null, def.fxColor, size, 1, false);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 战斗特效(怪物)失败");
            }
        }

        // 内部

        private static long Key(int attacker, int target)
        {
            return (long)attacker * 100000L + target;
        }

        private static bool Visible()
        {
            if (!CombatDb.Shared.fxEnabled)
            {
                return false;
            }
            ClusterMapScreen screen = ClusterMapScreen.Instance;
            if (CombatDb.Shared.fxRequireMapOpen)
            {
                if (screen == null || !screen.gameObject.activeInHierarchy)
                {
                    return false;
                }
            }
            if (SpaceHost.Instance == null)
            {
                return false;   // 漏洞9：会话对象没了就直接结算不播特效
            }
            return true;
        }

        // 弹药图标解析链（K5）：Def.GetUISprite → Assets.GetSprite(icon_sprite) → null（光团）。
        private static Sprite ResolveAmmoSprite(AmmoDef ammo)
        {
            try
            {
                if (!string.IsNullOrEmpty(ammo.prefabId))
                {
                    Tuple<Sprite, Color> t = Def.GetUISprite(new Tag(ammo.prefabId), "ui");
                    if (t != null && t.first != null)   // Klei 自己的 Tuple 用 first/second
                    {
                        return t.first;
                    }
                }
            }
            catch (Exception)
            {
                // expected: Def.GetUISprite 解析失败时改走动画取图，不影响特效继续播放
            }
            try
            {
                if (!string.IsNullOrEmpty(ammo.anim))
                {
                    KAnimFile file = Assets.GetAnim(ammo.anim);
                    if (file != null)
                    {
                        Sprite s = Def.GetUISpriteFromMultiObjectAnim(file, "ui", false, "");
                        if (s != null)
                        {
                            return s;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // expected: 动画取图也失败时图标退化为纯色光团（返回 null）
            }
            return null;   // 退化为光团
        }

        private static float ClampSize(float sizeContent, float scale)
        {
            float s = Mathf.Max(0.0001f, scale);
            float min = CombatDb.Shared.minPixels / s;
            float max = CombatDb.Shared.maxPixels / s;
            return Mathf.Clamp(sizeContent, min, Mathf.Max(min, max));
        }

        private static void PlayInternal(AxialI from, AxialI to, Sprite sprite, Color color,
            float sizeContent, int pellets, bool rocket)
        {
            ClusterMapScreen screen = ClusterMapScreen.Instance;
            if (screen == null || !screen.gameObject.activeInHierarchy)
            {
                return;
            }
            SpaceZones z = StarMapVisuals.Zones;
            if (z != null && z.valid && z.IsSub(from) != z.IsSub(to))
            {
                Debug.LogWarning("[Space] 跨区攻击特效跳过（不该发生，漏洞4）："
                    + SpaceCfg.Pos(from) + "→" + SpaceCfg.Pos(to));
                return;
            }
            if (activeCount >= CombatDb.Shared.maxConcurrent)
            {
                return;   // 超并发只跳过特效，伤害已结算
            }
            Vector3 a;
            Vector3 b;
            Transform root;
            Transform root2;
            if (!StarMapVisuals.TryCellWorld(screen, from, out a, out root))
            {
                return;
            }
            if (!StarMapVisuals.TryCellWorld(screen, to, out b, out root2))
            {
                return;
            }
            if (root == null)
            {
                return;
            }
            float scale = StarMapVisuals.GetMapScale(screen);
            float size = ClampSize(sizeContent, scale);
            if (pellets < 1)
            {
                pellets = 1;
            }
            for (int i = 0; i < pellets; i++)
            {
                if (activeCount >= CombatDb.Shared.maxConcurrent)
                {
                    break;
                }
                Vector3 jitter = Vector3.zero;
                if (pellets > 1)
                {
                    float j = CombatDb.Shared.pelletJitterPx;
                    jitter = new Vector3(UnityEngine.Random.Range(-j, j), UnityEngine.Random.Range(-j, j), 0f);
                }
                float delay = i * CombatDb.Shared.pelletDelay + UnityEngine.Random.Range(0f, CombatDb.Shared.jitterDelay);
                activeCount++;
                try
                {
                    SpaceHost.Instance.StartCoroutine(MFLog.SafeRoutine(FlyRoutine(root, a + jitter, b, sprite, color, size, delay), "routine:CombatFx"));
                }
                catch (Exception e)
                {
                    activeCount = Mathf.Max(0, activeCount - 1);
                    MFLog.Exc(LogCat.Combat, e, "[Space] 战斗特效协程启动失败");
                }
            }
        }

        // 单点爆发（紧急脱离的脱离点闪光等；K5：warp_anim 完整播放待美术资源）。
        internal static void PlayBurst(AxialI at, Color color, float sizeContent)
        {
            try
            {
                if (!Visible())
                {
                    return;
                }
                ClusterMapScreen screen = ClusterMapScreen.Instance;
                if (screen == null)
                {
                    return;
                }
                if (activeCount >= CombatDb.Shared.maxConcurrent)
                {
                    return;
                }
                Vector3 w;
                Transform root;
                if (!StarMapVisuals.TryCellWorld(screen, at, out w, out root))
                {
                    return;
                }
                if (root == null)
                {
                    return;
                }
                float scale = StarMapVisuals.GetMapScale(screen);
                float size = ClampSize(sizeContent, scale);
                activeCount++;
                SpaceHost.Instance.StartCoroutine(MFLog.SafeRoutine(BurstRoutine(root, w, color, size), "routine:CombatFx"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 爆发特效失败");
            }
        }

        private static IEnumerator BurstRoutine(Transform root, Vector3 at, Color color, float size)
        {
            try
            {
                GameObject go = new GameObject("SpaceBurst", typeof(RectTransform), typeof(Image));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(root, false);
                rt.position = at;
                Image img = go.GetComponent<Image>();
                img.sprite = SpaceCombatFx.Ball();
                img.color = new Color(color.r, color.g, color.b, 0.7f);
                img.raycastTarget = false;
                float dur = Mathf.Max(0.05f, CombatDb.Shared.hitDuration * 2f);
                float t = 0f;
                while (t < dur && go != null)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / dur);
                    if (rt != null)
                    {
                        float s = size * Mathf.Lerp(0.6f, 1.4f, k);
                        rt.sizeDelta = new Vector2(s, s);
                    }
                    if (img != null)
                    {
                        img.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.7f, 0f, k));
                    }
                    yield return null;
                }
                if (go != null)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
            finally
            {
                activeCount = Mathf.Max(0, activeCount - 1);
            }
        }

        private static IEnumerator FlyRoutine(Transform root, Vector3 from, Vector3 to, Sprite sprite,
            Color color, float size, float delay)
        {
            try
            {
                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }
                GameObject go = new GameObject("SpaceShot", typeof(RectTransform), typeof(Image));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(root, false);
                rt.position = from;
                rt.sizeDelta = new Vector2(size, size);
                Image img = go.GetComponent<Image>();
                img.sprite = sprite != null ? sprite : SpaceCombatFx.Ball();
                img.color = color;
                img.raycastTarget = false;
                img.preserveAspect = true;

                float dur = Mathf.Max(0.02f, CombatDb.Shared.flyDuration);
                float t = 0f;
                while (t < dur && go != null)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / dur);
                    if (rt != null)
                    {
                        rt.position = Vector3.Lerp(from, to, k);
                    }
                    yield return null;
                }
                if (go != null)
                {
                    UnityEngine.Object.Destroy(go);
                }
                // Impact：尺寸 0.8→0.6、alpha 0.7→0（§15.6）
                float hit = CombatDb.Shared.hitDuration;
                if (root != null && hit > 0f)
                {
                    GameObject hgo = new GameObject("SpaceHit", typeof(RectTransform), typeof(Image));
                    RectTransform hrt = hgo.GetComponent<RectTransform>();
                    hrt.SetParent(root, false);
                    hrt.position = to;
                    Image himg = hgo.GetComponent<Image>();
                    himg.sprite = sprite != null ? sprite : SpaceCombatFx.Ball();
                    himg.raycastTarget = false;
                    himg.preserveAspect = true;
                    float ht = 0f;
                    while (ht < hit && hgo != null)
                    {
                        ht += Time.unscaledDeltaTime;
                        float k = Mathf.Clamp01(ht / hit);
                        if (hrt != null)
                        {
                            float s = size * Mathf.Lerp(0.80f, 0.60f, k);
                            hrt.sizeDelta = new Vector2(s, s);
                        }
                        if (himg != null)
                        {
                            himg.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.7f, 0f, k));
                        }
                        yield return null;
                    }
                    if (hgo != null)
                    {
                        UnityEngine.Object.Destroy(hgo);
                    }
                }
            }
            finally
            {
                activeCount = Mathf.Max(0, activeCount - 1);
            }
        }
    }
}
