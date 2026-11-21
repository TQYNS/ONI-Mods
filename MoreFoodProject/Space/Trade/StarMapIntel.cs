// StarMapIntel.cs — 星图数据：阅读即解析（固定消耗 1 份，不可分割）

using System;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class StarMapIntel
    {
        internal const string PrefabId = "StarMapIntel";

        // 假情报概率（原 trade/config.yaml 的 fake_map_chance，随字段删除下移到代码）
        internal const float FakeChance = 0.2f;

        // 挂"阅读"入口：与原版故事特性同一套 Lore 交互（卷轴也是这么接的）
        internal static void AttachLore(GameObject go)
        {
            if (go == null)
            {
                return;
            }
            LoreBearerUtil.AddLoreTo(go, delegate(InfoDialogScreen screen)
            {
                bool consume = true;
                try
                {
                    if (UnityEngine.Random.value < FakeChance)
                    {
                        MFLog.Warn(LogCat.Trade, "[Space] 星图数据校验失败（假情报），未解锁任何坐标");
                        screen.AddPlainText("坐标校验失败：这份数据只是一堆噪声，没有任何可用信息。");
                    }
                    else if (SpaceReveal.RevealRandomSubCell())
                    {
                        screen.AddPlainText("星图数据已解析，子星图有一处星域被揭示。");
                    }
                    else
                    {
                        screen.AddPlainText("子图坐标已全部解锁，这份数据没有更多可解析的内容。");
                        consume = false;   // 没有解锁成功 ⇒ 留着不吞
                    }
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Trade, e, "[Space] 星图数据解析异常");
                    screen.AddPlainText("解析失败，数据已保留。");
                    consume = false;
                }
                if (consume)
                {
                    ConsumeOne(go);
                }
            });
        }

        // 固定消耗 1 份（可堆叠时只抽 1 个，不可分割）
        private static void ConsumeOne(GameObject go)
        {
            try
            {
                if (go == null)
                {
                    return;
                }
                Pickupable p = go.GetComponent<Pickupable>();
                if (p != null && p.TotalAmount > 1f)
                {
                    Pickupable one = p.Take(1f);
                    if (one != null)
                    {
                        Util.KDestroyGameObject(one.gameObject);
                        return;
                    }
                }
                Util.KDestroyGameObject(go);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 消耗星图数据失败");
            }
        }
    }
}
