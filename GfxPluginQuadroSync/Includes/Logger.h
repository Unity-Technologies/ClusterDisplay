#pragma once

#include <sstream>

namespace GfxQuadroSync
{
    /// Type of logging messages (matches managed UnityEngine.LogType)
    enum class LogType
    {
        Error = 0,      ///< Used for Errors.
        Assert = 1,     ///< Used for Asserts.
        Warning = 2,    ///< Used for Warnings.
        Log = 3,        ///< Used for regular log messages.
        Exception = 4,  ///< Used for Exceptions.
    };

    /**
     * \brief Helper class performing work to process logs.
     *
     * \remark Sending actual log messages is done using the CLUSTER_LOG, CLUSTER_LOG_WARNING and CLUSTER_LOG_ERROR macros.
     */
    class Logger final
    {
    public:
        /**
         * Returns access to the singleton responsible for log messages.
         *
         * \remark Inline to help performances as it is called by every LOG_* macro every time.
         */
        static Logger& Instance()
        {
            static Logger staticInstance;
            return staticInstance;
        }

        /// Type of callback to managed function that receive the log messages
        typedef void(__stdcall* ManagedCallback)(int, const char*);

        /// Sets the function to be called for every logging message we receive.
        void SetManagedCallback(ManagedCallback managedCallback);

        /// Returns if we need to spend time producing the message (because there is someone interested in them).
        bool AreMessagesUseful() const { return m_ManagedCallback != nullptr; }

        /**
         * Method called by LoggingStream to send a logging message.
         *
         * \param[in] logType Type of log message.
         * \param[in] message The actual log message text.
         */
        void LogMessage(LogType logType, const std::string& message);

    private:
        // Private constructor and destructor to enforce singleton usage
        Logger() = default;
        ~Logger() = default;

        // Member variables
        ManagedCallback m_ManagedCallback = nullptr;
    };

    /**
     * Internal mechanic class, no need to manually use it.
     *
     * \remark Used by CLUSTER_LOG, CLUSTER_LOG_WARNING and CLUSTER_LOG_ERROR macros.
     */
    class LoggingStream final : public std::ostringstream
    {
    public:
        LoggingStream(LogType logType)
            : m_LogType(logType)
        {
            *this << "QuadroSync: ";
        }

        ~LoggingStream()
        {
            Logger::Instance().LogMessage(m_LogType, str());
        }

    private:
        const LogType m_LogType;
    };
}

/**
 * \brief Macro to be used to log error messages.
 *
 * Use this macro to log error messages as you would use a std::ostringstream.  As a bonus, processing will be limited
 * to a simple if in the event where logging is not enabled.
 */
#define CLUSTER_LOG_ERROR if ( GfxQuadroSync::Logger::Instance().AreMessagesUseful() ) LoggingStream(GfxQuadroSync::LogType::Error)

 /**
  * \brief Macro to be used to log warning messages.
  *
  * Use this macro to log error messages as you would use a std::ostringstream.  As a bonus, processing will be limited
  * to a simple if in the event where logging is not enabled.
  */
#define CLUSTER_LOG_WARNING if ( GfxQuadroSync::Logger::Instance().AreMessagesUseful() ) LoggingStream(GfxQuadroSync::LogType::Warning)

/**
 * \brief Macro to be used to log messages.
 *
 * Use this macro to log error messages as you would use a std::ostringstream.  As a bonus, processing will be limited
 * to a simple if in the event where logging is not enabled.
 */
#define CLUSTER_LOG if ( GfxQuadroSync::Logger::Instance().AreMessagesUseful() ) LoggingStream(GfxQuadroSync::LogType::Log)
