#pragma once

#include "d3d11.h"
#include "dxgi.h"
#include "IGraphicsDevice.h"

namespace GfxQuadroSync
{
    class D3D11GraphicsDevice final : public IGraphicsDevice
    {
    public:
        D3D11GraphicsDevice(
            ID3D11Device* device, 
            IDXGISwapChain* swapChain,
            UINT32 interval,
            UINT presentFlags);

        virtual ~D3D11GraphicsDevice() = default;

        inline GraphicsDeviceType GetDeviceType() const { return GraphicsDeviceType::GRAPHICS_DEVICE_D3D11; }
        
        inline IUnknown*       GetDevice() const { return m_D3D11Device; }
        inline IDXGISwapChain* GetSwapChain() const { return m_SwapChain; }
        inline UINT32          GetSyncInterval() const { return m_SyncInterval; }
        inline UINT            GetPresentFlags() const { return m_PresentFlags; }

        inline void SetDevice(IUnknown* const device) { m_D3D11Device = static_cast<ID3D11Device*>(device); }
        inline void SetSwapChain(IDXGISwapChain* const swapChain) { m_SwapChain = swapChain; }

    private:
        ID3D11Device* m_D3D11Device;
        IDXGISwapChain* m_SwapChain;        
        UINT32 m_SyncInterval;
        UINT m_PresentFlags;
    };
}
