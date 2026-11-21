using System;
using EventFramework.Distribution.Contracts;
using System.Collections.Generic;
using UnityEngine;
using MoreFood_KFC;

namespace MoreFood_KFC
{
    internal static class SpacePlanEvents
    {
        internal const string RareMeteorShowerKey = "plan_rare_meteor_shower";
        internal const string PrehistoricBeastKey = "plan_prehistoric_beast";
        internal const string MysteryGiftKey = "plan_mystery_gift";
        internal const string MysteryGiftAcceptHook = "mystery_gift.accept";

        private static bool registered;

        internal static void RegisterHandlers()
        {
            if (registered)
            {
                return;
            }
            registered = true;
            try
            {
                EventFrameworkApi.RegisterHandler(RareMeteorShowerKey, new EventFramework.Distribution.Contracts.EventHandler
                {
                    onTrigger = RareMeteorShower
                });
                EventFrameworkApi.RegisterHandler(PrehistoricBeastKey, new EventFramework.Distribution.Contracts.EventHandler
                {
                    onTrigger = PrehistoricBeast
                });
                EventFrameworkApi.RegisterHandler(MysteryGiftKey, new EventFramework.Distribution.Contracts.EventHandler
                {
                    // 触发只负责开面板；发放等玩家点「签收」，避免与 hook 重复发一次
                    onTrigger = delegate(IEventContext inst) { }
                });
                // 选项「签收」是唯一发放入口
                EventFrameworkApi.RegisterHook(MysteryGiftAcceptHook, MysteryGift);
                MFLog.Info(LogCat.Event,"[Space] 计划事件处理器已注册：" + RareMeteorShowerKey + " / " + PrehistoricBeastKey
                    + " / " + MysteryGiftKey + "（选项 hook=" + MysteryGiftAcceptHook + "）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 计划事件处理器注册失败");
            }
        }

        private static void RareMeteorShower(IEventContext inst)
        {
            try
            {
                float roll = UnityEngine.Random.value;
                int spawned;
                string label;
                if (roll < 0.15f)
                {
                    spawned = CometRain.SpawnResourceComets(SimHashes.Fullerene, 3, 120f, 260f);
                    label = "富勒烯陨石雨";
                }
                else if (roll < 0.35f)
                {
                    spawned = CometRain.SpawnResourceComets(SimHashes.Niobium, 3, 120f, 260f);
                    label = "铌矿陨石雨";
                }
                else if (roll < 0.60f)
                {
                    spawned = CometRain.SpawnResourceComets(SimHashes.Diamond, 4, 120f, 260f);
                    label = "钻石陨石雨";
                }
                else if (roll < 0.90f)
                {
                    spawned = CometRain.SpawnResourceComets(SimHashes.UraniumOre, 4, 120f, 260f);
                    label = "铀矿陨石雨";
                }
                else
                {
                    spawned = CometRain.SpawnCreatureComets("Moo", 2);
                    label = "海牛坠落";
                }
                MFLog.Info(LogCat.Event,"[Space] 稀有陨石雨（" + label + "）：生成 " + spawned + " 个");
                // 正文由 yaml tooltip 提供（静态文案）
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 稀有陨石雨执行失败");
            }
        }

        private static void MysteryGift(IEventContext inst)
        {
            try
            {
                float roll = UnityEngine.Random.value;
                if (roll < 0.40f)
                {
                    GiftSeeds(inst);
                }
                else if (roll < 0.70f)
                {
                    GiftRareResource(inst);
                }
                else if (roll < 0.85f)
                {
                    GiftFood(inst);
                }
                else if (roll < 0.95f)
                {
                    GiftCreature(inst);
                }
                else
                {
                    float amount = UnityEngine.Random.Range(1000f, 2000f);
                    TradeRegistry.AddCurrency(amount);
                    inst.SetNoticeArg("result", "星币 " + amount.ToString("0") + " 直接到账");
                    MFLog.Info(LogCat.Event,"[Space] 神秘礼物 → 星币 " + amount);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 神秘礼物执行失败");
            }
        }

        private static void GiftSeeds(IEventContext inst)
        {
            int want = UnityEngine.Random.Range(3, 9);
            string seedId = PickSeedPrefab();
            if (string.IsNullOrEmpty(seedId))
            {
                Debug.LogWarning("[Space] 神秘礼物：找不到可用的作物种子 prefab");
                return;
            }
            if (!EventFrameworkApi.Deliver(seedId, (float)want))
            {
                inst.SetNoticeArg("result", "打印舱没有空位，礼物没能送达");
                Debug.LogWarning("[Space] 神秘礼物：种子派送失败 " + seedId);
                return;
            }
            ScheduleSeedMutation(want);
            inst.SetNoticeArg("result", "收到变异作物种子 ×" + want);
            MFLog.Info(LogCat.Event,"[Space] 神秘礼物 → 变异种子 " + seedId + " ×" + want);
        }

        private static void ScheduleSeedMutation(int want)
        {
            try
            {
                GameScheduler.Instance.Schedule("SpaceMysterySeedMutate", 2f, delegate(object data)
                {
                    MutateSeedsNearPad((int)data);
                }, want);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 神秘礼物：安排种子变异失败");
            }
        }

        private static void MutateSeedsNearPad(int want)
        {
            try
            {
                Telepad pad = FindTelepad();
                if (pad == null)
                {
                    return;
                }
                int padCell = Grid.PosToCell(pad);
                List<MutantPlant> all = SpaceRealEventRuntime.AllSceneComponents<MutantPlant>();
                int done = 0;
                for (int i = 0; i < all.Count && done < want; i++)
                {
                    MutantPlant mp = all[i];
                    if (mp == null || !mp.IsOriginal)
                    {
                        continue;
                    }
                    if (Grid.GetCellDistance(padCell, Grid.PosToCell(mp)) > 2)
                    {
                        continue;
                    }
                    try
                    {
                        mp.Mutate();
                        done++;
                    }
                    catch (Exception e)
                    {
                        MFLog.Exc(LogCat.Event, e, "[Space] 神秘礼物：单个种子变异失败");
                    }
                }
                MFLog.Info(LogCat.Event,"[Space] 神秘礼物：已变异种子 " + done + "/" + want);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 神秘礼物：种子变异失败");
            }
        }

        private static void GiftRareResource(IEventContext inst)
        {
            float[] kilos = { 100f, 200f, 300f };
            float kg = kilos[UnityEngine.Random.Range(0, kilos.Length)];
            string[] pool = { "Niobium", "Fullerene", "Diamond", "UraniumOre", "GoldAmalgam" };
            string[] names = { "铌", "富勒烯", "钻石", "铀矿", "金汞齐" };
            int idx = UnityEngine.Random.Range(0, pool.Length);
            if (!EventFrameworkApi.Deliver(pool[idx], kg))
            {
                inst.SetNoticeArg("result", "打印舱没有空位，礼物没能送达");
                Debug.LogWarning("[Space] 神秘礼物：稀有资源派送失败 " + pool[idx]);
                return;
            }
            inst.SetNoticeArg("result", "收到稀有资源：" + names[idx] + " " + kg.ToString("0") + "kg");
            MFLog.Info(LogCat.Event,"[Space] 神秘礼物 → " + names[idx] + " " + kg);
        }

        private static void GiftFood(IEventContext inst)
        {
            string foodName;
            float kcalPerUnit;
            string foodId = PickFood(out foodName, out kcalPerUnit);
            if (string.IsNullOrEmpty(foodId))
            {
                Debug.LogWarning("[Space] 神秘礼物：找不到可用食物 prefab");
                return;
            }
            float targetKcal = UnityEngine.Random.Range(3000f, 12000f);
            int count = Mathf.Clamp(Mathf.RoundToInt(targetKcal / Mathf.Max(1f, kcalPerUnit)), 1, 40);
            if (!EventFrameworkApi.Deliver(foodId, (float)count))
            {
                inst.SetNoticeArg("result", "打印舱没有空位，礼物没能送达");
                Debug.LogWarning("[Space] 神秘礼物：食物派送失败 " + foodId);
                return;
            }
            float total = count * kcalPerUnit;
            inst.SetNoticeArg("result", "收到食物：" + foodName + " ×" + count
                + "（约 " + total.ToString("0") + " 千卡）");
            MFLog.Info(LogCat.Event,"[Space] 神秘礼物 → " + foodId + " ×" + count + "（约 " + total.ToString("0") + " 千卡）");
        }

        private static void GiftCreature(IEventContext inst)
        {
            string[] pool = { "Hatch", "Pacu", "Pip", "Moo" };
            string pick = pool[UnityEngine.Random.Range(0, pool.Length)];
            int want = UnityEngine.Random.Range(1, 4);
            string display = pick;
            GameObject prefab = Assets.TryGetPrefab(new Tag(pick));
            if (prefab != null)
            {
                KPrefabID pid = prefab.GetComponent<KPrefabID>();
                if (pid != null)
                {
                    string proper = pid.GetProperName();
                    if (!string.IsNullOrEmpty(proper))
                    {
                        display = proper;
                    }
                }
            }
            if (!EventFrameworkApi.Deliver(pick, (float)want))
            {
                inst.SetNoticeArg("result", "打印舱没有空位，礼物没能送达");
                Debug.LogWarning("[Space] 神秘礼物：小动物派送失败 " + pick);
                return;
            }
            inst.SetNoticeArg("result", "收到小动物：" + display + " ×" + want);
            MFLog.Info(LogCat.Event,"[Space] 神秘礼物 → 生物 " + pick + " ×" + want);
        }

        private static readonly List<string> seedPool = new List<string>();

        private static string PickSeedPrefab()
        {
            if (seedPool.Count == 0)
            {
                CollectSeeds();
            }
            if (seedPool.Count == 0)
            {
                return null;
            }
            return seedPool[UnityEngine.Random.Range(0, seedPool.Count)];
        }

        private static void CollectSeeds()
        {
            try
            {
                List<GameObject> all = Assets.GetPrefabsWithComponent<MutantPlant>();
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        AddSeed(all[i]);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 神秘礼物：收集变异种子失败");
            }
            if (seedPool.Count == 0)
            {
                try
                {
                    List<GameObject> all = Assets.GetPrefabsWithTag(new Tag("Seed"));
                    if (all != null)
                    {
                        for (int i = 0; i < all.Count; i++)
                        {
                            AddSeed(all[i]);
                        }
                    }
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Event, e, "[Space] 神秘礼物：收集种子失败");
                }
            }
            MFLog.Info(LogCat.Event,"[Space] 神秘礼物：可用种子 " + seedPool.Count + " 种");
        }

        private static void AddSeed(GameObject go)
        {
            try
            {
                if (go == null || go.GetComponent<Pickupable>() == null)
                {
                    return;
                }
                KPrefabID pid = go.GetComponent<KPrefabID>();
                if (pid == null || !pid.HasTag(new Tag("Seed")))
                {
                    return;
                }
                string id = pid.PrefabTag.Name;
                if (string.IsNullOrEmpty(id) || seedPool.Contains(id))
                {
                    return;
                }
                seedPool.Add(id);
            }
            catch (Exception)
            {
                // expected: 种子池扫描失败按无种子处理，礼物退化为食物
            }
        }

        private static string PickFood(out string displayName, out float kcalPerUnit)
        {
            displayName = null;
            kcalPerUnit = 0f;
            List<string> pool = new List<string>();
            try
            {
                List<string> ours = MoreFood_KFC.ModFoods.CollectFoodIds();
                if (ours != null)
                {
                    for (int i = 0; i < ours.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(ours[i]))
                        {
                            pool.Add(ours[i]);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 神秘礼物：取本 Mod 食物失败");
            }
            if (pool.Count == 0)
            {
                pool.Add("MushBar");
                pool.Add("BasicForagingPlant");
            }
            while (pool.Count > 0)
            {
                int idx = UnityEngine.Random.Range(0, pool.Count);
                string id = pool[idx];
                GameObject prefab = Assets.TryGetPrefab(new Tag(id));
                Edible edible = prefab != null ? prefab.GetComponent<Edible>() : null;
                if (edible != null && edible.FoodInfo != null && edible.FoodInfo.CaloriesPerUnit > 0f)
                {
                    kcalPerUnit = edible.FoodInfo.CaloriesPerUnit / 1000f;
                    displayName = id;
                    KPrefabID pid = prefab.GetComponent<KPrefabID>();
                    if (pid != null)
                    {
                        string proper = pid.GetProperName();
                        if (!string.IsNullOrEmpty(proper))
                        {
                            displayName = proper;
                        }
                    }
                    return id;
                }
                pool.RemoveAt(idx);
            }
            return null;
        }

        private static void PrehistoricBeast(IEventContext inst)
        {
            try
            {
                string[] pool = { "IceBelly", "GoldBelly", "Moo", "Raptor", "Stego", "AlgaeStego" };
                string pick = pool[UnityEngine.Random.Range(0, pool.Length)];
                int count = UnityEngine.Random.Range(1, 4);
                int spawned = CometRain.SpawnCreatureComets(pick, count);
                string display = SpaceRealEvents.ProperNameOf(pick);
                MFLog.Info(LogCat.Event,"[Space] 史前巨兽：" + pick + " ×" + spawned);
                inst.SetNoticeArg("target", display);
                inst.SetNoticeArg("count", spawned.ToString());
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 史前巨兽执行失败");
            }
        }

        // 取当前世界的打印仓（本 Mod 自己的定位需求：把变异的种子放在它附近）
        // 派送本身走门面 EventFrameworkApi.Deliver，这里只负责拿位置
        private static Telepad FindTelepad()
        {
            try
            {
                var all = Components.Telepads.Items;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null) return all[i];
                }
            }
            catch (Exception)
            {
                // expected: 传送器列表探测失败按无传送器处理
            }
            return null;
        }
    }

    internal static class CometRain
    {
        private static GameObject meteorTemplate;

        // 退档清理：模板是 DontDestroyOnLoad 的，不主动销毁会跨会话滞留（下次触发按需重建）
        internal static void DestroyTemplates()
        {
            try
            {
                if (meteorTemplate != null)
                {
                    UnityEngine.Object.Destroy(meteorTemplate);
                }
            }
            catch (Exception)
            {
                // expected: 模板已销毁或尚未创建，清理失败可忽略（随后统一置空）
            }
            meteorTemplate = null;
        }

        internal static int SpawnResourceComets(SimHashes element, int count, float minMass, float maxMass)
        {
            return Spawn(count, delegate(Comet comet, PrimaryElement pe)
            {
                if (pe != null)
                {
                    pe.SetElement(element, true);
                    pe.Temperature = 500f;
                    pe.Mass = UnityEngine.Random.Range(minMass, maxMass);
                }
                NoDamage(comet);
            });
        }

        internal static int SpawnCreatureComets(string creaturePrefab, int count)
        {
            return Spawn(count, delegate(Comet comet, PrimaryElement pe)
            {
                if (pe != null)
                {
                    pe.SetElement(SimHashes.Granite, true);
                    pe.Temperature = 400f;
                    pe.Mass = 200f;
                }
                NoDamage(comet);
                comet.craterPrefabs = null;
                comet.OnImpact = delegate
                {
                    SpawnCreature(creaturePrefab, comet);
                };
            });
        }

        private static void NoDamage(Comet comet)
        {
            if (comet == null)
            {
                return;
            }
            comet.entityDamage = 0;
            comet.totalTileDamage = 0f;
            comet.addTiles = 0;
            comet.splashRadius = 0;
            comet.explosionOreCount = new Vector2I(0, 0);
            comet.canHitDuplicants = false;
        }

        private static int Spawn(int count, System.Action<Comet, PrimaryElement> setup)
        {
            GameObject prefab = EnsureTemplate();
            if (prefab == null)
            {
                Debug.LogWarning("[Space] 陨石雨：坠落体模板创建失败");
                return 0;
            }
            int ok = 0;
            for (int i = 0; i < count; i++)
            {
                int cell = RandomSurfaceCell();
                if (cell < 0)
                {
                    continue;
                }
                GameObject go = Util.KInstantiate(prefab, Grid.CellToPos(cell));
                if (go == null)
                {
                    continue;
                }
                Comet comet = go.GetComponent<Comet>();
                if (comet != null)
                {
                    comet.spawnWithOffset = true;
                    comet.Velocity = new Vector2(UnityEngine.Random.Range(-2.5f, 2.5f), -16f);
                }
                if (setup != null)
                {
                    setup(comet, go.GetComponent<PrimaryElement>());
                }
                go.SetActive(true);
                ok++;
            }
            return ok;
        }

        private static void SpawnCreature(string prefabId, Comet comet)
        {
            try
            {
                GameObject prefab = Assets.TryGetPrefab(new Tag(prefabId));
                if (prefab == null)
                {
                    Debug.LogWarning("[Space] 生物 prefab 不存在：" + prefabId);
                    return;
                }
                Vector3 pos = comet != null ? comet.transform.GetPosition() : Vector3.zero;
                int cell = Grid.PosToCell(pos);
                if (!Grid.IsValidCell(cell))
                {
                    cell = RandomSurfaceCell();
                }
                if (cell < 0)
                {
                    return;
                }
                GameObject go = Util.KInstantiate(prefab, Grid.CellToPos(cell));
                if (go != null)
                {
                    go.SetActive(true);
                    MFLog.Info(LogCat.Event,"[Space] 生物随陨石落地：" + prefabId + " @cell " + cell);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 生物落地生成失败");
            }
        }

        private static GameObject EnsureTemplate()
        {
            if (meteorTemplate != null)
            {
                return meteorTemplate;
            }
            try
            {
                GameObject go = EntityTemplates.CreateEntity("SpaceMeteorComet", "陨石", true);
                go.AddOrGet<SaveLoadRoot>();
                go.AddOrGet<LoopingSounds>();
                Comet comet = go.AddOrGet<Comet>();
                comet.massRange = new Vector2(120f, 260f);
                comet.EXHAUST_ELEMENT = SimHashes.CarbonDioxide;
                comet.temperatureRange = new Vector2(473.15f, 573.15f);
                comet.entityDamage = 0;
                comet.explosionOreCount = new Vector2I(0, 0);
                comet.totalTileDamage = 0f;
                comet.splashRadius = 0;
                comet.impactSound = "Meteor_Large_Impact";
                comet.flyingSoundID = 1;
                comet.explosionEffectHash = SpawnFXHashes.MeteorImpactDust;
                comet.addTiles = 0;
                PrimaryElement pe = go.AddOrGet<PrimaryElement>();
                pe.SetElement(SimHashes.Granite, true);
                pe.Temperature = 500f;
                KBatchedAnimController kbac = go.AddOrGet<KBatchedAnimController>();
                kbac.AnimFiles = new KAnimFile[] { Assets.GetAnim("meteor_rock_kanim") };
                kbac.isMovable = true;
                kbac.initialAnim = "fall_loop";
                kbac.initialMode = KAnim.PlayMode.Loop;
                kbac.visibilityType = KAnimControllerBase.VisibilityType.OffscreenUpdate;
                go.AddOrGet<KCircleCollider2D>().radius = 0.5f;
                go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                go.AddTag(GameTags.Comet);
                go.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(go);
                meteorTemplate = go;
                return meteorTemplate;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 陨石模板创建失败");
                return null;
            }
        }

        private static int RandomSurfaceCell()
        {
            try
            {
                WorldContainer w = SpaceRealEventRuntime.MainWorld();
                if (w == null)
                {
                    return -1;
                }
                int x = w.WorldOffset.x + UnityEngine.Random.Range(4, Mathf.Max(5, w.Width - 4));
                int top = w.WorldOffset.y + w.Height - 2;
                for (int y = top; y >= w.WorldOffset.y; y--)
                {
                    int c = Grid.XYToCell(x, y);
                    if (Grid.IsValidCell(c) && Grid.Solid[c])
                    {
                        return c;
                    }
                }
            }
            catch (Exception)
            {
                // expected: 地表探测失败返回 -1，由调用方按无处可落处理
            }
            return -1;
        }
    }
}
