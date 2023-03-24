#include "D3D11GraphicsDevice.h"
#include "Logger.h"

namespace GfxQuadroSync
{
    ComSharedPtr<ID3D11Texture2D> GetBackBufferTexture(IDXGISwapChain* const swapChain)
    {
        ID3D11Texture2D* backBufferTexture;
        auto hr = swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&backBufferTexture));
        if (FAILED(hr))
        {
            CLUSTER_LOG_ERROR << "SaveToPresent failed to get swap chain buffer 0: " << hr;
            throw std::exception();
        }
        return ComSharedPtr<ID3D11Texture2D>(backBufferTexture);
    }

    ComSharedPtr<ID3D11RenderTargetView> CreateRenderTargetView(ID3D11Device* const device,
        const ComSharedPtr<ID3D11Texture2D>& texture)
    {
        ID3D11RenderTargetView* backBufferRenderTargetView;
        auto hr = device->CreateRenderTargetView(texture.get(), nullptr, &backBufferRenderTargetView);
        if (FAILED(hr))
        {
            CLUSTER_LOG_ERROR << "SaveToPresent failed to create RenderTargetView: " << hr;
            throw std::exception();
        }
        return ComSharedPtr<ID3D11RenderTargetView>(backBufferRenderTargetView);
    }

    ComSharedPtr<ID3D11Texture2D> CreateCompatibleTexture(ID3D11Device* const device,
        const ComSharedPtr<ID3D11Texture2D>& compatibleWith)
    {
        D3D11_TEXTURE2D_DESC backBufferCopyDesc;
        compatibleWith->GetDesc(&backBufferCopyDesc);
        backBufferCopyDesc.BindFlags = 0;

        ID3D11Texture2D* compatibleTexture;
        auto hr = device->CreateTexture2D(&backBufferCopyDesc, nullptr, &compatibleTexture);
        if (FAILED(hr))
        {
            CLUSTER_LOG_ERROR << "SaveToPresent failed to allocate copy of back buffer: " << hr;
            throw std::exception();
        }
        return ComSharedPtr<ID3D11Texture2D>(compatibleTexture);
    }

    D3D11GraphicsDevice::D3D11GraphicsDevice(
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

    void D3D11GraphicsDevice::InitiatePresentRepeats()
    {
        if (m_BackBufferTexture || m_BackBufferRenderTargetView || m_DeviceContext || m_SavedToPresent)
        {
            CLUSTER_LOG_ERROR << "SaveToPresent called multiple times without calling FreeSavedToPresent";
            return;
        }

        m_BackBufferTexture = GetBackBufferTexture(m_SwapChain);
        m_BackBufferRenderTargetView = CreateRenderTargetView(m_D3D11Device, m_BackBufferTexture);
        m_SavedToPresent = CreateCompatibleTexture(m_D3D11Device, m_BackBufferTexture);

        {
            ID3D11DeviceContext* deviceContext;
            m_D3D11Device->GetImmediateContext(&deviceContext);
            m_DeviceContext.reset(deviceContext);
        }
        ID3D11RenderTargetView* const renderTargetViews[] = {m_BackBufferRenderTargetView.get()};
        m_DeviceContext->OMSetRenderTargets(1, renderTargetViews, nullptr);

        m_DeviceContext->CopyResource(m_SavedToPresent.get(), m_BackBufferTexture.get());
    }

    void D3D11GraphicsDevice::PrepareSinglePresentRepeat()
    {
        if (m_DeviceContext && m_BackBufferTexture && m_SavedToPresent)
        {
            m_DeviceContext->CopyResource(m_BackBufferTexture.get(), m_SavedToPresent.get());
        }
    }

    void D3D11GraphicsDevice::ConcludePresentRepeats()
    {
        m_DeviceContext.reset();
        m_SavedToPresent.reset();
        m_BackBufferRenderTargetView.reset();
        m_BackBufferTexture.reset();
    }
}
