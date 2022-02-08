#include "D3D12GraphicsDevice.h"

namespace GfxQuadroSync
{
    D3D12GraphicsDevice::D3D12GraphicsDevice(
        ID3D12Device* const device,
        IDXGISwapChain* const swapChain,
        const UINT32 syncInterval,
        const UINT presentFlags)
        : m_D3D12Device(device)
        , m_SwapChain(swapChain)
        , m_SyncInterval(syncInterval)
        , m_PresentFlags(presentFlags)
    {
    }
}
