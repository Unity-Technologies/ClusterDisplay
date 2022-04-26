#pragma once

#include "d3d12.h"
#include "dxgi.h"
#include "IGraphicsDevice.h"

namespace GfxQuadroSync
{
    class D3D12GraphicsDevice final : public IGraphicsDevice
    {
    public:
        D3D12GraphicsDevice(
            ID3D12Device* device,
            IDXGISwapChain* swapChain,
            UINT32 interval,
            UINT presentFlags);

        virtual ~D3D12GraphicsDevice() = default;

        GraphicsDeviceType GetDeviceType() const override { return GraphicsDeviceType::GRAPHICS_DEVICE_D3D12; }

        IUnknown*       GetDevice() const override { return m_D3D12Device; }
        IDXGISwapChain* GetSwapChain() const override { return m_SwapChain; }
        UINT32          GetSyncInterval() const override { return m_SyncInterval; }
        UINT            GetPresentFlags() const override { return m_PresentFlags; }

        void SetDevice(IUnknown* const device) override { m_D3D12Device = static_cast<ID3D12Device*>(device); }
        void SetSwapChain(IDXGISwapChain* const swapChain) override { m_SwapChain = swapChain; }

    private:
        ID3D12Device* m_D3D12Device;
        IDXGISwapChain* m_SwapChain;
        UINT32 m_SyncInterval;
        UINT m_PresentFlags;
    };
}
