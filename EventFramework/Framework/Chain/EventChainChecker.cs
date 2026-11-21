// EventChainChecker.cs — 事件框架 · 【链式闭环静态检测】

using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventChainChecker
    {
        // 本次检测剪掉的边数
        public static int BrokenEdges { get; private set; }

        // 本次检测发现的环数
        public static int FoundCycles { get; private set; }

 // 运行时防环累计拒绝次数（Dump 用）
        public static int CycleRejects { get; private set; }

        // 运行时某条分支因"已访问"被拒时调用（只计数，日志由调用方打）
        public static void NoteCycleReject()
        {
            CycleRejects++;
        }

        private static readonly List<string> _path = new List<string>();
        private static readonly HashSet<string> _onPath = new HashSet<string>();
        private static readonly HashSet<string> _done = new HashSet<string>();

        // 检测所有链式闭环，发现环就剪掉"回到路径上已有点"的那条边。
        public static void DetectAndBreakCycles()
        {
            BrokenEdges = 0; FoundCycles = 0;
            _path.Clear(); _onPath.Clear(); _done.Clear();

            try
            {
                foreach (var def in EventRegistry.All)
                {
                    if (def == null || string.IsNullOrEmpty(def.id)) continue;
                    if (_done.Contains(def.id)) continue;
                    Dfs(def.id, 0);
                }

                if (FoundCycles > 0)
                    Debug.LogWarning("[MoreFood/Event][Chain] 静态检测：发现 " + FoundCycles
                                     + " 处闭环（只报警不剪边；运行时的路径去重会拒绝闭环，事件本体保留）");
                else
                    Debug.Log("[MoreFood/Event][Chain] 静态检测：无闭环");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Chain] DetectAndBreakCycles 异常: " + e);
            }
        }

        private static void Dfs(string id, int depth)
        {
            if (depth > 64)
            {
                Debug.LogWarning("[MoreFood/Event][Chain] 分支深度超过 64，疑似过深，停止该分支: " + id);
                return;
            }
            if (_onPath.Contains(id))
            {
                // 找到了一个环：id 已在当前路径上
                FoundCycles++;
                ReportCycle(id);
                return;
            }

            _path.Add(id);
            _onPath.Add(id);

            var def = EventRegistry.Get(id);
            if (def != null)
            {
 // 出边 = 根级 next_event + 每个选项的 next_event（chain 已废除）
                var outs = new List<string>();
                CollectOuts(def.next_event, def.id + ".next_event", outs);
                if (def.options != null)
                {
                    for (int i = 0; i < def.options.Count; i++)
                    {
                        var o = def.options[i];
                        if (o == null) continue;
                        CollectOuts(o.next_event, def.id + ".options[" + i + "].next_event", outs);
                    }
                }

                for (int i = 0; i < outs.Count; i++)
                {
                    string nx = outs[i];
                    if (_onPath.Contains(nx))
                    {
                        FoundCycles++;
                        ReportCycle(nx);
                        continue;
                    }
                    if (!EventRegistry.Has(nx))
                    {
                        Debug.LogWarning("[MoreFood/Event][Chain] 分支目标不存在（运行时再判）："
                                         + id + " → " + nx);
                        continue;
                    }

                    Dfs(nx, depth + 1);
                }
            }

            _path.RemoveAt(_path.Count - 1);
            _onPath.Remove(id);
            _done.Add(id);
        }

 // 环分类报告：环上【无】one_shot ⇒ Info（合法循环，需要 next_allow_repeat）；
        private static void ReportCycle(string closeTo)
        {
            string pathText = BuildPathText();
            bool hasOneShot = IsOneShot(closeTo);
            if (!hasOneShot)
            {
                for (int i = 0; i < _path.Count; i++)
                {
                    if (IsOneShot(_path[i])) { hasOneShot = true; break; }
                }
            }

            if (hasOneShot)
                Debug.LogWarning("[MoreFood/Event][Chain] 检测到环：" + pathText + " → " + closeTo
                                 + " ⚠ 环上有 one_shot 节点 ⇒ 链回到它时会断裂；要么去掉 one_shot，要么改图");
            else
                Debug.Log("[MoreFood/Event][Chain] 检测到环：" + pathText + " → " + closeTo
                          + "（合法循环：运行时会按路径去重拦截，确需循环请写 next_allow_repeat: true）");
        }

        private static bool IsOneShot(string id)
        {
            var d = EventRegistry.Get(id);
            return d != null && d.repeat != null && d.repeat.one_shot;
        }

        // 取出一个 next_event 列表里的全部目标 id
        private static void CollectOuts(List<object> raw, string owner, List<string> into)
        {
            if (raw == null) return;
            var bs = EventDef.ParseBranches(raw, owner);
            for (int i = 0; i < bs.Count; i++) into.Add(bs[i].next);
        }

        private static string BuildPathText()
        {
            if (_path.Count == 0) return "(空)";
            return string.Join(" → ", _path.ToArray());
        }
    }
}
