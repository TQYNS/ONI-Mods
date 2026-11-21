// 用途：查询接口：按 id、预制体、口径、标签取定义

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatDb
    {
        // 查询 / 索引访问

        internal static CombatMonsterDef FindMonster(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            CombatMonsterDef d;
            return monsterById.TryGetValue(id, out d) ? d : null;
        }

        internal static AmmoDef FindAmmo(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            AmmoDef d;
            return ammoById.TryGetValue(id, out d) ? d : null;
        }

        internal static AmmoDef FindAmmoByPrefab(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                return null;
            }
            AmmoDef d;
            return ammoByPrefab.TryGetValue(prefabId, out d) ? d : null;
        }

        internal static AmmoDef FindAmmoByTag(Tag tag)
        {
            AmmoDef d;
            return ammoByTag.TryGetValue(tag.Name, out d) ? d : null;
        }

        internal static ModuleDef FindModule(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            ModuleDef d;
            return moduleById.TryGetValue(id, out d) ? d : null;
        }

        internal static RocketComponentSlots FindComponent(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                return null;
            }
            RocketComponentSlots d;
            return componentByPrefab.TryGetValue(prefabId, out d) ? d : null;
        }

        internal static ModuleDef FindModuleByPrefab(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                return null;
            }
            ModuleDef d;
            return moduleByPrefab.TryGetValue(prefabId, out d) ? d : null;
        }

        internal static List<AmmoDef> AmmoOfCaliber(string caliber)
        {
            List<AmmoDef> list;
            return !string.IsNullOrEmpty(caliber) && ammoByCaliber.TryGetValue(caliber, out list)
                ? list : null;
        }

        /// <summary>自动选弹的排序口径（当前统一按伤害）。</summary>
        internal static string PriorityOf(ModuleDef weapon)
        {
            return Shared.ammoDefaultPriority;
        }
    }
}
