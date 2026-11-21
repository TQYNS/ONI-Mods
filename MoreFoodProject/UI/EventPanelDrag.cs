// EventPanelDrag.cs — 宿主自备的窗口拖动组件（自事件框架的同名实现复制）
//
// 卷轴阅读窗口复用事件面板的拖动行为：按住窗口任意位置都能拖，位移按缩放比换算并夹在父容器内。

using UnityEngine;
using UnityEngine.EventSystems;

namespace MoreFood_KFC
{
    public class EventPanelDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        // 被拖动的目标
        public RectTransform target;

        // 拖动缩放系数
        private float _scale = 1f;

        // 开始拖拽面板：记下缩放比用于换算位移
        public void OnBeginDrag(PointerEventData e)
        {
            try
            {
                _scale = target != null ? target.lossyScale.x : 1f;
                if (_scale <= 0.0001f) _scale = 1f;
            }
            catch (System.Exception ex)
            {
                _scale = 1f;
                MFLog.Exc(LogCat.UI, ex, "[MoreFood/UI] 拖动取缩放失败，按 1 处理");
            }
        }

        // 拖拽中：把指针位移换算成面板位移
        public void OnDrag(PointerEventData e)
        {
            try
            {
                if (target == null || e == null) return;

                Vector2 p = target.anchoredPosition + e.delta / _scale;

                var parent = target.parent as RectTransform;
                if (parent != null)
                {
                    float mx = parent.rect.width * 0.5f - 80f;
                    float my = parent.rect.height * 0.5f - 80f;
                    p.x = Mathf.Clamp(p.x, -mx, mx);
                    p.y = Mathf.Clamp(p.y, -my, my);
                }
                target.anchoredPosition = p;
            }
            catch (System.Exception ex)
            {
                MFLog.Exc(LogCat.UI, ex, "[MoreFood/UI] 拖动位移失败");
            }
        }
    }
}
