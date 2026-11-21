using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace EventFramework.Distribution.Contracts
{
    // 候选登记与择优：只把赢家 LoadFrom 一次，其余文件不进 AppDomain
    public static class EventFrameworkHub
    {
        private static readonly List<FrameworkCandidate> Cands = new List<FrameworkCandidate>();

        private static bool Sealed;

        public static IFrameworkBootstrap Winner;

        public static FrameworkCandidate WinnerCandidate;

        public static void Register(FrameworkCandidate c)
        {
            try
            {
                if (c == null || c.Ver == null || string.IsNullOrEmpty(c.FullPath)) return;
                for (int i = 0; i < Cands.Count; i++)
                {
                    if (Cands[i].FullPath == c.FullPath) return;
                }
                Cands.Add(c);
                DistributionLog.Info("登记候选 " + c);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("候选登记失败：" + e);
            }
        }

        public static void SealAndActivate()
        {
            try
            {
                // 一次性守卫：V5 实测 Harmony 不去重，重复装配会双跑
                if (Sealed) return;
                Sealed = true;
                if (Cands.Count == 0)
                {
                    DistributionLog.Info("无候选，框架不启动");
                    return;
                }
                FrameworkCandidate win = PickWinner();
                WinnerCandidate = win;
                Assembly impl = LoadWinner(win);
                DistributionLog.Info("赢家 MVID " + impl.ManifestModule.ModuleVersionId);
                Type type = impl.GetType("EventFramework.Distribution.Bootstrap.FrameworkBootstrap", true);
                IFrameworkBootstrap api = (IFrameworkBootstrap)Activator.CreateInstance(type);
                api.Activate(new Harmony(EventFrameworkApi.FixedHarmonyId));
                Winner = api;
                DistributionLog.Info("择优完成 v" + win.Ver + "，落败 " + (Cands.Count - 1)
                    + " 份；实现版本 v" + api.Ver);
            }
            catch (Exception e)
            {
                Winner = null;
                WinnerCandidate = null;
                Sealed = false;
                DistributionLog.Warn("择优激活失败：" + e);
            }
        }

        // 赢家只加载一次：AppDomain 已有同版本则直接复用，已存在别的版本只告警（避免第二套静态状态）
        private static Assembly LoadWinner(FrameworkCandidate win)
        {
            string simple = CandidateFile.AssemblyNameOf(win.FullPath);
            if (!string.IsNullOrEmpty(simple))
            {
                Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < loaded.Length; i++)
                {
                    Assembly a = loaded[i];
                    try
                    {
                        AssemblyName an = a.GetName();
                        if (an == null || an.Name != simple) continue;
                        if (an.Version != null && win.Ver != null && an.Version.CompareTo(win.Ver) == 0)
                        {
                            DistributionLog.Info("复用已加载程序集 " + simple + " v" + an.Version
                                + "，不再 LoadFrom（" + win.FullPath + "）");
                            return a;
                        }
                        DistributionLog.Warn("已存在不同版本的 " + simple + " v" + an.Version
                            + "（期望 v" + win.Ver + "），沿用已加载的那份以免出现第二套静态状态");
                        return a;
                    }
                    catch (Exception e)
                    {
                        DistributionLog.Collect("读取已加载程序集名失败：" + e.GetType().Name);
                    }
                }
            }
            return Assembly.LoadFrom(win.FullPath);
        }

        // 同版本保留先登记者；比较失败只跳过该候选
        private static FrameworkCandidate PickWinner()
        {
            FrameworkCandidate win = Cands[0];
            for (int i = 1; i < Cands.Count; i++)
            {
                try
                {
                    if (Cands[i].Ver.CompareTo(win.Ver) > 0)
                    {
                        DistributionLog.Collect("落败 " + win);
                        win = Cands[i];
                    }
                    else
                    {
                        DistributionLog.Collect("落败 " + Cands[i]);
                    }
                }
                catch (Exception e)
                {
                    DistributionLog.Collect("候选比较异常：" + e.GetType().Name);
                }
            }
            DistributionLog.FlushCollected("候选比较");
            return win;
        }
    }
}
