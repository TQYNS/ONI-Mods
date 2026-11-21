using System;
using System.Collections.Generic;
using System.Text;
using EventFramework.Distribution.Contracts;
using UnityEngine;

namespace EventFramework
{
    public sealed class AudioLibraryFile
    {
        // 本 yaml 的音频条目清单
        public List<AudioLibraryEntry> sounds { get; set; } = null;
    }

    public sealed class AudioLibraryEntry
    {
        // 音频 key（事件 yaml 按它引用；重复即覆盖）
        public string id { get; set; } = null;
        // 音源类别（EventMediaKind：sound / ui_sound / loop_sound / music / snapshot / voice / ui_enum / file）
        public string kind { get; set; } = null;
        // 音源变体：每次播放加权抽一个（至少要一个）
        public List<AudioVariant> variants { get; set; } = null;

        // 音量（1 = 原始）
        public float volume { get; set; } = 1f;
        // 音量 ±抖动
        public float volume_jitter { get; set; } = 0f;
        // 音高（1 = 原始）
        public float pitch { get; set; } = 1f;
        // 音高 ±抖动
        public float pitch_jitter { get; set; } = 0f;

        // 是否循环播放
        public bool loop { get; set; } = false;
        // 起播后播 N 游戏秒自动停（<=0 = 不停）
        public float play_seconds { get; set; } = 0f;
        // 效果结束时是否停（默认 true；false 的留到收尾再停）
        public bool stop_on_end { get; set; } = true;
        // 收到该信号即停（自定义信号）
        public string stop_on_signal { get; set; } = null;

        // 起播前延迟（秒）
        public float delay { get; set; } = 0f;
        // 从音频第 N 毫秒开始（需句柄；快照/UISounds 忽略）
        public int start_ms { get; set; } = 0;
        // 播到第 N 毫秒停（需句柄；快照/UISounds 忽略）
        public int end_ms { get; set; } = 0;
        // 播放位置："listener"（跟听者）/ "zero"（原点）
        public string position { get; set; } = "listener";
        // 自定义 file 音频是否 3D（起播取一次位置，不跟随）
        public bool spatial { get; set; } = false;
        // 同源音效已在响 ⇒ 本次不播（防叠加）
        public bool dedup { get; set; } = true;
        // 互斥：列出的音效正在响 ⇒ 本次静默不播（单向）
        public List<string> mute_when { get; set; } = null;
        // 3D 衰减最小距离
        public float min_distance { get; set; } = 1f;
        // 3D 衰减最大距离
        public float max_distance { get; set; } = 60f;
        // 轨道：sfx / bgm / voice / ambient（后三者独占）
        public string play_as { get; set; } = null;
        // 同一 key 两次播放的最小间隔（秒）
        public float cooldown { get; set; } = 0f;
        // 同一 key 同时最多几条（0 = 不限）
        public int max_concurrent { get; set; } = 0;

        // 收到这些信号就播（EventSignal）
        public List<string> fire_on { get; set; } = null;
        // 关联旗标名（EventAudioTrigger 据此判断）
        public string flag { get; set; } = null;
    }

    public sealed class AudioVariant
    {
        // 变体名（标识用）
        public string name { get; set; } = null;
        // 音频文件路径（相对 Mod 的 sounds/）
        public string file { get; set; } = null;
        // 加权抽取的权重（<=0 不参与）
        public float weight { get; set; } = 1f;
    }

    public static class EventAudioLibrary
    {
        public static readonly string[] DIR_NAMES =
            { "sounds", "Sounds", "sound", "Sound" };

        public static int LoadedFiles { get; private set; }
        public static int LoadedEntries { get; private set; }
        public static int FailedEntries { get; private set; }
        public static int LooseIdCount { get; private set; }

// 加载全部音频定义（返回加载条数）
        public static int LoadAll()
        {
            LoadedFiles = 0;
            LoadedEntries = 0;
            FailedEntries = 0;
            LooseIdCount = 0;
            ContentDiag.Begin("sounds");

            List<YamlFile> files;
            try
            {
                files = YamlFileScanner.Scan(DIR_NAMES, "sounds");
            }
            catch (Exception e)
            {
                DistributionLog.Exc("[AudioLib] 扫描异常（本次没有音效库可用）", e);
                ContentDiag.End("sounds");
                return 0;
            }

            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                AudioLibraryFile lib = YamlLoad.Parse<AudioLibraryFile>(f.text, f.filePath, "sounds");
                if (lib == null)
                {
                    continue;
                }

                if (lib.sounds == null || lib.sounds.Count == 0)
                {
                    ContentDiag.Skip("sounds", f.filePath, "", "顶层缺少 sounds 段或为空",
                        "顶层键写 sounds:，下面每项含 id 与 variants");
                    continue;
                }

                LoadedFiles++;
                for (int k = 0; k < lib.sounds.Count; k++)
                    CompileEntry(lib.sounds[k], f, k);
            }

            DistributionLog.Info("[AudioLib] 音效库加载完成：文件 " + LoadedFiles
                      + " 个，音效 " + LoadedEntries + " 条，失败 " + FailedEntries + " 条"
                      + (LooseIdCount > 0
                         ? "；其中 " + LooseIdCount + " 条键含点号等非 [A-Za-z0-9_] 字符 → 按原样注册（不阻断）"
                         : ""));
            ContentDiag.End("sounds");
            return LoadedEntries;
        }

        private static void CompileEntry(AudioLibraryEntry e, YamlFile f, int index)
        {
            if (e == null) return;

            string where = "sounds[" + index + "]";
            if (string.IsNullOrEmpty(e.id))
            {
                FailedEntries++;
                ContentDiag.Skip("sounds", f.filePath, where,
                    "缺少 id（该文件第 " + (index + 1) + " 条音效）",
                    "补 id: <音效键名>（事件 yaml 里按这个名字引用）");
                return;
            }

            string id = IdentifierValidator.Normalize(e.id);
            if (string.IsNullOrEmpty(id))
            {
                FailedEntries++;
                ContentDiag.Skip("sounds", f.filePath, where,
                    "id 归一化后为空：" + IdentifierValidator.Why(e.id),
                    "id 只能用字母数字下划线，且不能以数字开头");
                return;
            }

            if (!IdentifierValidator.IsValid(id)) LooseIdCount++;

            if (e.variants == null || e.variants.Count == 0)
            {
                FailedEntries++;
                ContentDiag.Skip("sounds", f.filePath, "「" + id + "」", "没有 variants（至少要一个音源）",
                    "补 variants: [{sound: <原版枚举名>}] 或 [{file: <sounds/ 下的相对路径>}]，至少一条");
                return;
            }

            var t = new EventAudioTemplate();
            t.key = id;
            t.kind = ParseKind(e.kind, e, f);
            t.volume = e.volume;
            t.volume_jitter = e.volume_jitter;
            t.pitch = e.pitch;
            t.pitch_jitter = e.pitch_jitter;
            t.play_mode = e.loop ? EventPlayMode.loop : EventPlayMode.once;
            t.loop = e.loop;
            t.play_seconds = e.play_seconds;
            t.stop_on_end = e.stop_on_end;
            t.stop_on_signal = e.stop_on_signal;
            t.delay = e.delay;
            t.start_ms = e.start_ms;
            t.end_ms = e.end_ms;
            t.position = string.IsNullOrEmpty(e.position) ? "listener" : e.position;
            t.spatial = e.spatial;
            t.dedup = e.dedup;
            t.mute_when = e.mute_when;
            t.min_distance = e.min_distance;
            t.max_distance = e.max_distance;
            t.play_as = string.IsNullOrEmpty(e.play_as) ? null : e.play_as.Trim().ToLowerInvariant();
            t.cooldown = e.cooldown;
            t.max_concurrent = e.max_concurrent;
            t.fire_on = e.fire_on;
            t.flag = string.IsNullOrEmpty(e.flag) ? null : e.flag.Trim();
            t.variants = e.variants;
            t.name = FirstSource(e.variants);

            EventAudioFactory.Register(t);
            LoadedEntries++;
        }

        private static EventMediaKind ParseKind(string kind, AudioLibraryEntry e, YamlFile f)
        {
            if (string.IsNullOrEmpty(kind))
            {
                var vs = e.variants;
                for (int i = 0; i < vs.Count; i++)
                {
                    if (vs[i] != null && !string.IsNullOrEmpty(vs[i].file)) return EventMediaKind.file;
                }
                return EventMediaKind.sound;
            }

            try
            {
                return (EventMediaKind)Enum.Parse(typeof(EventMediaKind), kind.Trim(), true);
            }
            catch (Exception ex)
            {
                DistributionLog.Exc("[AudioLib] 音效 '" + e.id + "' 的 kind '" + kind
                                    + "' 不认识 → 按 sound 处理", ex);
                ContentDiag.Suspect("sounds", f != null ? f.filePath : null, "「" + e.id + "」",
                    "kind='" + kind + "' 不认识", "改成 sound / ui_sound / loop_sound / music / snapshot / voice / ui_enum / file 之一（大小写不敏感）");
                return EventMediaKind.sound;
            }
        }

        private static string FirstSource(List<AudioVariant> vs)
        {
            for (int i = 0; i < vs.Count; i++)
            {
                var v = vs[i];
                if (v == null) continue;
                if (!string.IsNullOrEmpty(v.file)) return v.file;
                if (!string.IsNullOrEmpty(v.name)) return v.name;
            }
            return null;
        }

// 校验各音频的变体清单（返回有问题的条数）
        public static int ValidateVariants()
        {
            int bad = 0;
            foreach (var t in EventAudioFactory.All)
            {
                if (t == null || t.variants == null || t.variants.Count == 0) continue;

                int usable = 0;
                for (int i = 0; i < t.variants.Count; i++)
                {
                    var v = t.variants[i];
                    if (v == null) continue;

                    if (v.weight <= 0f)
                    {
                        Debug.Log("[MoreFood/Event][AudioLib] 音效 '" + t.key + "' 的变体 weight="
                                  + v.weight + " ≤ 0 → 不参与抽取");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(v.file))
                    {
                        if (!EventAudioFileLibrary.Exists(v.file))
                        {
                            bad++;
                            Debug.LogWarning("[MoreFood/Event][AudioLib] 音效 '" + t.key + "' 的变体文件 '"
                                             + v.file + "' 找不到（sounds/ 下没有）");
                        }
                        else usable++;
                    }
                    else if (!string.IsNullOrEmpty(v.name))
                    {
                        usable++;
                    }
                    else
                    {
                        bad++;
                        Debug.LogWarning("[MoreFood/Event][AudioLib] 音效 '" + t.key
                                         + "' 有一个变体既没有 name 也没有 file → 无效");
                    }
                }

                if (usable == 0)
                {
                    bad++;
                    Debug.LogWarning("[MoreFood/Event][AudioLib] 音效 '" + t.key
                                   + "' 没有任何可用变体（全部 weight≤0 或音源无效）");
                }
            }
            return bad;
        }

        // 打印调试信息（诊断用）
        public static void Dump()
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var t in EventAudioFactory.All)
            {
                if (t == null || t.variants == null || t.variants.Count == 0) continue;
                n++;
                sb.AppendLine("[MoreFood/Event][AudioLib]   " + t.key + "（" + t.kind
                              + "，变体 " + t.variants.Count + "）");
            }
            Debug.Log("[MoreFood/Event][AudioLib] ── 音效库条目 ──（共 " + n + " 条）");
            if (n > 0) Debug.Log(sb.ToString());
        }
    }
}
