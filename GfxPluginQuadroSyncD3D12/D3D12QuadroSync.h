#pragma once

#define DEBUG_LOG_QUADRO_SYNC

#include "../External/NvAPI/nvapi.h"

class ID3D12Device;
class TD3D12DXGISwapChain;
class IDXGISwapChain;

namespace GfxQuadroSync {

    class PluginCSwapGroupClient
    {
    public:
        PluginCSwapGroupClient();
        ~PluginCSwapGroupClient();

        void Prepare();
        bool Initialize(ID3D12Device* pDevice, IDXGISwapChain* pSwapChain);
        void Dispose(ID3D12Device* pDevice, IDXGISwapChain* pSwapChain);

        void SetupWorkStation();/// const;
        void DisposeWorkStation();/// const;

        bool Render(ID3D12Device* pDevice, IDXGISwapChain* pSwapChain, int pVsync = 1, int pFlags = 0);// const;
        void ResetFrameCount(ID3D12Device* pDevice);
        NvU32 QueryFrameCount(ID3D12Device* pDevice);

        void EnableSystem(ID3D12Device* pDevice, IDXGISwapChain* pSwapChain, bool value);
        void EnableSwapGroup(ID3D12Device* pDevice, IDXGISwapChain* pSwapChain, bool value);
        void EnableSwapBarrier(ID3D12Device* pDevice, bool value);
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
        bool  m_Initialized;

#ifdef DEBUG_LOG_QUADRO_SYNC
        HANDLE m_fileDebug;
        void WriteFileDebug(const char* message, bool append = true);
#endif
    };
    
}
