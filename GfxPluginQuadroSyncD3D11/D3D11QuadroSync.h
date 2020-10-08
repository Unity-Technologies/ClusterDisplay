#pragma once

#define DEBUG_LOG_QUADRO_SYNC

#include "../External/NvAPI/nvapi.h"

class ID3D11Device;
class IDXGISwapChain;

namespace GfxQuadroSync {

class PluginCSwapGroupClient
{
public:
    PluginCSwapGroupClient();
    ~PluginCSwapGroupClient();

    void Prepare();
    bool Initialize(ID3D11Device* pDevice, IDXGISwapChain* pSwapChain);
    void Dispose(ID3D11Device* pDevice, IDXGISwapChain* pSwapChain);

    void SetupWorkStation();
    void DisposeWorkStation();

    bool Render(ID3D11Device* pDevice, IDXGISwapChain* pSwapChain, int pVsync = 1, int pFlags = 0);
    void ResetFrameCount(ID3D11Device* pDevice);
    NvU32 QueryFrameCount(ID3D11Device* pDevice);

    void EnableSystem(ID3D11Device* pDevice, IDXGISwapChain* pSwapChain, bool value);
    void EnableSwapGroup(ID3D11Device* pDevice, IDXGISwapChain* pSwapChain, bool value);
    void EnableSwapBarrier(ID3D11Device* pDevice, bool value);
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

#ifdef DEBUG_LOG_QUADRO_SYNC
    HANDLE m_fileDebug;
    void WriteFileDebug(const char* const message, bool append = true);
#endif
};

}
