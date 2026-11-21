// EventPanelStyleKeeper.cs — 宿主自备的文本样式守卫（自事件框架的同名实现复制，行为一致）
//
// 用途：卷轴阅读窗口复用事件面板的排版皮肤。模板会在 Start 里回灌样式，
// 所以头几帧反复把登记过的文本按记录值写回去，之后自停。

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MoreFood_KFC
{
    // 文本样式守卫
    public class EventPanelStyleKeeper : MonoBehaviour
    {
        public class Item
        {
            // 文本节点
            public LocText lt;
            // 颜色
            public Color color;
            // 字号
            public float size;
            // 对齐方式
            public TextAlignmentOptions align;
        }

        public readonly List<Item> items = new List<Item>();

        // 只跑头几帧就自停：够压过模板在 Start 里的回灌，又不至于和悬停变色打架
        private int _frames = 6;

        // 该文本是否已在自检列表里
        public bool Contains(LocText lt)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].lt == lt) return true;
            return false;
        }

        // 把一个文本登记进自检列表（记录颜色 / 字号 / 对齐供比对）
        public void Add(LocText lt, Color color, float size, TextAlignmentOptions align)
        {
            if (lt == null) return;
            var it = new Item();
            it.lt = lt; it.color = color; it.size = size; it.align = align;
            items.Add(it);
            EventUiKit.ApplyTextStyle(lt, color, size, align);
        }

        private void LateUpdate()
        {
            if (_frames <= 0) { enabled = false; return; }
            _frames--;
            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null || it.lt == null) continue;
                    EventUiKit.ApplyTextStyle(it.lt, it.color, it.size, it.align);
                }
            }
            catch (System.Exception e)
            {
                enabled = false;
                MFLog.HotFail("ui:EventPanelStyleKeeper.LateUpdate", e);
            }
        }
    }
}
