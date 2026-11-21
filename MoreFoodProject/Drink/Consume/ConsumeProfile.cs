// 用途：饮水值/效果的共享档案，饮品与带饮水值的食物共用。
namespace MoreFood_KFC
{
    public class ConsumeProfile
    {
        // 每 kg 补水值
        public float hydrationPerKg = 0f;
        // 单次上限；食物为 1 份
        public float maxServeMass = 1f;
        // 附加效果 buff 的 id（可空）
        public string effectId = null;
        // 士气加成（仅正式饮品使用）
        public float moraleBonus = 0f;
        // 是否正式饮品：只有 true 才进 ItemTable 与饮品行为层
        public bool isDrink = false;
    }
}
