using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 模块实物物品（v1.7）

    public class ModuleItems : IMultiEntityConfig
    {
        // prefab_id / moduleId / 名称 / 描述 / 动画 / slot_type
        internal static readonly string[][] Items = new string[][]
        {
            new string[] { "ModuleItem_weapon_small", "weapon_small", SpaceModStrings.ITEMS.MODULE.MODULEITEMWEAPONSMALL.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMWEAPONSMALL.DESC, "weapon_small_kanim", "weapon" },
            new string[] { "ModuleItem_weapon_medium", "weapon_medium", SpaceModStrings.ITEMS.MODULE.MODULEITEMWEAPONMEDIUM.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMWEAPONMEDIUM.DESC, "weapon_medium_kanim", "weapon" },
            new string[] { "ModuleItem_weapon_large", "weapon_large", SpaceModStrings.ITEMS.MODULE.MODULEITEMWEAPONLARGE.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMWEAPONLARGE.DESC, "weapon_large_kanim", "weapon" },
            new string[] { "ModuleItem_module_penetrator_1", "module_penetrator_1", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEPENETRATOR1.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEPENETRATOR1.DESC, "module_penetrator_1_kanim", "utility" },
            new string[] { "ModuleItem_module_penetrator_2", "module_penetrator_2", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEPENETRATOR2.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEPENETRATOR2.DESC, "module_penetrator_2_kanim", "utility" },
            new string[] { "ModuleItem_module_penetrator_3", "module_penetrator_3", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEPENETRATOR3.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEPENETRATOR3.DESC, "module_penetrator_3_kanim", "utility" },
            new string[] { "ModuleItem_module_speed_1", "module_speed_1", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULESPEED1.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULESPEED1.DESC, "module_speed_1_kanim", "utility" },
            new string[] { "ModuleItem_module_speed_2", "module_speed_2", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULESPEED2.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULESPEED2.DESC, "module_speed_2_kanim", "utility" },
            new string[] { "ModuleItem_module_speed_3", "module_speed_3", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULESPEED3.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULESPEED3.DESC, "module_speed_3_kanim", "utility" },
            new string[] { "ModuleItem_module_radar_1", "module_radar_1", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULERADAR1.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULERADAR1.DESC, "module_radar_1_kanim", "sensor" },
            new string[] { "ModuleItem_module_radar_2", "module_radar_2", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULERADAR2.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULERADAR2.DESC, "module_radar_2_kanim", "sensor" },
            new string[] { "ModuleItem_module_radar_3", "module_radar_3", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULERADAR3.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULERADAR3.DESC, "module_radar_3_kanim", "sensor" },
            new string[] { "ModuleItem_module_repair_1", "module_repair_1", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEREPAIR1.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEREPAIR1.DESC, "module_repair_1_kanim", "defense" },
            new string[] { "ModuleItem_module_repair_2", "module_repair_2", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEREPAIR2.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEREPAIR2.DESC, "module_repair_2_kanim", "defense" },
            new string[] { "ModuleItem_module_repair_3", "module_repair_3", SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEREPAIR3.NAME, SpaceModStrings.ITEMS.MODULE.MODULEITEMMODULEREPAIR3.DESC, "module_repair_3_kanim", "defense" },
        };

        public List<GameObject> CreatePrefabs()
        {
            List<GameObject> list = new List<GameObject>();
            for (int i = 0; i < Items.Length; i++)
            {
                list.Add(Create(Items[i][0], Items[i][1], Items[i][2], Items[i][3], Items[i][4], Items[i][5]));
            }
            return list;
        }

        public void OnPrefabInit(GameObject inst)
        {
        }

        public void OnSpawn(GameObject inst)
        {
        }

        private static GameObject Create(string prefabId, string moduleId, string name,
            string desc, string anim, string slotType)
        {
            KAnimFile file = Assets.GetAnim(anim);
            if (file == null)
            {
                Debug.LogWarning("[Space] 模块动画缺失：" + anim + "（" + moduleId + "），回退 floppy_disc_kanim");
                file = Assets.GetAnim("floppy_disc_kanim");
            }
            List<Tag> tags = new List<Tag>();
            tags.Add(new Tag("ModuleItem"));
            tags.Add(new Tag("module_" + slotType));
            tags.Add(new Tag("module_" + moduleId));
            tags.Add(GameTags.IndustrialProduct);   // 沙盒「工业产品」分类可见
            // 走 IngredientConfigs 的标准散落物创建路径：
            // Front 层 + RECTANGLE 0.8×0.4 + EntitySplitter，与 37 个正常原料物品完全一致
            GameObject go = IngredientConfigs.CreateStandard(prefabId, name, desc, file, tags);

            return go;
        }
    }
}
