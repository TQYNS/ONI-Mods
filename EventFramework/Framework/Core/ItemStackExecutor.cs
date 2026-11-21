// ItemStackExecutor.cs — 事件框架 · 【提交 / 奖励的执行器】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class ItemStackExecutor
    {
        // 1g 容差：float 累加误差在 100 项左右可达 0.001
        private const float EPS = 0.01f;

        // TrySubmit 的返回状态
        public enum SubmitResult
        {
            // 无有效条目（空组 / 全部 id 失败）→ 略过本次提交
            Skipped,
            // 材料不够 → 一个都不扣
            Failed,
            // 扣除成功
            Success
        }

        // 前置检查（只检查不扣）——供 ApplyChoice 前置拦截用
        public static bool CanSubmit(ItemParseResult res, string owner, bool warnEnabled)
        {
            // 空组 / 全部 id 失败：视为通过（用户明说"略过这个条件"）
            if (res == null || res.IsEmpty) return true;

            // 逐条检查
            for (int i = 0; i < res.valid.Count; i++)
            {
                var req = res.valid[i];
                float total = SumAvailableAcrossMap(new Tag(req.id));

                if (total + EPS < req.mass)
                {
                    // 检查失败日志是【玩家可见反馈】，不受 Warning 静默控制
                    Debug.Log("[MoreFood/Event][Item] " + owner + " 提交检查失败：需要 "
                              + req.mass + " kg '" + req.id + "'，全地图只有 "
                              + total.ToString("F2") + " kg");
                    return false;
                }
            }

            return true;
        }

        // 提交：先检查全部（不动任何 Storage）→ 再逐个扣
        public static SubmitResult TrySubmit(ItemParseResult res, string owner, bool warnEnabled)
        {
            // 规则 4：无有效条目 → 略过条件
            if (res == null || res.IsEmpty)
            {
                // ：选项压根没配 submit 时不再刷"无有效条目"（那只是没这个条件）
                if (warnEnabled && res != null && !res.notConfigured)
                    Debug.LogWarning("[MoreFood/Event][Item] " + owner
                                     + " 提交组无有效条目 → 略过本次提交");
                return SubmitResult.Skipped;
            }

            try
            {
                // ---- 第一步：全检查（不动任何 Storage）----
                for (int i = 0; i < res.valid.Count; i++)
                {
                    var req = res.valid[i];
                    float total = SumAvailableAcrossMap(new Tag(req.id));
                    if (total + EPS < req.mass)
                    {
                        Debug.Log("[MoreFood/Event][Item] " + owner + " 提交失败：需要 "
                                  + req.mass + " kg '" + req.id + "'，全地图（Storage 内）只有 "
                                  + total.ToString("F2") + " kg");
                        return SubmitResult.Failed;
                    }
                }

                // ---- 第二步：逐个扣 ----
                for (int i = 0; i < res.valid.Count; i++)
                {
                    var req = res.valid[i];
                    float consumed = ConsumeAcrossMap(new Tag(req.id), req.mass);
                    if (consumed + EPS < req.mass)
                    {
                        // 正常流程不会走到（第一步已校验）；浮点误差或并发下会到这里
                        Debug.LogWarning("[MoreFood/Event][Item] " + owner
                                         + " 部分扣除异常：需要 " + req.mass
                                         + " kg '" + req.id + "'，实际扣了 "
                                         + consumed.ToString("F2") + " kg（按已扣处理）");
                    }
                }

                // ---- 规则 5：提交成功后 Debug 检查信息 ----
                var sb = new System.Text.StringBuilder();
                sb.Append("[MoreFood/Event][Item][提交成功] ").Append(owner).Append(" ⇒ ");
                for (int i = 0; i < res.valid.Count; i++)
                {
                    if (i > 0) sb.Append(" + ");
                    sb.Append(res.valid[i].mass).Append("kg ").Append(res.valid[i].id);
                }
                Debug.Log(sb.ToString());

                return SubmitResult.Success;
            }
            catch (System.Exception e)
            {
                // 规则 4 例外：异常无论 warnEnabled 都输出
                Debug.LogWarning("[MoreFood/Event][Item] TrySubmit 异常（按失败处理）: " + e);
                return SubmitResult.Failed;
            }
        }

        // 奖励：逐条下发（物品 → 找能接货的 Storage；池 → 展开后逐条下发）
        public static int Grant(ItemParseResult res, string owner, bool warnEnabled)
        {
            if (res == null || res.IsEmpty)
            {
                if (warnEnabled && res != null && !res.notConfigured)
                    Debug.LogWarning("[MoreFood/Event][Item] " + owner
                                     + " 奖励组无有效条目 → 略过本次奖励");
                return 0;
            }

            int ok = 0;
            for (int i = 0; i < res.valid.Count; i++)
            {
                var rw = res.valid[i];
                try
                {
                    // 池引用：展开成实际物品后各自下发，成功的条数计入
                    if (rw.isRewardBox)
                    {
                        if (RewardBoxExecutor.ExpandAndGrant(rw.id) > 0) ok++;
                        continue;
                    }

                    if (StoreAnywhere(new Tag(rw.id), rw.mass)) ok++;
                    else if (warnEnabled)
                        Debug.LogWarning("[MoreFood/Event][Item] " + owner
                                         + " 奖励 '" + rw.id + "' " + rw.mass
                                         + "kg 无法派送（没有打印门，且所有 Storage 已满）");
                }
                catch (System.Exception e)
                {
                    // 规则 4 例外：异常无论 warnEnabled 都输出
                    Debug.LogWarning("[MoreFood/Event][Item] " + owner + " 奖励 '"
                                   + rw.id + "' 异常: " + e);
                }
            }

            if (ok > 0)
                Debug.Log("[MoreFood/Event][Item] " + owner + " 奖励下发 "
                          + ok + "/" + res.valid.Count + " 条");
            return ok;
        }

        // 底层：全地图物品检索
        private static WorldInventory GetWorldInventory()
        {
            try
            {
                var cm = ClusterManager.Instance;
                if (cm == null) return null;
                var world = cm.activeWorld;
                if (world == null) return null;
                return world.worldInventory;
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("item:inventory", "[Item] 取世界库存失败 → 按无库存处理", e);
                return null;
            }
        }

        // tag → 该 world 里全部该 tag 的 Pickupable（O(1) 查表，由 WorldInventory 增量维护）。
        private static ICollection<Pickupable> GetIndexedPickups(Tag tag, out bool usable)
        {
            usable = false;
            try
            {
                var inv = GetWorldInventory();
                if (inv == null || !inv.HasValidCount) return null;

                usable = true;
                return inv.GetPickupables(tag, false);   // 没有该 tag 时返回 null = 该 tag 为 0 件
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("item:pickups", "[Item] 取可拾取列表失败 → 本 tag 按 0 件处理", e);
                usable = false;
                return null;
            }
        }

        // 判断某个 Pickupable 是否在 Storage 里（而不是散落在地上）。
        private static Storage GetOwningStorage(Pickupable p)
        {
            if (p == null) return null;
            // 散落在地上时返回 null（Storage 不在 pickup 自己身上，只能从 p.storage 取）
            
            
            return p.storage;
        }

        private static float MassOf(Pickupable p)
        {
            if (p == null) return 0f;
            var pe = p.GetComponent<PrimaryElement>();
            return pe == null ? 0f : pe.Mass;
        }

        // 全地图某 tag 的可用总质量（语义修正：
        public static float SumAvailableAcrossMap(Tag tag)
        {
            bool usable;
            var bucket = GetIndexedPickups(tag, out usable);
            if (usable)
            {
                float indexed = 0f;
                if (bucket != null)
                {
                    var list = new List<Pickupable>(bucket);
                    for (int i = 0; i < list.Count; i++) indexed += MassOf(list[i]);
                }
                return indexed;
            }

            float total = 0f;
            var all = Components.Pickupables.Items;

            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null) continue;

                var kp = p.GetComponent<KPrefabID>();
                if (kp == null || !kp.HasTag(tag)) continue;

                total += MassOf(p);
            }
            return total;
        }

        // 全地图（Storage 内 + 地上）逐个扣，返回实际扣除量
        public static float ConsumeAcrossMap(Tag tag, float need)
        {
            bool usable;
            var bucket = GetIndexedPickups(tag, out usable);
            if (usable)
            {
                float indexed = 0f;
                if (bucket == null) return 0f;

                var list = new List<Pickupable>(bucket);
                for (int i = 0; i < list.Count && indexed + EPS < need; i++)
                {
                    var p = list[i];
                    if (p == null) continue;

                    float mass = MassOf(p);
                    if (mass <= EPS) continue;

                    float take = Mathf.Min(mass, need - indexed);
                    ConsumeFrom(p, tag, take);
                    indexed += take;
                }
                return indexed;
            }

            float remaining = need;
            var all = Components.Pickupables.Items;

            for (int i = 0; i < all.Count && remaining > EPS; i++)
            {
                var p = all[i];
                if (p == null) continue;

                var kp = p.GetComponent<KPrefabID>();
                if (kp == null || !kp.HasTag(tag)) continue;

                float take = Mathf.Min(MassOf(p), remaining);
                if (take <= EPS) continue;

                ConsumeFrom(p, tag, take);
                remaining -= take;
            }
            return need - Mathf.Max(0f, remaining);
        }

        // 从某个物品上扣：在 Storage 里 → 走 Storage（它会处理 split / 销毁）；
        private static void ConsumeFrom(Pickupable p, Tag tag, float amount)
        {
            if (p == null) return;

            var st = GetOwningStorage(p);
            if (st != null)
            {
                st.ConsumeIgnoringDisease(tag, amount);
                return;
            }

            var pe = p.GetComponent<PrimaryElement>();
            if (pe == null) return;

            pe.Mass = pe.Mass - amount;
            if (pe.Mass <= 0f)
            {
                try { Util.KDestroyGameObject(p.gameObject); }
                catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("item:destroy", "[Item] 销毁已耗尽物品失败", e); }
            }
        }

        // 找一个能接货的 Storage，把 mass 塞进去。
        public static bool StoreAnywhere(Tag tag, float mass)
        {
            // ---- ⓪ 优先走打印门：原版派送函数 + 传送门动画（不碰打印冷却）----
            if (PodDelivery.TryDeliver(tag.Name, mass)) return true;

            // ---- ① 先拿 prefab（找不到直接返回，别浪费时间遍历建筑）----
            var prefab = Assets.TryGetPrefab(tag);
            if (prefab == null)
            {
                Debug.LogWarning("[MoreFood/Event][Item] Assets.TryGetPrefab('" + tag.Name
                                 + "') 返回 null → 无法下发奖励");
                return false;
            }

            // ---- ② 找有空间的目标 Storage ----
            var all = Components.BuildingCompletes.Items;
            for (int i = 0; i < all.Count; i++)
            {
                var bc = all[i];
                if (bc == null) continue;

                var st = bc.GetComponent<Storage>();
                if (st == null) continue;
                if (!st.showInUI) continue;

                // ⚠ MassStored() 上一轮通过了编译 ⇒ 保留
                float used = st.MassStored();
                if (used + mass > st.capacityKg) continue;

                // ---- ③ 实例化物品 + 设置质量 ----
                var go = Util.KInstantiate(prefab, null, null);
                if (go == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Item] KInstantiate('" + tag.Name + "') 失败");
                    return false;
                }

                var pe = go.GetComponent<PrimaryElement>();
                if (pe != null) pe.Mass = mass;   // prefab 默认质量可能不是我们想要的
                go.SetActive(true);                // 必须在 Store 之前激活

                // ---- ④ 塞进 Storage（5 参：hide_popups / block_events / do_disease_transfer / is_deserializing）----
                st.Store(go, true, false, true, false);
                return true;
            }
            return false;
        }

    }
}
