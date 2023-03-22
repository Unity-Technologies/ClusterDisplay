#pragma once

#include "d3d11.h"
#include "dxgi.h"
#include "IGraphicsDevice.h"
#include "ComHelpers.h"

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

        virtual ~D3D11GraphicsDevice();

        GraphicsDeviceType GetDeviceType() const override { return GraphicsDeviceType::GRAPHICS_DEVICE_D3D11; }

        IUnknown*       GetDevice() const override { return m_D3D11Device; }
        IDXGISwapChain* GetSwapChain() const override { return m_SwapChain; }
        UINT32          GetSyncInterval() const override { return m_SyncInterval; }
        UINT            GetPresentFlags() const override { return m_PresentFlags; }

        void SetDevice(IUnknown* const device) override { m_D3D11Device = static_cast<ID3D11Device*>(device); }
        void SetSwapChain(IDXGISwapChain* const swapChain) override { m_SwapChain = swapChain; }

        void InitiatePresentRepeats() override;
        void PrepareSinglePresentRepeat() override;
        void ConcludePresentRepeats() override;

    private:
        ID3D11Device* m_D3D11Device;
        IDXGISwapChain* m_SwapChain;
        UINT32 m_SyncInterval;
        UINT m_PresentFlags;

        ComSharedPtr<ID3D11Texture2D> m_BackBufferTexture;
        ComSharedPtr<ID3D11RenderTargetView> m_BackBufferRenderTargetView;
        ComSharedPtr<ID3D11Texture2D> m_SavedToPresent;
        ComSharedPtr<ID3D11DeviceContext> m_DeviceContext;
    };
}
