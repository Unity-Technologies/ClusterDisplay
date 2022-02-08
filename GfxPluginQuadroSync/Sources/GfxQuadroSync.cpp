#include "D3D11GraphicDevice.h"
#include "D3D12GraphicDevice.h"
#include "QuadroSync.h"
#include "GfxQuadroSync.h"
#include "PluginUtils.h"

#include "../Unity/IUnityRenderingExtensions.h"
#include "../Unity/IUnityGraphicsD3D11.h"
#include "../Unity/IUnityGraphicsD3D12.h"

namespace GfxQuadroSync
{
	static IUnityInterfaces* s_UnityInterfaces = nullptr;
	static IUnityGraphics* s_UnityGraphics = nullptr;
	static IUnityGraphicsD3D11* s_UnityGraphicsD3D11 = nullptr;
	static IUnityGraphicsD3D12v7* s_UnityGraphicsD3D12 = nullptr;
	
	static IGraphicDevice* s_GraphicDevice = nullptr;
	static PluginCSwapGroupClient s_SwapGroupClient;
	static bool s_Initialized = false;

	// Override the function defining the load of the plugin
	extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
		UnityPluginLoad(IUnityInterfaces * unityInterfaces)
	{
		if (unityInterfaces)
		{
			WriteFileDebug("* Success: UnityPluginLoad triggered\n", false);

			s_UnityInterfaces = unityInterfaces;
			const auto unityGraphics = unityInterfaces->Get<IUnityGraphics>();
			if (unityGraphics)
			{
				s_UnityGraphics = unityGraphics;
				unityGraphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
			}
		}
		else
		{
			WriteFileDebug("* Error: UnityPluginLoad, unityInterfaces is null \n", true);
		}
	}

	// Freely defined function to pass a callback to plugin-specific scripts
	extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
		GetRenderEventFunc()
	{
		return OnRenderEvent;
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
				s_GraphicDevice->GetDevice(),
				s_GraphicDevice->GetSwapChain(),
				s_GraphicDevice->GetSyncInterval(),
				s_GraphicDevice->GetPresentFlags())
			: false;
	}

	static void GetRenderDeviceInterface(UnityGfxRenderer renderer)
	{
		switch (renderer)
		{
		case UnityGfxRenderer::kUnityGfxRendererD3D11:
			WriteFileDebug("* Success: get s_UnityGraphicsD3D11\n", true);
			s_UnityGraphicsD3D11 = s_UnityInterfaces->Get<IUnityGraphicsD3D11>();
			break;
		case UnityGfxRenderer::kUnityGfxRendererD3D12:
			WriteFileDebug("* Success: get kUnityGfxRendererD3D12\n", true);
			s_UnityGraphicsD3D12 = s_UnityInterfaces->Get<IUnityGraphicsD3D12v7>();
			break;
		default:
			WriteFileDebug("* Error: Graphic API not supported\n");
			break;
		}
	}

	// Override function to receive graphics event 
	static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
	{
		if (eventType == kUnityGfxDeviceEventInitialize && !s_Initialized)
		{
			WriteFileDebug("* Success: kUnityGfxDeviceEventInitialize called\n", true);
			
			auto renderer = s_UnityInterfaces->Get<IUnityGraphics>()->GetRenderer();
			GetRenderDeviceInterface(renderer);
			s_Initialized = true;
		}
		else if (eventType == kUnityGfxDeviceEventShutdown)
		{
			s_Initialized = false;
			s_UnityInterfaces = nullptr;
			s_UnityGraphics = nullptr;
			s_UnityGraphicsD3D11 = nullptr;
			s_UnityGraphicsD3D12 = nullptr;

			if (s_GraphicDevice != nullptr)
			{
				delete(s_GraphicDevice);
				s_GraphicDevice = nullptr;
			}
		}
	}

	// Plugin function to handle a specific rendering event.
	static void UNITY_INTERFACE_API
		OnRenderEvent(int eventID, void* data)
	{
		switch (eventID)
		{
		case (int)EQuadroSyncRenderEvent::QuadroSyncInitialize:
			QuadroSyncInitialize();
			break;
		case (int)EQuadroSyncRenderEvent::QuadroSyncQueryFrameCount:
			QuadroSyncQueryFrameCount(static_cast<int* const>(data));
			break;
		case (int)EQuadroSyncRenderEvent::QuadroSyncResetFrameCount:
			QuadroSyncResetFrameCount();
			break;
		case (int)EQuadroSyncRenderEvent::QuadroSyncDispose:
			QuadroSyncDispose();
			break;
		case (int)EQuadroSyncRenderEvent::QuadroSyncEnableSystem:
			QuadroSyncEnableSystem(static_cast<bool>(data));
			break;
		case (int)EQuadroSyncRenderEvent::QuadroSyncEnableSwapGroup:
			QuadroSyncEnableSwapGroup(static_cast<bool>(data));
			break;
		case (int)EQuadroSyncRenderEvent::QuadroSyncEnableSwapBarrier:
			QuadroSyncEnableSwapBarrier(static_cast<bool>(data));
			break;
		case (int)EQuadroSyncRenderEvent::QuadroSyncEnableSyncCounter:
			QuadroSyncEnableSyncCounter(static_cast<bool>(data));
			break;
		default:
			break;
		}
	}

	// Verify if the D3D Device and the Swap Chain are valid.
	// The Swapchain can be invalid (for obscure reason) during the first Unity frame.
	bool IsContextValid()
	{
		if (s_UnityGraphics == nullptr)
		{
			WriteFileDebug("* Failed: IsContextValid, s_UnityGraphics == nullptr \n", true);
			return false;
		}

		if (s_GraphicDevice == nullptr)
		{
			WriteFileDebug("* Error: IsContextValid, s_GraphicDevice == nullptr \n", true);
			return false;
		}

		if (s_UnityGraphics->GetRenderer() != UnityGfxRenderer::kUnityGfxRendererD3D11)
		{
			WriteFileDebug("* Error: s_UnityGraphics->GetRenderer() != UnityGfxRenderer::kUnityGfxRendererD3D11 \n", true);
			return false;
		}

		if (s_GraphicDevice->GetDevice() == nullptr)
		{
			WriteFileDebug("* Error: IsContextValid, s_D3D11Device == nullptr \n", true);
			s_GraphicDevice->SetDevice(s_UnityGraphicsD3D11->GetDevice());
		}

		if (s_GraphicDevice->GetSwapChain() == nullptr)
		{
			WriteFileDebug("* Error: IsContextValid, s_D3D11SwapChain == nullptr \n", true);
			s_GraphicDevice->SetSwapChain(s_UnityGraphicsD3D11->GetSwapChain());
		}

		return (s_GraphicDevice->GetDevice() != nullptr && s_GraphicDevice->GetSwapChain() != nullptr);
	}

	bool InitializeGraphicDevice()
	{
		if (s_GraphicDevice == nullptr)
		{
			if (s_UnityGraphicsD3D11)
			{
				auto device = s_UnityGraphicsD3D11->GetDevice();
				auto swapChain = s_UnityGraphicsD3D11->GetSwapChain();
				auto syncInterval = s_UnityGraphicsD3D11->GetSyncInterval();
				auto presentFlags = s_UnityGraphicsD3D11->GetPresentFlags();

				s_GraphicDevice = new D3D11GraphicDevice(device, swapChain, syncInterval, presentFlags);
				WriteFileDebug("* Success: D3D11GraphicDevice succesfully created\n");
			}
			else if (s_UnityGraphicsD3D12)
			{
				auto device = s_UnityGraphicsD3D12->GetDevice();
				auto swapChain = s_UnityGraphicsD3D12->GetSwapChain();
				auto syncInterval = s_UnityGraphicsD3D12->GetSyncInterval();
				auto presentFlags = s_UnityGraphicsD3D12->GetPresentFlags();

				s_GraphicDevice = new D3D12GraphicDevice(device, swapChain, syncInterval, presentFlags);
				WriteFileDebug("Success: D3D12GraphicDevice succesfully created\n");
			}
			else
			{
				WriteFileDebug("* Error: Graphic API incompatible\n");
				return false;
			}
		}
		return true;
	}

	// Enable Workstation SwapGroup & potentially join the SwapGroup / Barrier
	void QuadroSyncInitialize()
	{
		if (!InitializeGraphicDevice())
		{
			WriteFileDebug("* Error: failed during QuadroSyncInitialize\n");
			return;
		}

		if (!IsContextValid())
			return;

		s_SwapGroupClient.SetupWorkStation();
		s_SwapGroupClient.Initialize(
			s_GraphicDevice->GetDevice(), 
			s_GraphicDevice->GetSwapChain());
	}

	// Query the actual frame count (master or custom one)
	void QuadroSyncQueryFrameCount(int* const value)
	{
		if (!IsContextValid() || value == nullptr)
			return;

		auto frameCount = s_SwapGroupClient.QueryFrameCount(s_GraphicDevice->GetDevice());
		*value = (int)frameCount;
	}

	// Reset the frame count (master or custom one)
	void QuadroSyncResetFrameCount()
	{
		if (!IsContextValid())
			return;

		s_SwapGroupClient.ResetFrameCount(s_GraphicDevice->GetDevice());
	}

	// Leave the Barrier and Swap Group, disable the Workstation SwapGroup
	void QuadroSyncDispose()
	{
		if (!IsContextValid())
			return;

		s_SwapGroupClient.Dispose(
			s_GraphicDevice->GetDevice(), 
			s_GraphicDevice->GetSwapChain());

		s_SwapGroupClient.DisposeWorkStation();
	}

	// Directly join or leave the Swap Group and Barrier
	void QuadroSyncEnableSystem(const bool value)
	{
		if (!IsContextValid())
			return;

		s_SwapGroupClient.EnableSystem(
			s_GraphicDevice->GetDevice(), 
			s_GraphicDevice->GetSwapChain(), value);
	}

	// Toggle to join/leave the SwapGroup
	void QuadroSyncEnableSwapGroup(const bool value)
	{
		if (!IsContextValid())
			return;

		s_SwapGroupClient.EnableSwapGroup(
			s_GraphicDevice->GetDevice(), 
			s_GraphicDevice->GetSwapChain(), 
			value);
	}

	// Toggle to join/leave the Barrier
	void QuadroSyncEnableSwapBarrier(const bool value)
	{
		if (!IsContextValid())
			return;

		s_SwapGroupClient.EnableSwapBarrier(
			s_GraphicDevice->GetDevice(), 
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
