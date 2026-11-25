// RecipeScrollPatches.cs —— 配方卷轴 · 隐藏 / 查阅解锁 / 存档

using HarmonyLib;
using KSerialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

namespace MoreFood_KFC
{
    // ① 主补丁：Initialize 期间临时替换 recipe_list
    [HarmonyPatch(typeof(ComplexFabricatorSideScreen), "Initialize")]
    internal static class RecipeScroll_InitializeFilter_Patch
    {
        private static readonly FieldInfo RecipeListField =
            AccessTools.Field(typeof(ComplexFabricator), "recipe_list");

        private static void Prefix(ComplexFabricator target, out ComplexRecipe[] __state)
        {
            __state = null;
            try
            {
                if (target == null || RecipeListField == null) return;

                // 关键：先让注册表解析一次（首次调用时 recipes 刚就绪）
                RecipeScrollRegistry.ResolveRecipes();

                var original = RecipeListField.GetValue(target) as ComplexRecipe[];
                if (original == null || original.Length == 0) return;

                var filtered = FilterForUI(original);
                if (filtered.Length == original.Length) return;   // 没有可过滤的

                RecipeListField.SetValue(target, filtered);
                __state = original;

                MFLog.Info(LogCat.UI, "[MoreFood/RecipeScroll] UI 过滤：recipe_list "
                    + original.Length + " → " + filtered.Length
                    + "（隐藏 " + (original.Length - filtered.Length) + " 条）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/RecipeScroll] UI 过滤 Prefix 异常");
            }
        }

        private static void Postfix(ComplexFabricator target, ComplexRecipe[] __state)
        {
            if (__state == null) return;
            try
            {
                if (target != null && RecipeListField != null)
                    RecipeListField.SetValue(target, __state);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/RecipeScroll] UI 过滤 Postfix 异常");
            }
        }

        private static ComplexRecipe[] FilterForUI(ComplexRecipe[] src)
        {
            var list = new List<ComplexRecipe>(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                ComplexRecipe r = src[i];
                if (r == null || string.IsNullOrEmpty(r.id)) { list.Add(r); continue; }

                // 不受管 → 保留
                if (!RecipeScrollRegistry.IsManaged(r.id)) { list.Add(r); continue; }

                // 受管 + 已读 → 保留；受管 + 未读 → 剔除
                if (RecipeScrollRegistry.IsReadRecipe(r.id)) list.Add(r);
            }
            return list.ToArray();
        }
    }

    // ② 接管卷轴阅读
    [HarmonyPatch(typeof(LoreBearer), "OnClickRead")]
    internal static class RecipeScroll_OnClickReadPatch
    {
        private static bool Prefix(LoreBearer __instance)
        {
            try
            {
                if (__instance == null) return true;
                GameObject go = __instance.gameObject;
                if (go == null) return true;

                KPrefabID pid = go.GetComponent<KPrefabID>();
                if (pid == null || !pid.HasTag(RecipeScrollRegistry.ItemTag)) return true;

                string foodId = RecipeScrollRegistry.FoodIdOfItem(pid.PrefabTag.Name);
                if (string.IsNullOrEmpty(foodId)) return true;

                KSelectable selectable = go.GetComponent<KSelectable>();
                string title = selectable != null
                    ? selectable.GetProperName()
                    : ("配方卷轴·" + RecipeScrollItems.FoodName(foodId));

                // 先取「阅读之前」的已读快照，再按快照生成正文。
                //   绝不能先 MarkRead 再问 IsReadFood —— 那样首次阅读也会命中 ALREADY_FMT。
                bool alreadyRead = RecipeScrollRegistry.IsReadFood(foodId);
                string body = RecipeScrollText.UnlockText(foodId, alreadyRead);

                ScrollReadView view = new ScrollReadView();
                view.AddPlainText(body);
                if (!ScrollReadWindow.Show("good", title, view))
                {
                    InfoDialogScreen screen = LoreBearer.ShowPopupDialog();
                    if (screen == null) return true;   // 两种弹窗都失败 ⇒ 不解锁、不消耗，交还原版
                    screen.SetHeader(title);
                    view.ReplayInto(screen);
                    screen.AddDefaultOK(true);
                }
                // 正文确实展示给用户后才写状态、才消耗卷轴
                if (!alreadyRead)
                {
                    RecipeScrollRegistry.MarkRead(foodId);
                }
                Util.KDestroyGameObject(go);
                return false;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/RecipeScroll] 查阅配方卷轴失败（走原版）");
                return true;
            }
        }
    }

    // ③ 存档载体
    [SerializationConfig(MemberSerialization.OptIn)]
    public class RecipeScrollSaveData : KMonoBehaviour, ISaveLoadable
    {
        [Serialize] public List<string> readFoods = new List<string>();     // 旧字段（脏数据源）
        [Serialize] public List<string> readFoodsV2 = new List<string>();   // 新字段，唯一可信

        // 存档载体的静态引用
        private static RecipeScrollSaveData carrier;
        internal static RecipeScrollSaveData Carrier { get { return carrier; } }

        // 把 Registry 的当前已读集覆盖写回存档载体（由 MarkRead 调用）。
        // 方向恒为「内存 → 存档」，不反向合并；G-40 的旧 bug 就是方向写反。
        internal static void WriteThrough()
        {
            if (carrier == null) return;
            if (carrier.readFoodsV2 == null) carrier.readFoodsV2 = new List<string>();
            RecipeScrollRegistry.AppendReadSet(carrier.readFoodsV2);
        }

        [OnDeserialized]
        // 回调：OnDeserializedMethod
        public void OnDeserializedMethod()
        {
            try
            {
                if (readFoods != null && readFoods.Count > 0)
                {
                    Debug.LogWarning("[MoreFood/RecipeScroll] 检测到旧字段 readFoods（"
                        + readFoods.Count + " 条）→ 视为旧 bug 脏数据，已丢弃");
                    readFoods.Clear();
                }
                RecipeScrollRegistry.LoadReadSet(readFoodsV2);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/RecipeScroll] 读档恢复失败");
            }
        }

        internal void Bind()
        {
            carrier = this;
            RecipeScrollRegistry.ResetForSession();
        }
    }

    [HarmonyPatch(typeof(SaveGame), "OnPrefabInit")]
    internal static class RecipeScroll_SaveMountPatch
    {
        private static void Postfix(SaveGame __instance)
        {
            try
            {
                if (__instance == null || __instance.gameObject == null) return;
                RecipeScrollSaveData data = __instance.gameObject.AddOrGet<RecipeScrollSaveData>();
                data.Bind();
                MFLog.Info(LogCat.UI, "[MoreFood/RecipeScroll] 存档载体已挂上 SaveGame 对象（V2 已读 "
                    + (data.readFoodsV2 != null ? data.readFoodsV2.Count : 0) + " 条）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/RecipeScroll] 挂存档载体失败");
            }
        }
    }

    // ④ Db.Initialize 后显式解析配方归属
    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class RecipeScroll_DbInit_ResolvePatch
    {
        private static void Postfix()
        {
            try { RecipeScrollRegistry.ResolveRecipes(); }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/RecipeScroll] Db.Initialize 后 ResolveRecipes 异常");
            }
        }
    }
}
