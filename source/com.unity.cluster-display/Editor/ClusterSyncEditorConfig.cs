using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ClusterSyncEditorConfigField : Attribute {}

    [CreateAssetMenu(fileName = "ClusterSyncEditorConfig", menuName = "Cluster Display/Cluster Sync Editor Config", order = 1)]
    [InitializeOnLoad]
    [Serializable]
    public class ClusterSyncEditorConfig : SingletonScriptableObject<ClusterSyncEditorConfig>
    {
        static ClusterSyncEditorConfig ()
        {
            CommandLineParser.onPollArguments += (List<string> m_Arguments) =>
            {
                if (!TryGetInstance(out var editorConfig))
                {
                    return;
                }

                if (EditorApplication.isPlayingOrWillChangePlaymode && editorConfig.m_IgnoreEditorCmdLine)
                {
                    m_Arguments.AddRange(ClusterDisplayState.IsEmitter
                        ? editorConfig.m_EditorInstanceEmitterCmdLine.Split(' ')
                        : editorConfig.m_EditorInstanceRepeaterCmdLine.Split(' '));
                }
            };

            // Grab command line args related to cluster config
            ClusterSync.onPreEnableClusterDisplay += () =>
            {
                if (!TryGetInstance(out var editorConfig))
                {
                    return;
                }

                if (!editorConfig.m_IgnoreEditorCmdLine)
                {
                    var editorInstanceCmdLine = editorConfig.m_EditorInstanceIsEmitter ? 
                        editorConfig.m_EditorInstanceEmitterCmdLine : 
                        editorConfig.m_EditorInstanceRepeaterCmdLine;

                    CommandLineParser.OverrideArguments(editorInstanceCmdLine.Split(' ').ToList()); 
                }

                if (editorConfig.m_UseTargetFramerate)
                    Application.targetFrameRate = editorConfig.m_TargetFrameRate;
            };
        }

        [UnityEngine.Serialization.FormerlySerializedAs("m_EditorInstanceIsMaster")]
        [SerializeField] public bool m_EditorInstanceIsEmitter;
        
        [UnityEngine.Serialization.FormerlySerializedAs("m_EditorInstanceMasterCmdLine")]
        [SerializeField] public string m_EditorInstanceEmitterCmdLine;
        [UnityEngine.Serialization.FormerlySerializedAs("m_EditorInstanceSlaveCmdLine")]
        [SerializeField] public string m_EditorInstanceRepeaterCmdLine;

        [SerializeField] public bool m_IgnoreEditorCmdLine;
        public void SetupForEditorTesting(bool isEmitter) => m_EditorInstanceIsEmitter = isEmitter;
        public void UseEditorCmd(bool useEditorCmd) => m_IgnoreEditorCmdLine = !useEditorCmd;

        [SerializeField] public bool m_UseTargetFramerate;
        [SerializeField] public int m_TargetFrameRate;

        public ClusterSyncEditorConfig (bool editorInstanceIsEmitter)
        {
            m_EditorInstanceIsEmitter = editorInstanceIsEmitter;
            m_EditorInstanceEmitterCmdLine = "-emitterNode 0 1 224.0.1.0:25691,25692";
            m_EditorInstanceRepeaterCmdLine = "-node 1 224.0.1.0:25692,25691";

            m_IgnoreEditorCmdLine = false;
            m_UseTargetFramerate = false;
            m_TargetFrameRate = 60;
        }
    }
}
