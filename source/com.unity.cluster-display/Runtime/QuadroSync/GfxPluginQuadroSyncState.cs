using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Initialization state of the GfxPluginQuadroSyncSystem plugin.
    /// </summary>
    public enum GfxPluginQuadroSyncInitializationState
    {
        /// <summary>
        /// QuadroSync is not currently initialized.
        /// </summary>
        NotInitialized = 0,
        /// <summary>
        /// QuadroSync is initialized and should be usable.
        /// </summary>
        Initialized = 1,
        /// <summary>
        /// Initialization failed because the received Unity interfaces was null.
        /// </summary>
        FailedUnityInterfacesNull = 2,
        /// <summary>
        /// Initialization failed because of an unsupported graphic api.
        /// </summary>
        UnsupportedGraphicApi = 3,
        /// <summary>
        /// Failed to get the ID3D[11|12]Device.
        /// </summary>
        MissingDevice = 4,
        /// <summary>
        /// Failed to get the IDXGISwapChain.
        /// </summary>
        MissingSwapChain = 5,
        /// <summary>
        /// Initialization failed because of a generic error when trying to setup swap chain or barrier for QuadroSync.
        /// </summary>
        SwapChainOrBarrierGenericFailure = 6,
        /// <summary>
        /// Initialization failed because no swap group support was detect.  Is the hardware present?
        /// </summary>
        NoSwapGroupDetected = 7,
        /// <summary>
        /// Initialization failed because of a problem querying information about swap groups.
        /// </summary>
        QuerySwapGroupFailed = 8,
        /// <summary>
        /// Initialization failed because there was a failure joining the swap group.
        /// </summary>
        FailedToJoinSwapGroup = 9,
        /// <summary>
        /// Initialization failed because a mismatch was detected between the swap group identifier and the available
        /// swap groups.
        /// </summary>
        SwapGroupMismatch = 10,
        /// <summary>
        /// Initialization failed because there was a failure joining the swap barrier.
        /// </summary>
        FailedToBindSwapBarrier = 11,
        /// <summary>
        /// Initialization failed because a mismatch was detected between the swap barrier identifier and the available
        /// swap barriers.
        /// </summary>
        SwapBarrierIdMismatch = 12,
    }

    public static class GfxPluginQuadroSyncInitializationStateExtension
    {
        /// <summary>
        /// Returns a short descriptive text for the specified <see cref="GfxPluginQuadroSyncInitializationState"/>.
        /// </summary>
        /// <param name="enumValue">The enum constant</param>
        /// <returns>The short descriptive text of the enum or "Unknown initialization state" if an unknown enum
        /// constant is received.</returns>
        public static string ToDescriptiveText(this GfxPluginQuadroSyncInitializationState enumValue) =>
            enumValue switch
            {
                GfxPluginQuadroSyncInitializationState.NotInitialized => "Not initialized",
                GfxPluginQuadroSyncInitializationState.Initialized => "Initialized",
                GfxPluginQuadroSyncInitializationState.FailedUnityInterfacesNull => "Unity interfaces null",
                GfxPluginQuadroSyncInitializationState.UnsupportedGraphicApi => "Unsupported graphic api",
                GfxPluginQuadroSyncInitializationState.MissingDevice => "Failed to get the ID3D[11|12]Device",
                GfxPluginQuadroSyncInitializationState.MissingSwapChain => "Failed to get the IDXGISwapChain",
                GfxPluginQuadroSyncInitializationState.SwapChainOrBarrierGenericFailure => "Error during setup of swap chain or barrier",
                GfxPluginQuadroSyncInitializationState.NoSwapGroupDetected => "No swap group detect (is the hardware present?)",
                GfxPluginQuadroSyncInitializationState.QuerySwapGroupFailed => "Failed to query information about swap groups",
                GfxPluginQuadroSyncInitializationState.FailedToJoinSwapGroup => "Failed to join swap group",
                GfxPluginQuadroSyncInitializationState.SwapGroupMismatch => "Mismatch between swap group identifier and available swap groups",
                GfxPluginQuadroSyncInitializationState.FailedToBindSwapBarrier => "Failed to joining swap barrier",
                GfxPluginQuadroSyncInitializationState.SwapBarrierIdMismatch => "Failed to joining swap barrier",
                _ => "Unknown initialization state",
            };
    }

    /// <summary>
    /// Status of the QuadroSync plugin as returned by <see cref="GfxPluginQuadroSyncSystem.FetchState"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GfxPluginQuadroSyncState
    {
        /// <summary>
        /// Initialization status of the QuadroSync system
        /// </summary>
        public GfxPluginQuadroSyncInitializationState InitializationState { get; }
        /// <summary>
        /// Swap group identifier
        /// </summary>
        public uint SwapGroupId { get; }
        /// <summary>
        /// Swap barrier identifier
        /// </summary>
        public uint SwapBarrierId { get; }
        /// <summary>
        ///  Number of frames successfully presented using QuadroSync's present call
        /// </summary>
        public ulong PresentedFramesSuccess { get; }
        /// <summary>
        /// Number of frames that failed to be presented using QuadroSync's present call
        /// </summary>
        public ulong PresentedFramesFailure { get; }
    }
}
