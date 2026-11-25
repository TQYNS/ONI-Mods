// EventSaveData.cs — MoreFood · 宿主随档载体：持续 buff 快照 + 灾星闸口，挂载与补写全走宿主补丁（见 叙事者.md §5.2）

using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class EventSaveData : KMonoBehaviour
    {
        // 持续 buff 快照（id / 数值 / 剩余游戏秒），由 PersistBuffs 即时写入
        [Serialize] public List<string> buffId = new List<string>();
        [Serialize] public List<float> buffValue = new List<float>();
        [Serialize] public List<float> buffRemain = new List<float>();

        // 任意来源的灾星已发生；置位即作废大灭绝，不再进抽取池（事件.md §17.6）
        [Serialize] public bool impactorGateUsed;

        // 当前宿主对象（= SaveGame.gameObject），换宿主时重新取
        private static EventSaveData cached;
        private static GameObject cachedHost;

        // 取（必要时挂上）本局载体；拿不到 SaveGame 时返回 null，调用方按"无快照"处理
        internal static EventSaveData Attach()
        {
            try
            {
                SaveGame sg = SaveGame.Instance;
                if (sg == null)
                {
                    return null;
                }
                GameObject go = sg.gameObject;
                if (go == null)
                {
                    return null;
                }
                if (cached != null && cachedHost == go)
                {
                    return cached;
                }
                cachedHost = go;
                cached = go.GetComponent<EventSaveData>();
                if (cached == null)
                {
                    cached = go.AddComponent<EventSaveData>();
                }
                return cached;
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.Save, e, "[MoreFood/Save] buff 快照载体挂载失败（读档后持续 buff 会丢失）");
                return null;
            }
        }

        // 只清持续 buff 快照（换局/清档）；存档重写路径绝不能碰它，否则快照会在存盘瞬间被抹掉
        public void ResetBuffSnapshot()
        {
            buffId.Clear(); buffValue.Clear(); buffRemain.Clear();
        }

        public string Describe()
        {
            return "buffSnapshot=" + buffId.Count;
        }
    }
}
