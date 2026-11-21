// EventLocator.cs — 事件定位器（独立模块）
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventLocator
    {
        // instId → 目标实体 InstanceID 队列
        private static readonly Dictionary<string, List<int>> _queues =
            new Dictionary<string, List<int>>(StringComparer.Ordinal);
        // instId → 绑定时的模板版本号（与 EventDef.version 比对做脏标记）
        private static readonly Dictionary<string, int> _versions =
            new Dictionary<string, int>(StringComparer.Ordinal);

        // 宿主注册的星图跳转解析器：若 instanceId 对应一个星图坐标，优先按星图跳转（点击事件通知/历史时调用）
        public static Func<string, bool> StarmapJump;

        // 作者入口：一行启用
        public static void Bind(EventInstance inst, IList<GameObject> targets)
        {
            if (inst == null || string.IsNullOrEmpty(inst.instanceId)) return;
            try
            {
                var q = new List<int>();
                if (targets != null)
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        var go = targets[i];
                        if (go == null) continue;                 // 已销毁 → 直接移除
                        var kpid = go.GetComponent<KPrefabID>();
                        if (kpid == null) continue;               // 无存档索引 → 无法定位
                        q.Add(kpid.InstanceID);
                    }
                }
                BindRaw(inst.instanceId, inst.def != null ? inst.def.version : 1, q);
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Locate] Bind 异常: " + e); }
        }

        // 内容 Mod 只给 id：目标实体编号由它自己取，框架不再碰 GameObject
        public static void BindRaw(string instId, int version, IList<int> ids)
        {
            if (string.IsNullOrEmpty(instId)) return;
            try
            {
                var q = new List<int>();
                if (ids != null)
                {
                    for (int i = 0; i < ids.Count; i++)
                    {
                        if (ids[i] != 0) q.Add(ids[i]);
                    }
                }
                _queues[instId] = q;
                _versions[instId] = version <= 0 ? 1 : version;
                CaptureToSave();
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Locate] BindRaw 异常: " + e); }
        }

        public static bool Has(string instId)
        {
            List<int> q;
            return !string.IsNullOrEmpty(instId) && _queues.TryGetValue(instId, out q) && q.Count > 0;
        }

        // 点击通知/历史时调用：从队头起查，无效/失败/异常 → 出队；成功 → 队头转队尾（循环）。每次只做一轮。
        public static bool Jump(string instId)
        {
            if (string.IsNullOrEmpty(instId)) return false;
            // 星图跳转优先（宿主注册；仅当该 instanceId 已注册星图坐标时生效，否则回退下方世界实体定位）
            if (StarmapJump != null && StarmapJump(instId)) return true;
            try
            {
                List<int> q;
                if (!_queues.TryGetValue(instId, out q) || q.Count == 0) return false;

                int n = q.Count;
                for (int guard = 0; guard < n; guard++)
                {
                    int id = q[0];
                    if (JumpToEntity(id))
                    {
                        q.RemoveAt(0);
                        q.Add(id);                 // 队头 → 队尾
                        CaptureToSave();
                        return true;
                    }
                    q.RemoveAt(0);                 // 无效/失败 → 出队，查下一个
                }
                CaptureToSave();
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Locate] Jump 异常: " + e);
                return false;
            }
        }

        private static bool JumpToEntity(int id)
        {
            var tracker = KPrefabIDTracker.Get();
            if (tracker == null) return false;
            var kpid = tracker.GetInstance(id);
            if (kpid == null) return false;
            var go = kpid.gameObject;
            if (go == null) return false;

            var sel = go.GetComponent<KSelectable>();
            if (sel != null) SelectTool.Instance.SelectAndFocus(go.transform.position, sel);
            else GameUtil.FocusCamera(Grid.PosToCell(go.transform.position), true);
            return true;
        }

        public static void Drop(string instId)
        {
            if (string.IsNullOrEmpty(instId)) return;
            if (_queues.Remove(instId)) { _versions.Remove(instId); CaptureToSave(); }
        }

        // 是否还有可跳转目标（供历史行的"跳转"按钮亮灭）
        public static bool CanJump(string instId)
        {
            List<int> q;
            return !string.IsNullOrEmpty(instId) && _queues.TryGetValue(instId, out q) && q.Count > 0;
        }

        // 目标全部消失时的一次性提示
        public static void NotifyNoTarget()
        {
            try
            {
                Notifier n = null;
                var all = Components.Notifiers != null ? Components.Notifiers.Items : null;
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        if (all[i] != null && !all[i].DisableNotifications) { n = all[i]; break; }
                    }
                }
                if (n == null) return;
                n.Add(new Notification("目标已死亡或消失，无法跳转", NotificationType.BadMinor,
                    (System.Func<List<Notification>, object, string>)null, null,
                    true, 0f, null, null, null, true, true, false));
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Locate] NotifyNoTarget 异常: " + e); }
        }

        public static void Clear()
        {
            _queues.Clear();
            _versions.Clear();
        }

        // 存档（写入 IEventSaveData 的并行表；沿用事件系统既有落盘通道）
        public static void CaptureToSave()
        {
            var d = EventSaveBridge.Data;
            if (d == null) return;
            try
            {
                var ids = new List<string>();
                var queues = new List<string>();
                var vers = new List<int>();
                foreach (var kv in _queues)
                {
                    if (kv.Value == null || kv.Value.Count == 0) continue;
                    ids.Add(kv.Key);
                    queues.Add(string.Join("|", kv.Value.ConvertAll(n => n.ToString()).ToArray()));
                    int ver; _versions.TryGetValue(kv.Key, out ver);
                    vers.Add(ver);
                }
                d.locInstId = ids;
                d.locQueue = queues;
                d.locVersion = vers;
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Locate] CaptureToSave 异常: " + e); }
        }

        // 读档重建（由 EventSaveBridge.Restore 调用）。版本不符（模板 version 变了）⇒ 该队列作脏丢弃。
        public static void LoadFromSave()
        {
            Clear();
            var d = EventSaveBridge.Data;
            if (d == null || d.locInstId == null) return;
            try
            {
                int n = d.locInstId.Count;
                for (int i = 0; i < n; i++)
                {
                    string instId = d.locInstId[i];
                    if (string.IsNullOrEmpty(instId)) continue;
                    string raw = (d.locQueue != null && i < d.locQueue.Count) ? d.locQueue[i] : null;
                    if (string.IsNullOrEmpty(raw)) continue;
                    int saveVer = (d.locVersion != null && i < d.locVersion.Count) ? d.locVersion[i] : 1;

                    int curVer = saveVer;
                    var act = EventSystem.ActiveInstances;
                    if (act != null)
                    {
                        for (int j = 0; j < act.Count; j++)
                            if (act[j] != null && act[j].instanceId == instId && act[j].def != null)
                            { curVer = act[j].def.version; break; }
                    }
                    if (curVer != saveVer) continue;   // ★ 版本脏 → 丢弃

                    var q = new List<int>();
                    string[] parts = raw.Split('|');
                    for (int k = 0; k < parts.Length; k++)
                    {
                        int v;
                        if (int.TryParse(parts[k], out v) && v != 0) q.Add(v);
                    }
                    _queues[instId] = q;
                    _versions[instId] = curVer;
                }
                Debug.Log("[MoreFood/Event][Locate] 读档重建：队列 " + _queues.Count + " 条");
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Locate] LoadFromSave 异常: " + e); }
        }
    }
}
