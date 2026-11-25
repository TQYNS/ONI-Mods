using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    [SerializationConfig(MemberSerialization.OptIn)]   // KSerialization OptOut 只认 public 字段，必须 OptIn
    public class SpaceStation : ClusterGridEntity
    {
        [Serialize]
        internal string stationId;

        [Serialize]
        internal string displayName;

        [Serialize]
        internal string kind;

        [Serialize]
        internal bool subMap;

        // v5.8：动画状态在这里定一次（站点 yaml 可单配、可配一组随机、也可不配走全局组），之后随存档不变
        [Serialize]
        internal string animState;

        // 卫星贴图：每站固定一个 kanim 文件（组内尽量不重复），选定后随存档不变
        [Serialize]
        internal string animFile;

        [Serialize]
        internal bool isShip;

        private ClusterTraveler traveler;

        // 所有已生成站点（只用于卫星贴图去重；载入时逐个 OnSpawn 注册）
        private static readonly List<SpaceStation> AllStations = new List<SpaceStation>();

        private bool animResolved;

        internal string StationName
        {
            get { return string.IsNullOrEmpty(displayName) ? "贸易站" : displayName; }
        }

        internal string Key
        {
            get
            {
                return string.IsNullOrEmpty(stationId)
                    ? "legacy@" + SpaceCfg.Pos(Location)
                    : stationId;
            }
        }

        internal StationDef Def
        {
            get { return TradeDb.FindStation(stationId); }
        }

        public override string Name
        {
            get { return StationName; }
        }

        public override EntityLayer Layer
        {
            get { return EntityLayer.POI; }
        }

        public override List<ClusterGridEntity.AnimConfig> AnimConfigs
        {
            get
            {
                return new List<ClusterGridEntity.AnimConfig>
                {
                    new ClusterGridEntity.AnimConfig
                    {
                        animFile = Assets.GetAnim(AnimName()),
                        initialAnim = AnimState()
                    }
                };
            }
        }

        public override bool IsVisible
        {
            get { return true; }
        }

        public override bool ShowPath()
        {
            return isShip;
        }

        public override bool ShowProgressBar()
        {
            return isShip && traveler != null && traveler.IsTraveling();
        }

        public override float GetProgress()
        {
            return traveler != null ? traveler.GetMoveProgress() : 0f;
        }

        public override ClusterRevealLevel IsVisibleInFOW
        {
            // 语义修正：IsVisibleInFOW=Visible 在原版 GetRevealLevel 里等于
            get { return ClusterRevealLevel.Peeked; }
        }

        internal string AnimName()
        {
            ResolveAnim();
            if (!string.IsNullOrEmpty(animFile))
            {
                return animFile;
            }
            // 卫星贴图组不可用时才走到这里：回退原版 POI 动画（状态名见 SpaceCfg.StationAnimState）
            return SpaceCfg.StationAnim;
        }

        internal string AnimState()
        {
            ResolveAnim();
            return string.IsNullOrEmpty(animState) ? SpaceCfg.StationAnimState : animState;
        }

        // 优先：trade/anim/*.yaml 的卫星贴图组（组内随机、尽量不重复）
        private void ResolveAnim()
        {
            if (!string.IsNullOrEmpty(animFile))
            {
                if (Assets.GetAnim(animFile) != null)
                {
                    animResolved = true;
                    return;
                }
                Debug.LogWarning("[Space] 站点 " + Key + " 的卫星贴图 " + animFile + " 已失效，重新分配");
                animFile = null;
                animState = null;
                animResolved = false;
            }
            if (animResolved)
            {
                return;
            }
            animResolved = true;

            StationDef d = Def;
            string groupName = d != null ? d.AnimGroupName : "normal";
            SatelliteAnimGroupDef sat = TradeDb.SatelliteAnimGroup(groupName);
            if (sat != null)
            {
                string file = AllocateSatelliteAnim(sat);
                if (!string.IsNullOrEmpty(file))
                {
                    animFile = file;
                    animState = string.IsNullOrEmpty(sat.anim_state) ? "object" : sat.anim_state;
                    MFLog.Info(LogCat.Space, "[Space] 站点 " + Key + " 卫星贴图 → " + animFile
                        + "（组 " + groupName + "）");
                    return;
                }
                Debug.LogWarning("[Space] 卫星贴图组 " + groupName + " 没有可用动画，回退旧站点动画");
            }

            if (!string.IsNullOrEmpty(animState))
            {
                return;
            }
            if (d != null)
            {
                string[] states = d.AnimStates();
                if (states != null && states.Length > 0)
                {
                    animState = states[states.Length == 1 ? 0 : UnityEngine.Random.Range(0, states.Length)];
                    MFLog.Info(LogCat.Space, "[Space] 站点 " + d.id + " 动画来自站点配置 → " + animState);
                    return;
                }
            }
            animState = SpaceCfg.StationAnimState;
        }

        // 组内随机抽一个本档还没被其他站点占用的贴图；全占满才允许重复
        private static string AllocateSatelliteAnim(SatelliteAnimGroupDef group)
        {
            List<string> valid = new List<string>();
            string[] names = group.anims;
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (string.IsNullOrEmpty(name) || valid.Contains(name))
                {
                    continue;
                }
                if (Assets.GetAnim(name) == null)
                {
                    Debug.LogWarning("[Space] 卫星贴图缺动画文件：" + name
                        + "（组 " + group.group + "），已跳过");
                    continue;
                }
                valid.Add(name);
            }
            if (valid.Count == 0)
            {
                return null;
            }

            HashSet<string> used = new HashSet<string>();
            for (int i = AllStations.Count - 1; i >= 0; i--)
            {
                SpaceStation st = AllStations[i];
                if (st == null)
                {
                    AllStations.RemoveAt(i);
                    continue;
                }
                if (!string.IsNullOrEmpty(st.animFile))
                {
                    used.Add(st.animFile);
                }
            }

            List<string> free = new List<string>();
            for (int i = 0; i < valid.Count; i++)
            {
                if (!used.Contains(valid[i]))
                {
                    free.Add(valid[i]);
                }
            }
            if (free.Count > 0)
            {
                return free[UnityEngine.Random.Range(0, free.Count)];
            }
            Debug.LogWarning("[Space] 卫星贴图组 " + group.group + " 的 " + valid.Count
                + " 张候选已被占满，本次允许重复");
            return valid[UnityEngine.Random.Range(0, valid.Count)];
        }

        internal void ApplyIdentity()
        {
            ResolveAnim();
            if (!string.IsNullOrEmpty(stationId))
            {
                TradeRegistry.EnsureState(stationId);
            }
            InfoDescription info = GetComponent<InfoDescription>();
            if (info != null)
            {
                info.description = BuildDescription();
            }
        }

        private string BuildDescription()
        {
            string id = stationId ?? "";
            if (id.StartsWith("evt_temp_alien", System.StringComparison.Ordinal))
            {
                return "一座来历不明的信号中继站，货架上备有大量稀有物资与传奇货物。"
                    + "停留时间有限，补给完成后会自行离开。";
            }
            if (id.StartsWith("evt_temp_freighter", System.StringComparison.Ordinal))
            {
                return "一艘满载货物的星际货船，正在本星球轨道停靠。"
                    + "把火箭开到相邻一格即可登船交易。";
            }
            if (!string.IsNullOrEmpty(kind) && kind == "blackmarket")
            {
                return "一座黑市卫星，出售稀缺与违禁物资，回收价更高，但偶尔会关闭。";
            }
            if (!string.IsNullOrEmpty(kind) && kind == "pirate")
            {
                return "一座海盗控制的前哨卫星，物资丰富，但交易规则与普通站点不同。";
            }
            return "一座贸易卫星，可在此买卖物资、补给燃料。"
                + "把火箭停靠到本站或相邻一格即可交易。";
        }

        protected override void OnSpawn()
        {
            try
            {
            base.OnSpawn();
            if (!AllStations.Contains(this))
            {
                AllStations.Add(this);
            }
            traveler = GetComponent<ClusterTraveler>();
            ClusterDestinationSelector selector = GetComponent<ClusterDestinationSelector>();
            if (selector != null)
            {
                selector.assignable = false;
                if (isShip)
                {
                    selector.canNavigateFogOfWar = true;
                }
                if (selector.GetDestination() != Location)
                {
                    selector.SetDestination(Location);
                }
            }
            if (isShip && traveler != null)
            {
                traveler.revealsFogOfWarAsItTravels = true;
                traveler.getSpeedCB = new System.Func<float>(delegate { return SpaceCfg.FreighterSpeed; });
            }
            ApplyIdentity();
            if (stationId == TradeDb.Config.hunter_station_id && !string.IsNullOrEmpty(stationId))
            {
                TradeRegistry.HunterStationSeen = true;
                // 读档时站点先于 TradeSaveData.Restore 生成：先记 Seen，等恢复后再发，
                if (SpaceRestoreGate.Pending && SpaceRestoreGate.Deserialized)
                {
                    MFLog.Info(LogCat.Space, "[Space] 猎人协会已生成（读档中）：首见奖励等存档恢复后结算");
                }
                else
                {
                    TradeRegistry.GrantHunterFirstContact();
                }
            }
            }
            catch (System.Exception e) { MFLog.HotFail("spawn:SpaceStation", e); }
        }

        protected override void OnCleanUp()
        {
            try
            {
            AllStations.Remove(this);
            base.OnCleanUp();
            }
            catch (System.Exception e) { MFLog.HotFail("cleanup:SpaceStation", e); }
        }

        internal static GameObject CreatePrefab(string id, string displayName)
        {
            GameObject go = EntityTemplates.CreateEntity(id, displayName, true);
            go.AddOrGet<SaveLoadRoot>();
            go.AddOrGet<SpaceStation>();
            // 移除原生侧屏按钮「交易（打开独立窗口）」——已有自建交易 UI（底部按钮排）。
            go.AddOrGet<InfoDescription>();
            go.SetActive(false);
            Assets.AddPrefab(go.GetComponent<KPrefabID>());
            return go;
        }
    }

    public class TradeOpenButton : SpaceBeaconButton
    {
        public override string SidescreenButtonText
        {
            get { return "交易（打开独立窗口）"; }
        }

        public override string SidescreenButtonTooltip
        {
            get
            {
                return "打开贸易窗口。远程（火箭不在本站）时只能看收购清单、不能交易；"
                    + "火箭停靠本站后可买卖、可补燃料。";
            }
        }

        public override int ButtonSideScreenSortOrder()
        {
            return 1;
        }

        public override bool SidescreenButtonInteractable()
        {
            return GetComponent<SpaceStation>() != null;
        }

        public override void OnSidescreenButtonPressed()
        {
            try
            {
                TradeUi.OpenTrade(GetComponent<SpaceStation>(), false);
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 打开贸易窗口失败");
            }
        }
    }
}
