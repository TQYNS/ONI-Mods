using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{

    public struct DropEntry
    {
        // 掉落物 / 池 id
        public string Id;
        // 数量（kg 或件数）
        public float Amount;
        public DropEntry(string id, float amount) { Id = id; Amount = amount; }
    }

    public enum DropMode
    {
        Append = 0,   // 保留原版，追加模式
        Override = 1, // 同 ID 覆盖，合并追加
        Replace = 2,  // 清空原版，只写本次
    }

    public sealed class CreatureDropFactory
    {
        public sealed class RuleEntry
        {
            // 掉落模式（按质量 / 按件等）
            public DropMode Mode;
            // 掉落条目数组
            public DropEntry[] Drops;
        }

        private static readonly Dictionary<string, RuleEntry> Rules
            = new Dictionary<string, RuleEntry>(StringComparer.Ordinal);

        // 是否已注册
        private static bool _registered;

        public string CreatureId { get; }
        public DropMode Mode { get; }
        public DropEntry[] Drops { get; }

        public CreatureDropFactory(string creatureId, params DropEntry[] drops)
            : this(creatureId, DropMode.Append, drops) { }

        public CreatureDropFactory(string creatureId, DropMode mode, params DropEntry[] drops)
        {
            if (string.IsNullOrEmpty(creatureId))
                throw new ArgumentException("creatureId 不能为空", nameof(creatureId));

            CreatureId = creatureId;
            Mode = mode;
            Drops = drops ?? Array.Empty<DropEntry>();
            Rules[creatureId] = new RuleEntry { Mode = mode, Drops = Drops };
        }

// 注册全部掉落定义（幂等，重复调用不重复注册）
        internal static void RegisterAll()
        {
            if (_registered) return;
            _registered = true;

            // ==[ZONE:DROPS]BEGIN==
            // ---- 模式 1：Append ----
            // ---- 模式 2：Override ----
            // ---- 模式 3：Replace ----
// ==[ITEM:SeaTurtle:DROP]BEGIN==
            // ════ SeaTurtle 的掉落（2 项，动物产物管理生成）════
            // ==[GROUP:family=SeaTurtle]==
            new CreatureDropFactory("SeaTurtle", DropMode.Replace,
                new DropEntry("ShellfishMeat", 6.0f),  // ==[ERR:0]==
                new DropEntry("SoftshellTurtleMeat", 4.0f));  // ==[ERR:0]==
// ==[ITEM:SeaTurtle:DROP]END==

// ==[ITEM:SeaTurtleBaby:DROP]BEGIN==
            // ════ SeaTurtleBaby 的掉落（2 项，动物产物管理生成）════
            // ==[GROUP:family=SeaTurtle]==
            new CreatureDropFactory("SeaTurtleBaby", DropMode.Replace,
                new DropEntry("ShellfishMeat", 6.0f),  // ==[ERR:0]==
                new DropEntry("SoftshellTurtleMeat", 4.0f));  // ==[ERR:0]==
// ==[ITEM:SeaTurtleBaby:DROP]END==

// ==[ITEM:WoodDeer:DROP]BEGIN==
            // ════ WoodDeer 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=WoodDeer]==
            new CreatureDropFactory("WoodDeer",
                new DropEntry("DeerAntler", 0.3f));  // ==[ERR:0]==
// ==[ITEM:WoodDeer:DROP]END==

// ==[ITEM:GlassDeer:DROP]BEGIN==
            // ════ GlassDeer 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=WoodDeer]==
            new CreatureDropFactory("GlassDeer",
                new DropEntry("DeerAntler", 0.3f));  // ==[ERR:0]==
// ==[ITEM:GlassDeer:DROP]END==

// ==[ITEM:WoodDeerBaby:DROP]BEGIN==
            // ════ WoodDeerBaby 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=WoodDeer]==
            new CreatureDropFactory("WoodDeerBaby",
                new DropEntry("DeerAntler", 0.3f));  // ==[ERR:0]==
// ==[ITEM:WoodDeerBaby:DROP]END==

// ==[ITEM:GlassDeerBaby:DROP]BEGIN==
            // ════ GlassDeerBaby 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=WoodDeer]==
            new CreatureDropFactory("GlassDeerBaby",
                new DropEntry("DeerAntler", 0.3f));  // ==[ERR:0]==
// ==[ITEM:GlassDeerBaby:DROP]END==

// ==[ITEM:Moo:DROP]BEGIN==
            // ════ Moo 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=Moo]==
            new CreatureDropFactory("Moo", DropMode.Replace,
                new DropEntry("BeefMeat", 10.0f));  // ==[ERR:0]==
// ==[ITEM:Moo:DROP]END==

// ==[ITEM:DieselMoo:DROP]BEGIN==
            // ════ DieselMoo 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=Moo]==
            new CreatureDropFactory("DieselMoo", DropMode.Replace,
                new DropEntry("BeefMeat", 10.0f));  // ==[ERR:0]==
// ==[ITEM:DieselMoo:DROP]END==

// ==[ITEM:CrabFreshWater:DROP]BEGIN==
            // ════ CrabFreshWater 的掉落（2 项，动物产物管理生成）════
            new CreatureDropFactory("CrabFreshWater", DropMode.Replace,
                new DropEntry("CrabLeg", 8.0f),  // ==[ERR:0]==
                new DropEntry("CrabClaw", 2.0f));  // ==[ERR:0]==
// ==[ITEM:CrabFreshWater:DROP]END==

// ==[ITEM:CrabFreshWaterBaby:DROP]BEGIN==
            // ════ CrabFreshWaterBaby 的掉落（2 项，动物产物管理生成）════
            new CreatureDropFactory("CrabFreshWaterBaby", DropMode.Replace,
                new DropEntry("CrabLeg", 8.0f),  // ==[ERR:0]==
                new DropEntry("CrabClaw", 2.0f));  // ==[ERR:0]==
// ==[ITEM:CrabFreshWaterBaby:DROP]END==

// ==[ITEM:Pacu:DROP]BEGIN==
            // ════ Pacu 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=Pacu]==
            new CreatureDropFactory("Pacu",
                new DropEntry("FishBone", 0.3f));  // ==[ERR:0]==
// ==[ITEM:Pacu:DROP]END==

// ==[ITEM:PacuCleaner:DROP]BEGIN==
            // ════ PacuCleaner 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=Pacu]==
            new CreatureDropFactory("PacuCleaner",
                new DropEntry("FishBone", 0.3f));  // ==[ERR:0]==
// ==[ITEM:PacuCleaner:DROP]END==

// ==[ITEM:PacuTropical:DROP]BEGIN==
            // ════ PacuTropical 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=Pacu]==
            new CreatureDropFactory("PacuTropical",
                new DropEntry("FishBone", 0.3f));  // ==[ERR:0]==
// ==[ITEM:PacuTropical:DROP]END==

// ==[ITEM:PacuBaby:DROP]BEGIN==
            // ════ PacuBaby 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=Pacu]==
            new CreatureDropFactory("PacuBaby",
                new DropEntry("FishBone", 0.3f));  // ==[ERR:0]==
// ==[ITEM:PacuBaby:DROP]END==

// ==[ITEM:PacuCleanerBaby:DROP]BEGIN==
            // ════ PacuCleanerBaby 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=Pacu]==
            new CreatureDropFactory("PacuCleanerBaby",
                new DropEntry("FishBone", 0.3f));  // ==[ERR:0]==
// ==[ITEM:PacuCleanerBaby:DROP]END==

// ==[ITEM:PacuTropicalBaby:DROP]BEGIN==
            // ════ PacuTropicalBaby 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=Pacu]==
            new CreatureDropFactory("PacuTropicalBaby",
                new DropEntry("FishBone", 0.3f));  // ==[ERR:0]==
// ==[ITEM:PacuTropicalBaby:DROP]END==

// ==[ITEM:PrehistoricPacu:DROP]BEGIN==
            // ════ PrehistoricPacu 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=PrehistoricPacu]==
            new CreatureDropFactory("PrehistoricPacu",
                new DropEntry("FishBone", 8.0f));  // ==[ERR:0]==
// ==[ITEM:PrehistoricPacu:DROP]END==

// ==[ITEM:PrehistoricPacuBaby:DROP]BEGIN==
            // ════ PrehistoricPacuBaby 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=PrehistoricPacu]==
            new CreatureDropFactory("PrehistoricPacuBaby",
                new DropEntry("FishBone", 8.0f));  // ==[ERR:0]==
// ==[ITEM:PrehistoricPacuBaby:DROP]END==

// ==[ITEM:IceBelly:DROP]BEGIN==
            // ════ IceBelly 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=IceBelly]==
            new CreatureDropFactory("IceBelly",
                new DropEntry("LargeBone", 10.0f));  // ==[ERR:0]==
// ==[ITEM:IceBelly:DROP]END==

// ==[ITEM:GoldBelly:DROP]BEGIN==
            // ════ GoldBelly 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=IceBelly]==
            new CreatureDropFactory("GoldBelly",
                new DropEntry("LargeBone", 10.0f));  // ==[ERR:0]==
// ==[ITEM:GoldBelly:DROP]END==

// ==[ITEM:IceBellyBaby:DROP]BEGIN==
            // ════ IceBellyBaby 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=IceBelly]==
            new CreatureDropFactory("IceBellyBaby",
                new DropEntry("LargeBone", 10.0f));  // ==[ERR:0]==
// ==[ITEM:IceBellyBaby:DROP]END==

// ==[ITEM:GoldBellyBaby:DROP]BEGIN==
            // ════ GoldBellyBaby 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=IceBelly]==
            new CreatureDropFactory("GoldBellyBaby",
                new DropEntry("LargeBone", 10.0f));  // ==[ERR:0]==
// ==[ITEM:GoldBellyBaby:DROP]END==

// ==[ITEM:Raptor:DROP]BEGIN==
            // ════ Raptor 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=Raptor]==
            new CreatureDropFactory("Raptor",
                new DropEntry("LargeBone", 10.0f));  // ==[ERR:0]==
// ==[ITEM:Raptor:DROP]END==

// ==[ITEM:RaptorBaby:DROP]BEGIN==
            // ════ RaptorBaby 的掉落（1 项，动物产物管理生成）════
            // ==[GROUP:family=Raptor]==
            new CreatureDropFactory("RaptorBaby",
                new DropEntry("LargeBone", 10.0f));  // ==[ERR:0]==
// ==[ITEM:RaptorBaby:DROP]END==

            // ==[ZONE:DROPS]END==
        }

// 供补丁调用：把本表登记的掉落规则套到具体实体上
        internal static void ApplyTo(Butcherable instance, Dictionary<string, float> drops)
        {
            if (instance == null || drops == null) return;
            var kp = instance.GetComponent<KPrefabID>();
            if (kp == null) return;
            if (!Rules.TryGetValue(kp.PrefabTag.Name, out var rule)) return;

            if (rule.Mode == DropMode.Replace) drops.Clear();

            for (int i = 0; i < rule.Drops.Length; i++)
            {
                var d = rule.Drops[i];
                if (string.IsNullOrEmpty(d.Id) || d.Amount <= 0f) continue;

                if (rule.Mode == DropMode.Append)
                {
                    if (!drops.ContainsKey(d.Id)) drops[d.Id] = d.Amount;
                }
                else
                {
                    drops[d.Id] = d.Amount;
                }
            }
        }

        // creatureId：creature id
        public static void Unregister(string creatureId) => Rules.Remove(creatureId);
        public static void Clear() => Rules.Clear();
        // creatureId：标识 id
        public static bool Has(string creatureId) => Rules.ContainsKey(creatureId);
    }

    [HarmonyPatch(typeof(Butcherable), "SetDrops",
        new Type[] { typeof(Dictionary<string, float>) })]
    public static class ButcherableSetDropsPatch
    {
        [HarmonyPrepare]
        static void Prepare()
        {
            try
            {
                CreatureDropFactory.RegisterAll();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:ButcherableSetDropsPatch", e); }
        }

        static void Prefix(Butcherable __instance, Dictionary<string, float> drops)
        {
            try
            {
                CreatureDropFactory.ApplyTo(__instance, drops);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:ButcherableSetDropsPatch", e); }
        }
    }
}
