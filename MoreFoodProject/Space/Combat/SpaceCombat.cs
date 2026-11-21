using System;
using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    // 星图生物实体

    [SerializationConfig(MemberSerialization.OptIn)]   // ★ KSerialization OptOut 只认 public 字段，必须 OptIn
    public class SpaceMonster : ClusterGridEntity
    {
        [Serialize]
        internal string defId;

        [Serialize]
        internal float hp;

        [Serialize]
        internal float armor;

        [Serialize]
        internal bool fled;   // 旧档遗留字段（保留读写兼容，不再参与逻辑）

        // 死亡/离场即时置位（运行期，不入存档）：拦住原版 SetupVisGameObjects 重建 visualizer
        internal bool removed;

        internal MonsterRuntime rt;

        internal CombatMonsterDef Def
        {
            get { return CombatDb.FindMonster(defId); }
        }

        internal float MaxHp
        {
            get
            {
                CombatMonsterDef d = Def;
                return d != null && d.hp > 0f ? d.hp : 100f;
            }
        }

        public override string Name
        {
            get
            {
                CombatMonsterDef d = Def;
                return d != null && !string.IsNullOrEmpty(d.name) ? d.name : "星图生物";
            }
        }

        // 只有完全揭示的格子才认这只怪；Peeked / Hidden 一律当它不存在
        private bool CellRevealed()
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null)
            {
                return false;
            }
            return grid.GetCellRevealLevel(Location) == ClusterRevealLevel.Visible;
        }

        public override EntityLayer Layer
        {
            get { return EntityLayer.POI; }
        }

        public override List<ClusterGridEntity.AnimConfig> AnimConfigs
        {
            get
            {
                CombatMonsterDef d = Def;
                string anim = d != null && !string.IsNullOrEmpty(d.anim) ? d.anim : SpaceCfg.MonsterAnim;
                KAnimFile file = Assets.GetAnim(anim);
                if (file == null)
                {
                    // 资源缺失（yaml 写错 / kanim 未打包）⇒ 退回共用占位，别让 visualizer 拿到空 animFile
                    MFLog.Warn(LogCat.Combat, "[Space] 怪物动画缺失：" + anim + "（" + defId + "），回退 " + SpaceCfg.MonsterAnim);
                    file = Assets.GetAnim(SpaceCfg.MonsterAnim);
                }
                // ★ 怪物美术目前是"只有一帧 ui"的 UI 贴图（打标/怪物组/ui），没有 idle 动画；
                //   写死 idle_loop 会播不出来 → 取不到该状态时退回 kanim 的第一个动画（通常就是 ui）。
                string state = SpaceCfg.MonsterAnimState;
                if (file != null && !HasAnim(file, state) && HasAnim(file, "ui"))
                {
                    state = "ui";   // 专属贴图只有一帧 ui
                }
                return new List<ClusterGridEntity.AnimConfig>
                {
                    new ClusterGridEntity.AnimConfig
                    {
                        animFile = file,
                        initialAnim = state,
                        playMode = KAnim.PlayMode.Loop
                    }
                };
            }
        }

        private static bool HasAnim(KAnimFile file, string state)
        {
            try
            {
                if (file == null || string.IsNullOrEmpty(state) || !file.IsAnimLoaded)
                {
                    return false;
                }
                KAnimFileData data = file.GetData();
                return data != null && data.GetAnim(state) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool IsVisible
        {
            // 未揭示格当作不存在：visualizer 不建、ToolTip 不挂，格子里什么都点不到
            get { return !removed && !fled && CellRevealed(); }
        }

        public override ClusterRevealLevel IsVisibleInFOW
        {
            // ★ 跟随迷雾：Visible=原版语义为"无视迷雾永显"；Peeked=半揭示问号/全揭示实体
            get { return ClusterRevealLevel.Peeked; }
        }

        // ★ 任务C：让怪物成为"可被原版选中的星图实体"，从而走原版信息面板（DetailsScreen）。
        protected override void OnSpawn()
        {
            try
            {
            if (m_selectable == null)
            {
                m_selectable = gameObject.AddOrGet<KSelectable>();
            }
            base.OnSpawn();
            }
            catch (System.Exception e) { MFLog.HotFail("spawn:SpaceCombat", e); }
        }

        internal static GameObject CreatePrefab(string id, string displayName)
        {
            GameObject go = EntityTemplates.CreateEntity(id, displayName, true);
            go.AddOrGet<SaveLoadRoot>();
            go.AddOrGet<SpaceMonster>();
            go.SetActive(false);
            Assets.AddPrefab(go.GetComponent<KPrefabID>());
            return go;
        }
    }
}
