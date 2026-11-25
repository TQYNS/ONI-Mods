// LoadOrderResolver.cs — 事件框架 · 【加载顺序：三档分类 + 拓扑排序，只处理真启用的 mod】

using System;
using System.Collections.Generic;
using System.IO;
using EventFramework.Distribution.Contracts;
using UnityEngine;

namespace EventFramework
{
    public sealed class ModInfo
    {
        // 对应的 mod 对象
        public KMod.Mod mod;
        // mod 标题（loadbefore / loadafter 按它匹配）
        public string title;
        // mod 前缀（如本 Mod 的 ModFoods:）
        public string prefix;
        // mod 根目录绝对路径
        public string modRoot;
        // 是否含 yaml 目录
        public bool hasLoadYaml;
        // 是否含补丁文件
        public bool hasPatches;
        public List<string> loadBefore = new List<string>();
        public List<string> loadAfter = new List<string>();
        // 发现顺序号（拓扑排序的稳定依据）
        public int discoveryIndex;

        // 判断这条记录的标题是否等于 t（去首尾空格后逐字符比较）
        public bool HasTitle(string t)
        {
            return !string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(title)
                   && string.Equals(title, t.Trim(), StringComparison.Ordinal);
        }
    }

    public sealed class LoadOrderFile
    {
        // 要求排在这些 mod 之前
        public List<string> loadbefore { get; set; } = null;
        // 要求排在这些 mod 之后
        public List<string> loadafter { get; set; } = null;
    }

    public static class LoadOrderResolver
    {
        // 本文件日志前缀
        private const string LOG_TAG = "[MoreFood/Load]";
        // 探针日志前缀
        private const string PROBE_TAG = "[MoreFood/Load][Probe]";

        // 认作「补丁目录」的目录名
        public static readonly string[] PATCH_DIR_NAMES = { "reward", "Reward", "EventYaml", "event", "Event" };

        // 解析结果缓存
        private static List<ModInfo> _cached;

        // 「所有 Mod 的内容都已加载完毕」标志。
        // 为什么需要：OnLoad 阶段（mod 自己的 DLL 正在被 DLLLoader 加载）各 Mod 的
        // `loaded_content` 还是 0，此时 Resolve() 会把所有 Mod 判成「未勾选启用」。
        // 若把这份结果写进 _cached，整局游戏的 yaml 扫描都会命中 0 个文件。
        // 所以：加载完成之前一律不写缓存（每次现算），完成时才允许缓存。
        private static bool _loadComplete;

        // 由框架在 OnAllModsLoaded（全部 Mod 内容已就绪）调用一次
        public static void MarkLoadComplete()
        {
            _loadComplete = true;
            // 丢掉加载期可能写入的任何结果，强制下一次按真实状态重新解析
            _cached = null;
        }

        public static List<ModInfo> Resolve()
        {
            if (_cached != null) return _cached;

            var infos = new List<ModInfo>();

            // 1) 取全量 mod 列表（含已安装但未启用的）
            List<KMod.Mod> mods = null;
            try
            {
                var g = Global.Instance;
                if (g != null && g.modManager != null) mods = g.modManager.mods;
            }
            catch (Exception e)
            {
                DistributionLog.Exc("⚠ 取 mods 列表异常（本次只能按默认顺序加载）", e);
            }

            if (mods == null || mods.Count == 0)
            {
                Debug.LogWarning(LOG_TAG + " ⚠ 没有可用 Mod 列表");
                if (_loadComplete) _cached = infos;   // 加载完成前多半是 modManager 还没建好，别缓存
                return infos;
            }

            // 2) 只保留【真正启用】的 mod —— 与原版加载机制对齐
            var enabledMods = new List<KMod.Mod>(mods.Count);
            var skipped = new List<string>();

            for (int i = 0; i < mods.Count; i++)
            {
                var m = mods[i];
                if (m == null) continue;

                // ---- 探针：把每个 mod 的状态摊进日志 ----
                ProbeMod(m);

                // ---- 过滤 ----
                string reason;
                if (IsModEnabled(m, out reason))
                {
                    enabledMods.Add(m);
                }
                else
                {
                    skipped.Add(SafeTitle(m) + "(" + reason + ")");
                }
            }

            Debug.Log(LOG_TAG + " ★ 已启用 " + enabledMods.Count + " / 全量 " + mods.Count
                      + " 个 mod" + (skipped.Count > 0 ? "；跳过 " + skipped.Count + " 个" : ""));

            if (skipped.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(LOG_TAG).Append(" ⏭ 跳过明细：");
                for (int i = 0; i < skipped.Count; i++)
                {
                    if (i > 0) sb.Append(" | ");
                    sb.Append(skipped[i]);
                }
                Debug.Log(sb.ToString());
            }

            if (enabledMods.Count == 0)
            {
                if (!_loadComplete)
                {
                    // 加载期必然走到这里：各 Mod 的 loaded_content 还没置位
                    Debug.Log(LOG_TAG + " 加载尚未完成，本次判定「无启用 mod」不作数，结果不缓存");
                }
                else
                {
                    Debug.LogWarning(LOG_TAG + " ⚠ 过滤后没有已启用的 mod（含本 Mod 本身？）"
                                              + "—— 检查 mod 选择界面是否全部未勾选");
                    _cached = infos;
                }
                return infos;
            }

            // 3) 只对已启用的 mod 分配 ID 前缀
            var prefixes = ModPrefixResolver.ResolvePrefixes(enabledMods);

            // 4) 逐个处理已启用的 mod
            for (int i = 0; i < enabledMods.Count; i++)
            {
                var mod = enabledMods[i];
                if (mod == null) continue;

                string prefix;
                prefixes.TryGetValue(mod, out prefix);

                var info = new ModInfo();
                info.mod = mod;
                info.discoveryIndex = i;                        // 现在 i 是"启用列表里的序号"
                info.title = ModPrefixResolver.ResolveModTitle(mod);
                info.prefix = prefix;
                info.modRoot = GetContentPath(mod);

                if (string.IsNullOrEmpty(info.modRoot))
                {
                    Debug.LogWarning(LOG_TAG + " ⚠ '" + (info.title ?? "?") + "' 的 ContentPath 不可用 → 跳过");
                    continue;
                }

                ScanLoadYaml(info);
                info.hasPatches = !info.hasLoadYaml && ScanHasPatches(info);

                infos.Add(info);
            }

            SortStable(infos);
            LogResolved(infos);

            // 加载完成前不缓存：此时 loaded_content 还没置位，结果不作数
            if (_loadComplete)
            {
                _cached = infos;
            }
            else
            {
                Debug.Log(LOG_TAG + " 解析发生在 Mod 加载完成之前 ⇒ 本次结果不缓存（等 OnAllModsLoaded 后重算）");
            }
            return infos;
        }

        public static void ClearCache()
        {
            _cached = null;
            ModPrefixResolver.ClearCache();
        }

        // 核心判据：该 mod 是否应当被框架处理
        private static bool IsModEnabled(KMod.Mod m, out string reason)
        {
            reason = null;
            if (m == null) { reason = "mod 为 null"; return false; }

            // ---- ① 核心：这次游戏会话加载了内容 ----
            try
            {
                if (m.loaded_content == (KMod.Content)0)
                {
                    reason = "未加载任何内容（未勾选启用）";
                    return false;
                }
            }
            catch (Exception e)
            {
                DistributionLog.Exc("⚠ 读取 loaded_content 异常，该 Mod 视为未加载", e);
                reason = "读取 loaded_content 异常: " + e.Message;
                return false;
            }

            // ---- ② 磁盘状态：NotInstalled 一定跳过 ----
            try
            {
                if (m.status == KMod.Mod.Status.NotInstalled)
                {
                    reason = "status=NotInstalled（磁盘上没有）";
                    return false;
                }
            }
            catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ }

            // ---- ③ DLC 维度：只在 enabledForDlc 有明确内容时才检查 ----
            try
            {
                if (m.enabledForDlc != null && m.enabledForDlc.Count > 0)
                {
                    if (!m.IsEnabledForActiveDlc())
                    {
                        reason = "当前 DLC 未启用（enabledForDlc=["
                                 + string.Join(",", m.enabledForDlc.ToArray()) + "]）";
                        return false;
                    }
                }
            }
            catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ }

            return true;
        }

        // 探针：把每个 mod 的关键字段打进日志
        private static void ProbeMod(KMod.Mod m)
        {
            if (m == null) return;
            try
            {
                string title = SafeTitle(m);

                string path = "<异常>";
                try { path = m.ContentPath ?? "<null>"; }
                catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ }

                string dlcList = "<异常>";
                try
                {
                    dlcList = (m.enabledForDlc == null)
                        ? "<null>"
                        : (m.enabledForDlc.Count == 0 ? "[]" : "[" + string.Join(",", m.enabledForDlc.ToArray()) + "]");
                }
                catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ }

                string statusStr;
                try { statusStr = m.status.ToString(); }
                catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ statusStr = "?"; }

                string loadedStr = "<异常>";
                try { loadedStr = m.loaded_content.ToString(); }
                catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ }

                string availStr = "<异常>";
                try { availStr = m.available_content.ToString(); }
                catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ }

                bool isActive = false, dlcEnabled = false, isLocal = false, hasContent = false;
                try { isActive = m.IsActive(); } catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ }
                try { dlcEnabled = m.IsEnabledForActiveDlc(); } catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ }
                try { isLocal = m.IsLocal; } catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ }
                try { hasContent = m.HasContent(); } catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ }

                Debug.Log(string.Format(
                    "{0} title='{1}' enabled={2} status={3} isActive={4} loaded={5} available={6} " +
                    "dlcEnabled={7} isLocal={8} hasContent={9} dlcList={10} path='{11}'",
                    PROBE_TAG, title, m.enabled, statusStr, isActive, loadedStr, availStr,
                    dlcEnabled, isLocal, hasContent, dlcList, path));
            }
            catch (Exception e)
            {
                DistributionLog.Exc("探针打印 mod 状态异常（只影响诊断输出）", e);
            }
        }

        private static string SafeTitle(KMod.Mod m)
        {
            if (m == null) return "<null>";
            try { return m.title ?? "<null>"; }
            catch { /* X8 预期：KMod 属性探测，读不到按缺省处理 */ return "<title 异常>"; }
        }

        // 以下为原有实现，未改动

        private static string GetContentPath(KMod.Mod mod)
        {
            try
            {
                string p = mod.ContentPath;
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p;
                return p;
            }
            catch (Exception e)
            {
                DistributionLog.Exc("⚠ 取 ContentPath 异常，该 Mod 的内容目录不可用", e);
                return null;
            }
        }

        private static void ScanLoadYaml(ModInfo info)
        {
            string path = Path.Combine(info.modRoot, "Load.yaml");
            if (!File.Exists(path)) return;

            info.hasLoadYaml = true;
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                DistributionLog.Exc("⚠ 读取 Load.yaml 失败 " + path + "（忽略该 Mod 的顺序声明）", e);
                return;
            }

            var parsed = YamlLoad.Parse<LoadOrderFile>(text, info.title + "/Load.yaml", "load");
            if (parsed == null)
            {
                return;
            }
            if (parsed.loadbefore != null) info.loadBefore.AddRange(parsed.loadbefore);
            if (parsed.loadafter != null) info.loadAfter.AddRange(parsed.loadafter);
        }

        private static bool ScanHasPatches(ModInfo info)
        {
            for (int d = 0; d < PATCH_DIR_NAMES.Length; d++)
            {
                string dir = Path.Combine(info.modRoot, PATCH_DIR_NAMES[d]);
                if (!Directory.Exists(dir)) continue;

                string[] files;
                try { files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories); }
                catch (Exception e)
                {
                    DistributionLog.Exc("⚠ 枚举补丁目录失败 " + dir + "（该目录的补丁不参与判定）", e);
                    ContentDiag.File("event", dir, "目录枚举失败：" + e.Message, "检查目录权限或是否被占用");
                    continue;
                }

                for (int i = 0; i < files.Length; i++)
                {
                    string ext = Path.GetExtension(files[i]);
                    if (!ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
                     && !ext.Equals(".yml", StringComparison.OrdinalIgnoreCase)) continue;

                    string text;
                    try { text = File.ReadAllText(files[i]); }
                    catch (Exception e)
                    {
                        // 这份读不了只影响"该 Mod 是否声明了补丁"的判定，不阻断加载
                        DistributionLog.Exc("⚠ 读取补丁文件失败 " + files[i] + "（该文件不参与顺序判定）", e);
                        ContentDiag.File("event", info.title + "/" + Path.GetFileName(files[i]),
                            "读取失败：" + e.Message, "检查文件是否被占用、编码是否为 UTF-8");
                        continue;
                    }

                    if (TextDeclaresOverride(text)) return true;
                }
            }
            return false;
        }

        private static bool TextDeclaresOverride(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0) continue;

                char first = line[0];
                if (first == ' ' || first == '\t' || first == '#') continue;

                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                if (!line.Substring(0, colon).Trim().Equals("mode", StringComparison.Ordinal)) continue;

                string value = line.Substring(colon + 1).Trim();
                int hash = value.IndexOf('#');
                if (hash >= 0) value = value.Substring(0, hash).Trim();
                if (value.Equals("override", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static int Tier(ModInfo info)
        {
            if (info.hasLoadYaml) return 2;
            return info.hasPatches ? 1 : 0;
        }

        private static void SortStable(List<ModInfo> infos)
        {
            var ordered = new List<ModInfo>(infos);
            ordered.Sort(delegate (ModInfo a, ModInfo b)
            {
                int ta = Tier(a), tb = Tier(b);
                if (ta != tb) return ta.CompareTo(tb);
                return a.discoveryIndex.CompareTo(b.discoveryIndex);
            });

            int n = ordered.Count;
            var edges = new List<int>[n];
            var indeg = new int[n];
            for (int i = 0; i < n; i++) edges[i] = new List<int>();

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    if (Tier(ordered[i]) < Tier(ordered[j])) AddEdge(edges, indeg, i, j);
                }
            }

            for (int i = 0; i < n; i++)
            {
                var info = ordered[i];
                for (int k = 0; k < info.loadBefore.Count; k++) AddTitleEdge(ordered, edges, indeg, i, info.loadBefore[k], true, info.title);
                for (int k = 0; k < info.loadAfter.Count; k++) AddTitleEdge(ordered, edges, indeg, i, info.loadAfter[k], false, info.title);
            }

            var result = new List<ModInfo>(n);
            var done = new bool[n];
            int emitted = 0;
            bool cycleWarned = false;

            while (emitted < n)
            {
                int pick = -1;
                for (int i = 0; i < n; i++)
                {
                    if (done[i] || indeg[i] != 0) continue;
                    if (pick < 0 || ordered[i].discoveryIndex < ordered[pick].discoveryIndex) pick = i;
                }

                if (pick < 0)
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (done[i]) continue;
                        if (pick < 0 || ordered[i].discoveryIndex < ordered[pick].discoveryIndex) pick = i;
                    }
                    if (!cycleWarned)
                    {
                        cycleWarned = true;
                        Debug.LogWarning(LOG_TAG + " ⚠ Load.yaml 的顺序声明成环 → 已断开，按 mod 列表顺序兜底");
                    }
                }

                done[pick] = true;
                result.Add(ordered[pick]);
                emitted++;
                for (int k = 0; k < edges[pick].Count; k++)
                {
                    int to = edges[pick][k];
                    if (indeg[to] > 0) indeg[to]--;
                }
            }

            infos.Clear();
            infos.AddRange(result);
        }

        private static void AddEdge(List<int>[] edges, int[] indeg, int from, int to)
        {
            for (int k = 0; k < edges[from].Count; k++)
                if (edges[from][k] == to) return;
            edges[from].Add(to);
            indeg[to]++;
        }

        private static void AddTitleEdge(List<ModInfo> ordered,
                                        List<int>[] edges, int[] indeg, int fromIdx, string otherTitle,
                                        bool thisBefore, string selfTitle)
        {
            if (string.IsNullOrEmpty(otherTitle)) return;

            int other = -1;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].HasTitle(otherTitle)) { other = i; break; }
            }

            if (other < 0)
            {
                Debug.LogWarning(LOG_TAG + " ⚠ '" + selfTitle + "' 的 Load.yaml 引用了未知 mod.title '"
                                 + otherTitle.Trim() + "' → 该条顺序声明被忽略");
                return;
            }
            if (other == fromIdx) return;

            if (thisBefore) AddEdge(edges, indeg, fromIdx, other);
            else AddEdge(edges, indeg, other, fromIdx);
        }

        private static void LogResolved(List<ModInfo> infos)
        {
            var a = new List<string>();
            var b = new List<string>();
            var c = new List<string>();

            for (int i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                int t = Tier(info);
                if (t == 0) a.Add(info.title);
                else if (t == 1) b.Add(info.title);
                else c.Add(info.title);
            }

            var sb = new System.Text.StringBuilder();
            sb.Append(LOG_TAG).Append(" 加载顺序（共 ").Append(infos.Count).Append(" 个 mod）：");
            sb.Append("\n    [A 无补丁] ").Append(a.Count == 0 ? "-" : string.Join(", ", a.ToArray()));
            sb.Append("\n    [B 有补丁] ").Append(b.Count == 0 ? "-" : string.Join(", ", b.ToArray()));
            sb.Append("\n    [C Load.yaml] ").Append(c.Count == 0 ? "-" : string.Join(", ", c.ToArray()));
            Debug.Log(sb.ToString());

            if (b.Count < 2) return;

            for (int i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                if (info.hasPatches && !info.hasLoadYaml)
                {
                    Debug.Log(LOG_TAG + " ⚠ mod '" + info.title + "' 未提供 Load.yaml，但含有补丁文件"
                              + " → 默认排在无 Load.yaml 的 mod 之后、有 Load.yaml 的 mod 之前"
                              + " → 如需精确控制顺序，请添加 Load.yaml");
                }
            }
        }
    }
}
