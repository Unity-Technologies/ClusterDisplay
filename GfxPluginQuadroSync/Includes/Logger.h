#pragma once

#include <sstream>

#include "../../External/NvAPI/nvapi_lite_common.h"

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

    /**
     * operator<< for NvAPI_Status that will write it to the stream as a number and a string (string returned by
     * NvAPI_GetErrorMessage).  Ideal to conclude a message about a call to NvAPI that failed.
     */
    std::ostream& operator<<(std::ostream& os, NvAPI_Status status);
}

/**
 * \brief Macro to be used to log error messages.
 *
 * Use this macro to log error messages as you would use a std::ostringstream.  As a bonus, processing will be limited
 * to a simple if in the event where logging is not enabled.
 *
 * \remark Inverting the condition in the if and putting everything in the else might look strange but this is to avoid
 *         problems in cases where someone would do something like:
 *         if ( condition ) CLUSTER_LOG_ERROR << "Hello"; else somethingElse();
 *         Without that somethingElse would be executed when !AreMessagesUseful() as opposed as when !condition.
 */
#define CLUSTER_LOG_ERROR if ( !GfxQuadroSync::Logger::Instance().AreMessagesUseful() ) ; else LoggingStream(GfxQuadroSync::LogType::Error)

 /**
  * \brief Macro to be used to log warning messages.
  *
  * Use this macro to log error messages as you would use a std::ostringstream.  As a bonus, processing will be limited
  * to a simple if in the event where logging is not enabled.
  *
  * \remark Inverting the condition in the if and putting everything in the else might look strange but this is to avoid
  *         problems in cases where someone would do something like:
  *         if ( condition ) CLUSTER_LOG_ERROR << "Hello"; else somethingElse();
  *         Without that somethingElse would be executed when !AreMessagesUseful() as opposed as when !condition.
  */
#define CLUSTER_LOG_WARNING if ( !GfxQuadroSync::Logger::Instance().AreMessagesUseful() ) ; else LoggingStream(GfxQuadroSync::LogType::Warning)

/**
 * \brief Macro to be used to log messages.
 *
 * Use this macro to log error messages as you would use a std::ostringstream.  As a bonus, processing will be limited
 * to a simple if in the event where logging is not enabled.
 *
 * 
 * \remark Inverting the condition in the if and putting everything in the else might look strange but this is to avoid
 *         problems in cases where someone would do something like:
 *         if ( condition ) CLUSTER_LOG_ERROR << "Hello"; else somethingElse();
 *         Without that somethingElse would be executed when !AreMessagesUseful() as opposed as when !condition.
 */
#define CLUSTER_LOG if ( !GfxQuadroSync::Logger::Instance().AreMessagesUseful() ) ; else LoggingStream(GfxQuadroSync::LogType::Log)
