using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 弹药物品 prefab（v1.7：7 种，1kg/个，一个物品一发）

    public class AmmoItems : IMultiEntityConfig
    {
        // prefab_id / 名称 / 描述 / 动画 / 口径
        internal static readonly string[][] Items = new string[][]
        {
            new string[] { "AmmoHEBasic", SpaceModStrings.ITEMS.AMMO.AMMOHEBASIC.NAME, SpaceModStrings.ITEMS.AMMO.AMMOHEBASIC.DESC, "missile_kanim", "small" },
            new string[] { "AmmoCruise", SpaceModStrings.ITEMS.AMMO.AMMOCRUISE.NAME, SpaceModStrings.ITEMS.AMMO.AMMOCRUISE.DESC, "ammo_cruise_kanim", "small" },
            new string[] { "AmmoCluster", SpaceModStrings.ITEMS.AMMO.AMMOCLUSTER.NAME, SpaceModStrings.ITEMS.AMMO.AMMOCLUSTER.DESC, "ammo_cluster_kanim", "small" },
            new string[] { "AmmoThermobaric", SpaceModStrings.ITEMS.AMMO.AMMOTHERMOBARIC.NAME, SpaceModStrings.ITEMS.AMMO.AMMOTHERMOBARIC.DESC, "ammo_thermobaric_kanim", "medium" },
            new string[] { "AmmoLaserGuided", SpaceModStrings.ITEMS.AMMO.AMMOLASERGUIDED.NAME, SpaceModStrings.ITEMS.AMMO.AMMOLASERGUIDED.DESC, "ammo_laser_guided_kanim", "medium" },
            new string[] { "AmmoPenetrator", SpaceModStrings.ITEMS.AMMO.AMMOPENETRATOR.NAME, SpaceModStrings.ITEMS.AMMO.AMMOPENETRATOR.DESC, "ammo_penetrator_kanim", "large" },
            new string[] { "AmmoICBM", SpaceModStrings.ITEMS.AMMO.AMMOICBM.NAME, SpaceModStrings.ITEMS.AMMO.AMMOICBM.DESC, "ammo_icbm_kanim", "large" },
        };

        public List<GameObject> CreatePrefabs()
        {
            List<GameObject> list = new List<GameObject>();
            for (int i = 0; i < Items.Length; i++)
            {
                list.Add(Create(Items[i][0], Items[i][1], Items[i][2], Items[i][3], Items[i][4]));
            }
            return list;
        }

        public void OnPrefabInit(GameObject inst)
        {
        }

        public void OnSpawn(GameObject inst)
        {
        }

        private static GameObject Create(string id, string name, string desc, string anim, string caliber)
        {
            KAnimFile file = Assets.GetAnim(anim);
            if (file == null)
            {
                Debug.LogWarning("[Space] 弹药动画缺失：" + anim + "（" + id + "），回退 floppy_disc_kanim");
                file = Assets.GetAnim("floppy_disc_kanim");
            }
            List<Tag> tags = new List<Tag>();
            tags.Add(new Tag("ammo"));
            tags.Add(new Tag("caliber_" + caliber));
            tags.Add(GameTags.IndustrialProduct);   // 沙盒「工业产品」分类可见
            // 走 IngredientConfigs 的标准散落物创建路径（与正常原料物品一致）
            GameObject go = IngredientConfigs.CreateStandard(id, name, desc, file, tags);

            return go;
        }
    }
}
