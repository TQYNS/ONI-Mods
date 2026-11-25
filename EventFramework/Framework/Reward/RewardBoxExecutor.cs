using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class RewardBoxExecutor
    {
        // 万分制比例尺（10000 = 100%）
        private const int BP_SCALE = 10000;

        // 把奖励池定义展开成实际物品堆清单（含权重与数量区间取值）
        public static List<ItemStack> Expand(RewardBoxDef pool)
        {
            var result = new List<ItemStack>();
            if (pool == null) return result;

            RunIndependents(pool, result);
            RunGroups(pool, result);
            if (result.Count == 0) RunFallback(pool, result);

            return result;
        }

        // keyOrFullId：keyorfull id
        public static int ExpandAndGrant(string keyOrFullId)
        {
            var pool = RewardBoxRegistry.Get(keyOrFullId);
            if (pool == null)
            {
                Debug.LogWarning(RewardBoxRegistry.LOG_TAG + " 池不存在: '" + keyOrFullId + "' → 本次奖励跳过");
                return 0;
            }

            List<ItemStack> items;
            try
            {
                items = Expand(pool);
            }
            catch (Exception e)
            {
                Debug.LogWarning(RewardBoxRegistry.LOG_TAG + " 池 '" + pool.fullId + "' 展开异常 → " + e);
                return 0;
            }

            if (items.Count == 0)
            {
                Debug.LogWarning(RewardBoxRegistry.LOG_TAG + " 池 '" + pool.fullId + "' 展开为空（无 fallback 兜底）→ 本次奖励跳过");
                return 0;
            }

            int ok = 0;
            for (int i = 0; i < items.Count; i++)
            {
                try
                {
                    if (ItemStackExecutor.StoreAnywhere(new Tag(items[i].id), items[i].mass)) ok++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning(RewardBoxRegistry.LOG_TAG + " 池 '" + pool.fullId + "' 下发 '"
                                     + items[i].id + "' 异常 → " + e);
                }
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(items[i].id).Append(" ").Append(items[i].mass.ToString("0.##")).Append("kg");
            }

            Debug.Log(RewardBoxRegistry.LOG_TAG + " 池 '" + pool.fullId + "' 展开 "
                      + items.Count + " 项，派送 " + ok + " 项：" + sb);
            return ok;
        }

        private static void RunIndependents(RewardBoxDef pool, List<ItemStack> into)
        {
            if (pool.entries == null) return;
            for (int i = 0; i < pool.entries.Count; i++)
            {
                var e = pool.entries[i];
                if (e == null || !string.IsNullOrEmpty(e.group)) continue;
                RunIndependent(e, into);
            }
        }

        private static void RunIndependent(RewardEntry e, List<ItemStack> into)
        {
            int draws = e.draws > 0 ? e.draws : 1;
            int pity = e.pity_count > 0 ? e.pity_count : 0;
            int missed = 0;

            for (int n = 1; n <= draws; n++)
            {
                if (!RollHit(e, n, missed, pity))
                {
                    missed++;
                    continue;
                }

                string itemId = ResolveTarget(e);
                if (string.IsNullOrEmpty(itemId))
                {
                    WarnNoTarget(e);
                    missed++;
                    continue;
                }

                string reason;
                if (!ItemTagValidator.IsPickupableById(itemId, out reason))
                {
                    Debug.LogWarning(RewardBoxRegistry.LOG_TAG + " 池条目产出的 '" + itemId + "' 不可拾取（"
                        + reason + "）→ 跳过该次产出");
                    missed++;
                    continue;
                }

                Emit(e, itemId, into);
                missed = 0;
            }
        }

        private static bool RollHit(RewardEntry e, int drawIndex, int missed, int pity)
        {
            if (pity > 0 && missed >= pity) return true;

            int bp = e.chance_bp + e.chance_offset * (drawIndex - 1);
            if (bp <= 0) return false;
            if (bp >= BP_SCALE) return true;
            return UnityEngine.Random.Range(1, BP_SCALE + 1) <= bp;
        }

        private static void RunGroups(RewardBoxDef pool, List<ItemStack> into)
        {
            if (pool.entries == null) return;

            var order = new List<string>();
            var groups = new Dictionary<string, List<RewardEntry>>(StringComparer.Ordinal);

            for (int i = 0; i < pool.entries.Count; i++)
            {
                var e = pool.entries[i];
                if (e == null || string.IsNullOrEmpty(e.group)) continue;

                List<RewardEntry> bucket;
                if (!groups.TryGetValue(e.group, out bucket))
                {
                    bucket = new List<RewardEntry>();
                    groups[e.group] = bucket;
                    order.Add(e.group);
                }
                bucket.Add(e);
            }

            for (int i = 0; i < order.Count; i++)
            {
                RunGroup(order[i], groups[order[i]], into);
            }
        }

        private static void RunGroup(string groupName, List<RewardEntry> group, List<ItemStack> into)
        {
            int total = 0;
            for (int i = 0; i < group.Count; i++)
            {
                int w = group[i].weight;
                if (w > 0) total += w;
            }

            if (total <= 0)
            {
                Debug.LogWarning(RewardBoxRegistry.LOG_TAG + " 互斥组 '" + groupName + "' 的权重全部 <= 0 → 该组不产出");
                return;
            }

            int roll = UnityEngine.Random.Range(0, total);
            int acc = 0;
            RewardEntry picked = null;
            for (int i = 0; i < group.Count; i++)
            {
                int w = group[i].weight;
                if (w <= 0) continue;
                acc += w;
                if (roll < acc) { picked = group[i]; break; }
            }
            if (picked == null) picked = group[group.Count - 1];

            string itemId = ResolveTarget(picked);
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning(RewardBoxRegistry.LOG_TAG + " 互斥组 '" + groupName + "' 选中的条目解析不到物品 → 该组不产出");
                WarnNoTarget(picked);
                return;
            }

            string reason;
            if (!ItemTagValidator.IsPickupableById(itemId, out reason))
            {
                Debug.LogWarning(RewardBoxRegistry.LOG_TAG + " 池条目产出的 '" + itemId + "' 不可拾取（"
                    + reason + "）→ 跳过该次产出");
                return;
            }

            Emit(picked, itemId, into);
        }

        private static void RunFallback(RewardBoxDef pool, List<ItemStack> into)
        {
            if (pool.fallback == null) return;

            for (int i = 0; i < pool.fallback.Count; i++)
            {
                var e = pool.fallback[i];
                if (e == null) continue;

                string itemId = ResolveTarget(e);
                if (string.IsNullOrEmpty(itemId)) continue;

                string reason;
                if (!ItemTagValidator.IsPickupableById(itemId, out reason)) continue;

                Emit(e, itemId, into);
            }
        }

        private static void WarnNoTarget(RewardEntry e)
        {
            string key = e.HasSingleTarget
                ? ("id:" + e.id)
                : ("tags:" + (e.tags != null && e.tags.Count > 0 ? string.Join(",", e.tags.ToArray()) : "?"));

            Debug.LogWarning(RewardBoxRegistry.LOG_TAG + " 条目 " + key
                + " 解析不到物品（id 不存在 / 标签组合没有任何可拾取候选）→ 该次不产出");
        }

        private static string ResolveTarget(RewardEntry e)
        {
            if (e.HasSingleTarget) return e.id;
            if (!e.HasTagTarget) return null;

            var candidates = new List<string>();
            var prefabs = Assets.Prefabs;
            if (prefabs == null) return null;

            for (int i = 0; i < prefabs.Count; i++)
            {
                var kpid = prefabs[i];
                if (kpid == null) continue;
                if (!kpid.HasTag(GameTags.Pickupable)) continue;

                // 修复：标签也可能挂在元素上（如 GameTags.RefinedMetal），故除 KPrefabID 外再查元素标签
                var pe = kpid.GetComponent<PrimaryElement>();

                bool allMatch = true;
                for (int t = 0; t < e.tags.Count; t++)
                {
                    string tagName = e.tags[t];
                    if (string.IsNullOrEmpty(tagName)) { allMatch = false; break; }
                    Tag want = new Tag(tagName);
                    bool has = kpid.HasTag(want)
                        || (pe != null && pe.Element != null && pe.Element.HasTag(want));
                    if (!has) { allMatch = false; break; }
                }
                if (!allMatch) continue;

                string name = kpid.PrefabTag.Name;
                if (!string.IsNullOrEmpty(name)) candidates.Add(name);
            }

            if (candidates.Count == 0) return null;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private static void Emit(RewardEntry e, string itemId, List<ItemStack> into)
        {
            int count = RollAmount(e.parsedAmount);
            float mass = UnitMass(itemId) * count;
            if (mass < ItemStackParser.MIN_MASS) mass = ItemStackParser.MIN_MASS;

            into.Add(new ItemStack { id = itemId, mass = mass });
        }

        private static int RollAmount(ParsedAmount parsed)
        {
            if (parsed == null) return 1;
            if (parsed.isFixed) return parsed.fixedValue > 0 ? parsed.fixedValue : 1;

            var table = parsed.weightedList;
            if (table == null || table.Count == 0) return 1;

            int total = 0;
            for (int i = 0; i < table.Count; i++) total += table[i][1];
            if (total <= 0) return table[0][0];

            int roll = UnityEngine.Random.Range(0, total);
            int acc = 0;
            for (int i = 0; i < table.Count; i++)
            {
                acc += table[i][1];
                if (roll < acc) return table[i][0];
            }
            return table[table.Count - 1][0];
        }

        private static float UnitMass(string itemId)
        {
            try
            {
                var prefab = Assets.TryGetPrefab(new Tag(itemId));
                if (prefab != null)
                {
                    var pe = prefab.GetComponent<PrimaryElement>();
                    if (pe != null && pe.MassPerUnit > 0f) return pe.MassPerUnit;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(RewardBoxRegistry.LOG_TAG + " 取 '" + itemId
                                 + "' 的单位质量失败，按 1kg 计 → " + e);
            }
            return 1f;
        }
    }
}
