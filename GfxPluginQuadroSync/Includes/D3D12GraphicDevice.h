#pragma once

#include "d3d12.h"
#include "dxgi.h"
#include "IGraphicsDevice.h"

namespace GfxQuadroSync
{
    class D3D12GraphicDevice final : public IGraphicDevice
    {
    public:
        D3D12GraphicDevice(
            ID3D12Device* device, 
            IDXGISwapChain* swapChain, 
            UINT32 interval, 
            UINT presentFlags);

        virtual ~D3D12GraphicDevice() = default;

        inline GraphicDeviceType GetDeviceType() const { return GraphicDeviceType::GRAPHICS_DEVICE_D3D12; }

        inline IUnknown*       GetDevice() const { return m_D3D12Device; }
        inline IDXGISwapChain* GetSwapChain() const { return m_SwapChain; }
        inline UINT32          GetSyncInterval() const { return m_SyncInterval; }
        inline UINT            GetPresentFlags() const { return m_PresentFlags; }

        inline void SetDevice(IUnknown* const device) { m_D3D12Device = static_cast<ID3D12Device*>(device); }
        inline void SetSwapChain(IDXGISwapChain* const swapChain) { m_SwapChain = swapChain; }

    private:
        ID3D12Device* m_D3D12Device;
        IDXGISwapChain* m_SwapChain;
        UINT32 m_SyncInterval;
        UINT m_PresentFlags;
    };
}
