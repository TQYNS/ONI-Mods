// EventAudioVolume.cs — 事件框架 · 音量跟随：读用户音量并对自建 channel 实时生效

using System;
using System.Collections.Generic;
using System.Reflection;
using EventFramework.Distribution.Contracts;
using UnityEngine;

namespace EventFramework
{
    public static class EventAudioVolume
    {
        private struct Entry
        {
            public string playAs;      // 轨道名（决定跟哪个音量档）
            public float baseVolume;   // 模板自身音量
            public float applied;      // 最近写入通道的值
        }

        private static readonly Dictionary<object, Entry> _channels = new Dictionary<object, Entry>();
        private static MethodInfo _miSetVolume;

        public static int TrackedCount { get { return _channels.Count; } }

        // 播放时登记通道，返回应写入的最终音量
        internal static float Register(object channel, string playAs, float baseVolume)
        {
            float target = Mathf.Clamp01(baseVolume * BusFactorFor(playAs));
            if (channel != null)
            {
                Entry e;
                e.playAs = playAs;
                e.baseVolume = baseVolume;
                e.applied = target;
                _channels[channel] = e;
            }
            return target;
        }

        // 通道停止后注销
        internal static void Forget(object channel)
        {
            if (channel == null) return;
            _channels.Remove(channel);
        }

        // 会话切换全清
        public static void Clear()
        {
            _channels.Clear();
        }

        // 音量变化时实时刷新（值未变则短路）
        public static void UpdateAllLive()
        {
            if (_channels.Count == 0) return;

            List<object> dead = null;

            foreach (var kv in _channels)
            {
                Entry e = kv.Value;
                float target = Mathf.Clamp01(e.baseVolume * BusFactorFor(e.playAs));
                if (Mathf.Approximately(target, e.applied)) continue;

                try
                {
                    SetChannelVolume(kv.Key, target);
                    e.applied = target;
                    _channels[kv.Key] = e;
                }
                catch (Exception ex)
                {
                    // 通道已失效：记一次并移出跟踪表，音量下次重建
                    DistributionLog.ExcOnce("volume:set", "[AudioVolume] 设置通道音量失败 → 移出跟踪表", ex);
                    if (dead == null) dead = new List<object>();
                    dead.Add(kv.Key);
                }
            }

            if (dead != null)
                for (int i = 0; i < dead.Count; i++) Forget(dead[i]);
        }

        private static void SetChannelVolume(object channel, float v)
        {
            if (_miSetVolume == null)
                _miSetVolume = EventMediaPlayer.FindMethod(channel.GetType(), "setVolume", 1, typeof(float));
            if (_miSetVolume == null) return;
            _miSetVolume.Invoke(channel, new object[] { v });
        }

        // play_as → 音量档
        internal static string BusOf(string playAs)
        {
            if (string.IsNullOrEmpty(playAs)) return "SFX";
            switch (playAs.Trim().ToLowerInvariant())
            {
                case "bgm": return "Music";
                case "music": return "Music";
                case "ambient": return "Ambience";
                case "voice": return "SFX";
                case "sfx": return "SFX";
                default: return "SFX";
            }
        }

        // 当前轨道系数（轨道音量 × 主音量）
        internal static float BusFactorFor(string playAs)
        {
            try
            {
                var mix = AudioMixer.instance;
                if (mix == null) return 1f;
                float bus = ReadLevel(mix, BusOf(playAs), 1f);
                float master = ReadLevel(mix, "Master", 1f);
                return Mathf.Clamp01(bus * master);
            }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("volume:bus", "[AudioVolume] 取轨道音量失败 → 按 1 处理", e);
                return 1f;
            }
        }

        private static float ReadLevel(AudioMixer mix, string key, float dflt)
        {
            try
            {
                AudioMixer.UserVolumeBus vb;
                if (mix.userVolumeSettings != null
                    && mix.userVolumeSettings.TryGetValue(key, out vb) && vb != null)
                    return Mathf.Clamp01(vb.busLevel);
            }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("volume:level:" + key, "[AudioVolume] 读取音量键 '" + key + "' 失败 → 用默认值", e);
            }
            return dflt;
        }
    }
}