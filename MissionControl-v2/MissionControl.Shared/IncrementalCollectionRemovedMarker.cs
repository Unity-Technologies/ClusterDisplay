using System;

namespace Unity.ClusterDisplay.MissionControl
{
    class IncrementalCollectionRemovedMarker : IncrementalCollectionObject
    {
        public IncrementalCollectionRemovedMarker(Guid id) : base(id)
        {
        }

        public override IncrementalCollectionObject NewOfTypeWithId()
        {
            return new IncrementalCollectionRemovedMarker(Id);
        }

        protected override void DeepCopyImp(IncrementalCollectionObject from) { }
    }
}
