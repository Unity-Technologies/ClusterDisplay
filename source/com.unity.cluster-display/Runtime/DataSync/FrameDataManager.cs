using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [System.Serializable]
    public class FrameDataManager
    {
        public PipeIDManager _pipeIDManager;
        private readonly IDataWatcher[] _watchers = new IDataWatcher[ushort.MaxValue];

        private readonly FrameDataAccumulator frameDataAccumulator = new FrameDataAccumulator();
        private readonly FrameDataDistributor _frameDataDistributor = new FrameDataDistributor();

        public FrameDataManager (PipeIDManager pipeIDManager)
        {
            _pipeIDManager = pipeIDManager;
        }

        public void RegisterWatcher (IDataWatcher watcher)
        {
            if (!(watcher is IPipeIDContainer))
                return;

            var idContainer = watcher as IPipeIDContainer;
            if (!idContainer.ValidID)
            {
                var pipeId = _pipeIDManager.GenerateID();
                idContainer.ApplyID(pipeId);
            }

            if (_watchers[idContainer.ID] != null)
                throw new System.Exception($"Unable to register watcher with UUID: {idContainer.ID}, it has already been registered!");

            Debug.Log($"Registered watcher with UUID: \"{idContainer.ID}\".");
            _watchers[idContainer.ID] = watcher;
        }

        public void UnregisterWatcher (PipeID pipeId)
        {
            Debug.Log($"Unregistered watcher with UUID: \"{pipeId}\".");
            _watchers[pipeId] = null;
        }

        public bool Accumulate (NativeArray<byte> buffer, ref int endPos)
        {
            Debug.Log(endPos);
            return true;
        }
    }
}