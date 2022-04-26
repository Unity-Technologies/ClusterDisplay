#include "Logger.h"

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
}
