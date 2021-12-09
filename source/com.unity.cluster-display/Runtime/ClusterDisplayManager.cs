using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Make sure ClusterRenderer executes late.
    public class ClusterDisplayManager : SingletonMonoBehaviour<ClusterDisplayManager>
    {
        public delegate void ClusterDisplayBehaviourDelegate();

        public delegate void ClusterDisplayOnFrameRenderDelegate(ScriptableRenderContext context, Camera[] cameras);

        public delegate void ClusterDisplayOnCameraRenderDelegate(ScriptableRenderContext context, Camera camera);

        public static ClusterDisplayBehaviourDelegate preInitialize;
        public static ClusterDisplayBehaviourDelegate awake;
        public static ClusterDisplayBehaviourDelegate start;
        public static ClusterDisplayBehaviourDelegate onEnable;
        public static ClusterDisplayBehaviourDelegate onDisable;
        public static ClusterDisplayBehaviourDelegate onDestroy;
        public static ClusterDisplayBehaviourDelegate onApplicationQuit;
        public static ClusterDisplayBehaviourDelegate update;
        public static ClusterDisplayBehaviourDelegate lateUpdate;
        public static ClusterDisplayBehaviourDelegate onDrawGizmos;

        public static ClusterDisplayBehaviourDelegate onBeforePresent;
        private Coroutine endOfFrameCoroutine;

        public static ClusterDisplayOnFrameRenderDelegate onBeginFrameRender;
        public static ClusterDisplayOnCameraRenderDelegate onBeginCameraRender;
        public static ClusterDisplayOnCameraRenderDelegate onEndCameraRender;
        public static ClusterDisplayOnFrameRenderDelegate onEndFrameRender;

        private void RegisterRenderPipelineDelegates ()
        {
            UnregisterRenderPipelineDelegates();

            RenderPipelineManager.beginFrameRendering += OnBeginFrameRender;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRender;
            RenderPipelineManager.endFrameRendering += OnEndFrameRender;
        }

        private void UnregisterRenderPipelineDelegates ()
        {
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRender;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRender;
            RenderPipelineManager.endFrameRendering -= OnEndFrameRender;
        }
        
        protected override void OnAwake()
        {
            ClusterDebug.Log("Cluster Display started bootstrap.");

            RegisterRenderPipelineDelegates();
            endOfFrameCoroutine = StartCoroutine(BeforePresentCoroutine());

            preInitialize?.Invoke();

            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
            
            awake?.Invoke();   
        }

        private void OnEnable() => onEnable?.Invoke();
        private void Start() => start?.Invoke();
        private void OnDisable() => onDisable?.Invoke();

        private void OnDestroy()
        {
            UnregisterRenderPipelineDelegates();
            onDestroy?.Invoke();
        }

        private void OnApplicationQuit() => onApplicationQuit?.Invoke();

        private void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) =>
            onBeginFrameRender?.Invoke(context, cameras);

        private void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) =>
            onBeginCameraRender?.Invoke(context, camera);

        private void OnEndCameraRender(ScriptableRenderContext context, Camera camera) =>
            onEndCameraRender?.Invoke(context, camera);

        private void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) =>
            onEndFrameRender?.Invoke(context, cameras);

        private void Update() => update?.Invoke();
        private void LateUpdate() => lateUpdate?.Invoke();
        private void OnDrawGizmos() => onDrawGizmos?.Invoke();

        private IEnumerator BeforePresentCoroutine ()
        {
            var waitForEndOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return waitForEndOfFrame;
                OnBeforePresent();
            }
        }

        private void OnBeforePresent() =>
            onBeforePresent?.Invoke();
    }
}