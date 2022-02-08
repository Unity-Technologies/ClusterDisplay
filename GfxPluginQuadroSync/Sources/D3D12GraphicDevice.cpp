#include "D3D12GraphicDevice.h"

namespace GfxQuadroSync
{
    D3D12GraphicDevice::D3D12GraphicDevice(
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
