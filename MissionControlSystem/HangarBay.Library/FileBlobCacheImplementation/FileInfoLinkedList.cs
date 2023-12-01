using System.Collections;
using System.Runtime.Serialization;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Library
{
    /// <summary>
    /// Small wrapper around <see cref="LinkedList{T}"/> that keeps track of total CompressedSize of all the
    /// <see cref="CacheFileInfo"/> in the list.
    /// </summary>
    /// <remarks>Assumes that the CompressedSize of <see cref="CacheFileInfo"/> added in the list never change.
    /// </remarks>
    class FileInfoLinkedList: ICollection<CacheFileInfo>, IReadOnlyCollection<CacheFileInfo>, ICollection,
        IDeserializationCallback, ISerializable
    {
        public FileInfoLinkedList()
        {
            m_Storage = new();
        }
        public FileInfoLinkedList(IEnumerable<CacheFileInfo> collection)
        {
            m_Storage = new(collection);
            CompressedSize = m_Storage.Sum(fi => fi.CompressedSize);
        }


        public LinkedListNode<CacheFileInfo>? Last => m_Storage.Last;
        public LinkedListNode<CacheFileInfo>? First => m_Storage.First;
        public int Count => m_Storage.Count;

        public void AddAfter(LinkedListNode<CacheFileInfo> node, LinkedListNode<CacheFileInfo> newNode)
        {
            m_Storage.AddAfter(node, newNode);
            CompressedSize += newNode.Value.CompressedSize;
        }
        public LinkedListNode<CacheFileInfo> AddAfter(LinkedListNode<CacheFileInfo> node, CacheFileInfo value)
        {
            var ret = m_Storage.AddAfter(node, value);
            CompressedSize += value.CompressedSize;
            return ret;
        }
        public void AddBefore(LinkedListNode<CacheFileInfo> node, LinkedListNode<CacheFileInfo> newNode)
        {
            m_Storage.AddBefore(node, newNode);
            CompressedSize += newNode.Value.CompressedSize;
        }
        public LinkedListNode<CacheFileInfo> AddBefore(LinkedListNode<CacheFileInfo> node, CacheFileInfo value)
        {
            var ret = m_Storage.AddBefore(node, value);
            CompressedSize += value.CompressedSize;
            return ret;
        }
        public void AddFirst(LinkedListNode<CacheFileInfo> node)
        {
            m_Storage.AddFirst(node);
            CompressedSize += node.Value.CompressedSize;
        }
        public LinkedListNode<CacheFileInfo> AddFirst(CacheFileInfo value)
        {
            var ret = m_Storage.AddFirst(value);
            CompressedSize += value.CompressedSize;
            return ret;
        }
        public void AddLast(LinkedListNode<CacheFileInfo> node)
        {
            m_Storage.AddLast(node);
            CompressedSize += node.Value.CompressedSize;
        }
        public LinkedListNode<CacheFileInfo> AddLast(CacheFileInfo value)
        {
            var ret = m_Storage.AddLast(value);
            CompressedSize += value.CompressedSize;
            return ret;
        }
        public void Clear()
        {
            m_Storage.Clear();
            CompressedSize = 0;
        }
        public bool Contains(CacheFileInfo value) => m_Storage.Contains(value);
        public void CopyTo(CacheFileInfo[] array, int index) => m_Storage.CopyTo(array, index);
        public LinkedListNode<CacheFileInfo>? Find(CacheFileInfo value) => m_Storage.Find(value);
        public LinkedListNode<CacheFileInfo>? FindLast(CacheFileInfo value) => m_Storage.FindLast(value);
        public LinkedList<CacheFileInfo>.Enumerator GetEnumerator() => m_Storage.GetEnumerator();
        public void GetObjectData(SerializationInfo info, StreamingContext context) => m_Storage.GetObjectData(info, context);
        public void OnDeserialization(object? sender) => m_Storage.OnDeserialization(sender);
        public void Remove(LinkedListNode<CacheFileInfo> node)
        {
            m_Storage.Remove(node);
            CompressedSize -= node.Value.CompressedSize;
        }
        public bool Remove(CacheFileInfo value)
        {
            bool ret = m_Storage.Remove(value);
            if (ret)
            {
                CompressedSize -= value.CompressedSize;
            }
            return ret;
        }
        public void RemoveFirst()
        {
            long compressedSize = 0;
            if (m_Storage.First != null)
            {
                compressedSize = m_Storage.First.Value.CompressedSize;
            }
            m_Storage.RemoveFirst();
            CompressedSize -= compressedSize;
        }
        public void RemoveLast()
        {
            long compressedSize = 0;
            if (m_Storage.Last != null)
            {
                compressedSize = m_Storage.Last.Value.CompressedSize;
            }
            m_Storage.RemoveLast();
            CompressedSize -= compressedSize;
        }

        /// <summary>
        /// Total CompressedSize of all the <see cref="CacheFileInfo"/> in the list.
        /// </summary>
        public long CompressedSize { get; private set; }

        /// <summary>
        /// Checks if this is a node for this list.
        /// </summary>
        /// <param name="node">The node to test.</param>
        /// <remarks>Equivalent of testing node.List == LinkedList, but we need this method since the actual LinkedList
        /// is private.</remarks>
        public bool IsForThisList(LinkedListNode<CacheFileInfo> node) => node.List == m_Storage;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        IEnumerator<CacheFileInfo> IEnumerable<CacheFileInfo>.GetEnumerator() => GetEnumerator();

        bool ICollection.IsSynchronized => ((ICollection)m_Storage).IsSynchronized;
        object ICollection.SyncRoot => ((ICollection)m_Storage).SyncRoot;
        void ICollection.CopyTo(Array array, int index) => ((ICollection)m_Storage).CopyTo(array, index);

        int ICollection<CacheFileInfo>.Count => ((ICollection<CacheFileInfo>)m_Storage).Count;
        bool ICollection<CacheFileInfo>.IsReadOnly => ((ICollection<CacheFileInfo>)m_Storage).IsReadOnly;
        void ICollection<CacheFileInfo>.Add(CacheFileInfo item) => AddLast(item);
        bool ICollection<CacheFileInfo>.Contains(CacheFileInfo item) => ((ICollection<CacheFileInfo>)m_Storage).Contains(item);

        /// <summary>
        /// The actual <see cref="LinkedList{T}"/>
        /// </summary>
        LinkedList<CacheFileInfo> m_Storage;
    }
}
