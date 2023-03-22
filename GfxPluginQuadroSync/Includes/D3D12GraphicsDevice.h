#pragma once

#include "d3d12.h"
#include "dxgi.h"
#include "IGraphicsDevice.h"
#include "ComHelpers.h"

#include <vector>

struct IDXGISwapChain3;

namespace GfxQuadroSync
{
    class D3D12GraphicsDevice final : public IGraphicsDevice
    {
    public:
        D3D12GraphicsDevice(
            ID3D12Device* device,
            IDXGISwapChain* swapChain,
            ID3D12CommandQueue* commandQueue,
            UINT32 interval,
            UINT presentFlags);

        virtual ~D3D12GraphicsDevice();

        GraphicsDeviceType GetDeviceType() const override { return GraphicsDeviceType::GRAPHICS_DEVICE_D3D12; }

        IUnknown*       GetDevice() const override { return m_D3D12Device; }
        IDXGISwapChain* GetSwapChain() const override;
        UINT32          GetSyncInterval() const override { return m_SyncInterval; }
        UINT            GetPresentFlags() const override { return m_PresentFlags; }

        void SetDevice(IUnknown* const device) override { m_D3D12Device = static_cast<ID3D12Device*>(device); }
        void SetSwapChain(IDXGISwapChain* const swapChain) override;

        void InitiatePresentRepeats() override;
        void PrepareSinglePresentRepeat() override;
        void ConcludePresentRepeats() override;

    private:
        bool IsFenceCreated() const { return m_CommandExecutionDoneFence != nullptr; }
        void EnsureFenceCreated();
        void QueueUpdateFence();
        void WaitForFence();
        void FreeResources();

        ID3D12Device* m_D3D12Device;
        ComSharedPtr<IDXGISwapChain3> m_SwapChain;
        ID3D12CommandQueue* m_CommandQueue;
        UINT32 m_SyncInterval;
        UINT m_PresentFlags;

        ComSharedPtr<ID3D12Fence> m_CommandExecutionDoneFence;
        UINT64 m_CommandExecutionDoneFenceNextValue = 1;
        HandleWrapper m_BarrierReachedEvent;

        ComSharedPtr<ID3D12CommandAllocator> m_CommandAllocator;
        ComSharedPtr<ID3D12GraphicsCommandList> m_CommandList;
        std::vector<ComSharedPtr<ID3D12Resource>> m_BackBuffers;
        ComSharedPtr<ID3D12Resource> m_SavedTexture;
        UINT m_FirstRepeatBackBufferIndex = -1;
    };
}
