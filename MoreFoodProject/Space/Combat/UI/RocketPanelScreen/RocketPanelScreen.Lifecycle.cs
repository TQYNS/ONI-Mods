// 用途：火箭面板生命周期：单例、开关、刷新节拍与选中态

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal sealed partial class RocketPanelScreen : KScreen
    {
        // 火箭管理面板（v1.7）
        private static RocketPanelScreen instance;

        private GameObject window;
        private GameObject rightRoot;
        private GameObject leftRoot;
        internal Clustercraft selected;

        // ★ 性能修复：缓存 selected → RocketCombatState，避免每帧 GetComponent
        private Clustercraft cachedSelCraft;
        private RocketCombatState cachedSelState;

        private Page page = Page.Rocket;
        private bool pickerOpen;
        private int monsterPage;
        private bool ammoPickerOpen;
        private int ammoPickerWeapon;
        private bool ammoPickerPrimary;
        private string pickerType = "weapon";
        private float nextRefresh;
        private bool dirty = true;
        private bool built;

        // ★ 局部刷新委托 —— 每次全量重建时登记"血量条/弹药灰条/文字"的更新闭包；
        private readonly List<System.Action> liveUpdaters = new List<System.Action>();

        private enum Page
        {
            Rocket,
            Assembly,
            Monsters
        }

        internal static RocketPanelScreen GetOrCreate()
        {
            if (instance != null)
            {
                return instance;
            }
            Transform parent = TradeUi.OverlayParent();
            if (parent == null)
            {
                Debug.LogWarning("[Space] 火箭面板找不到 UI 父节点");
                return null;
            }
            GameObject go = new GameObject("SpaceRocketPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform root = go.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            instance = go.AddComponent<RocketPanelScreen>();
            go.SetActive(false);
            return instance;
        }

        internal static void ClosePanel()
        {
            RocketPanelScreen screen = instance;
            try
            {
                UiModalBlocker.Close();
            }
            catch (Exception)
            {
                // expected: 遮罩可能已关闭，重复关闭抛异常可忽略，随后继续关面板
            }
            // ★ 问题2：不再 CloseAll（会把星图一起关掉 → 背景跳转回非星图界面）；只关本面板
            if (screen == null || screen.gameObject == null)
            {
                return;
            }
            try
            {
                screen.Show(false);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 关闭火箭面板失败");
            }
        }

        protected override void OnCleanUp()
        {
            try
            {
            if (instance == this)
            {
                instance = null;
            }
            base.OnCleanUp();
            }
            catch (System.Exception e) { MFLog.HotFail("cleanup:RocketPanelScreen", e); }
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            if (!built)
            {
                Build();
                built = true;
            }
            UiModalBlocker.Open(transform, 0.35f);
            dirty = true;
            Refresh();
        }

        protected override void OnShow(bool show)
        {
            base.OnShow(show);
            if (!show)
            {
                // ★ 面板隐藏时复位悬停计数 —— 鼠标可能停在条目上，但面板已不可见，
                PanelHoverGuard.Open = 0;
                try
                {
                    UiModalBlocker.Close();
                }
                catch (Exception)
                {
                    // expected: 面板已隐藏，遮罩关闭失败不影响隐藏流程
                }
                return;
            }
            UiModalBlocker.Open(transform, 0.35f);
            dirty = true;
            Refresh();
        }

        protected override void OnDeactivate()
        {
            UiModalBlocker.Close();
            base.OnDeactivate();
        }

        private void Update()
        {
            try
            {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }
            RocketCombatState stCur = GetSelectedState();
            bool combat = stCur != null && stCur.inCombat;
            if (dirty)
            {
                // ★ 用户操作 / 结构变化 → 全量重建。必须放在"暂停判断"之前，
                nextRefresh = Time.unscaledTime + (combat ? 0.2f : 2f);
                dirty = false;
                Refresh();
                return;
            }
            // ★ 暂停只停"自动节拍刷新"（不影响上面的用户操作刷新）。
            if (Time.timeScale <= 0f)
            {
                return;
            }
            bool due = combat && Time.unscaledTime >= nextRefresh;
            if (!due)
            {
                return;
            }
            // ★ 战斗节拍只做"局部刷新"（血量/装甲/燃料条、弹药灰条、文字），
            nextRefresh = Time.unscaledTime + 0.2f;
            RunLiveUpdaters();
            }
            catch (System.Exception e) { MFLog.HotFail("update:RocketPanelScreen", e); }
        }

        // ★ 性能修复：selected → RocketCombatState 缓存（仅当 selected 变化或缓存失效时重取）
        private RocketCombatState GetSelectedState()
        {
            if (selected == null)
            {
                cachedSelCraft = null;
                cachedSelState = null;
                return null;
            }
            if (selected != cachedSelCraft || cachedSelState == null)
            {
                cachedSelCraft = selected;
                cachedSelState = selected.GetComponent<RocketCombatState>();
            }
            return cachedSelState;
        }

        /// <summary>执行登记过的局部刷新闭包（不重建任何 GameObject）。</summary>
        private void RunLiveUpdaters()
        {
            for (int i = 0; i < liveUpdaters.Count; i++)
            {
                try
                {
                    liveUpdaters[i]();
                }
                catch (Exception)
                {
                    // expected: 单个局部刷新闭包失败不阻断其余刷新
                }
            }
        }

        private bool PointerBusy()
        {
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                return true;
            }
            UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        /// <summary>★ 第十八轮-2/8：外部事件（交战开始/结束、怪物结算、开火消耗弹药）请求面板全量重建一次。
        internal static void RequestRebuild()
        {
            if (instance != null)
            {
                instance.dirty = true;
            }
        }

        internal void Select(Clustercraft craft)
        {
            selected = craft;
            dirty = true;
        }

        /// <summary>★ 问题2：打开面板但【不 PushScreen】——避免顶替星图活动屏（背景跳转回非星图）。
        internal void Open(Clustercraft craft)
        {
            Select(craft != null && RocketValidity.IsValidCraft(craft) ? craft : null);
            gameObject.SetActive(true);
            if (!built)
            {
                Build();
                built = true;
            }
            try
            {
                UiModalBlocker.Open(transform, 0.35f);
            }
            catch (Exception)
            {
                // expected: 遮罩打开失败不阻断面板显示
            }
            dirty = true;
            Refresh();
        }

        private void Build()
        {
            window = TradeUi.Node("RocketPanelWindow", transform, 0f, 0f, 1180f, 720f);
            RectTransform rt = window.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            TradeUi.RoundPanel("bg", window.transform, 0f, 0f, 1180f, 720f, 12, TradeUi.BgRoot);
            TradeUi.RoundPanel("head", window.transform, 0f, 0f, 1180f, 46f, 12, TradeUi.Panel);
            TradeUi.Text("title", window.transform, 22f, 8f, 500f, 30f, "火箭", 20f, TradeUi.Gold, false);
            TradeUi.MakeCloseButton(window.transform, 1128f, 6f, delegate { ClosePanel(); });

            leftRoot = TradeUi.Node("left", window.transform, 16f, 56f, 270f, 648f);
            rightRoot = TradeUi.Node("right", window.transform, 300f, 56f, 864f, 648f);
        }

        private void Refresh()
        {
            if (!built || rightRoot == null)
            {
                return;
            }
            liveUpdaters.Clear();   // ★ 第十八轮-2：全量重建前清空旧闭包（旧控件已随重建销毁）
            PruneSelected();
            RebuildLeft();
            RebuildRight();
        }

        private void PruneSelected()
        {
            if (selected != null && selected.gameObject != null)
            {
                return;
            }
            selected = null;
            List<Clustercraft> crafts = AllCrafts();
            if (crafts.Count > 0)
            {
                selected = crafts[0];
            }
        }
    }
}
