#include "D3D11GraphicsDevice.h"
#include "D3D12GraphicsDevice.h"
#include "QuadroSync.h"
#include "GfxQuadroSync.h"
#include "Logger.h"

#include "../Unity/IUnityRenderingExtensions.h"
#include "../Unity/IUnityGraphicsD3D11.h"
#include "../Unity/IUnityGraphicsD3D12.h"

namespace GfxQuadroSync
{
    static IUnityInterfaces* s_UnityInterfaces = nullptr;
    static IUnityGraphics* s_UnityGraphics = nullptr;
    static IUnityGraphicsD3D11* s_UnityGraphicsD3D11 = nullptr;
    static IUnityGraphicsD3D12v7* s_UnityGraphicsD3D12 = nullptr;

    static std::unique_ptr<IGraphicsDevice> s_GraphicsDevice = nullptr;
    static PluginCSwapGroupClient s_SwapGroupClient;
    static bool s_Initialized = false;

    // Override the function defining the load of the plugin
    extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
        UnityPluginLoad(IUnityInterfaces * unityInterfaces)
    {
        if (unityInterfaces)
        {
            CLUSTER_LOG << "UnityPluginLoad triggered";

            s_UnityInterfaces = unityInterfaces;
            s_UnityGraphics = unityInterfaces->Get<IUnityGraphics>();
            if (s_UnityGraphics)
            {
                s_UnityGraphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);

                // Run OnGraphicsDeviceEvent(initialize) manually on plugin load
                // to not miss the event in case the graphics device is already initialized
                OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
            }
        }
        else
        {
            CLUSTER_LOG_ERROR << "UnityPluginLoad, unityInterfaces is null";
        }
    }

    // Freely defined function to pass a callback to plugin-specific scripts
    extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
        GetRenderEventFunc()
    {
        return OnRenderEvent;
    }

    // Freely defined function to pass a callback to the function to call with log messages
    extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API SetLogCallback(Logger::ManagedCallback callback)
    {
        Logger::Instance().SetManagedCallback(callback);
    }

    // Override the query method to use the `PresentFrame` callback
    // It has been added specially for the Quadro Sync system
    extern "C" bool UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
        UnityRenderingExtQuery(UnityRenderingExtQueryType query)
    {
        if (!IsContextValid())
            return false;

        return (query == UnityRenderingExtQueryType::kUnityRenderingExtQueryOverridePresentFrame)
            ? s_SwapGroupClient.Render(
                s_GraphicsDevice->GetDevice(),
                s_GraphicsDevice->GetSwapChain(),
                s_GraphicsDevice->GetSyncInterval(),
                s_GraphicsDevice->GetPresentFlags())
            : false;
    }

    static void GetRenderDeviceInterface(UnityGfxRenderer renderer)
    {
        switch (renderer)
        {
        case UnityGfxRenderer::kUnityGfxRendererD3D11:
            CLUSTER_LOG << "Detected D3D11 renderer";
            s_UnityGraphicsD3D11 = s_UnityInterfaces->Get<IUnityGraphicsD3D11>();
            break;
        case UnityGfxRenderer::kUnityGfxRendererD3D12:
            CLUSTER_LOG << "Detected D3D12 renderer";
            s_UnityGraphicsD3D12 = s_UnityInterfaces->Get<IUnityGraphicsD3D12v7>();
            break;
        default:
            CLUSTER_LOG_ERROR << "Graphic API not supported";
            break;
        }
    }

    // Override function to receive graphics event
    static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
    {
        if (eventType == kUnityGfxDeviceEventInitialize && !s_Initialized)
        {
            CLUSTER_LOG << "kUnityGfxDeviceEventInitialize called";
            s_Initialized = true;
        }
        else if (eventType == kUnityGfxDeviceEventShutdown)
        {
            s_Initialized = false;
            s_UnityInterfaces = nullptr;
            s_UnityGraphics = nullptr;
            s_UnityGraphicsD3D11 = nullptr;
            s_UnityGraphicsD3D12 = nullptr;
            s_GraphicsDevice = nullptr;
        }
    }

    // Plugin function to handle a specific rendering event.
    static void UNITY_INTERFACE_API
        OnRenderEvent(int eventID, void* data)
    {
        switch (static_cast<EQuadroSyncRenderEvent>(eventID))
        {
        case EQuadroSyncRenderEvent::QuadroSyncInitialize:
            QuadroSyncInitialize();
            break;
        case EQuadroSyncRenderEvent::QuadroSyncQueryFrameCount:
            QuadroSyncQueryFrameCount(static_cast<int* const>(data));
            break;
        case EQuadroSyncRenderEvent::QuadroSyncResetFrameCount:
            QuadroSyncResetFrameCount();
            break;
        case EQuadroSyncRenderEvent::QuadroSyncDispose:
            QuadroSyncDispose();
            break;
        case EQuadroSyncRenderEvent::QuadroSyncEnableSystem:
            QuadroSyncEnableSystem(static_cast<bool>(data));
            break;
        case EQuadroSyncRenderEvent::QuadroSyncEnableSwapGroup:
            QuadroSyncEnableSwapGroup(static_cast<bool>(data));
            break;
        case EQuadroSyncRenderEvent::QuadroSyncEnableSwapBarrier:
            QuadroSyncEnableSwapBarrier(static_cast<bool>(data));
            break;
        case EQuadroSyncRenderEvent::QuadroSyncEnableSyncCounter:
            QuadroSyncEnableSyncCounter(static_cast<bool>(data));
            break;
        default:
            break;
        }
    }

    void SetDevice()
    {
        if (s_UnityGraphicsD3D11 != nullptr)
        {
            auto device = s_UnityGraphicsD3D11->GetDevice();
            s_GraphicsDevice->SetDevice(device);
        }
        else if (s_UnityGraphicsD3D12)
        {
            auto device = s_UnityGraphicsD3D12->GetDevice();
            s_GraphicsDevice->SetDevice(device);
        }
    }

    void SetSwapChain()
    {
        if (s_UnityGraphicsD3D11 != nullptr)
        {
            auto swapChain = s_UnityGraphicsD3D11->GetSwapChain();
            s_GraphicsDevice->SetSwapChain(swapChain);
        }
        else if (s_UnityGraphicsD3D12)
        {
            auto swapChain = s_UnityGraphicsD3D12->GetSwapChain();
            s_GraphicsDevice->SetSwapChain(swapChain);
        }
    }

    // Verify if the D3D Device and the Swap Chain are valid.
    // The Swapchain can be invalid (for obscure reason) during the first Unity frame.
    bool IsContextValid()
    {
        if (s_UnityGraphics == nullptr)
        {
            CLUSTER_LOG_ERROR << "IsContextValid, s_UnityGraphics == nullptr";
            return false;
        }

        if (s_GraphicsDevice == nullptr)
        {
            return false;
        }

        if (s_UnityGraphics->GetRenderer() != UnityGfxRenderer::kUnityGfxRendererD3D11 &&
            s_UnityGraphics->GetRenderer() != UnityGfxRenderer::kUnityGfxRendererD3D12)
        {
            CLUSTER_LOG_ERROR << "IsContextValid, s_UnityGraphics->GetRenderer() != UnityGfxRenderer::kUnityGfxRendererD3D11-12";
            return false;
        }

        if (s_GraphicsDevice->GetDevice() == nullptr)
        {
            CLUSTER_LOG_WARNING << "IsContextValid, GetDevice() == nullptr";
            SetDevice();
        }

        if (s_GraphicsDevice->GetSwapChain() == nullptr)
        {
            CLUSTER_LOG_WARNING << "IsContextValid, GetSwapChain() == nullptr";
            SetSwapChain();
        }

        return (s_GraphicsDevice->GetDevice() != nullptr && s_GraphicsDevice->GetSwapChain() != nullptr);
    }

    bool InitializeGraphicsDevice()
    {
        // We cannot call this function earlier, because GetRenderer is sometimes
        // not initialized at the right time (kUnityGfxRendererNull is returned).
        auto renderer = s_UnityGraphics->GetRenderer();
        GetRenderDeviceInterface(renderer);

        if (s_GraphicsDevice == nullptr)
        {
            if (s_UnityGraphicsD3D11 != nullptr)
            {
                auto device = s_UnityGraphicsD3D11->GetDevice();
                auto swapChain = s_UnityGraphicsD3D11->GetSwapChain();
                auto syncInterval = s_UnityGraphicsD3D11->GetSyncInterval();
                auto presentFlags = s_UnityGraphicsD3D11->GetPresentFlags();

                s_GraphicsDevice = std::make_unique<D3D11GraphicsDevice>(device, swapChain, syncInterval, presentFlags);
                CLUSTER_LOG << "D3D11GraphicsDevice successfully created";
            }
            else if (s_UnityGraphicsD3D12 != nullptr)
            {
                auto device = s_UnityGraphicsD3D12->GetDevice();
                auto swapChain = s_UnityGraphicsD3D12->GetSwapChain();
                auto syncInterval = s_UnityGraphicsD3D12->GetSyncInterval();
                auto presentFlags = s_UnityGraphicsD3D12->GetPresentFlags();

                s_GraphicsDevice = std::make_unique<D3D12GraphicsDevice>(device, swapChain, syncInterval, presentFlags);
                CLUSTER_LOG << "D3D12GraphicsDevice successfully created";
            }
            else
            {
                CLUSTER_LOG_ERROR << "Graphic API incompatible";
                return false;
            }
        }
        return true;
    }

    // Enable Workstation SwapGroup & potentially join the SwapGroup / Barrier
    void QuadroSyncInitialize()
    {
        if (!InitializeGraphicsDevice())
        {
            CLUSTER_LOG_ERROR << "Failed during QuadroSyncInitialize";
            return;
        }

        if (!IsContextValid())
            return;

        s_SwapGroupClient.SetupWorkStation();
        s_SwapGroupClient.Initialize(
            s_GraphicsDevice->GetDevice(),
            s_GraphicsDevice->GetSwapChain());
    }

    // Query the actual frame count (master or custom one)
    void QuadroSyncQueryFrameCount(int* const value)
    {
        if (!IsContextValid() || value == nullptr)
            return;

        auto frameCount = s_SwapGroupClient.QueryFrameCount(s_GraphicsDevice->GetDevice());
        *value = (int)frameCount;
    }

    // Reset the frame count (master or custom one)
    void QuadroSyncResetFrameCount()
    {
        if (!IsContextValid())
            return;

        s_SwapGroupClient.ResetFrameCount(s_GraphicsDevice->GetDevice());
    }

    // Leave the Barrier and Swap Group, disable the Workstation SwapGroup
    void QuadroSyncDispose()
    {
        if (!IsContextValid())
            return;

        s_SwapGroupClient.Dispose(
            s_GraphicsDevice->GetDevice(),
            s_GraphicsDevice->GetSwapChain());

        s_SwapGroupClient.DisposeWorkStation();
    }

    // Directly join or leave the Swap Group and Barrier
    void QuadroSyncEnableSystem(const bool value)
    {
        if (!IsContextValid())
            return;

        s_SwapGroupClient.EnableSystem(
            s_GraphicsDevice->GetDevice(),
            s_GraphicsDevice->GetSwapChain(), value);
    }

    // Toggle to join/leave the SwapGroup
    void QuadroSyncEnableSwapGroup(const bool value)
    {
        if (!IsContextValid())
            return;

        s_SwapGroupClient.EnableSwapGroup(
            s_GraphicsDevice->GetDevice(),
            s_GraphicsDevice->GetSwapChain(),
            value);
    }

    // Toggle to join/leave the Barrier
    void QuadroSyncEnableSwapBarrier(const bool value)
    {
        if (!IsContextValid())
            return;

        s_SwapGroupClient.EnableSwapBarrier(
            s_GraphicsDevice->GetDevice(),
            value);
    }

    // Enable or disable the Master Sync Counter
    void QuadroSyncEnableSyncCounter(const bool value)
    {
        if (!IsContextValid())
            return;

        s_SwapGroupClient.EnableSyncCounter(value);
    }

}
