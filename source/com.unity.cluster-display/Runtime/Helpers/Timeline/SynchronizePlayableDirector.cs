using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.ClusterDisplay.RPC;
using UnityEngine;
using UnityEngine.Playables;

namespace Unity.ClusterDisplay
{
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(PlayableDirector))]
    public class SynchronizePlayableDirector : MonoBehaviour
    {
        [SerializeField] private PlayableDirector m_PlayableDirector;
        private void OnValidate() => m_PlayableDirector = GetComponent<PlayableDirector>();

        private PlayableDirector playableDirector
        {
            get
            {
                if (m_PlayableDirector == null)
                {
                    m_PlayableDirector = GetComponent<PlayableDirector>();
                    if (m_PlayableDirector == null)
                        return null;

                    delegatesRegistered = false;
                }

                if (ClusterDisplayState.IsEmitter)
                {
                    if (!delegatesRegistered)
                    {
                        m_PlayableDirector.played -= OnPlay;
                        m_PlayableDirector.paused -= OnPause;
                        m_PlayableDirector.stopped -= OnStop;

                        m_PlayableDirector.played += OnPlay;
                        m_PlayableDirector.paused += OnPause;
                        m_PlayableDirector.stopped += OnStop;

                        delegatesRegistered = true;
                    }
                }

                return m_PlayableDirector;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 29)]
        public struct PlayableDirectorState
        {
            [FieldOffset(0)] public bool enabled;
            [FieldOffset(1)] public DirectorWrapMode extrapolationMode;
            [FieldOffset(5)] public double initialTime;
            [FieldOffset(13)] public double time;
            [FieldOffset(21)] public DirectorUpdateMode timeUpdateMode;
            [FieldOffset(25)] public PlayState state;
        }

        private PlayableDirectorState cachedPlayableDirectorState;
        private bool delegatesRegistered;

        private void OnPlay (PlayableDirector playableDirector) => Played();
        private void OnPause(PlayableDirector playableDirector) => Paused();
        private void OnStop(PlayableDirector playableDirector) => Stopped();

        [ClusterRPC(RPCExecutionStage.BeforeFixedUpdate)]
        public void Played ()
        {
            if (ClusterDisplayState.IsEmitter)
                return;

            var instance = playableDirector;
            if (instance == null)
                return;

            if (instance.timeUpdateMode != DirectorUpdateMode.Manual)
                instance.timeUpdateMode = DirectorUpdateMode.Manual;

            if (instance.state != PlayState.Playing)
                instance.Play();
        }

        [ClusterRPC(RPCExecutionStage.BeforeFixedUpdate)]
        public void Paused ()
        {
            if (ClusterDisplayState.IsEmitter)
                return;

            var instance = playableDirector;
            if (instance == null)
                return;

            if (instance.timeUpdateMode != DirectorUpdateMode.Manual)
                instance.timeUpdateMode = DirectorUpdateMode.Manual;

            if (instance.state != PlayState.Paused)
                instance.Pause();
        }

        [ClusterRPC(RPCExecutionStage.BeforeFixedUpdate)]
        public void Stopped ()
        {
            if (ClusterDisplayState.IsEmitter)
                return;

            var instance = playableDirector;
            if (instance == null)
                return;

            if (instance.timeUpdateMode != DirectorUpdateMode.Manual)
                instance.timeUpdateMode = DirectorUpdateMode.Manual;

            instance.Stop();
        }

        private void LateUpdate()
        {
            var instance = playableDirector;
            if (instance == null)
                return;

            if (ClusterDisplayState.IsEmitter)
            {
                cachedPlayableDirectorState.enabled = instance.enabled;
                cachedPlayableDirectorState.extrapolationMode = instance.extrapolationMode;
                cachedPlayableDirectorState.initialTime = instance.initialTime;
                cachedPlayableDirectorState.time = instance.time;
                cachedPlayableDirectorState.state = instance.state;

                Sync(cachedPlayableDirectorState);
                return;
            }
        }

        [ClusterRPC(RPCExecutionStage.BeforeFixedUpdate)]
        public void Sync (PlayableDirectorState playableDirectorState)
        {
            if (ClusterDisplayState.IsEmitter)
                return;

            var instance = playableDirector;
            if (instance == null)
                return;

            if (instance.enabled != playableDirectorState.enabled)
                instance.enabled = playableDirectorState.enabled;

            if (instance.extrapolationMode != playableDirectorState.extrapolationMode)
                instance.extrapolationMode = playableDirectorState.extrapolationMode;

            if (instance.initialTime != playableDirectorState.initialTime)
                instance.initialTime = playableDirectorState.initialTime;

            if (instance.timeUpdateMode != DirectorUpdateMode.Manual)
                instance.timeUpdateMode = DirectorUpdateMode.Manual;

            if (instance.time != playableDirectorState.time)
            {
                instance.time = playableDirectorState.time;
                instance.Evaluate();
            }
        }
    }
}
