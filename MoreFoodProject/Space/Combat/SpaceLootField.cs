using System;
using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    // 小行星带（§14 + K7 简化）

    internal static class SpaceAsteroidBeltVisual
    {
        internal const string FieldAnim = "harvestable_space_poi_kanim";
        internal const string RockAnim = "meteor_rock_kanim";
        internal const string RockState = "fall_loop";
        internal const float RockRingRadius = 0.42f;

        private static readonly string[] FieldStates = new string[]
        {
            "metallic_asteroid_field",
            "rocky_asteroid_field",
            "gilded_asteroid_field",
            "oxidized_asteroid_field",
            "ice_asteroid_field",
            "cloud"
        };

        internal static string PreferredState(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return "rocky_asteroid_field";
            }
            if (tag == "lost_logistics")
            {
                return "gilded_asteroid_field";
            }
            if (tag == "pirate_wreck" || tag == "battle_debris" || tag == "war_ruins")
            {
                return "metallic_asteroid_field";
            }
            return "rocky_asteroid_field";
        }

        internal static List<ClusterGridEntity.AnimConfig> ConfigsFor(string tag)
        {
            List<ClusterGridEntity.AnimConfig> list = new List<ClusterGridEntity.AnimConfig>();
            try
            {
                KAnimFile field = null;
                if (Assets.TryGetAnim(FieldAnim, out field) && field != null)
                {
                    string want = PreferredState(tag);
                    if (HasState(field, want))
                    {
                        list.Add(Make(field, want, Vector3.zero, 1f));
                        return list;
                    }
                    for (int i = 0; i < FieldStates.Length; i++)
                    {
                        if (HasState(field, FieldStates[i]))
                        {
                            list.Add(Make(field, FieldStates[i], Vector3.zero, 1f));
                            MFLog.Info(LogCat.Space, "[Space] 小行星带外观：" + FieldAnim + " 用状态 " + FieldStates[i]
                                + "（首选 " + want + " 缺失，来源 " + tag + "）");
                            return list;
                        }
                    }
                    Debug.LogWarning("[Space] 小行星带外观：" + FieldAnim + " 没有任何小行星带状态，回退陨石带");
                }
                else
                {
                    Debug.LogWarning("[Space] 小行星带外观：找不到 " + FieldAnim + "，回退陨石带");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 小行星带外观解析失败，回退陨石带");
                list.Clear();
            }
            BuildRockBelt(list);
            return list;
        }

        private static void BuildRockBelt(List<ClusterGridEntity.AnimConfig> list)
        {
            try
            {
                KAnimFile rock = null;
                if (!Assets.TryGetAnim(RockAnim, out rock) || rock == null)
                {
                    Debug.LogWarning("[Space] 小行星带外观：连 " + RockAnim + " 都没有，打捞点将不可见");
                    return;
                }
                if (!HasState(rock, RockState))
                {
                    Debug.LogWarning("[Space] 小行星带外观：" + RockAnim + " 没有 " + RockState
                        + " 状态，打捞点将不可见");
                    return;
                }
                list.Add(Make(rock, RockState, Vector3.zero, 1f));
                for (int i = 0; i < 6; i++)
                {
                    float ang = (float)i * Mathf.PI / 3f;
                    Vector3 off = new Vector3(Mathf.Cos(ang) * RockRingRadius,
                        Mathf.Sin(ang) * RockRingRadius, 0f);
                    list.Add(Make(rock, RockState, off, 0.8f + 0.1f * (float)(i % 4)));
                }
                MFLog.Info(LogCat.Space, "[Space] 小行星带外观：陨石带拼装 " + list.Count + " 块 " + RockAnim + " " + RockState);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 小行星带陨石拼装失败");
            }
        }

        private static ClusterGridEntity.AnimConfig Make(KAnimFile file, string state, Vector3 offset,
            float speed)
        {
            ClusterGridEntity.AnimConfig c = new ClusterGridEntity.AnimConfig();
            c.animFile = file;
            c.initialAnim = state;
            c.playMode = KAnim.PlayMode.Loop;
            c.animOffset = offset;
            c.animPlaySpeedModifier = speed;
            return c;
        }

        private static bool HasState(KAnimFile file, string state)
        {
            try
            {
                if (file == null || string.IsNullOrEmpty(state) || !file.IsAnimLoaded)
                {
                    return false;
                }
                KAnimFileData data = file.GetData();
                if (data == null)
                {
                    return false;
                }
                return data.GetAnim(state) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    [SerializationConfig(MemberSerialization.OptIn)]   // KSerialization OptOut 只认 public 字段，必须 OptIn
    public class SpaceLootField : ClusterGridEntity
    {
        [Serialize]
        internal string sourceTag = "monster_death";
        [Serialize]
        internal string label = "小行星带";
        [Serialize]
        internal List<string> lootItemIds = new List<string>();
        [Serialize]
        internal List<float> lootMasses = new List<float>();
        [Serialize]
        internal List<int> lootCounts = new List<int>();

        public override string Name
        {
            get { return string.IsNullOrEmpty(label) ? "小行星带" : label; }
        }

        public override EntityLayer Layer
        {
            get { return EntityLayer.POI; }
        }

        public override List<ClusterGridEntity.AnimConfig> AnimConfigs
        {
            get
            {
                // 占位，需替换为小行星带专属动画（K5）
                return SpaceAsteroidBeltVisual.ConfigsFor(sourceTag);
            }
        }

        public override bool IsVisible
        {
            get { return true; }
        }

        public override ClusterRevealLevel IsVisibleInFOW
        {
            // 跟随迷雾：Peeked=半揭示问号/全揭示实体（Visible 会无视迷雾永显）
            get { return ClusterRevealLevel.Peeked; }
        }

        internal string DescribeContents()
        {
            string s = "";
            for (int i = 0; i < lootItemIds.Count; i++)
            {
                if (i > 0)
                {
                    s += "，";
                }
                s += lootItemIds[i] + " " + Mathf.Round(i < lootMasses.Count ? lootMasses[i] : 0f) + "kg";
            }
            return string.IsNullOrEmpty(s) ? "（空）" : s;
        }

        internal string DescribePretty()
        {
            string s = "";
            for (int i = 0; i < lootItemIds.Count; i++)
            {
                if (i > 0) s += "、";
                string nm = lootItemIds[i];
                try { nm = TradeUtil.DisplayName(lootItemIds[i]); } catch { /* expected: 查不到显示名时退回原始物品 id 作名称 */ }
                float kg = i < lootMasses.Count ? lootMasses[i] : 0f;
                int cnt = i < lootCounts.Count ? lootCounts[i] : 0;
                if (kg > 0f) s += nm + " " + Mathf.Round(kg) + "kg";
                else if (cnt > 0) s += nm + " ×" + cnt;
                else s += nm;
            }
            return string.IsNullOrEmpty(s) ? "空" : s;
        }
        internal static GameObject CreatePrefab(string id, string displayName)
        {
            GameObject go = EntityTemplates.CreateEntity(id, displayName, true);
            go.AddOrGet<SaveLoadRoot>();
            go.AddOrGet<SpaceLootField>();
            go.SetActive(false);
            Assets.AddPrefab(go.GetComponent<KPrefabID>());
            return go;
        }
    }
}
