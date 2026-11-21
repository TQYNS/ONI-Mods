// ModOptions.cs — 模组设置页（PLib Options）：特性即选项，落盘 mod 目录的 config.json；详见《选项.md》

using System;
using System.IO;
using PeterHan.PLib.Options;

namespace MoreFood_KFC
{
    // config.json 放 mod 目录（便于开发期直接看），输出缩进（勿实现 PLib 接口/继承 PLib 类型：PLib 由 mod 目录加载，类型层引用会导致本 DLL 加载失败）
    [ConfigFile("config.json", true, false)]
    [Serializable]
    public class ModOptions
    {
        private static ModOptions current;

        // 设置页取当前值（首次访问时读盘；读不到用默认值）
        public static ModOptions Current
        {
            get
            {
                if (current == null)
                {
                    Reload();
                }
                return current;
            }
        }

        // 重新从 config.json 读取并刷新静态缓存 Current
        public static void Reload()
        {
            current = ReadFresh();
        }

        // 唯一读盘点：PLib 的 ReadSettings 每次都读盘、内部无缓存；缺文件或异常都用属性初始化器的默认值
        private static ModOptions ReadFresh()
        {
            try
            {
                return POptions.ReadSettings<ModOptions>() ?? new ModOptions();
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.General, "读取 config.json 失败，使用默认值 | ex={0}", e);
                return new ModOptions();
            }
        }

        // 实时取值入口（饮水闸口专用，见《选项.md》O-04）：比对 config.json 时间戳，变了才重读
        private static string _livePath;
        private static System.DateTime _liveStamp;
        private static bool _liveStampValid;
        private static ModOptions _liveSnapshot;
        private static float _liveCheckedAt = -1f;

        public static ModOptions Live
        {
            get
            {
                try
                {
                    // 每个复制人每秒都会查闸口：stat 最多每秒一次，避免 sim 线程上重复走磁盘
                    float now = UnityEngine.Time.realtimeSinceStartup;
                    if (_liveSnapshot != null && now - _liveCheckedAt < 1f) return _liveSnapshot;
                    _liveCheckedAt = now;

                    if (_livePath == null)
                        _livePath = POptions.GetConfigFilePath(typeof(ModOptions)) ?? string.Empty;

                    // 完全限定 System.DateTime：宿主侧存在同名 DateTime 类型，简写会解析到那个类型
                    System.DateTime stamp = (_livePath.Length > 0 && File.Exists(_livePath))
                        ? File.GetLastWriteTimeUtc(_livePath)
                        : System.DateTime.MinValue;

                    if (_liveSnapshot == null || !_liveStampValid || stamp != _liveStamp)
                    {
                        _liveStamp = stamp;
                        _liveStampValid = true;
                        _liveSnapshot = ReadFresh();
                    }
                }
                catch (Exception e)
                {
                    MFLog.Warn(LogCat.General, "读取饮水闸口设置失败，沿用上次值 | ex={0}", e);
                    if (_liveSnapshot == null) _liveSnapshot = new ModOptions();
                }
                return _liveSnapshot ?? Current;
            }
        }

        // 日志分级用两个勾选框表达（PLib 的枚举下拉 PComboBox 与游戏 TMP 不兼容，会崩设置页）
        // 两框都不勾 = 精简（仅错误与警告）；只勾信息 = 标准；勾诊断 = 诊断（含信息）
        [Option("记录信息日志",
                "勾选后记录初始化、注册、读档等生命周期节点；取消则回到精简模式（仅错误与警告）。默认勾选。",
                "日志")]
        public bool LogInfo { get; set; } = true;

        [Option("记录诊断日志",
                "勾选后在信息日志基础上增加分支与探测细节（只进内部缓冲，不写盘）；通常只在排障时开启。默认不勾选。",
                "日志")]
        public bool LogDiagnostic { get; set; } = false;

        // 翻译修复（默认勾选）
        [Option("翻译修复",
                "修正原版与其它 Mod 的缺失词条。默认开启。",
                "功能")]
        public bool TranslationFix { get; set; } = true;

        // Buff效果冷却（1~20 周期，默认 2）：成功触发后进入冷静期，期间不再触发 Buff效果
        [Option("Buff效果冷却",
                "饮品/食物内置Buff效果成功触发后的冷静期（1 ~ 20 周期，默认 2）。" +
                "冷静期内只拦Buff效果，属性与数值照常生效；保存后重进游戏生效。",
                "功能")]
        [Limit(1, 20)]
        public int SpecialEffectCooldownCycles { get; set; } = 2;

        // 兜底饮水闸口（分类「饮水」）：两个都不勾 = 桶装水整级不参与，取值走 Live 而非 Current
        [Option("桶装水",
                "勾选后，复制人在找不到饮品物品与饮水机时，才会去喝地上散落的净水。" +
                "默认关闭；改动后无需重进游戏，下一次派发饮水任务即生效。",
                "饮水")]
        public bool AllowBottledWater { get; set; } = false;

        [Option("桶装污染水",
                "勾选后，才允许兜底饮用地上散落的污染水，与「桶装水」互不依赖。" +
                "两个都勾时先找净水、找不到再找污染水。默认关闭。",
                "饮水")]
        public bool AllowBottledDirtyWater { get; set; } = false;

        // 起始星币（0~3000，默认 0）
        [Option("起始星币",
                "新殖民地开局时持有的星币数量（0 ~ 3000）。默认 0。",
                "交易")]
        [Limit(0, 3000)]
        public int StartingCredits { get; set; } = 0;

        // 库存数量（1~10 倍，默认 1）
        [Option("库存数量",
                "交易站可交易库存的倍率（1 ~ 10 倍）。默认 1。",
                "交易", Format = "F1")]
        [Limit(1, 10)]
        public float StockMultiplier { get; set; } = 1f;

        // 交易商持有星币（1~10 倍，默认 1）
        [Option("交易商持有星币",
                "交易商可用星币的倍率（1 ~ 10 倍）。默认 1。",
                "交易", Format = "F1")]
        [Limit(1, 10)]
        public float TraderCreditsMultiplier { get; set; } = 1f;

        // 捕食者拥挤判定（默认开启）：统计占用格数时排除同域内的活体猎物
        [Option("捕食者不计猎物拥挤",
                "勾选后，捕食者判断是否拥挤时不把同域的猎物算进占用格数（只减捕食者自己的数字）。" +
                "按 diet 中的活体猎物自动识别，不限于特定物种；保存后重进游戏生效。默认开启。",
                "补丁")]
        public bool PredatorIgnoresPreyCrowding { get; set; } = true;

        // 性能分析（默认不勾选）：不勾选时探针一条钩子都不装；改完设置后下次读档生效
        [Option("性能分析",
                "勾选后统计每个 Mod 的 Harmony 补丁耗时、鼠标点击回调与卡顿帧来源，报告写到 mod 目录的 perf_report.txt；" +
                "不勾选时一条钩子都不装。开关在下次读取存档时生效。默认不勾选。",
                "调试")]
        public bool PerfAnalysis { get; set; } = false;
    }
}
