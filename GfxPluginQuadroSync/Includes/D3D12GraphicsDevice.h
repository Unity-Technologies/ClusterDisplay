#pragma once

#include "d3d12.h"
#include "dxgi.h"
#include "IGraphicsDevice.h"

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

        void SaveToPresent() override;
        void RepeatSavedToPresent() override;
        void FreeSavedToPresent() override;

    private:
        bool IsFenceCreated() const { return m_CommandExecutionDoneFence != nullptr; }
        void EnsureFenceCreated();
        void QueueUpdateFence();
        void WaitForFence();
        void FreeResources();

        ID3D12Device* m_D3D12Device;
        IDXGISwapChain3* m_SwapChain = nullptr;
        ID3D12CommandQueue* m_CommandQueue;
        UINT32 m_SyncInterval;
        UINT m_PresentFlags;

        ID3D12Fence* m_CommandExecutionDoneFence = nullptr;
        UINT64 m_CommandExecutionDoneFenceNextValue = 1;
        HANDLE m_BarrierReachedEvent = NULL;

        ID3D12CommandAllocator* m_CommandAllocator = nullptr;
        ID3D12GraphicsCommandList* m_CommandList = nullptr;
        std::vector<ID3D12Resource*> m_BackBuffers;
        ID3D12Resource* m_SavedTexture = nullptr;
        UINT m_FirstRepeatBackBufferIndex = -1;
    };
}
