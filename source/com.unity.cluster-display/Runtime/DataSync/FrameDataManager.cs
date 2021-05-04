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
        public PipeIDManager _pipeUUIDManager;
        private readonly IDataWatcher[] _watchers = new IDataWatcher[ushort.MaxValue];

        private readonly FrameDataAccumulator frameDataAccumulator = new FrameDataAccumulator();
        private readonly FrameDataDistributor _frameDataDistributor = new FrameDataDistributor();

        public FrameDataManager (PipeIDManager pipeUUIDManager)
        {
            _pipeUUIDManager = pipeUUIDManager;
        }

        public void RegisterWatcher (IDataWatcher watcher)
        {
            if (!(watcher is IPipeIDContainer))
                return;

            var uuidContainer = watcher as IPipeIDContainer;
            if (!uuidContainer.ValidUUID)
            {
                var pipeUUID = _pipeUUIDManager.GenerateUUID();
                uuidContainer.ApplyUUID(pipeUUID);
            }

            if (_watchers[uuidContainer.UUID] != null)
                throw new System.Exception($"Unable to register watcher with UUID: {uuidContainer.UUID}, it has already been registered!");

            Debug.Log($"Registered watcher with UUID: \"{uuidContainer.UUID}\".");
            _watchers[uuidContainer.UUID] = watcher;
        }

        public void UnregisterWatcher (PipeID uuid)
        {
            Debug.Log($"Unregistered watcher with UUID: \"{uuid}\".");
            _watchers[uuid] = null;
        }

        public bool Accumulate (NativeArray<byte> buffer, ref int endPos)
        {
            Debug.Log(endPos);
            return true;
        }
    }
}