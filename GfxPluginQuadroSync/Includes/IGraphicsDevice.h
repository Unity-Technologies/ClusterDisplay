#pragma once

namespace GfxQuadroSync
{
    enum class GraphicsDeviceType
    {
        GRAPHICS_DEVICE_D3D11 = 0,
        GRAPHICS_DEVICE_D3D12,
        GRAPHICS_DEVICE_OPENGL,
        GRAPHICS_DEVICE_METAL,
        GRAPHICS_DEVICE_VULKAN,
    };

    class IGraphicsDevice
    {
    public:
        IGraphicsDevice() {}
        virtual ~IGraphicsDevice() {}

        virtual GraphicsDeviceType GetDeviceType() const = 0;

        virtual IUnknown* GetDevice() const = 0;
        virtual IDXGISwapChain* GetSwapChain() const = 0;
        virtual UINT32 GetSyncInterval() const = 0;
        virtual UINT GetPresentFlags() const = 0;

        virtual void SetDevice(IUnknown* const device) = 0;
        virtual void SetSwapChain(IDXGISwapChain* const swapChain) = 0;

        virtual void SaveToPresent() = 0;
        virtual void RepeatSavedToPresent() = 0;
        virtual void FreeSavedToPresent() = 0;
    };
}
