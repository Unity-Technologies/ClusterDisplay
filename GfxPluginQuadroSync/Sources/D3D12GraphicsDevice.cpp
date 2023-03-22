#include "D3D12GraphicsDevice.h"
#include "Logger.h"

#include <dxgi1_4.h>

namespace GfxQuadroSync
{
    D3D12GraphicsDevice::D3D12GraphicsDevice(
        ID3D12Device* const device,
        IDXGISwapChain* const swapChain,
        ID3D12CommandQueue* const commandQueue,
        const UINT32 syncInterval,
        const UINT presentFlags)
        : m_D3D12Device(device)
        , m_CommandQueue(commandQueue)
        , m_SyncInterval(syncInterval)
        , m_PresentFlags(presentFlags)
    {
        SetSwapChain(swapChain);
    }

    D3D12GraphicsDevice::~D3D12GraphicsDevice()
    {
    }

    IDXGISwapChain* D3D12GraphicsDevice::GetSwapChain() const
    {
        return m_SwapChain.get();
    }

    void D3D12GraphicsDevice::SetSwapChain(IDXGISwapChain* const swapChain)
    {
        IDXGISwapChain3* swapChain3;
        HRESULT hr = swapChain->QueryInterface<IDXGISwapChain3>(&swapChain3);
        if (FAILED(hr))
        {
            CLUSTER_LOG_ERROR << "IDXGISwapChain::QueryInterface IDXGISwapChain3 failed: " << hr;
            return;
        }

        m_SwapChain.reset(swapChain3);
    }

    void D3D12GraphicsDevice::InitiatePresentRepeats()
    {
        if (!m_SwapChain)
        {
            return;
        }

        if (m_CommandAllocator || m_CommandList || !m_BackBuffers.empty() || m_SavedTexture)
        {
            CLUSTER_LOG_ERROR << "SaveToPresent called multiple times without calling FreeSavedToPresent";
            return;
        }

        // Create CommandAllocator
        {
            ID3D12CommandAllocator* commandAllocator;
            HRESULT hr = m_D3D12Device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT,
                __uuidof(ID3D12CommandAllocator), reinterpret_cast<void**>(&commandAllocator));
            if (FAILED(hr))
            {
                CLUSTER_LOG_ERROR << "ID3D12Device::CreateCommandAllocator failed: " << hr;
                return;
            }
            m_CommandAllocator.reset(commandAllocator);
        }
        m_CommandAllocator->SetName(L"GfxPluginQuadroSync CommandAllocator");

        // Create CommandList
        {
            ID3D12GraphicsCommandList* commandList;
            HRESULT hr = m_D3D12Device->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, m_CommandAllocator.get(),
                nullptr, __uuidof(ID3D12GraphicsCommandList), reinterpret_cast<void**>(&commandList));
            if (FAILED(hr))
            {
                CLUSTER_LOG_ERROR << "ID3D12Device::CreateCommandList failed: " << hr;
                return;
            }
            m_CommandList.reset(commandList);
        }
        m_CommandList->SetName(L"GfxPluginQuadroSync CommandList");

        // Get the back buffers
        DXGI_SWAP_CHAIN_DESC1 swapChainDesc;
        HRESULT hr = m_SwapChain->GetDesc1(&swapChainDesc);
        if (FAILED(hr))
        {
            CLUSTER_LOG_ERROR << "IDXGISwapChain1::GetDesc1 failed: " << hr;
            return;
        }
        for (UINT backBufferIndex = 0; backBufferIndex < swapChainDesc.BufferCount; ++backBufferIndex)
        {
            ID3D12Resource* backBuffer;
            hr = m_SwapChain->GetBuffer(backBufferIndex, __uuidof(ID3D12Resource),
                reinterpret_cast<void**>(&backBuffer));
            if (FAILED(hr))
            {
                CLUSTER_LOG_ERROR << "IDXGISwapChain::GetBuffer failed to get swap chain buffer " << backBufferIndex
                    << ": " << hr;
                return;
            }
            m_BackBuffers.push_back(ComSharedPtr<ID3D12Resource>(backBuffer));
        }

        // Create a texture to hold the saved content
        UINT backBufferIndex = m_SwapChain->GetCurrentBackBufferIndex();
        auto backBufferResourceDesc = m_BackBuffers[backBufferIndex]->GetDesc();
        backBufferResourceDesc.Flags = D3D12_RESOURCE_FLAG_NONE;
        D3D12_HEAP_PROPERTIES heapProperties;
        D3D12_HEAP_FLAGS heapFlags;
        hr = m_BackBuffers[backBufferIndex]->GetHeapProperties(&heapProperties, &heapFlags);
        if (FAILED(hr))
        {
            CLUSTER_LOG_ERROR << "ID3D12Resource::GetHeapProperties failed: " << hr;
            return;
        }
        {
            ID3D12Resource* savedTexture;
            hr = m_D3D12Device->CreateCommittedResource(&heapProperties, D3D12_HEAP_FLAG_NONE, &backBufferResourceDesc,
                D3D12_RESOURCE_STATE_COMMON, nullptr, __uuidof(ID3D12Resource), reinterpret_cast<void**>(&savedTexture));
            if (FAILED(hr))
            {
                CLUSTER_LOG_ERROR << "ID3D12Device::CreateCommittedResource failed to create texture to store the "
                    << "picture to repeat: " << hr;
                return;
            }
            m_SavedTexture.reset(savedTexture);
        }
        m_SavedTexture->SetName(L"GfxPluginQuadroSync SavedTexture");

        // Copy current backbuffer to it
        m_CommandList->CopyResource(m_SavedTexture.get(), m_BackBuffers[backBufferIndex].get());

        // Indicate that the texture will become a copy source
        D3D12_RESOURCE_BARRIER renderTargetBarrier;
        renderTargetBarrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        renderTargetBarrier.Flags = D3D12_RESOURCE_BARRIER_FLAG_NONE;
        renderTargetBarrier.Transition.pResource = m_SavedTexture.get();
        renderTargetBarrier.Transition.StateBefore = D3D12_RESOURCE_STATE_COPY_DEST;
        renderTargetBarrier.Transition.StateAfter = D3D12_RESOURCE_STATE_COPY_SOURCE;
        renderTargetBarrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
        m_CommandList->ResourceBarrier(1, &renderTargetBarrier);

        // Conclude the operations
        m_CommandList->Close();
        ID3D12CommandList* const commandListsToExecute[] = {m_CommandList.get()};
        m_CommandQueue->ExecuteCommandLists(1, commandListsToExecute);

        // Wait for copy to be executed (is it really necessary?  Good question, but its safer and we are not in a
        // hurry anyway as this is only executed once at initialization time.)
        EnsureFenceCreated();
        QueueUpdateFence();
        WaitForFence();
    }

    void D3D12GraphicsDevice::PrepareSinglePresentRepeat()
    {
        if (!m_SwapChain)
        {
            return;
        }
        UINT backBufferIndex = m_SwapChain->GetCurrentBackBufferIndex();
        if (m_FirstRepeatBackBufferIndex == -1)
        {
            m_FirstRepeatBackBufferIndex = backBufferIndex;
        }

        WaitForFence();

        // Prepare the command allocator and list for new commands
        // Remarks: Need to be kept alive until processing of those commands are done, so we keep it until next frame.
        m_CommandAllocator->Reset();
        m_CommandList->Reset(m_CommandAllocator.get(), nullptr);

        // Indicate that the back buffer will be used as a render target.
        D3D12_RESOURCE_BARRIER renderTargetBarrier;
        renderTargetBarrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        renderTargetBarrier.Flags = D3D12_RESOURCE_BARRIER_FLAG_NONE;
        renderTargetBarrier.Transition.pResource = m_BackBuffers[backBufferIndex].get();
        renderTargetBarrier.Transition.StateBefore = D3D12_RESOURCE_STATE_PRESENT;
        renderTargetBarrier.Transition.StateAfter = D3D12_RESOURCE_STATE_COPY_DEST;
        renderTargetBarrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
        m_CommandList->ResourceBarrier(1, &renderTargetBarrier);

        // Copy the saved texture to it
        m_CommandList->CopyResource(m_BackBuffers[backBufferIndex].get(), m_SavedTexture.get());

        // Indicate that the back buffer will be used to present
        D3D12_RESOURCE_BARRIER presentBarrier;
        renderTargetBarrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        renderTargetBarrier.Flags = D3D12_RESOURCE_BARRIER_FLAG_NONE;
        renderTargetBarrier.Transition.pResource = m_BackBuffers[backBufferIndex].get();
        renderTargetBarrier.Transition.StateBefore = D3D12_RESOURCE_STATE_COPY_DEST;
        renderTargetBarrier.Transition.StateAfter = D3D12_RESOURCE_STATE_PRESENT;
        renderTargetBarrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
        m_CommandList->ResourceBarrier(1, &renderTargetBarrier);

        // Command list is completed
        m_CommandList->Close();
        ID3D12CommandList* const commandListsToExecute[] = {m_CommandList.get()};
        m_CommandQueue->ExecuteCommandLists(1, commandListsToExecute);

        // Add a barrier to be signaled when commands are done being processed
        QueueUpdateFence();
    }

    void D3D12GraphicsDevice::ConcludePresentRepeats()
    {
        if (!m_SwapChain)
        {
            return;
        }

        WaitForFence();

        // Looks like GetCurrentBackBufferIndex must match the one of the first time we have been called or it
        // generates problems where Unity then tries to render to a back buffer that is not the current back buffer.
        // So continue repeating frames and presenting (using the normal present, not Quadro Sync present) until they
        // match.
        while (m_SwapChain->GetCurrentBackBufferIndex() != m_FirstRepeatBackBufferIndex)
        {
            PrepareSinglePresentRepeat();
            HRESULT hr = m_SwapChain->Present(m_SyncInterval, m_PresentFlags);
            if (FAILED(hr))
            {
                CLUSTER_LOG_ERROR << "IDXGISwapChain::Present failed while re-aligning CurrentBackBufferIndex: " << hr;
            }
            WaitForFence();
        }

        FreeResources();
    }

    void D3D12GraphicsDevice::EnsureFenceCreated()
    {
        if (!m_BarrierReachedEvent)
        {
            m_BarrierReachedEvent.reset(CreateEvent(nullptr, FALSE, FALSE, nullptr));
        }

        if (!m_CommandExecutionDoneFence)
        {
            ID3D12Fence* commandExecutionDoneFence;
            HRESULT hr = m_D3D12Device->CreateFence(m_CommandExecutionDoneFenceNextValue - 1, D3D12_FENCE_FLAG_NONE,
                __uuidof(ID3D12Fence), reinterpret_cast<void**>(&commandExecutionDoneFence));
            if (FAILED(hr))
            {
                CLUSTER_LOG_ERROR << "ID3D12Device::CreateFence failed: " << hr;
                return;
            }
            m_CommandExecutionDoneFence.reset(commandExecutionDoneFence);
            m_CommandExecutionDoneFence->SetName(L"GfxPluginQuadroSync Fence");
        }
    }

    void D3D12GraphicsDevice::QueueUpdateFence()
    {
        ++m_CommandExecutionDoneFenceNextValue;
        HRESULT hr = m_CommandQueue->Signal(m_CommandExecutionDoneFence.get(), m_CommandExecutionDoneFenceNextValue);
        if (FAILED(hr))
        {
            CLUSTER_LOG_WARNING << "ID3D12CommandQueue::Signal failed: " << hr;
        }
    }

    void D3D12GraphicsDevice::WaitForFence()
    {
        if (!IsFenceCreated())
        {
            return;
        }

        if (m_CommandExecutionDoneFence->GetCompletedValue() < m_CommandExecutionDoneFenceNextValue)
        {
            ResetEvent(m_BarrierReachedEvent.get());
            m_CommandExecutionDoneFence->SetEventOnCompletion(m_CommandExecutionDoneFenceNextValue,
                m_BarrierReachedEvent.get());
            WaitForSingleObject(m_BarrierReachedEvent.get(), INFINITE);
        }
    }

    void D3D12GraphicsDevice::FreeResources()
    {
        m_BarrierReachedEvent.reset();
        m_CommandExecutionDoneFence.reset();
        m_CommandList.reset();
        m_CommandAllocator.reset();
        m_BackBuffers.clear();
        m_SavedTexture.reset();
    }
}
