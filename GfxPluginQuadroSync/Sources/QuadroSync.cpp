#include <string>
#include <sstream>
#include <fstream>

#include "d3d11.h"
#include "d3d12.h"

#include "QuadroSync.h"
#include "Logger.h"

namespace GfxQuadroSync
{
    PluginCSwapGroupClient::PluginCSwapGroupClient()
    {
        CLUSTER_LOG << "Initialize PluginCSwapGroupClient";
        Prepare();
    }

    PluginCSwapGroupClient::~PluginCSwapGroupClient()
    {
        CLUSTER_LOG << "Destroy PluginCSwapGroupClient";
    }

    void PluginCSwapGroupClient::Prepare()
    {
        // Prepare NVAPI for use in this application
        NvAPI_Status status = NvAPI_Initialize();

        if (status != NVAPI_OK)
        {
            CLUSTER_LOG_ERROR << "NvAPI_Initialize: " << status;
        }
        else
            CLUSTER_LOG << "NvAPI_Initialize successful";
    }

    void PluginCSwapGroupClient::SetupWorkStation()
    {
        // Register our request to use workstation SwapGroup resources in the driver
        NvU32 gpuCount;
        NvPhysicalGpuHandle nvGPUHandle[NVAPI_MAX_PHYSICAL_GPUS];
        NvAPI_Status status = NvAPI_EnumPhysicalGPUs(nvGPUHandle, &gpuCount);
        if (NVAPI_OK == status)
        {
            for (unsigned int gpuIndex = 0; gpuIndex < gpuCount; gpuIndex++)
            {
                // send request to enable NVAPI_GPU_WORKSTATION_FEATURE_MASK_SWAPGROUP
                status = NvAPI_GPU_WorkstationFeatureSetup(nvGPUHandle[gpuIndex], NVAPI_GPU_WORKSTATION_FEATURE_MASK_SWAPGROUP, 0);

                if (status == NvAPI_Status::NVAPI_OK)
                    CLUSTER_LOG << "GPU " << gpuIndex << ": NvAPI_GPU_WorkstationFeatureSetup successful";
                else
                    CLUSTER_LOG_ERROR << "GPU " << gpuIndex << ": NvAPI_GPU_WorkstationFeatureSetup failed: " << status;
            }
        }
    }

    void PluginCSwapGroupClient::DisposeWorkStation()
    {
        // Unregister our request to use workstation SwapGroup resources in the driver
        NvAPI_Status status;
        NvU32 gpuCount;
        NvPhysicalGpuHandle nvGPUHandle[NVAPI_MAX_PHYSICAL_GPUS];
        status = NvAPI_EnumPhysicalGPUs(nvGPUHandle, &gpuCount);
        if (NVAPI_OK == status)
        {
            for (unsigned int gpuIndex = 0; gpuIndex < gpuCount; gpuIndex++)
            {
                // send request to disable NVAPI_GPU_WORKSTATION_FEATURE_MASK_SWAPGROUP
                status = NvAPI_GPU_WorkstationFeatureSetup(nvGPUHandle[gpuIndex], 0, NVAPI_GPU_WORKSTATION_FEATURE_MASK_SWAPGROUP);

                if (status == NvAPI_Status::NVAPI_OK)
                    CLUSTER_LOG << "GPU " << gpuIndex << ": NvAPI_GPU_WorkstationFeatureSetup successful";
                else
                    CLUSTER_LOG_ERROR << "GPU " << gpuIndex << ": NvAPI_GPU_WorkstationFeatureSetup failed: " << status;
            }
        }
    }

    PluginCSwapGroupClient::InitializeStatus PluginCSwapGroupClient::Initialize(IUnknown* const pDevice,
                                                                                IDXGISwapChain* const pSwapChain)
    {
        auto status = NVAPI_OK;

        status = NvAPI_D3D1x_QueryMaxSwapGroup(pDevice, &m_GSyncSwapGroups, &m_GSyncBarriers);

        if (status == NvAPI_Status::NVAPI_OK)
            CLUSTER_LOG << "NvAPI_D3D1x_QueryMaxSwapGroup successful";
        else
        {
            CLUSTER_LOG_ERROR << "NvAPI_D3D1x_QueryMaxSwapGroup failed: " << status;
            return InitializeStatus::QuerySwapGroupFailed;
        }

        if (m_GSyncSwapGroups > 0)
        {
            if ((m_GroupId >= 0) && (m_GroupId <= m_GSyncSwapGroups))
            {
                status = NvAPI_D3D1x_JoinSwapGroup(pDevice, pSwapChain, m_GroupId, m_GroupId > 0 ? true : false);

                if (status == NvAPI_Status::NVAPI_OK)
                {
                    CLUSTER_LOG << "NvAPI_D3D1x_JoinSwapGroup returned NVAPI_OK";
                }
                else
                {
                    CLUSTER_LOG_ERROR << "NvAPI_D3D1x_JoinSwapGroup failed: " << status;
                }

#ifdef _DEBUG
                CLUSTER_LOG << "SwapGroup (" << m_GroupId << ") / (" << m_GSyncSwapGroups << ")";
#endif

                if (status != NVAPI_OK)
                {
                    return InitializeStatus::FailedToJoinSwapGroup;
                }
            }

            if (m_GSyncBarriers > 0)
            {
                NvU32 frameCount;

                //! heavy
                status = NvAPI_D3D1x_QueryFrameCount(pDevice, &frameCount);

                m_GSyncCounter = (status == NVAPI_OK);

                //! sync node
                if (m_GSyncMaster && m_GSyncCounter)
                {
                    status = NvAPI_D3D1x_ResetFrameCount(pDevice);
                }

                if ((m_BarrierId >= 0) && (m_BarrierId <= m_GSyncBarriers) &&
                    (m_GroupId >= 0) && (m_GroupId <= m_GSyncSwapGroups))
                {
                    status = NvAPI_D3D1x_BindSwapBarrier(pDevice, m_GroupId, m_BarrierId);

                    if (status == NvAPI_Status::NVAPI_OK)
                    {
                        CLUSTER_LOG << "NvAPI_D3D1x_BindSwapBarrier successful";
                    }
                    else
                    {
                        CLUSTER_LOG_ERROR << "NvAPI_D3D1x_BindSwapBarrier failed: " << status;
                    }

                    if (status != NVAPI_OK)
                    {
                        return InitializeStatus::FailedToBindSwapBarrier;
                    }
                }
            }
            else if (m_BarrierId > 0)
            {
                CLUSTER_LOG_ERROR << "NvAPI_D3D1x_QueryMaxSwapGroup returned 0 barriers";
                m_BarrierId = 0;
                return InitializeStatus::SwapBarrierIdMismatch;
            }

#ifdef _DEBUG
            CLUSTER_LOG << "BindSwapBarrier (" << m_BarrierId << ") / (" << m_GSyncBarriers << ")";
#endif

            status = NvAPI_D3D1x_QuerySwapGroup(pDevice, pSwapChain, &m_GroupId, &m_BarrierId);

            if (status == NvAPI_Status::NVAPI_OK)
                CLUSTER_LOG << "NvAPI_D3D1x_QuerySwapGroup successful";
            else
            {
                CLUSTER_LOG_ERROR << "NvAPI_D3D1x_QuerySwapGroup failed: " << status;
                return InitializeStatus::QuerySwapGroupFailed;
            }
        }
        else if (m_GSyncSwapGroups == 0)
        {
            CLUSTER_LOG_ERROR << "NvAPI_D3D1x_QueryMaxSwapGroup returned 0 groups";
            return InitializeStatus::NoSwapGroupDetected;
        }
        else
        {
            CLUSTER_LOG_ERROR << "NvAPI_D3D1x_QueryMaxSwapGroup returned " << m_GSyncSwapGroups
                              << " groups and m_GroupId is " << m_GroupId;
            m_GroupId = 0;
            return InitializeStatus::SwapGroupMismatch;
        }

        return (status == NVAPI_OK) ? InitializeStatus::Success : InitializeStatus::Failed;
    }

    void PluginCSwapGroupClient::Dispose(IUnknown* const pDevice,
                                         IDXGISwapChain* const pSwapChain)
    {
        NvAPI_Status status;
        if (m_GroupId > 0)
        {
            if (m_BarrierId > 0)
            {
                if (NVAPI_OK == (status = NvAPI_D3D1x_BindSwapBarrier(pDevice, m_GroupId, 0)))
                {
                    m_BarrierId = 0;
                }
            }

            if (NVAPI_OK == (status = NvAPI_D3D1x_JoinSwapGroup(pDevice, pSwapChain, 0, 0)))
            {
                m_GroupId = 0;
            }
        }

        m_presentSuccessCount = 0;
        m_presentFailureCount = 0;
    }

    NvU32 PluginCSwapGroupClient::QueryFrameCount(IUnknown* const pDevice)
    {
        NvU32 count = 0;

        if (m_GSyncCounter)
        {
            NvAPI_Status status;
            if (NVAPI_OK == (status = NvAPI_D3D1x_QueryFrameCount(pDevice, &count)))
            {
                m_FrameCount = count;
            }
        }
        else
        {
            ++m_FrameCount;
        }

        return m_FrameCount;
    }

    void PluginCSwapGroupClient::ResetFrameCount(IUnknown* const pDevice)
    {
        if (m_GSyncMaster)
        {
            auto status = NVAPI_OK;
            status = NvAPI_D3D1x_ResetFrameCount(pDevice);
        }
        else
        {
            m_FrameCount = 0;
        }
    }

    bool PluginCSwapGroupClient::Render(IUnknown* const pDevice,
        IDXGISwapChain* const pSwapChain,
        const int pVsync,
        const int pFlags)
    {
        const auto result = NvAPI_D3D1x_Present(pDevice, pSwapChain, pVsync, pFlags);

        if (result == NVAPI_OK)
        {
            ++m_presentSuccessCount;
            return true;
        }

        ++m_presentFailureCount;
        CLUSTER_LOG_ERROR << "NvAPI_D3D1x_Present failed: " << result;
        return false;
    }

    void PluginCSwapGroupClient::EnableSystem(IUnknown* const pDevice,
        IDXGISwapChain* const pSwapChain,
        const bool value)
    {
        m_IsActive = value;
        EnableSwapGroup(pDevice, pSwapChain, value);
        EnableSwapBarrier(pDevice, value);
    }

    void PluginCSwapGroupClient::EnableSwapGroup(IUnknown* const pDevice,
                                                 IDXGISwapChain* const pSwapChain,
                                                 const bool value)
    {
        const NvU32 newSwapGroup = (value) ? 1 : 0;
        CLUSTER_LOG << "EnableSwapGroup: (" << (value ? "true" : "false") << ", newSwapGroup ID is " << newSwapGroup;

        if ((newSwapGroup != m_GroupId) && (newSwapGroup <= m_GSyncSwapGroups))
        {
            const auto status = NvAPI_D3D1x_JoinSwapGroup(pDevice, pSwapChain, newSwapGroup, (newSwapGroup > 0));

            if (status == NvAPI_Status::NVAPI_OK)
            {
                CLUSTER_LOG << "NvAPI_D3D1x_JoinSwapGroup returned NVAPI_OK";
                m_GroupId = newSwapGroup;
            }
            else
            {
                CLUSTER_LOG_ERROR << "NvAPI_D3D1x_JoinSwapGroup failed: " << status;

#ifdef _DEBUG
                CLUSTER_LOG << "Values before Query: m_GroupeId(" << m_GroupId << "), m_BarrierId (" << m_BarrierId << ")";

                NvAPI_D3D1x_QuerySwapGroup(pDevice, pSwapChain, &m_GroupId, &m_BarrierId);
                CLUSTER_LOG << "Values after Query m_GroupeId(" << m_GroupId << "), m_BarrierId (" << m_BarrierId << ")";
#endif
            }
        }
    }

    void PluginCSwapGroupClient::EnableSwapBarrier(IUnknown* const pDevice, const bool value)
    {
        if (m_GroupId == 1)
        {
            const NvU32 newSwapBarrier = (value) ? 1 : 0;
            CLUSTER_LOG << "EnableSwapBarrier: " << (value ? "true" : "false") << ", newSwapBarrier ID is " << newSwapBarrier;

            if ((newSwapBarrier != m_BarrierId) && (newSwapBarrier <= m_GSyncBarriers))
            {
                const auto status = NvAPI_D3D1x_BindSwapBarrier(pDevice, m_GroupId, newSwapBarrier);

                if (status == NvAPI_Status::NVAPI_OK)
                {
                    CLUSTER_LOG << "NvAPI_D3D1x_BindSwapBarrier returned NVAPI_OK";
                    m_BarrierId = newSwapBarrier;
                }
                else
                {
                    CLUSTER_LOG_ERROR << "NvAPI_D3D1x_BindSwapBarrier failed: " << status;
                }
            }
            CLUSTER_LOG << "EnableSwapBarrier: already set, nothing has been called";
        }
        else
        {
            CLUSTER_LOG << "EnableSwapBarrier: (NULL), m_GroupId is different than 1";
        }
    }

    void PluginCSwapGroupClient::EnableSyncCounter(const bool value)
    {
        m_GSyncCounter = value;
    }
}
