// Plants.cs — 植物数据定义（模块化数据层）

using System.Collections.Generic;
using TUNING;
using UnityEngine;
using MoreFood_KFC;

namespace MoreFood_KFC{
    public static class ModPlants
    {
        // 一个周期的秒数
        public const float ONE_CYCLE_SECONDS = 600f;


        // PlantDef — 完整植物配置（所有字段均可定制）

        public class PlantDef
        {
            // 基础标识
            // 植物本体 ID：作物 prefab 的 id，种子/收获物登记都靠它反查本配置
            public string id;
            // 种子 ID（须与种子物品的 prefab id 一致，种植时按它配对）
            public string seedId;
            public string cropId;          // 收获物 ID（须已注册）

            // 动画
            // 植株动画名（kanim 资源名，不含路径）
            public string anim;
            // 种子动画名（种子物品自己的 kanim）
            public string seedAnim;

            // 文本（引用 ModStrings）
            // 植物显示名（取自 ModStrings，不写死字面量）
            public string name;
            // 植物图鉴/详情描述文本
            public string desc;
            // 已驯化状态下的附加描述（说明驯化后的收成/环境收益）
            public string domesticatedDesc;
// 种子显示名（ModStrings 取）
            public string seedName;
// 种子描述文本
            public string seedDesc;

            // 生长参数
            public float cropDuration;     // 秒
            // 每株每次收获的产出件数（进 CROPS.CROP_TYPES.CropVal）
            public int numProduced;
            // 收获后是否可再生：true=割完留株继续长，false=连根收走
            public bool renewable;

            // 环境耐受（开尔文）
            // 致死低温（K）：低于它就枯死
            public float lethalLow;
            // 低温停产阈值（K）：低于它停止生长但不死
            public float warningLow;
            // 高温停产阈值（K）：高于它停止生长但不死
            public float warningHigh;
            // 致死高温（K）：高于它就枯死
            public float lethalHigh;

            // 展示与物理参数（新增可定制字段）
            // 装饰度影响（视野范围 + 数值，EffectorValues）
            public EffectorValues decor;
            // 自动生成的植物/食物 Tag 之外再挂的额外 Tag
            public List<Tag> extraTags;
            public int width;              // 占格宽
            public int height;             // 占格高
            public float mass;             // 质量 kg
            public SimHashes element;      // 分类
        }


        // Make 工厂方法（扩展参数）

        private static PlantDef Make(
            string id,
            string seedId,
            string cropId,
            string anim,
            string seedAnim,
            string name,
            string desc,
            string domesticatedDesc,
            string seedName,
            string seedDesc,
            float cropDuration = 3f * ONE_CYCLE_SECONDS,
            int numProduced = 1,
            bool renewable = true,
            float lethalLow = 218.15f,
            float warningLow = 283.15f,
            float warningHigh = 303.15f,
            float lethalHigh = 398.15f,
            EffectorValues decor = default(EffectorValues),
            List<Tag> extraTags = null,
            int width = 1,
            int height = 1,
            float mass = 1f,
            SimHashes element = SimHashes.Creature)
        {
            return new PlantDef
            {
                id = id,
                seedId = seedId,
                cropId = cropId,
                anim = anim,
                seedAnim = seedAnim,
                name = name,
                desc = desc,
                domesticatedDesc = domesticatedDesc,
                seedName = seedName,
                seedDesc = seedDesc,
                cropDuration = cropDuration,
                numProduced = numProduced,
                renewable = renewable,
                lethalLow = lethalLow,
                warningLow = warningLow,
                warningHigh = warningHigh,
                lethalHigh = lethalHigh,
                decor = decor,
                extraTags = extraTags,
                width = width,
                height = height,
                mass = mass,
                element = element,
            };
        }


        // ★ 植物定义区域（生成器在此插入/替换/删除 ITEM:xxx:PLANT 块）

        // ==[ZONE:PLANT_DEFS]BEGIN==
        // ==[ZONE:PLANT_DEFS]END==


        // All 数组（生成器维护数组项列表）

        public static readonly PlantDef[] All = new PlantDef[]
        {
            // ==[ZONE:PLANT_ARRAY]BEGIN==

            // ==[ZONE:PLANT_ARRAY]END==
        };

        // 将收获物注册进 CROPS.CROP_TYPES（幂等）；U60 起一项产物表可含多个作物
        public static void EnsureCropTypeRegistered(PlantDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.cropId))
                return;
            if (CROPS.CROP_TYPES.Exists(v => HasProduct(v, def.cropId)))
                return;
            //如果产物已经存在，会导致周期被覆盖！
            CROPS.CROP_TYPES.Add(new Crop.CropVal(
                def.cropId,
                def.cropDuration,
                def.numProduced,
                def.renewable));
        }

        private static bool HasProduct(Crop.CropVal val, string cropId)
        {
            if (val.products == null) return false;
            for (int i = 0; i < val.products.Count; i++)
            {
                if (val.products[i].cropId == cropId) return true;
            }
            return false;
        }
    }
}
