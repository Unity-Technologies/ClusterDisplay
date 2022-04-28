#pragma once

#include "../../External/NvAPI/nvapi.h"

#include <cstdint>

class ID3D11Device;
class IDXGISwapChain;

namespace GfxQuadroSync
{
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

        bool Render(IUnknown* pDevice, IDXGISwapChain* pSwapChain, int pVsync = 1, int pFlags = 0);
        void ResetFrameCount(IUnknown* pDevice);
        NvU32 QueryFrameCount(IUnknown* pDevice);

        void EnableSystem(IUnknown* pDevice, IDXGISwapChain* pSwapChain, bool value);
        void EnableSwapGroup(IUnknown* pDevice, IDXGISwapChain* pSwapChain, bool value);
        NvU32 GetSwapGroupId() const { return m_GroupId; }
        void EnableSwapBarrier(IUnknown* pDevice, bool value);
        NvU32 GetSwapBarrierId() const { return m_BarrierId; }
        void EnableSyncCounter(const bool value);

        uint64_t GetPresentSuccessCount() const { return m_presentSuccessCount; }
        uint64_t GetPresentFailureCount() const { return m_presentFailureCount; }

    private:
        NvU32 m_GroupId = 1;
        NvU32 m_BarrierId = 1;
        NvU32 m_FrameCount = 0;
        NvU32 m_GSyncSwapGroups = 0;
        NvU32 m_GSyncBarriers = 0;
        bool  m_GSyncMaster = true;
        bool  m_GSyncCounter = false;
        bool  m_IsActive = false;
        uint64_t m_presentSuccessCount = 0;
        uint64_t m_presentFailureCount = 0;
    };

}
