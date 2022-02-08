#include "D3D11GraphicDevice.h"

namespace GfxQuadroSync
{
    D3D11GraphicDevice::D3D11GraphicDevice(
        ID3D11Device* const device, 
        IDXGISwapChain* const swapChain, 
        const UINT32 syncInterval, 
        const UINT presentFlags)
        : m_D3D11Device(device)
        , m_SwapChain(swapChain)
        , m_SyncInterval(syncInterval)
        , m_PresentFlags(presentFlags)
    {
    }
}
