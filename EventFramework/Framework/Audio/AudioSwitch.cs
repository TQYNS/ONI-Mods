// AudioSwitch.cs — 音频开关状态（独立于事件叙事状态；运行态，不存盘）
//
// 用途：音频模板的 flag 触发（EventAudioTemplate.flag → EventAudioTrigger）。
//   开 → 播绑定的音频；关 → 停。
// 与事件系统的叙事状态彻底解耦：本表不存盘，读档后一律从"全关"开始。
//   declared 表只记录"模板声明过哪些开关名"（启动时由 BuildBindings 重建），用于命名自检。
// 用 List 而非 HashSet：本项目未引用 System 程序集，HashSet→IEnumerable 会触发 ISet<> 编译错误。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class AudioSwitch
    {
        // 当前处于"开"状态的开关（数量极少，线性查找足够）
        private static readonly List<string> _on = new List<string>();
        // 音频模板里声明过的开关名（自检 / 命名检查用，不参与状态）
        private static readonly List<string> _declared = new List<string>();

        // 开关变化广播（EventAudioTrigger 订阅）
        public static event Action<string, bool> OnChanged;

        public static int OnCount { get { return _on.Count; } }
        public static int DeclaredCount { get { return _declared.Count; } }

        // 打开一个开关（真变化时广播）
        public static void On(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (_on.Contains(flag)) return;
            _on.Add(flag);
            Raise(flag, true);
        }

        // 关闭一个开关（真变化时广播）
        public static void Off(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (!_on.Remove(flag)) return;
            Raise(flag, false);
        }

        public static bool IsOn(string flag)
        {
            return !string.IsNullOrEmpty(flag) && _on.Contains(flag);
        }

        // 切换：已置位则清、未置位则置
        public static void Toggle(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (IsOn(flag)) Off(flag); else On(flag);
        }

        // 换档 / 新档：全部关闭并广播（让正在播的开关音正常停，而不是静默断掉）
        public static void Clear()
        {
            if (_on.Count == 0) return;

            string[] snapshot = new string[_on.Count];
            _on.CopyTo(snapshot);
            _on.Clear();

            for (int i = 0; i < snapshot.Length; i++) Raise(snapshot[i], false);
        }

        // 返回副本，避免外部改动内部状态
        public static List<string> All() { return new List<string>(_on); }

        // ---- 模板声明（不参与状态）----
        public static void Declare(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (_declared.Contains(flag)) return;
            _declared.Add(flag);
        }

        public static bool IsDeclared(string flag)
        {
            return !string.IsNullOrEmpty(flag) && _declared.Contains(flag);
        }

        public static List<string> AllDeclared() { return new List<string>(_declared); }

        public static void ClearDeclarations() { _declared.Clear(); }

        private static void Raise(string flag, bool on)
        {
            var h = OnChanged;
            if (h == null) return;

            var list = h.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                try { ((Action<string, bool>)list[i])(flag, on); }
                catch (Exception e)
                {
                    Debug.LogWarning("[MoreFood/Event][AudioSwitch] 回调异常（已忽略该订阅者）: " + e);
                }
            }
        }
    }
}
