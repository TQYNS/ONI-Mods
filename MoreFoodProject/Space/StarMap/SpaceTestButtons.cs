using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    public abstract class SpaceBeaconButton : KMonoBehaviour, ISidescreenButtonControl
    {
        public abstract string SidescreenButtonText { get; }

        public abstract string SidescreenButtonTooltip { get; }

        public string SidescreenTitle
        {
            get { return "星际航行"; }
        }

        public virtual bool SidescreenEnabled()
        {
            return true;
        }

        public virtual bool SidescreenButtonInteractable()
        {
            return true;
        }

        public int HorizontalGroupID()
        {
            return -1;
        }

        public virtual int ButtonSideScreenSortOrder()
        {
            return 0;
        }

        public void SetButtonTextOverride(ButtonMenuTextOverride textOverride)
        {
        }

        public abstract void OnSidescreenButtonPressed();
    }

    public class RiftJumpButton : SpaceBeaconButton
    {
        private bool Fake
        {
            get
            {
                SpaceRift rift = GetComponent<SpaceRift>();
                return rift != null && rift.IsFake;
            }
        }

        public override string SidescreenButtonText
        {
            get { return Fake ? "返回主星系" : "跃入裂缝（前往星外区）"; }
        }

        public override string SidescreenButtonTooltip
        {
            get
            {
                return Fake
                    ? "时空裂隙只提供返程：把停在裂隙上的火箭送回主星系那侧的时间裂口。不消耗、不结档、不改动原版流程。"
                    : "把停在这座裂缝上的火箭送到星外区（信号屏蔽区）里的时空裂隙旁，验证'只能靠裂缝双向往返'。";
            }
        }

        public override int ButtonSideScreenSortOrder()
        {
            return 10;
        }

        public override void OnSidescreenButtonPressed()
        {
            try
            {
                SpaceTestRuntime.RiftJump(GetComponent<SpaceRift>());
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 测试：裂隙跳跃失败");
            }
        }
    }

    public class TearEnterButton : SpaceBeaconButton
    {
        public override string SidescreenButtonText
        {
            get { return "前往：进入时间裂口"; }
        }

        public override string SidescreenButtonTooltip
        {
            get
            {
                return "把停在裂口上的火箭和舱内复制人一起送到星外区（时空裂隙），不销毁火箭；结算动画与成就会照常触发。";
            }
        }

        public override int ButtonSideScreenSortOrder()
        {
            return 5;
        }

        public override bool SidescreenButtonInteractable()
        {
            TemporalTear tear = GetComponent<TemporalTear>();
            ClusterGrid grid = ClusterGrid.Instance;
            if (tear == null || grid == null || !tear.IsOpen())
            {
                return false;
            }
            List<ClusterGridEntity> list;
            if (!grid.cellContents.TryGetValue(tear.Location, out list) || list == null)
            {
                return false;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Clustercraft)
                {
                    return true;
                }
            }
            return false;
        }

        public override void OnSidescreenButtonPressed()
        {
            try
            {
                SpaceTestRuntime.EnterTear(GetComponent<TemporalTear>());
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 测试：进入时空裂隙失败");
            }
        }
    }

    public class PathCheckButton : SpaceBeaconButton
    {
        public override string SidescreenButtonText
        {
            get { return "诊断：寻路能不能进星外区"; }
        }

        public override string SidescreenButtonTooltip
        {
            get
            {
                return "查两次路径：① 普通火箭（雾墙挡住）② 允许穿雾的火箭（裂缝闸门挡住）。两次都该是不可达，结果看日志。";
            }
        }

        public override int ButtonSideScreenSortOrder()
        {
            return 20;
        }

        public override void OnSidescreenButtonPressed()
        {
            try
            {
                SpaceTestRuntime.PathCheckTest();
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 测试：路径检查失败");
            }
        }
    }
}
