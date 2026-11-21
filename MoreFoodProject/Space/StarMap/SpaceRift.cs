using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    public class SpaceRift : ClusterGridEntity
    {
        public virtual bool IsFake
        {
            get { return false; }
        }

        public override string Name
        {
            get { return "时空裂缝"; }
        }

        public override EntityLayer Layer
        {
            get { return EntityLayer.POI; }
        }

        public override List<ClusterGridEntity.AnimConfig> AnimConfigs
        {
            get
            {
                return new List<ClusterGridEntity.AnimConfig>
                {
                    new ClusterGridEntity.AnimConfig
                    {
                        animFile = Assets.GetAnim("temporal_tear_kanim"),
                        initialAnim = "closed_loop"
                    }
                };
            }
        }

        public override bool IsVisible
        {
            get { return true; }
        }

        public override ClusterRevealLevel IsVisibleInFOW
        {
            get { return ClusterRevealLevel.Peeked; }
        }

        internal static GameObject CreatePrefab(string id, string displayName, bool fake)
        {
            GameObject go = EntityTemplates.CreateEntity(id, displayName, true);
            go.AddOrGet<SaveLoadRoot>();
            if (fake)
            {
                go.AddOrGet<SpaceFakeRift>();
                go.AddOrGet<RiftJumpButton>();
            }
            else
            {
                go.AddOrGet<SpaceRift>();
            }
            go.SetActive(false);
            Assets.AddPrefab(go.GetComponent<KPrefabID>());
            return go;
        }
    }

    public class SpaceFakeRift : SpaceRift
    {
        public override bool IsFake
        {
            get { return true; }
        }

        public override string Name
        {
            get { return "时空裂隙"; }
        }

        public override ClusterRevealLevel IsVisibleInFOW
        {
            get { return ClusterRevealLevel.Hidden; }
        }
    }
}
