// 用途：面板悬停守卫：统计指针是否停在面板上

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    //
    internal sealed class PanelHoverGuard : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        internal static int Open;
        private bool entered;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!entered)
            {
                entered = true;
                Open++;
            }
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (entered)
            {
                entered = false;
                Open--;
            }
        }

        private void OnDestroy()
        {
            if (entered)
            {
                entered = false;
                Open--;
            }
            if (Open < 0)
            {
                Open = 0;
            }
        }
    }
}
