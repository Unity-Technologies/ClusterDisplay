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
    }

    void D3D11GraphicsDevice::InitiatePresentRepeats()
    {
        if (m_BackBufferTexture || m_BackBufferRenderTargetView || m_DeviceContext || m_SavedToPresent)
        {
            CLUSTER_LOG_ERROR << "SaveToPresent called multiple times without calling FreeSavedToPresent";
            return;
        }

        {
            ID3D11Texture2D* backBufferTexture;
            HRESULT hr = m_SwapChain->GetBuffer(0, __uuidof(ID3D11Texture2D),
                reinterpret_cast<void**>(&backBufferTexture));
            if (FAILED(hr))
            {
                CLUSTER_LOG_ERROR << "SaveToPresent failed to get swap chain buffer 0: " << hr;
                return;
            }
            m_BackBufferTexture.reset(backBufferTexture);
        }

        {
            ID3D11RenderTargetView* backBufferRenderTargetView;
            HRESULT hr = m_D3D11Device->CreateRenderTargetView(m_BackBufferTexture.get(), nullptr,
                &backBufferRenderTargetView);
            if (FAILED(hr))
            {
                CLUSTER_LOG_ERROR << "SaveToPresent failed to create RenderTargetView: " << hr;
                return;
            }
            m_BackBufferRenderTargetView.reset(backBufferRenderTargetView);
        }

        {
            ID3D11DeviceContext* deviceContext;
            m_D3D11Device->GetImmediateContext(&deviceContext);
            m_DeviceContext.reset(deviceContext);
        }
        ID3D11RenderTargetView* const renderTargetViews[] = {m_BackBufferRenderTargetView.get()};
        m_DeviceContext->OMSetRenderTargets(1, renderTargetViews, nullptr);

        D3D11_TEXTURE2D_DESC backBufferCopyDesc;
        m_BackBufferTexture->GetDesc(&backBufferCopyDesc);
        backBufferCopyDesc.BindFlags = 0;
        {
            ID3D11Texture2D* savedToPresent;
            HRESULT hr = m_D3D11Device->CreateTexture2D(&backBufferCopyDesc, nullptr, &savedToPresent);
            if (FAILED(hr))
            {
                CLUSTER_LOG_ERROR << "SaveToPresent failed to allocate copy of back buffer: " << hr;
                return;
            }
            m_SavedToPresent.reset(savedToPresent);
        }

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
