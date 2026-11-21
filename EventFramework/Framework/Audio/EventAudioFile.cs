using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using EventFramework.Distribution.Contracts;
using UnityEngine;

namespace EventFramework
{
    public static class EventAudioFileLibrary
    {
        private static readonly Dictionary<string, string> _files =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] DIR_NAMES =
            { "sounds", "Sounds", "sound", "Sound", "audio", "Audio" };

        private static readonly string[] EXTENSIONS =
            { ".wav", ".ogg", ".mp3", ".flac", ".aiff" };

        // 是否已建索引
        private static bool _indexed;
        // 目录遍历告警只打一次
        private static bool _traversalWarned;

        public static int Count { get { return _files.Count; } }

// 确保全部音频文件已建索引（幂等）
        public static void EnsureIndexed()
        {
            if (_indexed) return;
            _indexed = true;

            try
            {
                var mods = LoadOrderResolver.Resolve();
                int usedMods = 0;

                for (int i = 0; i < mods.Count; i++)
                {
                    var info = mods[i];
                    if (info == null || string.IsNullOrEmpty(info.modRoot)) continue;

                    var dirs = CollectDirs(info.modRoot);
                    if (dirs.Count == 0) continue;

                    bool any = false;
                    for (int d = 0; d < dirs.Count; d++)
                        if (IndexDir(dirs[d])) any = true;
                    if (any) usedMods++;
                }

                var sb = new StringBuilder();
                sb.Append("[MoreFood/Event][AudioFile] 自定义音频文件索引：共 ").Append(_files.Count)
                  .Append(" 个（来自 ").Append(usedMods).Append(" 个 Mod）");

                int n = 0;
                foreach (var k in _files.Keys)
                {
                    if (n++ >= 20) { sb.Append(" ..."); break; }
                    sb.Append(" ").Append(k);
                }
                Debug.Log(sb.ToString());

                if (_files.Count == 0)
                    Debug.LogWarning("[MoreFood/Event][AudioFile] ⚠ 没索引到任何自定义音频文件 —— "
                                     + "确认文件放在 <Mod 根目录>/sounds/ 下，扩展名是 wav / ogg / mp3");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][AudioFile] 索引异常: " + e);
            }
        }

        // 是否存在（参数 name：名称）
        public static bool Exists(string name)
        {
            return Resolve(name) != null;
        }

        // 按名字解析出自定义音频文件路径（先建索引；名字为空或无匹配返回 null）
        public static string Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            EnsureIndexed();

            string k = name.Trim().Replace('\\', '/');
            if (k.Length == 0) return null;

            if (k.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                if (!_traversalWarned)
                {
                    _traversalWarned = true;
                    Debug.LogWarning("[MoreFood/Event][AudioFile] 音频名含 '..'，拒绝解析：'" + name
                                     + "'（请写相对 sounds/ 的路径，如 weapons/sword.wav）");
                }
                return null;
            }

            string path;
            if (_files.TryGetValue(k, out path)) return path;

            int slash = k.IndexOf('/');
            if (slash > 0 && IsDirName(k.Substring(0, slash)))
            {
                if (_files.TryGetValue(k.Substring(slash + 1), out path)) return path;
            }

            int last = k.LastIndexOf('/');
            if (last >= 0)
            {
                if (_files.TryGetValue(k.Substring(last + 1), out path)) return path;
            }
            return null;
        }

        // 打印自定义音频文件清单（最多 max 条），用于核对 yaml 里写的文件名
        public static void Dump(int max = 40)
        {
            EnsureIndexed();

            var sb = new StringBuilder();
            sb.Append("[MoreFood/Event][AudioFile] ── 自定义音频文件清单（共 ").Append(_files.Count).Append("）──");
            Debug.Log(sb.ToString());

            int n = 0;
            foreach (var kv in _files)
            {
                if (n++ >= max)
                {
                    Debug.Log("[MoreFood/Event][AudioFile]   ...（还有 " + (_files.Count - max) + " 个）");
                    break;
                }
                Debug.Log("[MoreFood/Event][AudioFile]   " + kv.Key + "  →  " + kv.Value);
            }
        }

        private static List<string> CollectDirs(string modRoot)
        {
            var dirs = new List<string>();

            for (int i = 0; i < DIR_NAMES.Length; i++)
            {
                string dir;
                try { dir = Path.Combine(modRoot, DIR_NAMES[i]); }
                catch (Exception e)
                {
                    // 该候选目录拼不出来就跳过，不影响其它音源目录
                    DistributionLog.ExcOnce("audio:dir:" + DIR_NAMES[i],
                        "[AudioFile] 拼接音频目录 '" + DIR_NAMES[i] + "' 失败（该目录跳过）：" + modRoot, e);
                    continue;
                }

                if (!Directory.Exists(dir)) continue;

                bool dup = false;
                for (int k = 0; k < dirs.Count; k++)
                {
                    if (string.Equals(dirs[k], dir, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                }
                if (!dup) dirs.Add(dir);
            }
            return dirs;
        }

        private static bool IndexDir(string dir)
        {
            string[] files;
            try { files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories); }
            catch (Exception e)
            {
                // 目录读不了只废这一个目录，其它目录照常索引
                DistributionLog.Exc("[AudioFile] 枚举音频目录失败（该目录下的音频不进索引）", e);
                ContentDiag.File("sounds", dir, "目录枚举失败：" + e.Message, "检查目录权限或是否被占用");
                return false;
            }

            bool any = false;
            for (int i = 0; i < files.Length; i++)
            {
                string f = files[i];
                if (!IsAudioExt(Path.GetExtension(f))) continue;

                string title = Path.GetFileName(f);
                if (string.IsNullOrEmpty(title)) continue;

                // 相对路径必然以 dir 为前缀（GetFiles 只返回该目录下的文件）
                string rel = "";
                if (f.Length > dir.Length) rel = f.Substring(dir.Length).TrimStart('\\', '/').Replace('\\', '/');

                bool added = AddKey(title, f);
                if (rel.Length > 0 && !string.Equals(rel, title, StringComparison.OrdinalIgnoreCase))
                    added |= AddKey(rel, f);
                if (added) any = true;
            }
            return any;
        }

        private static bool AddKey(string key, string path)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(path)) return false;

            string old;
            if (_files.TryGetValue(key, out old))
            {
                if (!string.Equals(old, path, StringComparison.OrdinalIgnoreCase))
                {
                    DistributionLog.WarnOnce("audio:dup:" + key,
                        "[AudioFile] 同名音频冲突：'" + key + "' → 保留先索引到的 " + old + "，忽略 " + path
                        + "（要区分请放进子目录，用相对路径引用）");
                    ContentDiag.Skip("sounds", path, "「" + key + "」", "同名音频已被先索引到的文件占用（先到者胜）",
                        "改名，或按相对路径引用 " + old);
                }
                return false;
            }

            _files[key] = path;
            return true;
        }

        private static bool IsAudioExt(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            for (int i = 0; i < EXTENSIONS.Length; i++)
            {
                if (ext.Equals(EXTENSIONS[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool IsDirName(string head)
        {
            if (string.IsNullOrEmpty(head)) return false;
            for (int i = 0; i < DIR_NAMES.Length; i++)
            {
                if (head.Equals(DIR_NAMES[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    public static class EventAudioFilePlayer
    {
        private static readonly Dictionary<string, object> _sounds =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> _soundSeconds =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private static bool _reflDone;                 // 反射初始化是否已完成
        private static bool _reflOk;                   // 反射初始化是否成功
        // 反射失败原因（诊断用）
        private static string _reflError = "未初始化";

        private static Type _rmType;                   // 反射缓存：RuntimeManager 类型
        private static PropertyInfo _piCore;              // 反射缓存：StudioSystem.CoreSystem 属性
        private static MethodInfo _miGetBus;              // 反射缓存：CoreSystem.GetBus 方法
        private static Type _coreType;                 // 反射缓存：FMOD Core System 类型
        private static Type _modeType;                 // 反射缓存：FMOD Mode 枚举
        private static Type _soundType;                // 反射缓存：FMOD Sound 类型
        private static Type _channelType;              // 反射缓存：FMOD Channel 类型
        private static Type _groupType;                // 反射缓存：FMOD ChannelGroup 类型
        private static MethodInfo _miCreateSound;         // 反射缓存：Sound.CreateSound 方法
        private static MethodInfo _miPlaySound;           // 反射缓存：Sound.Play 方法
        private static MethodInfo _miSoundRelease;        // 反射缓存：Sound.release 方法
        private static MethodInfo _miSoundGetLength;      // 反射缓存：Sound.getLength 方法
        private static MethodInfo _miSoundGetFormat;      // 反射缓存：Sound.getFormat 方法
        private static Type _timeUnitType;             // 反射缓存：FMOD TimeUnit 枚举
        private static MethodInfo _miChannelStop;          // 反射缓存：Channel.stop 方法
        private static MethodInfo _miChannelSetVolume;    // 反射缓存：Channel.setVolume 方法
        private static MethodInfo _miChannelSetPosition;  // 反射缓存：Channel.setPosition 方法
        private static MethodInfo _miChannelSetPitch;     // 反射缓存：Channel.setPitch 方法
        private static MethodInfo _miChannelSet3D;       // 反射缓存：Channel.set3DAttributes 方法
        private static MethodInfo _miChannelSet3DMinMax; // 反射缓存：Channel.set3DMinMaxDistance 方法
        private static Type _vecType;                  // 反射缓存：FMOD VECTOR 结构
        private static FieldInfo _fx;                   // 反射缓存：VECTOR.x 字段
        private static FieldInfo _fy;                   // 反射缓存：VECTOR.y 字段
        private static FieldInfo _fz;                   // 反射缓存：VECTOR.z 字段

        private static bool _busTried;                 // 是否已尝试解析 Bus
        private static object _busGroup;               // 解析到的 Bus（ChannelGroup）句柄

        public const uint BAD_RESULT = 9999u;       // 反射调用失败的哨兵值

        public static bool Available
        {
            get { return EnsureReflection(); }
        }

        public static string UnavailableReason
        {
            get { return _reflError; }
        }

        public static object Play(EventAudioTemplate t, string filePath, bool loop,
                                  out string detail, out float audioSeconds)
        {
            detail = "";
            audioSeconds = 0f;
            if (t == null || string.IsNullOrEmpty(filePath)) { detail = "参数为空"; return null; }

            object core;
            if (!TryGetCore(out core, out detail)) return null;

            bool spatial = t.spatial;
            string cacheKey = filePath + (loop ? "|L" : "") + (spatial ? "|3D" : "");
            object sound = null;

            if (!_sounds.TryGetValue(cacheKey, out sound))
            {
                try
                {
                    object mode = MakeMode(filePath, loop, spatial);
                    var args = new object[] { filePath, mode, null };
                    uint r = Result(_miCreateSound.Invoke(core, args));

                    if (r != 0u || args[2] == null)
                    {
                        detail = "createSound RESULT=" + r;
                        return null;
                    }

                    sound = args[2];

                    uint lenMs;
                    bool lenKnown;
                    detail = "createSound RESULT=0，" + DescribeSound(sound, out lenMs, out lenKnown);

                    if (lenKnown && lenMs == 0u)
                    {
                        detail += DescribeFormat(sound) + "（解码长度 0，格式可能不受支持）";
                        return null;
                    }

                    _sounds[cacheKey] = sound;
                    _soundSeconds[cacheKey] = lenKnown ? lenMs / 1000f : 0f;
                }
                catch (Exception e)
                {
                    DistributionLog.ExcOnce("audio:create:" + cacheKey, "[AudioFile] createSound 失败 → 该音源不可用", e);
                    detail = "createSound 异常: " + e.GetType().Name;
                    return null;
                }
            }
            else
            {
                detail = "复用已加载的 Sound";
            }

            float total;
            if (_soundSeconds.TryGetValue(cacheKey, out total) && total > 0f)
            {
                float played = t.start_ms > 0 ? t.start_ms / 1000f : 0f;
                audioSeconds = Mathf.Max(0f, total - played);
            }

            try
            {
                object group = GetGroup();
                if (group == null && _groupType != null) group = Activator.CreateInstance(_groupType);

                var args = new object[] { sound, group, false, null };
                uint r = Result(_miPlaySound.Invoke(core, args));
                detail += "，playSound RESULT=" + r;

                if (r != 0u || args[3] == null) return null;

                object channel = args[3];

                // ★ 音量跟随用户设置：自建 channel 不随 bus 走 → 登记后由 EventAudioVolume 实时修正
                float vol = Jitter(t.volume, t.volume_jitter);
                vol = EventAudioVolume.Register(channel, t.play_as, vol);
                if (_miChannelSetVolume != null && vol != 1f)
                    _miChannelSetVolume.Invoke(channel, new object[] { vol });

                float pitch = Jitter(t.pitch, t.pitch_jitter);
                if (_miChannelSetPitch != null && pitch != 1f)
                    _miChannelSetPitch.Invoke(channel, new object[] { pitch });

                if (t.start_ms > 0 && _miChannelSetPosition != null && _timeUnitType != null)
                {
                    uint pr = Result(_miChannelSetPosition.Invoke(channel,
                        new object[] { (uint)t.start_ms, Enum.Parse(_timeUnitType, "MS", true) }));
                    detail += "，定位 " + t.start_ms + "ms RESULT=" + pr;
                }

                if (spatial) ApplySpatial(channel, t);

                return channel;
            }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("audio:play:" + t.key, "[AudioFile] playSound 失败 → 本次不播", e);
                detail += "，playSound 异常: " + e.GetType().Name;
                return null;
            }
        }

        // 停止一个 FMOD 通道并回读字符串结果（调试用；通道为空或方法不可用会返回原因）
        public static string Stop(object channel)
        {
            if (channel == null) return "channel 为空";
            if (_miChannelStop == null) { EventAudioVolume.Forget(channel); return "stop 不可用"; }

            try { return "stop RESULT=" + Result(_miChannelStop.Invoke(channel, null)); }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][AudioFile] 停止异常: " + e);
                return "stop 异常: " + e.GetType().Name;
            }
            finally { EventAudioVolume.Forget(channel); }   // ★ 停止即注销音量跟随
        }

        // 会话切换清理：只停声 + 清缓存（release 交给 ReleaseAll）
        public static void ClearCaches()
        {
            EventAudioVolume.Clear();
            _sounds.Clear();
            _soundSeconds.Clear();
            Debug.Log("[MoreFood/Event][AudioFile] 会话清理：已停声并清空 Sound 缓存（不 release）");
        }

// 释放全部已加载音频（换档/退出时调用）
        public static void ReleaseAll()
        {
            if (_sounds.Count == 0) return;

            int ok = 0;
            try
            {
                foreach (var kv in _sounds)
                {
                    if (kv.Value == null || _miSoundRelease == null) continue;
                    if (Result(_miSoundRelease.Invoke(kv.Value, null)) == 0u) ok++;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][AudioFile] 释放 Sound 异常: " + e);
            }

            _sounds.Clear();
            _soundSeconds.Clear();
            EventAudioVolume.Clear();      // ★ 同步注销音量跟随登记（Sound 已释放）
            Debug.Log("[MoreFood/Event][AudioFile] 已释放缓存的 Sound：" + ok + " 个");
        }

        private static bool TryGetCore(out object core, out string detail)
        {
            core = null;
            detail = "";

            if (!EnsureReflection())
            {
                detail = _reflError;
                return false;
            }

            try
            {
                core = _piCore.GetValue(null);
                if (core == null) { detail = "CoreSystem 为 null"; return false; }
                return true;
            }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("audio:core", "[AudioFile] 取 FMOD CoreSystem 失败", e);
                detail = "取 CoreSystem 失败（RuntimeManager 还没起来？）: " + e.GetType().Name;
                return false;
            }
        }

        private static object GetGroup()
        {
            if (_busTried) return _busGroup;
            _busTried = true;

            try
            {
                if (_miGetBus == null) return null;

                object bus = _miGetBus.Invoke(null, new object[] { "bus:/" });
                if (bus == null) return null;

                var mg = EventMediaPlayer.FindMethod(bus.GetType(), "getChannelGroup", 1, null);
                if (mg == null)
                {
                    Debug.LogWarning("[MoreFood/Event][AudioFile] FMOD.Studio.Bus.getChannelGroup 不存在 "
                                     + "→ 退回默认总线");
                    return null;
                }

                var args = new object[] { null };
                uint r = Result(mg.Invoke(bus, args));

                if (r == 0u && args[0] != null)
                {
                    _busGroup = args[0];
                    Debug.Log("[MoreFood/Event][AudioFile] 已接入游戏主总线 bus:/ 的 ChannelGroup"
                              + "（自定义音频走游戏主音量）");
                    return _busGroup;
                }

                Debug.LogWarning("[MoreFood/Event][AudioFile] bus:/ 取 ChannelGroup 失败 RESULT=" + r
                                 + " → 退回默认总线（能响，但可能不受游戏音量控制）");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][AudioFile] 取主总线异常（退回默认总线）: " + e);
            }
            return null;
        }

        private static bool EnsureReflection()
        {
            if (_reflDone) return _reflOk;
            _reflDone = true;

            try
            {
                _rmType = FindType("FMODUnity", "FMODUnity.RuntimeManager");
                if (_rmType == null) { _reflError = "找不到 FMODUnity.RuntimeManager（FMOD 集成缺失）"; return false; }

                _piCore = _rmType.GetProperty("CoreSystem", BindingFlags.Public | BindingFlags.Static);
                if (_piCore == null) { _reflError = "RuntimeManager.CoreSystem 属性不存在"; return false; }

                _miGetBus = _rmType.GetMethod("GetBus", BindingFlags.Public | BindingFlags.Static,
                                              null, new Type[] { typeof(string) }, null);

                _coreType = _piCore.PropertyType;

                _miCreateSound = EventMediaPlayer.FindMethod(_coreType, "createSound", 3, typeof(string));
                if (_miCreateSound == null)
                {
                    _reflError = "FMOD.System.createSound(string, MODE, out Sound) 不存在";
                    return false;
                }

                _modeType = _miCreateSound.GetParameters()[1].ParameterType;

                _soundType = _miCreateSound.GetParameters()[2].ParameterType;
                if (_soundType != null && _soundType.IsByRef) _soundType = _soundType.GetElementType();

                _miPlaySound = EventMediaPlayer.FindMethod(_coreType, "playSound", 4, null);
                if (_miPlaySound == null) { _reflError = "FMOD.System.playSound 不存在"; return false; }

                _channelType = _miPlaySound.GetParameters()[3].ParameterType;
                if (_channelType != null && _channelType.IsByRef) _channelType = _channelType.GetElementType();

                _groupType = _miPlaySound.GetParameters()[1].ParameterType;
                if (_groupType != null && _groupType.IsByRef) _groupType = _groupType.GetElementType();

                _miSoundRelease = EventMediaPlayer.FindMethod(_soundType, "release", 0, null);
                _miChannelStop = EventMediaPlayer.FindMethod(_channelType, "stop", 0, null);
                _miChannelSetVolume = EventMediaPlayer.FindMethod(_channelType, "setVolume", 1, typeof(float));

                _miSoundGetLength = EventMediaPlayer.FindMethod(_soundType, "getLength", 2, null);
                if (_miSoundGetLength != null)
                    _timeUnitType = _miSoundGetLength.GetParameters()[1].ParameterType;
                _miSoundGetFormat = EventMediaPlayer.FindMethod(_soundType, "getFormat", 4, null);
                _miChannelSetPosition = EventMediaPlayer.FindMethod(_channelType, "setPosition", 2, null);
                _miChannelSetPitch = EventMediaPlayer.FindMethod(_channelType, "setPitch", 1, typeof(float));
                _miChannelSet3D = EventMediaPlayer.FindMethod(_channelType, "set3DAttributes", 2, null);
                _miChannelSet3DMinMax = EventMediaPlayer.FindMethod(_channelType, "set3DMinMaxDistance", 2, null);

                if (_miChannelSet3D != null)
                {
                    _vecType = _miChannelSet3D.GetParameters()[0].ParameterType;
                    if (_vecType != null && _vecType.IsByRef) _vecType = _vecType.GetElementType();
                    if (_vecType != null)
                    {
                        _fx = _vecType.GetField("x");
                        _fy = _vecType.GetField("y");
                        _fz = _vecType.GetField("z");
                    }
                }

                if (_miChannelStop == null)
                {
                    _reflError = "FMOD.Channel.stop 不存在（版本不符）";
                    return false;
                }

                _reflOk = true;
                return true;
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("audiofile:EnsureReflection", "[AudioFile] EnsureReflection 失败 → 已按兜底处理", e);
                _reflError = "反射初始化异常: " + e;
                return false;
            }
        }

        private static Type FindType(string assemblyName, string typeName)
        {
            try
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; i++)
                {
                    var a = asms[i];
                    if (a == null) continue;

                    string n;
                    try { n = a.GetName().Name; }
                    catch (Exception e)
                    {
                        DistributionLog.ExcOnce("audio:asm:" + a.FullName, "[AudioFile] 取程序集名失败，跳过该程序集", e);
                        continue;
                    }

                    if (!string.Equals(n, assemblyName, StringComparison.OrdinalIgnoreCase)) continue;

                    var t = a.GetType(typeName, false);
                    if (t != null) return t;
                }
            }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("audio:type:" + typeName,
                    "[AudioFile] 查找类型 " + assemblyName + "!" + typeName + " 失败 → 该音源不可用", e);
            }
            return null;
        }

        private static string DescribeSound(object sound, out uint lenMs, out bool lenKnown)
        {
            lenMs = 0u;
            lenKnown = false;

            try
            {
                if (_miSoundGetLength != null && _timeUnitType != null)
                {
                    var a = new object[] { null, Enum.Parse(_timeUnitType, "MS", true) };
                    if (Result(_miSoundGetLength.Invoke(sound, a)) == 0u && a[0] != null)
                    {
                        lenMs = Convert.ToUInt32(a[0]);
                        lenKnown = true;
                        return "解码 " + lenMs + "ms";
                    }
                }
            }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("audio:len", "[AudioFile] 读取音频长度失败 → 按未知处理", e);
            }

            return "音频信息读不到";
        }

        private static string DescribeFormat(object sound)
        {
            if (_miSoundGetFormat == null) return "";

            try
            {
                var a = new object[] { null, null, null, null };
                if (Result(_miSoundGetFormat.Invoke(sound, a)) != 0u) return "";
                return "（" + a[0] + "/" + a[1] + " " + a[2] + "ch " + a[3] + "bit）";
            }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("audio:fmt", "[AudioFile] 读取音频格式失败 → 不显示格式", e);
                return "";
            }
        }

        private static object MakeMode(string filePath, bool loop, bool spatial)
        {
            uint v = ModeFlag(spatial ? "_3D" : "_2D");
            v |= IsCompressed(filePath) ? ModeFlag("CREATECOMPRESSEDSAMPLE") : ModeFlag("CREATESAMPLE");
            v |= loop ? ModeFlag("LOOP_NORMAL") : ModeFlag("LOOP_OFF");
            return Enum.ToObject(_modeType, v);
        }

        private static void ApplySpatial(object channel, EventAudioTemplate t)
        {
            if (_miChannelSet3DMinMax != null)
            {
                try
                {
                    _miChannelSet3DMinMax.Invoke(channel,
                        new object[] { t.min_distance, t.max_distance });
                }
                catch (Exception e)
                {
                    DistributionLog.ExcOnce("audio:3dminmax", "[AudioFile] 设置 3D 衰减距离失败 → 用引擎默认", e);
                }
            }

            if (_miChannelSet3D == null || _vecType == null) return;

            try
            {
                _miChannelSet3D.Invoke(channel, new object[]
                {
                    MakeVector(EventMediaPlayer.ResolvePosition(t)),
                    MakeVector(Vector3.zero),
                });
            }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("audio:3dpos", "[AudioFile] 设置 3D 位置失败 → 该音源按 2D 播", e);
            }
        }

        private static object MakeVector(Vector3 v)
        {
            object o = Activator.CreateInstance(_vecType);
            if (_fx != null) _fx.SetValue(o, v.x);
            if (_fy != null) _fy.SetValue(o, v.y);
            if (_fz != null) _fz.SetValue(o, v.z);
            return o;
        }

        private static float Jitter(float v, float j)
        {
            if (j <= 0f) return v;
            return Mathf.Max(0f, v + UnityEngine.Random.Range(-j, j));
        }

        private static bool IsCompressed(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return false;

            return ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".flac", StringComparison.OrdinalIgnoreCase);
        }

        private static uint ModeFlag(string name)
        {
            try { return Convert.ToUInt32(Enum.Parse(_modeType, name, true)); }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("audio:mode:" + name, "[AudioFile] FMOD 模式位 '" + name + "' 取不到 → 按 0 处理", e);
                return 0u;
            }
        }

        private static uint Result(object r)
        {
            try { return r == null ? BAD_RESULT : Convert.ToUInt32(r); }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("audio:result", "[AudioFile] FMOD 返回值不可读 → 按失败处理", e);
                return BAD_RESULT;
            }
        }
    }
}
