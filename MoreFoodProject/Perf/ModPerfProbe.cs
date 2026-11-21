// ModPerfProbe.cs — 性能分析探针：按 Mod 归因 Harmony 补丁耗时，并记录鼠标点击与卡顿帧
// 设计要点：只对「被 Harmony 编译进其它方法 IL 的补丁方法」二次插桩，耗时按补丁代码所在程序集归属到 Mod；
// 卸载时 UnpatchAll 并清空全部统计，未勾选时进程内没有任何本探针的钩子。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using KMod;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MoreFood_KFC
{
    internal static class ModPerfProbe
    {
        private const string HarmonyId = "MoreFood_KFC.Perf";
        private const float ClickWindowSeconds = 0.5f;      // 点击后多少秒内的卡顿算「点击引起」
        private const float DumpIntervalSeconds = 10f;
        private const float SelfCheckDelaySeconds = 3f;
        private const float SlowClickMs = 50f;
        private const float StallMs = 50f;
        private const float SuspendMs = 5000f;      // 超过此值视为挂起/加载（切出窗口、读档阻塞），不算卡顿帧
        private const int MaxStalls = 40;
        private const int TopPerStall = 8;

        private static readonly Harmony probe = new Harmony(HarmonyId);
        private static bool active;

        // 一个被计时的目标方法（被 Harmony 打过补丁的原方法）一个槽
        private sealed class Slot
        {
            internal MethodBase method;
            internal string modName;
            internal string owners;
            internal string[] ownerMods;
            internal long calls;
            internal long ticks;
            internal long maxTicks;
        }

        private static Slot[] slots = new Slot[0];
        private static readonly Dictionary<MethodBase, int> slotOf = new Dictionary<MethodBase, int>();
        private static readonly List<string> bindFailed = new List<string>();
        private static readonly List<string> skippedGeneric = new List<string>();
        private static readonly List<string> uncacheable = new List<string>();

        [ThreadStatic] private static List<Frame> frames;

        private struct Frame
        {
            internal int slot;
            internal long ts;
        }

        // Mod 识别：目录 → 标题
        private static readonly Dictionary<string, string> dirToMod = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly List<string> modSummary = new List<string>();
        private static readonly Dictionary<Assembly, string> modOfAssembly = new Dictionary<Assembly, string>();

        // 帧 / 点击 / 卡顿
        private static long[] lastSnapshot = new long[0];
        private static float installAt;
        private static float nextDumpAt;
        private static float selfCheckAt;
        private static bool selfChecked;
        private static float modMapRetryAt;
        private static long totalCalls;

        private static long clickStartTs;
        private static float clickWindowUntil;
        private static string clickTarget = "";
        private static int clickCount;
        private static int slowClickCount;
        private static int suspendCount;
        private static float worstClickMs;

        private sealed class Stall
        {
            internal float at;
            internal float ms;
            internal bool fromClick;
            internal string where;
            internal int[] idx;
            internal long[] ticks;
        }

        private static readonly List<Stall> stalls = new List<Stall>();

        // 反射缓存
        private static MethodInfo miPrefix;
        private static MethodInfo miPostfix;
        private static MethodInfo miFrame;
        private static MethodInfo miClickStart;
        private static MethodInfo miClickEnd;

        // 入口：设置页保存后立即生效
        internal static void Apply(bool on)
        {
            if (on)
            {
                if (active)
                {
                    // 已开启：不重复插桩（重新扫描会卡一下），只补建可能缺失的 Mod 映射
                    TryBuildModMap();
                    return;
                }
                Install();
            }
            else if (active)
            {
                Uninstall();
            }
        }

        // 入口：读档完成后按 config.json 的开关决定是否常驻（此时各 Mod 的补丁已全部就位；关闭时一条钩子都不装）
        internal static void RefreshFromOptions()
        {
            try
            {
                ModOptions.Reload();
                bool on = ModOptions.Current.PerfAnalysis;
                MFLog.Info(LogCat.General, "[Perf] 进游戏后应用开关：PerfAnalysis=" + on);
                Apply(on);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Perf] 应用性能分析开关失败（按关闭处理）");
            }
        }

        internal static bool Active { get { return active; } }

        private static void Install()
        {
            active = true;
            ClearStats();
            dirToMod.Clear();
            modOfAssembly.Clear();
            TryBuildModMap();
            BindMethods();
            HookFrame();
            HookClick();
            installAt = UnityEngine.Time.unscaledTime;
            nextDumpAt = installAt + DumpIntervalSeconds;
            selfCheckAt = installAt + SelfCheckDelaySeconds;
            MFLog.Info(LogCat.General, "[Perf] 性能分析已开启：插桩 " + slots.Length + " 个补丁方法（失败 "
                + bindFailed.Count + "），已识别 " + dirToMod.Count + " 个 Mod 目录");
        }

        private static void Uninstall()
        {
            try
            {
                probe.UnpatchAll(HarmonyId);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Perf] 卸载探针补丁失败（可能有残留钩子）");
            }
            active = false;
            ClearStats();
            MFLog.Info(LogCat.General, "[Perf] 性能分析已关闭，探针钩子已卸载");
        }

        private static void ClearStats()
        {
            slots = new Slot[0];
            slotOf.Clear();
            bindFailed.Clear();
            skippedGeneric.Clear();
            uncacheable.Clear();
            frames = null;
            lastSnapshot = new long[0];
            stalls.Clear();
            totalCalls = 0;
            clickCount = 0;
            slowClickCount = 0;
            worstClickMs = 0f;
            clickWindowUntil = 0f;
            clickTarget = "";
            selfChecked = false;
            modMapRetryAt = 0f;
        }

        // ===== Mod 识别 =====

        // 启动早期 modManager 可能还没填充：拿不到目录就先空着，帧回调里定时补建
        private static void TryBuildModMap()
        {
            if (dirToMod.Count > 0)
            {
                return;
            }
            BuildModMap();
            if (dirToMod.Count == 0)
            {
                return;
            }
            modOfAssembly.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                try
                {
                    Patches info = Harmony.GetPatchInfo(slots[i].method);
                    if (info != null)
                    {
                        CollectOwners(info, slots[i]);
                    }
                }
                catch (Exception)
                {
                }
            }
            MFLog.Info(LogCat.General, "[Perf] Mod 目录映射已补建：识别 " + dirToMod.Count + " 个目录");
        }

        private static void BuildModMap()
        {            dirToMod.Clear();
            modSummary.Clear();
            modOfAssembly.Clear();
            List<Mod> mods = null;
            try
            {
                Global g = Global.Instance;
                if (g != null && g.modManager != null)
                {
                    mods = g.modManager.mods;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Perf] 取 Mod 列表失败 → 只能按程序集名显示");
            }
            if (mods == null)
            {
                return;
            }
            for (int i = 0; i < mods.Count; i++)
            {
                Mod mod = mods[i];
                if (mod == null)
                {
                    continue;
                }
                string title = null;
                string root = null;
                string sid = null;
                string status = "?";
                int content = 0;
                try { title = mod.title; } catch (Exception) { }
                try { root = mod.ContentPath; } catch (Exception) { }
                try { sid = mod.staticID; } catch (Exception) { }
                try { status = mod.status.ToString(); } catch (Exception) { }
                try { content = (int)mod.loaded_content; } catch (Exception) { }
                if (string.IsNullOrEmpty(title))
                {
                    title = string.IsNullOrEmpty(sid) ? ("Mod#" + i) : sid;
                }
                if (!string.IsNullOrEmpty(root))
                {
                    string key = NormalizeDir(root);
                    if (key.Length > 0 && !dirToMod.ContainsKey(key))
                    {
                        dirToMod[key] = title;
                    }
                }
                modSummary.Add(title + "（" + status + "，内容=" + content + "）");
            }
        }

        private static string NormalizeDir(string dir)
        {
            if (string.IsNullOrEmpty(dir))
            {
                return "";
            }
            try
            {
                return Path.GetFullPath(dir).TrimEnd('\\', '/').ToLowerInvariant();
            }
            catch (Exception)
            {
                return "";
            }
        }

        // 程序集 → Mod 标题：用程序集文件所在目录与 Mod 根目录做最长前缀匹配
        private static string ResolveMod(Assembly asm)
        {
            if (asm == null)
            {
                return "未识别";
            }
            string cached;
            if (modOfAssembly.TryGetValue(asm, out cached))
            {
                return cached;
            }
            string name = null;
            string loc = "";
            try { loc = asm.Location; } catch (Exception) { loc = ""; }
            if (string.IsNullOrEmpty(loc))
            {
                name = "未识别（内存程序集 " + asm.GetName().Name + "）";
            }
            else
            {
                string dir = NormalizeDir(Path.GetDirectoryName(loc));
                string best = null;
                foreach (KeyValuePair<string, string> kv in dirToMod)
                {
                    if (dir.Length >= kv.Key.Length && dir.StartsWith(kv.Key, StringComparison.Ordinal))
                    {
                        if (best == null || kv.Key.Length > best.Length)
                        {
                            best = kv.Key;
                        }
                    }
                }
                name = best != null ? dirToMod[best] : ("未识别（" + asm.GetName().Name + "）");
            }
            modOfAssembly[asm] = name;
            return name;
        }

        // ===== 插桩 =====

        private static void BindMethods()
        {
            miPrefix = typeof(ModPerfProbe).GetMethod("PerfStart", BindingFlags.Static | BindingFlags.NonPublic);
            miPostfix = typeof(ModPerfProbe).GetMethod("PerfEnd", BindingFlags.Static | BindingFlags.NonPublic);
            miFrame = typeof(ModPerfProbe).GetMethod("OnFrame", BindingFlags.Static | BindingFlags.NonPublic);
            miClickStart = typeof(ModPerfProbe).GetMethod("OnClickStart", BindingFlags.Static | BindingFlags.NonPublic);
            miClickEnd = typeof(ModPerfProbe).GetMethod("OnClickEnd", BindingFlags.Static | BindingFlags.NonPublic);

            // 以「被补丁的目标方法」为单位计时：Mono 下二次插桩补丁方法会 100% 报 IL Compile Error，故按目标方法归因
            List<Slot> list = new List<Slot>(256);
            int targetCount = 0;
            try
            {
                foreach (MethodBase target in Harmony.GetAllPatchedMethods())
                {
                    if (target == null || target.DeclaringType == typeof(ModPerfProbe) || IsOwnHookTarget(target))
                    {
                        continue;
                    }
                    // 泛型方法 / 泛型类型上的方法：Harmony 的 wrapper 用 GetMethodFromHandle 解析 __originalMethod 会抛
                    // ArgumentException（Cannot resolve method ... declaring type is generic），运行时炸游戏，必须整体跳过
                    if (target.IsGenericMethod || (target.DeclaringType != null && target.DeclaringType.IsGenericType))
                    {
                        if (skippedGeneric.Count < 40)
                        {
                            skippedGeneric.Add(Describe(target));
                        }
                        continue;
                    }
                    Patches info = Harmony.GetPatchInfo(target);
                    if (info == null)
                    {
                        continue;
                    }
                    targetCount++;
                    Slot s = new Slot();
                    s.method = target;
                    CollectOwners(info, s);
                    MethodInfo asInfo = target as MethodInfo;
                    if (asInfo != null && asInfo.ReturnType == typeof(System.Collections.IEnumerator))
                    {
                        uncacheable.Add(Describe(target) + "（协程，只计到创建 enumerator）");
                    }
                    list.Add(s);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Perf] 枚举已打补丁的方法失败（统计不完整）");
            }

            slots = list.ToArray();
            for (int i = 0; i < slots.Length; i++)
            {
                slotOf[slots[i].method] = i;
            }

            HarmonyMethod pre = new HarmonyMethod(miPrefix);
            HarmonyMethod post = new HarmonyMethod(miPostfix);
            for (int i = 0; i < slots.Length; i++)
            {
                try
                {
                    // 只用 prefix + postfix：本作 Mono 下 Harmony 的 finalizer 一律报 IL Compile Error（异常块不被支持）
                    probe.Patch(slots[i].method, pre, post, null, null);
                }
                catch (Exception e)
                {
                    bindFailed.Add(Describe(slots[i].method) + "：" + OneLine(e));
                    slotOf.Remove(slots[i].method);
                }
            }
            MFLog.Info(LogCat.General, "[Perf] 已扫描 " + targetCount + " 个被补丁方法，计时 "
                + (slots.Length - bindFailed.Count) + " 个，插桩失败 " + bindFailed.Count + " 个，跳过泛型 " + skippedGeneric.Count + " 个");
        }

        // 帧 / 点击统计自己挂的钩子目标，避免同一次调用被重复计时
        private static bool IsOwnHookTarget(MethodBase target)
        {
            try
            {
                return (target.DeclaringType == typeof(Game) && target.Name == "Update")
                    || (target.DeclaringType == typeof(KButton) && target.Name == "OnPointerClick");
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string OneLine(Exception e)
        {
            if (e == null)
            {
                return "?";
            }
            string text = e.GetType().Name + ": " + e.Message;
            if (e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message))
            {
                text += " / " + e.InnerException.Message;
            }
            return text.Length > 220 ? text.Substring(0, 220) : text;
        }

        // 一个目标方法上的补丁归属：owner 串（Harmony id）+ owner 所在 Mod（按补丁方法所属程序集判定）
        private static void CollectOwners(Patches info, Slot s)
        {
            List<string> owners = new List<string>(4);
            List<string> mods = new List<string>(4);
            AddOwners(info.Prefixes, owners, mods);
            AddOwners(info.Postfixes, owners, mods);
            AddOwners(info.Finalizers, owners, mods);
            s.owners = owners.Count == 0 ? "?" : string.Join(",", owners.ToArray());
            s.ownerMods = mods.ToArray();
            s.modName = mods.Count == 0 ? "未识别"
                : (mods.Count == 1 ? mods[0] : "共享（" + string.Join(" + ", mods.ToArray()) + "）");
        }

        private static void AddOwners(IList<Patch> patches, List<string> owners, List<string> mods)
        {
            if (patches == null)
            {
                return;
            }
            for (int i = 0; i < patches.Count; i++)
            {
                Patch p = patches[i];
                if (p == null)
                {
                    continue;
                }
                string owner = p.owner ?? "?";
                if (!owners.Contains(owner))
                {
                    owners.Add(owner);
                }
                MethodInfo pm = null;
                try { pm = p.PatchMethod; } catch (Exception) { }
                string mod = null;
                try
                {
                    if (pm != null)
                    {
                        mod = ResolveMod(pm.DeclaringType != null ? pm.DeclaringType.Assembly : pm.Module.Assembly);
                    }
                }
                catch (Exception)
                {
                    mod = null;
                }
                if (!string.IsNullOrEmpty(mod) && !mods.Contains(mod))
                {
                    mods.Add(mod);
                }
            }
        }

        private static string Describe(MethodBase m)
        {
            if (m == null)
            {
                return "?";
            }
            return (m.DeclaringType != null ? m.DeclaringType.FullName : "?") + "." + m.Name;
        }

        // 目标方法执行前后计时：prefix 压栈、postfix 出栈；异常时 postfix 不执行，栈深超阈值整栈丢弃兜底
        private static void PerfStart(MethodBase __originalMethod)
        {
            try
            {
                int idx;
                if (__originalMethod == null || !slotOf.TryGetValue(__originalMethod, out idx))
                {
                    return;
                }
                List<Frame> st = frames;
                if (st == null)
                {
                    st = new List<Frame>(16);
                    frames = st;
                }
                else if (st.Count > 64)
                {
                    st.Clear();
                }
                Frame f;
                f.slot = idx;
                f.ts = Stopwatch.GetTimestamp();
                st.Add(f);
            }
            catch (Exception)
            {
                // expected: 探针统计失败绝不能冒到游戏逻辑
            }
        }

        private static void PerfEnd()
        {
            try
            {
                List<Frame> st = frames;
                if (st == null || st.Count == 0)
                {
                    return;
                }
                int last = st.Count - 1;
                Frame f = st[last];
                st.RemoveAt(last);
                if (f.slot < 0 || f.slot >= slots.Length)
                {
                    return;   // 卸载后仍有在途调用：丢弃而不是越界
                }
                long dt = Stopwatch.GetTimestamp() - f.ts;
                Slot s = slots[f.slot];
                s.calls++;
                s.ticks += dt;
                if (dt > s.maxTicks)
                {
                    s.maxTicks = dt;
                }
                totalCalls++;
            }
            catch (Exception)
            {
                // expected: 探针统计失败绝不能冒到游戏逻辑
            }
        }

        private static void HookFrame()
        {
            MethodInfo update = AccessTools.Method(typeof(Game), "Update");
            if (update == null || miFrame == null)
            {
                MFLog.Warn(LogCat.General, "[Perf] 找不到 Game.Update，卡顿帧统计不可用");
                return;
            }
            try
            {
                probe.Patch(update, null, new HarmonyMethod(miFrame), null, null);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Perf] 挂帧统计失败");
            }
        }

        private static void HookClick()
        {
            MethodInfo click = null;
            try
            {
                click = AccessTools.Method(typeof(KButton), "OnPointerClick", new Type[] { typeof(PointerEventData) });
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Perf] 查找 KButton.OnPointerClick 失败");
            }
            if (click == null || miClickStart == null || miClickEnd == null)
            {
                MFLog.Warn(LogCat.General, "[Perf] KButton.OnPointerClick 不可用，点击统计关闭");
                return;
            }
            try
            {
                probe.Patch(click, new HarmonyMethod(miClickStart), new HarmonyMethod(miClickEnd), null, null);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Perf] 挂点击统计失败");
            }
        }

        private static void OnClickStart(KButton __instance)
        {
            clickStartTs = Stopwatch.GetTimestamp();
            try
            {
                clickTarget = __instance != null && __instance.gameObject != null ? __instance.gameObject.name : "?";
            }
            catch (Exception)
            {
                clickTarget = "?";
            }
        }

        private static void OnClickEnd(KButton __instance)
        {
            if (clickStartTs <= 0)
            {
                return;   // prefix 没跑到（极端情况），不做除法也不记样本
            }
            long dt = Stopwatch.GetTimestamp() - clickStartTs;
            float ms = (float)(dt * 1000.0 / Stopwatch.Frequency);
            clickCount++;
            if (ms > SuspendMs)
            {
                return;   // 点击跨越了挂起（切窗口 / 加载），不是真实点击耗时
            }
            if (ms >= SlowClickMs)
            {
                slowClickCount++;
                if (ms > worstClickMs)
                {
                    worstClickMs = ms;
                }
            }
            clickWindowUntil = UnityEngine.Time.unscaledTime + ClickWindowSeconds;
        }

        private static void OnFrame()
        {
            try
            {
                float now = UnityEngine.Time.unscaledTime;
                float dtMs = UnityEngine.Time.unscaledDeltaTime * 1000f;
                Slot[] arr = slots;

                // Mod 列表在启动早期可能还没填充：映射为空时每 2 秒补建一次，直到拿到
                if (dirToMod.Count == 0 && now >= modMapRetryAt)
                {
                    modMapRetryAt = now + 2f;
                    TryBuildModMap();
                }

                if (lastSnapshot.Length != arr.Length)
                {
                    lastSnapshot = new long[arr.Length];
                }
                long[] deltas = null;
                if (dtMs >= StallMs && dtMs <= SuspendMs && stalls.Count < MaxStalls)
                {
                    deltas = new long[arr.Length];
                }
                else if (dtMs > SuspendMs)
                {
                    suspendCount++;   // 切出窗口 / 读档阻塞：不是卡顿，单独计数
                }
                for (int i = 0; i < arr.Length; i++)
                {
                    long cur = arr[i].ticks;
                    if (deltas != null)
                    {
                        deltas[i] = cur - lastSnapshot[i];
                    }
                    lastSnapshot[i] = cur;
                }
                if (deltas != null)
                {
                    RecordStall(now, dtMs, deltas);
                }

                if (!selfChecked && now >= selfCheckAt)
                {
                    selfChecked = true;
                    SelfCheck();
                }
                if (now >= nextDumpAt)
                {
                    nextDumpAt = now + DumpIntervalSeconds;
                    DumpReport("定时");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Perf] 帧统计异常（本帧跳过）");
            }
        }

        private static void RecordStall(float now, float dtMs, long[] deltas)
        {
            int[] topIdx = new int[TopPerStall];
            long[] topTicks = new long[TopPerStall];
            int used = 0;
            for (int i = 0; i < deltas.Length; i++)
            {
                long d = deltas[i];
                if (d <= 0)
                {
                    continue;
                }
                if (used < TopPerStall)
                {
                    topIdx[used] = i;
                    topTicks[used] = d;
                    used++;
                    continue;
                }
                int min = 0;
                for (int k = 1; k < TopPerStall; k++)
                {
                    if (topTicks[k] < topTicks[min])
                    {
                        min = k;
                    }
                }
                if (d > topTicks[min])
                {
                    topIdx[min] = i;
                    topTicks[min] = d;
                }
            }
            Stall s = new Stall();
            s.at = now;
            s.ms = dtMs;
            s.fromClick = now <= clickWindowUntil;
            s.where = clickTarget;
            s.idx = new int[used];
            s.ticks = new long[used];
            Array.Copy(topIdx, s.idx, used);
            Array.Copy(topTicks, s.ticks, used);
            stalls.Add(s);
        }

        private static void SelfCheck()
        {
            if (slots.Length == 0)
            {
                MFLog.Warn(LogCat.General, "[Perf] 没有可计时的目标方法（其它 Mod 可能未加载，或全部插桩失败）");
                return;
            }
            if (totalCalls > 0)
            {
                MFLog.Info(LogCat.General, "[Perf] 自检通过：目标方法计时生效，已累计 " + totalCalls + " 次调用");
                return;
            }
            MFLog.Warn(LogCat.General, "[Perf] 自检未通过：3 秒内 0 次调用（可能与目标方法都恰好未被调用，或插桩失败）");
        }

        // ===== 报告 =====

        private static void DumpReport(string reason)
        {
            StringBuilder sb = new StringBuilder(4096);
            float now = UnityEngine.Time.unscaledTime;
            float elapsed = Mathf.Max(1f, now - installAt);
            double toMs = 1000.0 / Stopwatch.Frequency;

            sb.AppendLine("== MoreFood 性能分析报告（" + reason + "）==");
            sb.AppendLine("采样时长\t" + elapsed.ToString("F1") + " 秒");
            sb.AppendLine("精度档位\t" + (totalCalls > 0 ? "按被补丁的目标方法归因到 Mod（同一方法上多 Mod 共享时单列）" : "未生效（见日志自检）"));
            sb.AppendLine("计时方法数\t" + (slots.Length - bindFailed.Count) + " / " + slots.Length + "（插桩失败 " + bindFailed.Count + "）");
            sb.AppendLine("口径\t总ms 为该目标方法被补丁后的累计耗时；父方法里调子方法时两者都计（含嵌套），各行相加可能大于墙钟");
            sb.AppendLine("探针开销\t每次被计时调用额外约 +0.3µs（两次 Stopwatch 取时 + 一次字典查找）");
            sb.AppendLine("被计时方法调用\t" + totalCalls + " 次");
            sb.AppendLine("点击\t" + clickCount + " 次（慢点 ≥" + SlowClickMs.ToString("F0") + "ms：" + slowClickCount + " 次，最慢 "
                + worstClickMs.ToString("F1") + "ms）；挂起 >" + SuspendMs.ToString("F0") + "ms " + suspendCount + " 次（已排除）");
            sb.AppendLine();

            Dictionary<string, long[]> byMod = new Dictionary<string, long[]>(StringComparer.Ordinal);
            Dictionary<string, List<string>> ownersOfMod = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < slots.Length; i++)
            {
                Slot s = slots[i];
                long[] agg;
                if (!byMod.TryGetValue(s.modName, out agg))
                {
                    agg = new long[4];   // 0=槽数 1=调用 2=ticks 3=最大ticks
                    byMod[s.modName] = agg;
                    ownersOfMod[s.modName] = new List<string>();
                }
                agg[0]++;
                agg[1] += s.calls;
                agg[2] += s.ticks;
                if (s.maxTicks > agg[3])
                {
                    agg[3] = s.maxTicks;
                }
                if (!string.IsNullOrEmpty(s.owners) && !ownersOfMod[s.modName].Contains(s.owners))
                {
                    ownersOfMod[s.modName].Add(s.owners);
                }
            }

            List<KeyValuePair<string, long[]>> ordered = new List<KeyValuePair<string, long[]>>(byMod);
            ordered.Sort(delegate (KeyValuePair<string, long[]> a, KeyValuePair<string, long[]> b)
            {
                return b.Value[2].CompareTo(a.Value[2]);
            });

            sb.AppendLine("Mod\t槽\t调用\t总ms\t占墙钟%\t平均µs\t最大ms\tHarmony owner");
            long sumTicks = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                string mod = ordered[i].Key;
                long[] v = ordered[i].Value;
                sumTicks += v[2];
                double ms = v[2] * toMs;
                double avgUs = v[1] > 0 ? (v[2] * toMs * 1000.0 / v[1]) : 0.0;
                sb.AppendLine(mod + "\t" + v[0] + "\t" + v[1] + "\t" + ms.ToString("F1") + "\t"
                    + (ms / (elapsed * 1000.0) * 100.0).ToString("F2") + "\t" + avgUs.ToString("F1") + "\t"
                    + (v[3] * toMs).ToString("F2") + "\t" + string.Join(" | ", ownersOfMod[mod].ToArray()));
            }
            double sumMs = sumTicks * toMs;
            sb.AppendLine("未归因（原版逻辑 + 渲染 + 引擎内部，不经过任何补丁）\t-\t-\t-\t"
                + Mathf.Max(0f, 100f - (float)(sumMs / (elapsed * 1000.0) * 100.0)).ToString("F2") + "\t-\t-\t-");
            sb.AppendLine();

            // 热点方法：同时刻能看到「哪个方法最贵 + 它属于谁」，卡顿帧里只有方法级线索时靠这张表定位
            List<int> order = new List<int>(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].ticks > 0)
                {
                    order.Add(i);
                }
            }
            order.Sort(delegate (int a, int b) { return slots[b].ticks.CompareTo(slots[a].ticks); });
            sb.AppendLine("热点方法（按累计耗时，前 20）");
            sb.AppendLine("方法\tMod\t调用\t总ms\t平均µs\t最大ms");
            for (int i = 0; i < order.Count && i < 20; i++)
            {
                Slot s = slots[order[i]];
                double ms = s.ticks * toMs;
                double avgUs = s.calls > 0 ? (ms * 1000.0 / s.calls) : 0.0;
                sb.AppendLine(Describe(s.method) + "\t" + s.modName + "\t" + s.calls + "\t" + ms.ToString("F1")
                    + "\t" + avgUs.ToString("F1") + "\t" + (s.maxTicks * toMs).ToString("F2"));
            }
            sb.AppendLine();

            sb.AppendLine("已识别 Mod（" + modSummary.Count + "）");
            for (int i = 0; i < modSummary.Count; i++)
            {
                sb.AppendLine("\t" + modSummary[i]);
            }
            sb.AppendLine();

            if (uncacheable.Count > 0)
            {
                sb.AppendLine("不可精确计时（协程补丁）");
                for (int i = 0; i < uncacheable.Count; i++)
                {
                    sb.AppendLine("\t" + uncacheable[i]);
                }
                sb.AppendLine();
            }

            sb.AppendLine("卡顿帧（≥" + StallMs.ToString("F0") + "ms，最多记 " + MaxStalls + " 条）");
            if (stalls.Count == 0)
            {
                sb.AppendLine("\t无");
            }
            for (int i = 0; i < stalls.Count; i++)
            {
                Stall s = stalls[i];
                sb.AppendLine("\t#" + (i + 1) + " @ " + s.at.ToString("F1") + "s  " + s.ms.ToString("F1") + "ms"
                    + (s.fromClick ? "  [点击窗口]" : "") + (s.fromClick ? "  按钮=" + s.where : ""));
                for (int k = 0; k < s.idx.Length; k++)
                {
                    Slot slot = slots[s.idx[k]];
                    sb.AppendLine("\t\t" + slot.modName + " · " + Describe(slot.method) + "  "
                        + (s.ticks[k] * toMs).ToString("F2") + "ms");
                }
            }
            sb.AppendLine();

            if (skippedGeneric.Count > 0)
            {
                sb.AppendLine("跳过（泛型方法，Mono 包装后会抛 ArgumentException，不能计时）");
                for (int i = 0; i < skippedGeneric.Count; i++)
                {
                    sb.AppendLine("\t" + skippedGeneric[i]);
                }
                sb.AppendLine();
            }

            if (bindFailed.Count > 0)
            {
                sb.AppendLine("插桩失败的补丁方法（" + bindFailed.Count + "）");
                for (int i = 0; i < bindFailed.Count && i < 40; i++)
                {
                    sb.AppendLine("\t" + bindFailed[i]);
                }
                sb.AppendLine();
            }

            string text = sb.ToString();
            try
            {
                File.WriteAllText(ReportPath(), text, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Perf] 写报告文件失败（只输出日志摘要）");
            }
            MFLog.Info(LogCat.General, "[Perf] 报告已更新（" + reason + "）：" + ReportPath()
                + " | 计时 " + slots.Length + " 方法 / 调用 " + totalCalls + " 次 / 卡顿帧 " + stalls.Count);
        }

        private static string ReportPath()
        {
            try
            {
                string dir = Path.GetDirectoryName(typeof(ModPerfProbe).Assembly.Location);
                if (!string.IsNullOrEmpty(dir))
                {
                    return Path.Combine(dir, "perf_report.txt");
                }
            }
            catch (Exception)
            {
            }
            return "perf_report.txt";
        }
    }
}
