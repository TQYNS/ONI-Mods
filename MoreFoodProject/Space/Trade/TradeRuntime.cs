using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    internal sealed class TradeStockEntry
    {
        internal string id;
        internal float stock;          // 站点库存（你卖出去的也算进这里，下次刷新前可买回）
        internal float recycleLeft;    // 剩余可收（收购量）
        internal TradeItemValue value; // 本次会话解析出的估值（不落盘）
    }

    internal sealed class TradeStationState
    {
        internal string id;
        internal float cooldownEnd;
        internal int refreshCount;
        internal float currency;       // ★ 站点星币库存（参与结算）
        internal float pirateClosedUntil;
        internal float nextPirateRoll;
        internal readonly List<TradeStockEntry> shelf = new List<TradeStockEntry>();
        internal readonly Dictionary<string, float> recycleLeft =
            new Dictionary<string, float>(StringComparer.Ordinal);

        internal TradeStockEntry Find(string itemId)
        {
            for (int i = 0; i < shelf.Count; i++)
            {
                if (shelf[i].id == itemId)
                {
                    return shelf[i];
                }
            }
            return null;
        }

        internal void Clear()
        {
            shelf.Clear();
            recycleLeft.Clear();
        }
    }

    internal static class TradeRegistry
    {
        internal const int FlagsHunterGift = 1;
        internal const int FlagsHunterMet = 2;

        internal static float Currency;
        internal static int Rep;
        internal static int Flags;
        internal static readonly HashSet<string> Delivered = new HashSet<string>();
        internal static readonly Dictionary<string, TradeStationState> States =
            new Dictionary<string, TradeStationState>(StringComparer.Ordinal);
        internal static bool inited;

        // 起始星币只发放一次（随存档落盘，改设置不重发）
        internal static bool StartingCreditsGranted;

        // 未发放则补发一次起始星币并熔断
        internal static void EnsureStartingCredits()
        {
            if (StartingCreditsGranted) return;
            StartingCreditsGranted = true;
            int add = Mathf.Max(0, ModOptions.Current.StartingCredits);
            if (add > 0) Currency = Mathf.Max(0f, Currency) + add;
        }

        // ★ 本会话是否见过猎人协会站（不存档）：读档恢复会覆盖 Flags/Currency，
        internal static bool HunterStationSeen;

        // 成就/通知每个会话只发一次（恢复补发时不重复弹通知）
        private static bool hunterSideEffectsDone;

        private static readonly int[] RepThresholds = new int[] { 0, 20, 50, 100, 330, 400, 550 };
        private static readonly string[] RepNames = new string[] { "F", "E", "D", "C", "B", "A", "S" };
        private static readonly float[] RankSpeed = new float[] { 1.00f, 1.05f, 1.10f, 1.15f, 1.20f, 1.30f, 1.50f };
        private static readonly float[] RankFuel = new float[] { 1.00f, 0.95f, 0.90f, 0.85f, 0.80f, 0.70f, 0.50f };
        private static readonly float[] RankHp = new float[] { 1.00f, 1.10f, 1.20f, 1.35f, 1.50f, 1.70f, 2.00f };
        private static readonly float[] RankAttack = new float[] { 1.00f, 1.10f, 1.20f, 1.35f, 1.50f, 1.75f, 2.00f };
        private static readonly float[] RankAmount = new float[] { 0.00f, 0.05f, 0.10f, 0.20f, 0.35f, 0.55f, 0.80f };

        private static bool sessionBegun;

        internal static void Init()
        {
            if (inited)
            {
                SpaceAchievements.CheckMilestones();
                return;
            }
            inited = true;
            TradeDb.Load();
            TradeValuation.Init();
            TradeCatalog.Invalidate();
            // ★ 只有"进程内第一次"才写初始星币。
            if (!sessionBegun)
            {
                sessionBegun = true;
                Currency = TradeDb.Config.currency_start;
            }
            MFLog.Verbose(LogCat.Trade,"[Space] 贸易初始化：星币=" + Currency + " 声望=" + RankName() + "(" + Rep + ")"
                + " 站点配置=" + TradeDb.Stations.Count);
            // ★ 读档恢复的第二触发点：开星图/开贸易窗都会走 Init。
            SpaceRestoreGate.RunDeferredRestores();
            SpaceAchievements.CheckMilestones();
        }

        /// <summary>新会话（SaveGame.OnPrefabInit）调用：运行时状态复位。
        internal static void BeginSession()
        {
            inited = false;                 // 让下一次 Init 重跑一次 DB 检查（幂等）
            sessionBegun = true;            // 本会话的初值已定，Init 不再改星币
            Currency = TradeDb.Config.currency_start;
            Rep = 0;
            Flags = 0;
            StartingCreditsGranted = false;   // 新局重新计一次发放熔断
            HunterStationSeen = false;
            hunterSideEffectsDone = false;
            Delivered.Clear();
            ChannelPoints.Clear();
            States.Clear();
            EnsureStartingCredits();
            MFLog.Verbose(LogCat.Trade,"[Space] 贸易会话复位：星币=" + Currency + " 声望=0 等待读档恢复覆盖");
        }

        internal static float Now()
        {
            GameClock clock = GameClock.Instance;
            return clock != null ? clock.GetTime() : 0f;
        }

        internal static int RankIndex()
        {
            int r = Rep;
            int idx = 0;
            for (int i = 0; i < RepThresholds.Length; i++)
            {
                if (r >= RepThresholds[i])
                {
                    idx = i;
                }
            }
            return idx;
        }

        internal static string RankName()
        {
            return RepNames[RankIndex()];
        }

        // 是否到达最高声望级（S），避免阈值散落在各处
        internal static bool IsMaxRank()
        {
            return RankIndex() == RepNames.Length - 1;
        }

        internal static float RankSpeedMul()
        {
            return RankSpeed[RankIndex()];
        }

        internal static float RankFuelMul()
        {
            return RankFuel[RankIndex()];
        }

        internal static float RankHpMul()
        {
            return RankHp[RankIndex()];
        }

        internal static float RankAttackMul()
        {
            return RankAttack[RankIndex()];
        }

        internal static float RankAmountMul()
        {
            return RankAmount[RankIndex()];
        }

        // 捐款 / 荣誉点（渠道表见 trade/donation.yaml）
        internal static readonly Dictionary<string, int> ChannelPoints =
            new Dictionary<string, int>(StringComparer.Ordinal);

        // 旧字段：仅用于老存档迁移（新代码不写）
        internal static int DeliverCommonGained;
        internal static int DeliverLegendaryGained;
        internal static int DonateGained;

        internal static int ChannelPointsOf(string key)
        {
            int v;
            return !string.IsNullOrEmpty(key) && ChannelPoints.TryGetValue(key, out v) ? v : 0;
        }

        internal static int ChannelCap(DonationChannelDef ch)
        {
            return ch != null ? Mathf.Max(0, ch.cap) : 0;
        }

        internal static int ChannelLeft(DonationChannelDef ch)
        {
            return Mathf.Max(0, ChannelCap(ch) - ChannelPointsOf(ch == null ? null : ch.key));
        }

        internal static void AddChannelPoints(string key, int pts)
        {
            if (string.IsNullOrEmpty(key) || pts <= 0)
            {
                return;
            }
            ChannelPoints[key] = ChannelPointsOf(key) + pts;
        }

        /// <summary>按品质找"能装下这份食材"的 food 渠道（渠道按配置顺序取第一个未满的）。</summary>
        internal static DonationChannelDef PickFoodChannel(int quality)
        {
            List<DonationChannelDef> chs = TradeDb.DonationChannels(TradeDb.Config.hunter_station_id);
            if (chs == null)
            {
                return null;
            }
            for (int i = 0; i < chs.Count; i++)
            {
                DonationChannelDef ch = chs[i];
                if (ch == null || ch.kind != "food")
                {
                    continue;
                }
                if (quality < ch.min_quality.GetValueOrDefault(0f)
                    || quality > ch.max_quality.GetValueOrDefault(99f))
                {
                    continue;
                }
                if (ChannelLeft(ch) <= 0)
                {
                    continue;
                }
                return ch;
            }
            return null;
        }

        // 卖出食物时顺带给荣誉点（首次交付该 ID 才算，防止重复刷）
        internal static void OnFoodDelivered(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || Delivered.Contains(itemId))
            {
                return;
            }
            int quality = FoodQualityOf(itemId);
            if (quality < 0)
            {
                return;
            }
            DonationChannelDef ch = PickFoodChannel(quality);
            if (ch == null)
            {
                return;
            }
            Delivered.Add(itemId);
            int per = Mathf.Max(1, Mathf.RoundToInt(ch.points_per_unit));
            int gain = Mathf.Min(per, ChannelLeft(ch));
            AddChannelPoints(ch.key, gain);
            AddRep(gain);
            MFLog.Verbose(LogCat.Trade,"[Space] ★ 首次交付食材 " + itemId + "（品质 " + quality + "）→ 渠道 "
                + ch.name + "：荣誉点 +" + gain
                + "（累计 " + ChannelPointsOf(ch.key) + "/" + ChannelCap(ch) + "）");
            TradeUi.Notify("食材交付", "首次交付" + ch.name + "：荣誉点 +" + gain);
        }

        private static int FoodQualityOf(string itemId)
        {
            try
            {
                GameObject prefab = Assets.TryGetPrefab(itemId);
                if (prefab == null)
                {
                    return -1;
                }
                Edible ed = prefab.GetComponent<Edible>();
                if (ed == null)
                {
                    return -1;
                }
                return ed.GetQuality();
            }
            catch (Exception e)
            {
                MFLog.Exc("trade:quality:" + itemId, LogCat.Trade, e,
                    "[Space] 食材品质判定失败（{0}，按无品质处理）", itemId);
                return -1;
            }
        }

        internal static void AddRep(int delta)
        {
            if (delta == 0)
            {
                return;
            }
            int before = RankIndex();
            Rep = Mathf.Clamp(Rep + delta, 0, 550);
            int after = RankIndex();
            MFLog.Verbose(LogCat.Trade,"[Space] 声望 " + Rep + "（" + RankName() + "）");
            if (after != before)
            {
                MFLog.Verbose(LogCat.Trade,"[Space] ★ 声望等级 " + RepNames[before] + " → " + RepNames[after]
                    + "（火箭效果已随等级提升）");
            }
            SpaceAchievements.CheckMilestones();
        }

        internal static void AddCurrency(float delta)
        {
            Currency = Mathf.Max(0f, Currency + delta);
            SpaceAchievements.CheckMilestones();
        }

        /// <summary>猎人首见奖励（800 星币 + 成就/通知）。幂等：
        internal static void GrantHunterFirstContact()
        {
            if ((Flags & FlagsHunterGift) != 0)
            {
                return;
            }
            Flags |= FlagsHunterMet | FlagsHunterGift;
            AddCurrency(TradeDb.Config.hunter_gift);
            MFLog.Verbose(LogCat.Trade,"[Space] ★ 初次发现美食猎人协会：获得 " + TradeDb.Config.hunter_gift
                + " 星币（当前 " + Currency + "）");
            if (!hunterSideEffectsDone)
            {
                hunterSideEffectsDone = true;
                SpaceAchievements.Unlock(SpaceAchievements.HunterFirstContactId, "星际来客");
                TradeUi.Notify("美食猎人协会", "首次接触：获得 " + TradeDb.Config.hunter_gift + " 星币");
            }
            if (TradeSaveData.Attach() != null)
            {
                TradeSaveData.Attach()?.Capture();
            }
        }

        /// <summary>读档恢复后补发猎人首见奖励（存档早于首见时）。</summary>
        internal static void RestoreHunterGiftIfMissed()
        {
            if (HunterStationSeen)
            {
                GrantHunterFirstContact();
            }
        }

        internal static TradeStationState EnsureState(string stationId)
        {
            if (string.IsNullOrEmpty(stationId))
            {
                TradeStationState transient = new TradeStationState();
                transient.id = "";
                transient.cooldownEnd = Now();
                return transient;
            }
            TradeStationState st;
            if (!States.TryGetValue(stationId, out st) || st == null)
            {
                st = new TradeStationState();
                st.id = stationId;
                st.cooldownEnd = Now();
                States[stationId] = st;
            }
            return st;
        }

        internal static TradeStationState FindState(string stationId)
        {
            TradeStationState st;
            if (States.TryGetValue(stationId, out st))
            {
                return st;
            }
            return null;
        }

        // 站点层价格公式：买 = M × 1.40 × (1 + k)；卖 = M × 0.60 × (1 + TPI)
        // 站点的 k 只作用于买侧；声望 TPI 只作用于卖侧。卖价恒小于买价，无需再钳制。
        internal static float BuyUnit(StationDef def, TradeItemValue v)
        {
            if (v == null)
            {
                return 0.01f;
            }
            float k = def != null ? def.k : 0f;
            return Mathf.Max(0.01f, v.price * TradeValuation.BuyBase * (1f + k));
        }

        internal static float SellUnit(StationDef def, TradeItemValue v)
        {
            if (v == null)
            {
                return 0.01f;
            }
            float tpi = TradeValuation.TpiOf(RankIndex());
            return Mathf.Max(0.01f, v.price * TradeValuation.SellBase * (1f + tpi));
        }

        internal static bool PirateClosed(TradeStationState st)
        {
            return st != null && st.pirateClosedUntil > Now();
        }

        internal static float PirateCloseLeft(TradeStationState st)
        {
            return st == null ? 0f : Mathf.Max(0f, st.pirateClosedUntil - Now());
        }

        internal static void TickPirates()
        {
            try
            {
            float now = Now();
            foreach (KeyValuePair<string, TradeStationState> kv in States)
            {
                TradeStationState st = kv.Value;
                if (st == null)
                {
                    continue;
                }
                StationDef def = TradeDb.FindStation(st.id);
                if (def == null || !def.IsPirate)
                {
                    continue;
                }
                if (st.pirateClosedUntil > 0f && now >= st.pirateClosedUntil)
                {
                    st.pirateClosedUntil = 0f;
                    MFLog.Verbose(LogCat.Trade,"[Space] 海盗站 " + def.name + " 重新开门营业");
                    TradeUi.Notify("海盗站", def.name + " 已重新开门");
                }
                if (st.nextPirateRoll <= 0f)
                {
                    st.nextPirateRoll = now + UnityEngine.Random.Range(600f, 1800f);
                    continue;
                }
                if (st.pirateClosedUntil <= 0f && now >= st.nextPirateRoll)
                {
                    st.nextPirateRoll = now + UnityEngine.Random.Range(900f, 2400f);
                    float chance = def.close_chance > 0f ? def.close_chance : 0.25f;
                    if (UnityEngine.Random.value < chance)
                    {
                        float span = def.close_seconds > 0f ? def.close_seconds : 1800f;
                        st.pirateClosedUntil = now + span;
                        MFLog.Verbose(LogCat.Trade,"[Space] 海盗站 " + def.name + " 暂时关闭（约 " + Mathf.Round(span) + " 秒）");
                        TradeUi.Notify("海盗站", def.name + " 暂时关闭了，约 " + Mathf.Round(span) + " 秒后重开");
                    }
                }
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:TradeRuntime", e); }
        }

        internal static bool RefreshIfDue(TradeStationState st, StationDef def)
        {
            if (st == null || def == null)
            {
                return false;
            }
            float now = Now();
            if (st.shelf.Count > 0 && now < st.cooldownEnd)
            {
                return false;
            }
            Refresh(st, def);
            return true;
        }

        private static float RefreshSeconds(StationDef def)
        {
            if (def != null && def.refresh_days > 0f)
            {
                return def.refresh_days * 600f;
            }
            return TradeDb.Config.refresh_seconds;
        }

        internal static int RollEntries(StationDef def)
        {
            int[] r = def != null ? def.EntryRange() : null;
            if (r == null || r.Length == 0)
            {
                return 8;
            }
            int lo = r[0];
            int hi = r.Length > 1 ? r[1] : r[0];
            if (hi < lo)
            {
                int t = lo;
                lo = hi;
                hi = t;
                MFLog.Warn(LogCat.Trade,"[Space] 站点 " + def.id + " 的 entries 区间反转，已交换上下限");
            }
            if (hi <= lo)
            {
                return Mathf.Max(1, lo);
            }
            return UnityEngine.Random.Range(lo, hi + 1);
        }

        internal static void RollCurrency(TradeStationState st, StationDef def)
        {
            float lo = 800f;
            float hi = 1500f;
            if (def != null && def.currency_stock != null && def.currency_stock.Length >= 2)
            {
                lo = def.currency_stock[0];
                hi = def.currency_stock[1];
                if (hi < lo)
                {
                    float t = lo;
                    lo = hi;
                    hi = t;
                    MFLog.Warn(LogCat.Trade,"[Space] 站点 " + def.id + " 的 currency_stock 区间反转，已交换上下限");
                }
            }
            if (hi <= lo)
            {
                st.currency = Mathf.Max(0f, lo);
            }
            else
            {
                st.currency = UnityEngine.Random.Range(lo, hi);
            }
            st.currency *= ModOptions.Current.TraderCreditsMultiplier;
        }

        // ★ 与 MoreFood 的唯一代码接入点（其余全走 YAML）：这张配方卷轴对应的配方是否已查阅。
        internal static bool IsRetiredRecipeScroll(string itemId)
        {
            try
            {
                return MoreFood_KFC.RecipeScrollRegistry.IsReadItem(itemId);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void Refresh(TradeStationState st, StationDef def)
        {
            st.Clear();
            st.refreshCount++;
            if (def == null)
            {
                return;
            }
            StationCatalog cat = TradeCatalog.Catalog(def);
            if (cat == null || cat.sellList.Count == 0)
            {
                MFLog.Warn(LogCat.Trade,"[Space] 站点 " + def.id + " 的可卖池为空（检查 valuation/items/station 配置）");
                st.cooldownEnd = Now() + RefreshSeconds(def);
                RollCurrency(st, def);
                return;
            }

            int want = RollEntries(def);
            DrawPool pool = new DrawPool(cat);
            while (st.shelf.Count < want)
            {
                TradeItemValue v = pool.Draw();
                if (v == null)
                {
                    // 池子抽干（可卖种类比 entries 还少）——这是配置问题，不是抽签失败
                    MFLog.Warn(LogCat.Trade,"[Space] 站点 " + def.id + " 可卖池只有 " + cat.sellList.Count
                        + " 种，少于本次 entries=" + want + "，已抽干");
                    break;
                }
                AddEntry(st, def, v);
            }
            AddExtraPicks(st, def, pool);
            CapModuleRarity(st, def);

            RollCurrency(st, def);
            st.cooldownEnd = Now() + RefreshSeconds(def);
            MFLog.Verbose(LogCat.Trade,"[Space] 站点 " + def.id + " 刷新#" + st.refreshCount + " 条目=" + st.shelf.Count
                + "/" + want + " 星币=" + st.currency.ToString("0")
                + " 下次冷却结束=" + Mathf.RoundToInt(st.cooldownEnd) + "秒");
        }

        // ★ 两层抽：先按 rarity_weight 掷稀有度，再在该档里均匀抽一条（用户拍板）。
        private sealed class DrawPool
        {
            private readonly Dictionary<TradeRarity, List<TradeItemValue>> buckets =
                new Dictionary<TradeRarity, List<TradeItemValue>>();
            private readonly Dictionary<TradeRarity, float> weights =
                new Dictionary<TradeRarity, float>();
            private float total;

            internal DrawPool(StationCatalog cat)
            {
                for (int i = 0; i < TradeValuation.RarityCount; i++)
                {
                    TradeRarity r = (TradeRarity)i;
                    List<TradeItemValue> src;
                    float w = TradeValuation.WeightOf(r);
                    if (w <= 0f || !cat.sellByRarity.TryGetValue(r, out src)
                        || src == null || src.Count == 0)
                    {
                        continue;
                    }
                    // ★ 配方卷轴：对应的配方已经"查阅"过 ⇒ 本次刷新不再抽它
                    List<TradeItemValue> kept = new List<TradeItemValue>(src.Count);
                    for (int k = 0; k < src.Count; k++)
                    {
                        TradeItemValue v = src[k];
                        if (v != null && !IsRetiredRecipeScroll(v.id))
                        {
                            kept.Add(v);
                        }
                    }
                    if (kept.Count == 0)
                    {
                        continue;
                    }
                    buckets[r] = kept;
                    weights[r] = w;
                    total += w;
                }
            }

            internal bool HasAny
            {
                get { return total > 0f; }
            }

            internal TradeItemValue Draw()
            {
                if (total <= 0f)
                {
                    return null;
                }
                float roll = UnityEngine.Random.value * total;
                for (int i = 0; i < TradeValuation.RarityCount; i++)
                {
                    TradeRarity r = (TradeRarity)i;
                    List<TradeItemValue> list;
                    if (!buckets.TryGetValue(r, out list))
                    {
                        continue;
                    }
                    roll -= weights[r];
                    if (roll > 0f)
                    {
                        continue;
                    }
                    return Take(list, r);
                }
                // 浮点余数兜底：拿还有货的第一档
                for (int i = 0; i < TradeValuation.RarityCount; i++)
                {
                    TradeRarity r = (TradeRarity)i;
                    List<TradeItemValue> list;
                    if (buckets.TryGetValue(r, out list))
                    {
                        return Take(list, r);
                    }
                }
                return null;
            }

            internal TradeItemValue DrawOf(TradeRarity r)
            {
                List<TradeItemValue> list;
                if (!buckets.TryGetValue(r, out list) || list.Count == 0)
                {
                    return null;
                }
                return Take(list, r);
            }

            private TradeItemValue Take(List<TradeItemValue> list, TradeRarity r)
            {
                int i = UnityEngine.Random.Range(0, list.Count);
                TradeItemValue v = list[i];
                list[i] = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                if (list.Count == 0)
                {
                    buckets.Remove(r);
                    total -= weights[r];
                }
                return v;
            }
        }

        // extra_picks：指定稀有度必抽 N 条
        private static void AddExtraPicks(TradeStationState st, StationDef def, DrawPool pool)
        {
            if (def.extra_picks == null || def.extra_picks.Count == 0)
            {
                return;
            }
            foreach (KeyValuePair<string, int> kv in def.extra_picks)
            {
                if (kv.Value <= 0)
                {
                    continue;
                }
                TradeRarity r = TradeDb.ParseRarity(kv.Key);
                for (int n = 0; n < kv.Value; n++)
                {
                    TradeItemValue got = pool.DrawOf(r);
                    if (got == null)
                    {
                        MFLog.Warn(LogCat.Trade,"[Space] 站点 " + def.id + " 的 extra_picks." + kv.Key
                            + " 档已无货，少抽 " + (kv.Value - n) + " 条");
                        break;
                    }
                    AddEntry(st, def, got);
                    MFLog.Verbose(LogCat.Trade,"[Space] 站点 " + def.id + " extra_picks 必抽 " + kv.Key + " → " + got.id);
                }
            }
        }

        /// <summary>模块抽取上限：传奇/大师按站点类型封顶（普通 1/3，黑市/走私/海盗 2/4）。</summary>
        private static void CapModuleRarity(TradeStationState st, StationDef def)
        {
            int legendary = 0;
            int master = 0;
            int legendCap = def.ModuleLegendaryMax;
            int masterCap = def.ModuleMasterMax;
            for (int i = st.shelf.Count - 1; i >= 0; i--)
            {
                TradeStockEntry e = st.shelf[i];
                if (e == null || e.value == null || string.IsNullOrEmpty(e.id)
                    || !e.id.StartsWith("ModuleItem_", StringComparison.Ordinal))
                {
                    continue;
                }
                if (e.value.rarity == TradeRarity.Legendary)
                {
                    legendary++;
                    if (legendary > legendCap)
                    {
                        st.shelf.RemoveAt(i);
                        MFLog.Verbose(LogCat.Trade,"[Space] 站点 " + def.id + " 模块传奇超过上限 " + legendCap
                            + "，移除 " + e.id);
                    }
                }
                else if (e.value.rarity == TradeRarity.Master)
                {
                    master++;
                    if (master > masterCap)
                    {
                        st.shelf.RemoveAt(i);
                        MFLog.Verbose(LogCat.Trade,"[Space] 站点 " + def.id + " 模块大师超过上限 " + masterCap
                            + "，移除 " + e.id);
                    }
                }
            }
        }

        private static void AddEntry(TradeStationState st, StationDef def, TradeItemValue v)
        {
            if (v == null)
            {
                return;
            }
            TradeStockEntry e = new TradeStockEntry();
            e.id = v.id;
            e.value = v;
            float lo = v.stockMin;
            float hi = v.stockMax;
            if (hi < lo)
            {
                float t = lo;
                lo = hi;
                hi = t;
            }
            e.stock = Mathf.Max(1f, Mathf.Round((hi <= lo ? lo : UnityEngine.Random.Range(lo, hi))
                * ModOptions.Current.StockMultiplier));
            st.shelf.Add(e);
        }
    }

    // ★★ KSerialization 对没有 [SerializationConfig] 的类型只序列化 public 字段 ⇒ 必须 OptIn。
    internal sealed class TradeSaveData : KMonoBehaviour, ISaveLoadable
    {
        [Serialize]
        public float currency;
        // 起始星币是否已发放（跨读档熔断，改设置不重发）
        [Serialize]
        public bool startingCreditsGranted;
        [Serialize]
        public int rep;
        [Serialize]
        public int flags;
        [Serialize]
        public List<string> delivered = new List<string>();
        [Serialize]
        public int deliverCommonGained;
        [Serialize]
        public int deliverLegendaryGained;
        [Serialize]
        public int donateGained;
        // ★ 捐款改成"按渠道累计"（渠道表在 trade/donation.yaml）
        [Serialize]
        public List<string> channelKeys = new List<string>();
        [Serialize]
        public List<int> channelPoints = new List<int>();

        // ★ v5.8：存档版本（StationPriceCache / StationStock / StationCurrency 的失效判据）
        [Serialize]
        public int tradeVersion;

        [Serialize]
        public List<string> stationIds = new List<string>();
        [Serialize]
        public List<float> stationCooldown = new List<float>();
        [Serialize]
        public List<int> stationRefreshCount = new List<int>();
        [Serialize]
        public List<float> stationCurrency = new List<float>();
        [Serialize]
        public List<int> shelfStation = new List<int>();
        [Serialize]
        public List<string> shelfId = new List<string>();
        [Serialize]
        public List<float> shelfStock = new List<float>();
        [Serialize]
        public List<float> shelfRecycle = new List<float>();

        // 进行中的狩猎任务（questActive = 事件实例 id）
        [Serialize]
        public List<string> questActive = new List<string>();
        [Serialize]
        public List<int> questSiteCellQ = new List<int>();
        [Serialize]
        public List<int> questSiteCellR = new List<int>();
        [Serialize]
        public List<string> questCreature = new List<string>();
        [Serialize]
        public List<float> questDeadline = new List<float>();
        // 任务自述数据（读档不依赖事件实例是否还在）
        [Serialize]
        public List<string> questEvent = new List<string>();
        [Serialize]
        public List<string> questTier = new List<string>();
        [Serialize]
        public List<string> questShown = new List<string>();
        [Serialize]
        public List<int> questRep = new List<int>();
        // 接取时冻结的最终星币奖励；旧档缺项按 0 恢复
        [Serialize]
        public List<int> questCurrency = new List<int>();
        [Serialize]
        public List<int> questCycles = new List<int>();
        [Serialize]
        public List<int> questCount = new List<int>();
        [Serialize]
        public List<string> questTiers = new List<string>();
        [Serialize]
        public List<string> questTicketQuestId = new List<string>();
        [Serialize]
        public List<string> questTickets = new List<string>();
        [Serialize]
        public List<int> monsterCellQ = new List<int>();
        [Serialize]
        public List<int> monsterCellR = new List<int>();
        [Serialize]
        public List<string> monsterIds = new List<string>();
        [Serialize]
        public List<int> revealQ = new List<int>();
        [Serialize]
        public List<int> revealR = new List<int>();
        [Serialize]
        public int secondsSaved;
        [Serialize]
        public int lootPoolVersion;
        [Serialize]
        public List<string> lootPoolKeys = new List<string>();
        [Serialize]
        public List<string> lootPoolIds = new List<string>();
        [Serialize]
        public List<int> lootPoolCounts = new List<int>();
        [Serialize]
        public bool geneShuffleOverclock;

        private static GameObject host;
        internal static readonly List<TradeSaveData> AllCarriers = new List<TradeSaveData>();
        private static bool mirroring;

        internal static TradeSaveData Attach()
        {
            SpaceSaveHost hostEntity = SpaceSaveHost.FindOrCreate();
            if (hostEntity == null)
            {
                MFLog.Verbose(LogCat.Trade,"[SAVE-TRACE] L3 Attach(贸易)：FindOrCreate=null");
                return null;
            }
            host = hostEntity.gameObject;
            MFLog.Verbose(LogCat.Trade,"[SAVE-TRACE] L3 Attach(贸易)：宿主=" + host.name + "#" + host.GetInstanceID());
            TradeSaveData mine = host.AddOrGet<TradeSaveData>();
            if (mine != null && !AllCarriers.Contains(mine))
            {
                AllCarriers.Add(mine);
            }
            return mine;
        }

        internal static int AttachToAllSaveGames()
        {
            return 0;
        }

        [OnSerialized]
        public void TraceOnSerialized()
        {
            MFLog.Verbose(LogCat.Trade,"[SAVE-TRACE] S4 序列化通知(贸易)：站点=" + stationIds.Count + " 任务=" + questActive.Count
                + " 载体#" + GetInstanceID()
                + "@" + (gameObject != null ? gameObject.name + "#" + gameObject.GetInstanceID() : "?"));
        }

        [OnDeserialized]
        public void TraceOnDeserialized()
        {
            MFLog.Verbose(LogCat.Trade,"[SAVE-TRACE] L4 反序列化通知(贸易)：站点=" + stationIds.Count + " 任务=" + questActive.Count
                + " 载体#" + GetInstanceID()
                + "@" + (gameObject != null ? gameObject.name + "#" + gameObject.GetInstanceID() : "?"));
        }

        internal void Capture()
        {
            if (!SpaceSaveHost.LoadFinished)
            {
                MFLog.Verbose(LogCat.Trade,"[SAVE-TRACE] S2 Capture(贸易) 被闸掉：LoadFinished=false");
                return;
            }
            MFLog.Verbose(LogCat.Trade,"[SAVE-TRACE] S3 捕获落点(贸易)：活跃站点=" + TradeRegistry.States.Count
                + " 活跃任务=" + SpaceQuestRuntime.Active.Count
                + " 星币=" + TradeRegistry.Currency + " Flags=" + TradeRegistry.Flags
                + " 载体#" + GetInstanceID() + "@"
                + (gameObject != null ? gameObject.name + "#" + gameObject.GetInstanceID() : "(null)"));
            if (!mirroring)
            {
                mirroring = true;
                try
                {
                    for (int i = 0; i < AllCarriers.Count; i++)
                    {
                        TradeSaveData c = AllCarriers[i];
                        if (c != null && c != this)
                        {
                            c.Capture();
                        }
                    }
                }
                finally
                {
                    mirroring = false;
                }
            }
            currency = TradeRegistry.Currency;
            startingCreditsGranted = TradeRegistry.StartingCreditsGranted;
            rep = TradeRegistry.Rep;
            flags = TradeRegistry.Flags;
            tradeVersion = GlobalCacheVersion.TRADE_VERSION;
            delivered.Clear();
            delivered.AddRange(TradeRegistry.Delivered);
            deliverCommonGained = TradeRegistry.DeliverCommonGained;
            deliverLegendaryGained = TradeRegistry.DeliverLegendaryGained;
            donateGained = TradeRegistry.DonateGained;
            channelKeys.Clear();
            channelPoints.Clear();
            foreach (KeyValuePair<string, int> kv in TradeRegistry.ChannelPoints)
            {
                channelKeys.Add(kv.Key);
                channelPoints.Add(kv.Value);
            }

            stationIds.Clear();
            stationCooldown.Clear();
            stationRefreshCount.Clear();
            stationCurrency.Clear();
            shelfStation.Clear();
            shelfId.Clear();
            shelfStock.Clear();
            shelfRecycle.Clear();

            foreach (KeyValuePair<string, TradeStationState> kv in TradeRegistry.States)
            {
                TradeStationState st = kv.Value;
                if (st == null)
                {
                    continue;
                }
                int idx = stationIds.Count;
                stationIds.Add(st.id);
                stationCooldown.Add(st.cooldownEnd);
                stationRefreshCount.Add(st.refreshCount);
                stationCurrency.Add(st.currency);
                for (int i = 0; i < st.shelf.Count; i++)
                {
                    shelfStation.Add(idx);
                    shelfId.Add(st.shelf[i].id);
                    shelfStock.Add(st.shelf[i].stock);
                    shelfRecycle.Add(st.shelf[i].recycleLeft);
                }
            }
            SpaceQuestRuntime.Capture(this);
            SpaceReveal.Capture(revealQ, revealR);
            geneShuffleOverclock = SpaceRealEventRuntime.GeneShuffleBonus;
            secondsSaved = Mathf.RoundToInt(TradeRegistry.Now());
        }

        internal void Restore()
        {
            SpaceRealEventRuntime.GeneShuffleBonus = geneShuffleOverclock;
            TradeRegistry.Currency = currency;
            TradeRegistry.StartingCreditsGranted = startingCreditsGranted;
            TradeRegistry.Rep = Mathf.Clamp(rep, 0, 550);
            TradeRegistry.Flags = flags;
            TradeRegistry.Delivered.Clear();
            int deliveredKept = 0;
            for (int i = 0; i < delivered.Count; i++)
            {
                if (!string.IsNullOrEmpty(delivered[i]) && TradeRegistry.Delivered.Add(delivered[i]))
                {
                    deliveredKept++;
                }
            }
            TradeRegistry.DeliverCommonGained = Mathf.Max(0, deliverCommonGained);
            TradeRegistry.DeliverLegendaryGained = Mathf.Max(0, deliverLegendaryGained);
            TradeRegistry.DonateGained = Mathf.Max(0, donateGained);
            TradeRegistry.ChannelPoints.Clear();
            int n = channelKeys == null ? 0 : channelKeys.Count;
            for (int i = 0; i < n; i++)
            {
                string key = channelKeys[i];
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }
                int v = channelPoints != null && i < channelPoints.Count ? channelPoints[i] : 0;
                TradeRegistry.ChannelPoints[key] = TradeRegistry.ChannelPointsOf(key) + Mathf.Max(0, v);
            }
            // ★ 老存档迁移：只有旧计数字段、没有渠道表时，按渠道 key 名映射进来
            if (TradeRegistry.ChannelPoints.Count == 0 && TradeRegistry.DeliverCommonGained + TradeRegistry.DeliverLegendaryGained + TradeRegistry.DonateGained > 0)
            {
                TradeRegistry.AddChannelPoints("food_normal", TradeRegistry.DeliverCommonGained);
                TradeRegistry.AddChannelPoints("food_legendary", TradeRegistry.DeliverLegendaryGained);
                TradeRegistry.AddChannelPoints("money", TradeRegistry.DonateGained);
                MFLog.Verbose(LogCat.Trade,"[Space] 捐款旧档迁移：普通食材 " + TradeRegistry.DeliverCommonGained
                    + " / 传奇食材 " + TradeRegistry.DeliverLegendaryGained
                    + " / 星币 " + TradeRegistry.DonateGained);
            }

            // ★ TRADE_VERSION 不匹配 → 丢弃 StationPriceCache / StationStock / StationCurrency，下次开窗重算
            bool versionOk = tradeVersion == GlobalCacheVersion.TRADE_VERSION;
            if (!versionOk)
            {
                MFLog.Warn(LogCat.Trade,"[Space] 贸易存档版本 " + tradeVersion + " ≠ 当前 "
                    + GlobalCacheVersion.TRADE_VERSION + "：货架与站点星币作废，重算（玩家星币/声望/任务照旧）");
            }

            TradeRegistry.States.Clear();
            int stationsKept = 0;
            for (int i = 0; i < stationIds.Count; i++)
            {
                string id = stationIds[i];
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                TradeStationState st = new TradeStationState();
                st.id = id;
                st.cooldownEnd = GetAt(stationCooldown, i, 0f);
                st.refreshCount = GetAt(stationRefreshCount, i, 0);
                st.currency = versionOk ? GetAt(stationCurrency, i, 0f) : 0f;
                TradeRegistry.States[id] = st;
                stationsKept++;
            }

            int shelfDropped = 0;
            if (versionOk)
            {
                for (int i = 0; i < shelfId.Count; i++)
                {
                    int idx = GetAt(shelfStation, i, -1);
                    string itemId = GetAt(shelfId, i, null);
                    if (idx < 0 || idx >= stationIds.Count || string.IsNullOrEmpty(itemId))
                    {
                        shelfDropped++;
                        continue;
                    }
                    TradeStationState st = TradeRegistry.FindState(stationIds[idx]);
                    if (st == null)
                    {
                        shelfDropped++;
                        continue;
                    }
                    TradeStockEntry e = new TradeStockEntry();
                    e.id = itemId;
                    e.stock = GetAt(shelfStock, i, 0f);
                    e.recycleLeft = GetAt(shelfRecycle, i, 0f);
                    st.shelf.Add(e);
                    // ★ 0 配额也要写：否则读档后 RecycleLeft() 会重新掷配额，把「已收够」复活
                    st.recycleLeft[e.id] = Mathf.Max(0f, e.recycleLeft);
                }
            }

            int questsKept = SpaceQuestRuntime.Restore(this);
            int revealedKept = SpaceReveal.Restore(revealQ, revealR);
            // ★ 站点 OnSpawn 早于本恢复：先把存档的旧星币/flag 写回，再补发错过的首见奖励
            TradeRegistry.RestoreHunterGiftIfMissed();
            int bad = (stationIds.Count - stationsKept) + shelfDropped;
            if (bad > 0)
            {
                MFLog.Warn(LogCat.Trade,"[Space] 贸易存档恢复：丢弃非法条目 " + bad
                    + " 条（站点 " + stationsKept + "/" + stationIds.Count
                    + "，货架 " + (shelfId.Count - shelfDropped) + "/" + shelfId.Count + "），其余照常恢复");
            }
            MFLog.Verbose(LogCat.Trade,"[Space] 贸易读档：星币=" + TradeRegistry.Currency + " 声望=" + TradeRegistry.Rep
                + "（" + TradeRegistry.RankName() + "）站点=" + stationsKept
                + " 已交付食材=" + deliveredKept + " 任务=" + questsKept
                + " 已解锁子图格=" + revealedKept);
            TradeRegistry.EnsureStartingCredits();   // 旧档无该标记时补发一次
            SpaceAchievements.CheckMilestones();
        }

        private static T GetAt<T>(List<T> list, int i, T fallback)
        {
            if (list == null || i < 0 || i >= list.Count)
            {
                return fallback;
            }
            return list[i];
        }
    }
}
