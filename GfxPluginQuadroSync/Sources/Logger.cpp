#include "Logger.h"

#include "../../External/NvAPI/nvapi.h"

namespace GfxQuadroSync
{
    void Logger::SetManagedCallback(const ManagedCallback managedCallback)
    {
        m_ManagedCallback = managedCallback;
    }

    void Logger::LogMessage(const LogType logType, const std::string& message)
    {
        if (m_ManagedCallback)
        {
            m_ManagedCallback((int)logType, message.c_str());
        }
    }

    std::ostream& operator<<(std::ostream& os, const NvAPI_Status status)
    {
        NvAPI_ShortString statusString;
        NvAPI_GetErrorMessage(status, statusString);
        return os << statusString << " (" << (int)status << ')';
    }
}
