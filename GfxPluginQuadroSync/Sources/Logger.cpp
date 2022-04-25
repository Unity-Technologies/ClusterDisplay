#include "Logger.h"

namespace GfxQuadroSync
{
    void Logger::SetManagedCallback(ManagedCallback managedCallback)
    {
        m_ManagedCallback = managedCallback;
    }

    void Logger::LogMessage(LogType logType, const std::string& message)
    {
        if (m_ManagedCallback)
        {
            m_ManagedCallback((int)logType, message.c_str());
        }
    }
}
