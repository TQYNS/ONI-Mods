using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    public static class UiModalBlocker
    {
        // 遮罩根节点
        private static GameObject root;

        public static bool Active
        {
            // 与框架同名类同口径：只有"存在且真的可见"才算遮罩生效。
            // 只看 != null 会把面板隐藏后的残留遮罩当成生效（曾导致整张星图六角格射线被关死）。
            get { return root != null && root.activeInHierarchy; }
        }

        // 开一个遮罩层（默认暗度 0.3），挡住下层点击
        public static GameObject Open(Transform parent)
        {
            return Open(parent, 0.3f);
        }

        // 开一个指定暗度的遮罩层（先关掉已有遮罩）
        public static GameObject Open(Transform parent, float dim)
        {
            Close();
            if (parent == null)
            {
                return null;
            }
            GameObject go = new GameObject("MoreFood_UiBlocker", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.02f, 0.03f, 0.06f, Mathf.Clamp01(dim));
            img.raycastTarget = true;
            go.AddComponent<UiModalBlockerBehaviour>();
            go.transform.SetAsFirstSibling();
            root = go;
            return go;
        }

        public static void Close()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
            }
        }
    }

    public class UiModalBlockerBehaviour : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IScrollHandler
    {
        public void OnPointerEnter(PointerEventData e) { }
        public void OnPointerExit(PointerEventData e) { }
        public void OnPointerDown(PointerEventData e) { }
        public void OnPointerUp(PointerEventData e) { }
        public void OnPointerClick(PointerEventData e) { }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e) { }
        public void OnScroll(PointerEventData e) { }
    }

    internal static class ClusterMapHexBlockerPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!UiModalBlocker.Active)
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
