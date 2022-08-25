using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    internal class FileBlobCacheStub : IFileBlobCache
    {
        public class Entry
        {
            public Guid Id { get; set; }

            public virtual bool CompareTo(Entry other)
            {
                return other.Id == Id;
            }
        }

        public class IncreaseEntry: Entry
        {
            public long CompressedSize { get; set; }
            public long Size { get; set; }

            public override bool CompareTo(Entry other)
            {
                IncreaseEntry? otherIncrease = other as IncreaseEntry;
                return otherIncrease != null &&
                       CompressedSize == otherIncrease.CompressedSize &&
                       Size == otherIncrease.Size &&
                       base.CompareTo(other);
            }
        }

        public class DecreaseEntry: Entry
        {
            public override bool CompareTo(Entry other)
            {
                DecreaseEntry? otherDecrease = other as DecreaseEntry;
                return otherDecrease != null &&
                       base.CompareTo(other);
            }
        }

        public void IncreaseUsageCount(Guid fileBlobId, long compressedSize, long size)
        {
            --FakeIncreaseUsageCountErrorIn;
            if (FakeIncreaseUsageCountErrorIn == 0)
            {
                throw new FakeException();
            }
            Entries.Add(new IncreaseEntry() { Id = fileBlobId, CompressedSize = compressedSize, Size = size });
        }

        public void DecreaseUsageCount(Guid fileBlobId)
        {
            Entries.Add(new DecreaseEntry() { Id = fileBlobId });
        }

        public List<Entry> Entries { get; set; } = new();

        public bool CompareEntries(IEnumerable<Entry> entries)
        {
            int index = 0;
            foreach (var entry in entries)
            {
                if (index >= Entries.Count || !entry.CompareTo(Entries[index]))
                {
                    return false;
                }
                ++index;
            }
            return index == Entries.Count;
        }

        public void Clear()
        {
            Entries.Clear();
        }

        public class FakeException: Exception
        {
        }

        public int FakeIncreaseUsageCountErrorIn { get; set; } = -1;
    }
}
