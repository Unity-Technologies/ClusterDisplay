#pragma once

#include "../../External/NvAPI/nvapi.h"

class ID3D11Device;
class IDXGISwapChain;

namespace GfxQuadroSync
{
    class PluginCSwapGroupClient
    {
    public:
        PluginCSwapGroupClient();
        ~PluginCSwapGroupClient();

        void Prepare();
        bool Initialize(IUnknown* pDevice, IDXGISwapChain* pSwapChain);
        void Dispose(IUnknown* pDevice, IDXGISwapChain* pSwapChain);

        void SetupWorkStation();
        void DisposeWorkStation();

        bool Render(IUnknown* pDevice, IDXGISwapChain* pSwapChain, int pVsync = 1, int pFlags = 0);
        void ResetFrameCount(IUnknown* pDevice);
        NvU32 QueryFrameCount(IUnknown* pDevice);

        void EnableSystem(IUnknown* pDevice, IDXGISwapChain* pSwapChain, bool value);
        void EnableSwapGroup(IUnknown* pDevice, IDXGISwapChain* pSwapChain, bool value);
        void EnableSwapBarrier(IUnknown* pDevice, bool value);
        void EnableSyncCounter(const bool value);

    private:
        NvU32 m_GroupId;
        NvU32 m_BarrierId;
        NvU32 m_FrameCount;
        NvU32 m_GSyncSwapGroups;
        NvU32 m_GSyncBarriers;
        bool  m_GSyncMaster;
        bool  m_GSyncCounter;
        bool  m_IsActive;
    };

}
