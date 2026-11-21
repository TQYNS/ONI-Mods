using System;
using System.Reflection;
using Klei;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class DrinkGarnishRegistry
    {
        // 反射私有成员的默认绑定标志
        private const BindingFlags PrivateFlags = BindingFlags.Instance | BindingFlags.NonPublic;

// 注册全部杯饰定义（幂等）
        public static void RegisterAll()
        {
            if (Garnish.All == null)
            {
                Debug.LogWarning("[MoreFood/Drink][Garnish] Garnish.All 不可用，跳过调料注册");
                return;
            }

            int added = 0;
            int skipped = 0;

            foreach (var kv in Drinks.ItemTable)
            {
                DrinkData data = kv.Value;
                if (data == null || !data.garnish) continue;   // 未启用 = 合法配置，静默跳过

                if (string.IsNullOrEmpty(data.effectId)
                    || Db.Get() == null || Db.Get().effects == null
                    || !Db.Get().effects.Exists(data.effectId))
                {
                    Debug.LogWarning("[MoreFood/Drink][Garnish] " + kv.Key.Name
                        + " 启用了上桌调料但 effectId 无效，已跳过");
                    skipped++;
                    continue;
                }

                if (Garnish.All.Exists(g => g != null && g.itemTag == kv.Key)) continue;

                string anim = NormalizeAnim(data.garnishAnim);
                if (string.IsNullOrEmpty(anim))
                {
                    Debug.LogWarning("[MoreFood/Drink][Garnish] " + kv.Key.Name
                        + " 启用了上桌调料但未配置 garnish_anims，已跳过");
                    skipped++;
                    continue;
                }

                // 显示名优先走 LanguageHelper 的 po 映射；Db 初始化早期 GetProperName 只拿到 ID
                string name = vs_presetNameHelper.GetDisplayName(kv.Key.Name);
                if (string.IsNullOrEmpty(name))
                {
                    name = kv.Key.Name;
                    GameObject prefab = Assets.TryGetPrefab(kv.Key);
                    if (prefab != null)
                    {
                        KPrefabID kp = prefab.GetComponent<KPrefabID>();
                        if (kp != null)
                        {
                            string proper = kp.GetProperName();
                            if (!string.IsNullOrEmpty(proper)) name = proper;
                        }
                    }
                }

                Descriptor desc = default(Descriptor);
                desc.SetupDescriptor(
                    name,
                    "用餐时消耗 " + data.garnishConsumeRate.ToString("0.##") + "kg " + name
                    + "，补充饮水并生效。",
                    Descriptor.DescriptorType.Effect);

                var garnish = new Garnish
                {
                    itemTag = kv.Key,
                    effectId = data.effectId,
                    consumeRate = data.garnishConsumeRate,
                    priority = data.garnishPriority,
                    descriptor = desc,
                };

                if (Assets.GetAnim(anim) == null)
                {
                    Debug.LogWarning("[MoreFood/Drink][Garnish] " + kv.Key.Name
                        + " 的上桌动画不存在: " + anim + "（保留消耗/效果，仅无图案）");
                }
                else
                {
                    FieldInfo animField = typeof(Garnish).GetField("overrideAnimName", PrivateFlags);
                    FieldInfo symbolField = typeof(Garnish).GetField("overrideSymbolName", PrivateFlags);
                    if (animField != null) animField.SetValue(garnish, (HashedString)anim);
                    if (symbolField != null)
                    {
                        string symbol = string.IsNullOrEmpty(data.garnishSymbol) ? "object" : data.garnishSymbol;
                        symbolField.SetValue(garnish, new KAnimHashedString(symbol));
                    }
                }

                Garnish.All.Add(garnish);
                added++;
            }

            MFLog.Info(LogCat.Drink, "[MoreFood/Drink][Garnish] 注册完成：新增 " + added + "，跳过 " + skipped
                + "，Garnish.All 共 " + Garnish.All.Count);
        }

        private static string NormalizeAnim(string anim)
        {
            if (string.IsNullOrEmpty(anim)) return null;
            anim = anim.Trim();
            if (!anim.EndsWith("_kanim", StringComparison.OrdinalIgnoreCase)) anim += "_kanim";
            return anim;
        }
    }
}
