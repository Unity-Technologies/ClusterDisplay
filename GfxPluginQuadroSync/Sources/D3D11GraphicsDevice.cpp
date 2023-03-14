#include "D3D11GraphicsDevice.h"
#include "Logger.h"

namespace GfxQuadroSync
{
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

    D3D11GraphicsDevice::~D3D11GraphicsDevice()
    {
        FreeSavedToPresent();
    }

    void D3D11GraphicsDevice::SaveToPresent()
    {
        if (m_BackBufferTexture != nullptr || m_BackBufferRenderTargetView != nullptr || m_DeviceContext != nullptr ||
            m_SavedToPresent != nullptr)
        {
            CLUSTER_LOG_ERROR << "SaveToPresent called multiple times without calling FreeSavedToPresent";
            return;
        }

        HRESULT hr = m_SwapChain->GetBuffer(0, __uuidof(ID3D11Texture2D),
            reinterpret_cast<void**>(&m_BackBufferTexture));
        if (FAILED(hr))
        {
            CLUSTER_LOG_ERROR << "SaveToPresent failed to get swap chain buffer 0: " << hr;
            return;
        }

        hr = m_D3D11Device->CreateRenderTargetView(m_BackBufferTexture, nullptr, &m_BackBufferRenderTargetView);
        if (FAILED(hr))
        {
            CLUSTER_LOG_ERROR << "SaveToPresent failed to create RenderTargetView: " << hr;
            return;
        }

        m_D3D11Device->GetImmediateContext(&m_DeviceContext);
        m_DeviceContext->OMSetRenderTargets(1, &m_BackBufferRenderTargetView, nullptr);

        D3D11_TEXTURE2D_DESC backBufferCopyDesc;
        m_BackBufferTexture->GetDesc(&backBufferCopyDesc);
        backBufferCopyDesc.BindFlags = 0;
        hr = m_D3D11Device->CreateTexture2D(&backBufferCopyDesc, nullptr, &m_SavedToPresent);
        if (FAILED(hr))
        {
            CLUSTER_LOG_ERROR << "SaveToPresent failed to allocate copy of back buffer: " << hr;
            return;
        }

        m_DeviceContext->CopyResource(m_SavedToPresent, m_BackBufferTexture);
    }

    void D3D11GraphicsDevice::RepeatSavedToPresent()
    {
        if (m_DeviceContext != nullptr && m_BackBufferTexture != nullptr && m_SavedToPresent != nullptr)
        {
            m_DeviceContext->CopyResource(m_BackBufferTexture, m_SavedToPresent);
        }
    }

    void D3D11GraphicsDevice::FreeSavedToPresent()
    {
        if (m_DeviceContext != nullptr)
        {
            m_DeviceContext->Release();
            m_DeviceContext = nullptr;
        }
        if (m_SavedToPresent != nullptr)
        {
            m_SavedToPresent->Release();
            m_SavedToPresent = nullptr;
        }
        if (m_BackBufferRenderTargetView != nullptr)
        {
            m_BackBufferRenderTargetView->Release();
            m_BackBufferRenderTargetView = nullptr;
        }
        if (m_BackBufferTexture != nullptr)
        {
            m_BackBufferTexture->Release();
            m_BackBufferTexture = nullptr;
        }
    }
}
