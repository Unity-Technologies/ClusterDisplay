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

        /**
         * Called before starting a sequence of "additional present" required to warm up the quadro sync barrier.
         */
        virtual void InitiatePresentRepeats() = 0;
        /**
         * Called before every "additional present" required to warm up the quadro sync barrier.
         */
        virtual void PrepareSinglePresentRepeat() = 0;
        /**
         * Called after the sequence of "additional present" required to warm up the quadro sync barrier.
         */
        virtual void ConcludePresentRepeats() = 0;
    };
}
