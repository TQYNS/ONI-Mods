// Buildings/AutoMiner/AutoMinerHarvestMode.cs — 自动采矿机采集模式状态组件

using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class AutoMinerHarvestMode : KMonoBehaviour
    {
        [Serialize]
        public bool harvestMode;

        public static bool IsHarvestMode(GameObject go)
        {
            if (go == null) return false;
            var m = go.GetComponent<AutoMinerHarvestMode>();
            return m != null && m.harvestMode;
        }
    }
}
