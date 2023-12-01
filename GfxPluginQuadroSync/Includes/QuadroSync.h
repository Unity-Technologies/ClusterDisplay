#pragma once

#include "../External/NvAPI/nvapi.h"
#include "../Unity/IUnityInterface.h"

#include <atomic>
#include <cstdint>

class ID3D11Device;
class IDXGISwapChain;

namespace GfxQuadroSync
{
    class IGraphicsDevice;

    class PluginCSwapGroupClient
    {
    public:
        PluginCSwapGroupClient();
        ~PluginCSwapGroupClient();

        enum class InitializeStatus
        {
            Success,
            Failed,
            NoSwapGroupDetected,
            QuerySwapGroupFailed,
            FailedToJoinSwapGroup,
            SwapGroupMismatch,
            FailedToBindSwapBarrier,
            SwapBarrierIdMismatch,
        };

        void Prepare();
        InitializeStatus Initialize(IUnknown* pDevice, IDXGISwapChain* pSwapChain);
        void Dispose(IUnknown* pDevice, IDXGISwapChain* pSwapChain);

        void SetupWorkStation();
        void DisposeWorkStation();

        bool Render(IGraphicsDevice* pGraphicsDevice);
        void SkipSynchronizedPresentOfNextFrame() { m_SkipSynchronizedPresentOfNextFrame = true; }
        void ResetFrameCount(IUnknown* pDevice);
        NvU32 QueryFrameCount(IUnknown* pDevice);

        void EnableSystem(IUnknown* pDevice, IDXGISwapChain* pSwapChain, bool value);
        void EnableSwapGroup(IUnknown* pDevice, IDXGISwapChain* pSwapChain, bool value);
        NvU32 GetSwapGroupId() const { return m_GroupId.load(std::memory_order_relaxed); }
        void EnableSwapBarrier(IUnknown* pDevice, bool value);
        NvU32 GetSwapBarrierId() const { return m_BarrierId.load(std::memory_order_relaxed); }
        void EnableSyncCounter(const bool value);

        uint64_t GetPresentSuccessCount() const { return m_PresentSuccessCount.load(std::memory_order_relaxed); }
        uint64_t GetPresentFailureCount() const { return m_PresentFailureCount.load(std::memory_order_relaxed); }

        enum class BarrierWarmupAction
        {
            RepeatPresent,
            ContinueToNextFrame,
            BarrierWarmedUp,
        };

        // Type of callback to a managed function that is called after the first present when barrier is active.
        typedef BarrierWarmupAction(UNITY_INTERFACE_API* BarrierWarmupCallback)();

        void SetBarrierWarmupCallback(BarrierWarmupCallback callback)
        {
            m_BarrierWarmupCallback = callback ? callback : &EmptyBarrierWarmupCallback;
        }

    private:
        static BarrierWarmupAction EmptyBarrierWarmupCallback() { return BarrierWarmupAction::ContinueToNextFrame; }

        // Remarks: Some variables are atomic because they can be accessed from the rendering thread or the game loop
        // thread for the implementation of the GetState function.  There is no need for a strong correlation between
        // each of the variables since the GetState function is only for reporting the state, so using atomic is enough
        // (and faster than a mutex).
        std::atomic<NvU32> m_GroupId = 1;
        std::atomic<NvU32> m_BarrierId = 1;
        NvU32 m_FrameCount = 0;
        NvU32 m_GSyncSwapGroups = 0;
        NvU32 m_GSyncBarriers = 0;
        bool m_GSyncMaster = true;
        bool m_GSyncCounter = false;
        bool m_IsActive = false;
        bool m_NeedToWarmUpBarrier = false;
        bool m_SkipSynchronizedPresentOfNextFrame = false;
        std::atomic<uint64_t> m_PresentSuccessCount = 0;
        std::atomic<uint64_t> m_PresentFailureCount = 0;
        BarrierWarmupCallback m_BarrierWarmupCallback = &EmptyBarrierWarmupCallback;
    };

}
