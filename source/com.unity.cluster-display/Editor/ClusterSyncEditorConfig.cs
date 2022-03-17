using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    // Before you change the name of this class, make sure you also change the referencing name in CommandLineParser.
    [CreateAssetMenu(fileName = "ClusterSyncEditorConfig", menuName = "Cluster Display/Cluster Sync Editor Config", order = 1)]
    [InitializeOnLoad]
    [Serializable]
    class ClusterSyncEditorConfig : SingletonScriptableObject<ClusterSyncEditorConfig>
    {
        // This is called by CommandLineParser in the editor only through reflection.
        [CommandLineParser.CommandLineInjectionMethod]
        private static void PollArguments ()
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
        }

        static ClusterSyncEditorConfig () => ClusterSync.onDisableCLusterDisplay += () => CommandLineParser.OverrideArguments(new List<string>());

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
