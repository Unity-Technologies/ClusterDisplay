using System.Collections;
using JetBrains.Annotations;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Make sure ClusterRenderer executes late.
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    public class ClusterDisplayManager : SingletonMonoBehaviour<ClusterDisplayManager>
    {
        [SerializeField]
        [HideInInspector]
        private Camera m_ActiveCamera;

        public static Camera ActiveCamera
        {
            get
            {
                if (!TryGetInstance(out var instance, logError: false))
                    return null;
                return instance.m_ActiveCamera;
            }
        }

        static internal void SetActiveCamera(Camera camera)
        {
            if (!TryGetInstance(out var instance, logError: false))
                return;

            if (instance.m_ActiveCamera == camera)
                return;

            ClusterDebug.Log($"Switching active camera from: \"{(camera != null ? camera.gameObject.name : "NULL")}\" to camera: \"{camera.gameObject.name}\".");
            onChangeActiveCamera?.Invoke(instance.m_ActiveCamera, camera);
            instance.m_ActiveCamera = camera;
        }

        public delegate void OnChangeActiveCamera(Camera previousCamera, Camera newCamera);

        public delegate void ClusterDisplayBehaviourDelegate();

        public delegate void ClusterDisplayOnFrameRenderDelegate(ScriptableRenderContext context, Camera[] cameras);

        public delegate void ClusterDisplayOnCameraRenderDelegate(ScriptableRenderContext context, Camera camera);

        static ClusterSync GetOrCreateClusterSyncInstance()
        {
            if (ClusterSyncInstance is not {} instance)
            {
                // Creating ClusterSync instance on demand.
                ClusterDebug.Log($"Creating instance of: {nameof(ClusterSync)} on demand.");
                instance = new ClusterSync();
                ServiceLocator.Provide<IClusterSyncState>(instance);
            }

            Debug.Assert(instance != null);
            return instance;
        }

        internal static ClusterSync ClusterSyncInstance =>
            ServiceLocator.TryGet(out IClusterSyncState instance) ? instance as ClusterSync : null;

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

        public static OnChangeActiveCamera onChangeActiveCamera;
        public static ClusterDisplayOnFrameRenderDelegate onBeginFrameRender;
        public static ClusterDisplayOnCameraRenderDelegate onBeginCameraRender;
        public static ClusterDisplayOnCameraRenderDelegate onEndCameraRender;
        public static ClusterDisplayOnFrameRenderDelegate onEndFrameRender;

        private void RegisterRenderPipelineDelegates()
        {
            UnregisterRenderPipelineDelegates();

            ClusterDebug.Log("Registering render pipeline delegates.");

            RenderPipelineManager.beginFrameRendering += OnBeginFrameRender;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRender;
            RenderPipelineManager.endFrameRendering += OnEndFrameRender;
        }

        private void UnregisterRenderPipelineDelegates()
        {
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRender;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRender;
            RenderPipelineManager.endFrameRendering -= OnEndFrameRender;
        }

        protected override void OnAwake()
        {
            GetOrCreateClusterSyncInstance();

            ClusterDebug.Log("Cluster Display started bootstrap.");
            endOfFrameCoroutine = StartCoroutine(BeforePresentCoroutine());

            preInitialize?.Invoke();

            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            awake?.Invoke();
        }

        private void OnEnable()
        {
            SetInstance(this);
            RegisterRenderPipelineDelegates();
            GetOrCreateClusterSyncInstance().EnableClusterDisplay();
            onEnable?.Invoke();
        }

        private void Start() => start?.Invoke();

        private void OnDisable()
        {
            ClusterSyncInstance?.DisableClusterDisplay();
            onDisable?.Invoke();
        }

        private void OnDestroy()
        {
            UnregisterRenderPipelineDelegates();
            onDestroy?.Invoke();
        }

        private void OnApplicationQuit()
        {
            ClusterSyncInstance?.ShutdownAllClusterNodes();
            onApplicationQuit?.Invoke();
        }

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

        private IEnumerator BeforePresentCoroutine()
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
