using System;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    // 问题11：攻击箭头（Mod 自绘）
    internal sealed class CombatArrow : MonoBehaviour
    {
        private static CombatArrow instance;
        private static Clustercraft craft;
        private static AxialI targetCell;
        private static bool active;

        private Image line;

        // 显示/更新攻击箭头（从火箭指向目标格）。
        internal static void Show(Clustercraft c, AxialI cell)
        {
            // Bug2：不再自绘攻击箭头。攻击时沿用原版"选点模式"的原版箭头/高亮，
            craft = c;
            targetCell = cell;
            active = false;
        }

        internal static void Hide()
        {
            active = false;
            craft = null;
            try
            {
                if (instance != null && instance.line != null)
                {
                    instance.line.gameObject.SetActive(false);
                }
            }
            catch (Exception)
            {
                // expected: 箭头已随星图关闭被销毁，隐藏失败不影响后续清理
            }
        }

        private static void Ensure()
        {
            if (instance != null)
            {
                return;
            }
            Transform parent = TradeUi.OverlayParent();
            if (parent == null)
            {
                return;
            }
            GameObject go = new GameObject("SpaceAttackArrow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.35f, 0.25f, 0.85f);
            instance = go.AddComponent<CombatArrow>();
            instance.line = img;
        }

        private void Update()
        {
            try
            {
            if (!active || craft == null || line == null || craft.gameObject == null)
            {
                return;
            }
            try
            {
                ClusterMapScreen screen = ClusterMapScreen.Instance;
                if (screen == null)
                {
                    return;
                }
                Vector3 a;
                Transform rootA;
                if (!StarMapVisuals.TryCellWorld(screen, craft.Location, out a, out rootA) || rootA == null)
                {
                    return;
                }
                Vector3 b;
                Transform rootB;
                if (!StarMapVisuals.TryCellWorld(screen, targetCell, out b, out rootB) || rootB == null)
                {
                    return;
                }
                RectTransform rt = line.rectTransform;
                Vector3 d = b - a;
                rt.position = (a + b) * 0.5f;
                rt.sizeDelta = new Vector2(Mathf.Max(1f, d.magnitude), 5f);
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                rt.localRotation = Quaternion.Euler(0f, 0f, ang);
                line.gameObject.SetActive(true);
            }
            catch (Exception)
            {
                // expected: 单帧定位失败时跳过本帧箭头绘制，下帧仍会重试
            }
            }
            catch (System.Exception e) { MFLog.HotFail("update:CombatArrow", e); }
        }

        // 读档/关星图时清理（ResetForLoad 调）。
        internal static void ResetForLoad()
        {
            active = false;
            craft = null;
            try
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance.gameObject);
                    instance = null;
                }
            }
            catch (Exception)
            {
                // expected: 读档时箭头对象可能已销毁，销毁失败仍清空单例引用
            }
        }
    }
}
