
namespace MoreFood_KFC
{
    public static class DrinkEffectRegistry
    {
        // 注册 YAML 定义的饮品效果（建 Effect 并登记进 Db）
        public static void Register(DrinkEffectDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            if (BuffConfig.Exists(def.id)) return;

            // 描述统一交给 CountedBuffDisplay：标题 = buff 名字，正文 = 原生属性行 + 自定义并列行
            BuffDef buff = BuffConfig.Add(
                def.id,
                string.IsNullOrEmpty(def.name) ? def.id : def.name,
                "",
                def.is_bad);
            buff.duration = def.duration;

            if (def.modifiers != null)
            {
                for (int i = 0; i < def.modifiers.Count; i++)
                {
                    DrinkEffectModifierDef m = def.modifiers[i];
                    if (m == null || string.IsNullOrEmpty(m.attribute)) continue;
                    if (m.is_percent) buff.AddPercent(m.attribute, m.value);
                    else buff.Add(m.attribute, m.value);
                }
            }

            DrinkEffectBehaviorRegistry.Attach(buff);
        }
    }
}
