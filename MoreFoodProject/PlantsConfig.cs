// PlantsConfig.cs — 植物注册（IMultiEntityConfig）

using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC{
    public class PlantsConfig : IMultiEntityConfig
    {
        public List<GameObject> CreatePrefabs()
        {
            var list = new List<GameObject>(ModPlants.All.Length);
            foreach (var def in ModPlants.All)
                list.Add(CreatePlant(def));
            return list;
        }

        public void OnPrefabInit(GameObject inst) { }
        public void OnSpawn(GameObject inst) { }

        private static GameObject CreatePlant(ModPlants.PlantDef def)
        {
            // 0. 注册收获物到 CROPS
            ModPlants.EnsureCropTypeRegistered(def); 

            // 1. 创建放置型实体（使用 def 中的尺寸、质量、元素）
            GameObject plant = EntityTemplates.CreatePlacedEntity(
                def.id,
                def.name,
                def.desc,
                def.mass,                          // ← 可定制
                Assets.GetAnim(def.anim),
                "place",
                Grid.SceneLayer.BuildingFront,
                def.width,                         // ← 可定制
                def.height,                        // ← 可定制
                def.decor,
                default(EffectorValues),
                def.element,                       // ← 可定制
                def.extraTags,
                293f);

            // 2. 扩展为标准作物
            EntityTemplates.ExtendEntityToBasicPlant(
                plant,
                def.lethalLow,
                def.warningLow,
                def.warningHigh,
                def.lethalHigh,
                null,
                true,
                0f, 
                0.15f,
                def.cropId,
                true,
                true, 
                true, 
                false,
                true, 
                2400f,
                0f,
                2200f,
                def.id + "Original",
                def.name);

            // 3. 标准作物状态机
            plant.AddOrGet<StandardCropPlant>();

            // 4. 创建并注册种子
            GameObject seed = EntityTemplates.CreateAndRegisterSeedForPlant(
                plant,
                null,
                SeedProducer.ProductionType.Harvest,
                def.seedId,
                def.seedName,
                def.seedDesc,
                Assets.GetAnim(def.seedAnim),
                "object",
                1,
                new List<Tag> { GameTags.CropSeed },
                SingleEntityReceptacle.ReceptacleDirection.Top,
                default(Tag),
                0,
                def.domesticatedDesc,
                EntityTemplates.CollisionShape.CIRCLE,
                0.3f, 0.3f,
                null, "", false);

            // 5. 种植预览
            EntityTemplates.CreateAndRegisterPreviewForPlant(
                seed,
                def.id + "_preview",
                Assets.GetAnim(def.anim),
                "place",
                def.width,
                def.height);

            return plant;
        }
    }
}
