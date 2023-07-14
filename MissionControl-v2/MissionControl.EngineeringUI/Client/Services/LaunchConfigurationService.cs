using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class LaunchConfigurationServiceExtension
    {
        public static void AddLaunchConfigurationService(this IServiceCollection services)
        {
            services.AddSingleton<LaunchConfigurationService>();
        }
    }

    /// <summary>
    /// Service giving access (read / write) to MissionControl's <see cref="LaunchConfiguration"/>.
    /// </summary>
    public class LaunchConfigurationService
    {
        public LaunchConfigurationService(HttpClient httpClient,
            IConfiguration configuration,
            ObjectsUpdateService objectsUpdateService)
        {
            m_HttpClient = httpClient;
            m_HttpClient.BaseAddress = configuration.GetMissionControlUri();

            objectsUpdateService.RegisterForUpdates(ObservableObjectsName.CurrentMissionLaunchConfiguration,
                LaunchConfigurationUpdate);
            m_MissionControlValue.ObjectChanged += ReadOnlyObjectChanged;
        }

        /// <summary>
        /// Read only <see cref="LaunchConfiguration"/> reflecting the current state of MissionControl's Launch
        /// Configuration.
        /// </summary>
        /// <remarks>Nothing really enforce the read only aspect at compile time but it will throw an exception if
        /// modified by external users when it calls <see cref="ObservableObject.SignalChanges"/>.</remarks>
        public LaunchConfiguration ReadOnlyMissionControlValue => m_MissionControlValue;

        /// <summary>
        /// <see cref="LaunchConfiguration"/> reflecting the Launch Configuration as modified by the user.
        /// </summary>
        /// <remarks>Ideally someone modifying it should try to call <see cref="ObservableObject.SignalChanges"/> after
        /// modifications have been done (but it is not critical, mostly to trigger UI updates for parts that are not
        /// modifying it).</remarks>
        public LaunchConfiguration WorkValue => m_WorkValue;

        /// <summary>
        /// Does <see cref="WorkValue"/> needs to be pushed (has any change been made to it compared to the last
        /// MissionControl's last update)?
        /// </summary>
        public bool WorkValueNeedsPush => !m_WorkValue.Equals(m_MissionControlValue);

        /// <summary>
        /// Returns if <see cref="ReadOnlyMissionControlValue"/> has changed since the moment we started editing
        /// <see cref="WorkValue"/>.
        /// </summary>
        public bool HasMissionControlValueChangedSinceWorkValueModified => !m_MissionControlValue.Equals(m_WorkValueStartingPoint);

        /// <summary>
        /// Push <see cref="WorkValue"/> to MissionControl (so that <see cref="WorkValue"/> ==
        /// <see cref="ReadOnlyMissionControlValue"/> once update is complete).
        /// </summary>
        public async Task PushWorkToMissionControlAsync()
        {
            // Needs to be before or otherwise quick update from mission control could result in testing for
            // m_JustPushed before we had the time to set it!
            m_JustPushed = true;

            var responseMessage = await m_HttpClient.PutAsJsonAsync("currentMission/launchConfiguration", m_WorkValue,
                Json.SerializerOptions);
            if (!responseMessage.IsSuccessStatusCode)
            {
                m_JustPushed = false;
                throw new InvalidOperationException(await responseMessage.Content.ReadAsStringAsync());
            }
        }

        /// <summary>
        /// Restore <see cref="WorkValue"/> to <see cref="ReadOnlyMissionControlValue"/>.
        /// </summary>
        public void ClearWorkValue()
        {
            m_WorkValueStartingPoint.DeepCopyFrom(ReadOnlyMissionControlValue);
            WorkValue.DeepCopyFrom(ReadOnlyMissionControlValue);
            WorkValue.SignalChanges();
        }

        /// <summary>
        /// Set the working values to defaults (that is, uninitialized states)
        /// </summary>
        public void ResetParametersToDefault()
        {
            WorkValue.LaunchComplexes = Enumerable.Empty<LaunchComplexConfiguration>();
            WorkValue.Parameters = Enumerable.Empty<LaunchParameterValue>();
            WorkValue.SignalChanges();
        }

        void LaunchConfigurationUpdate(JsonElement update)
        {
            var deserializeRet = update.Deserialize<LaunchConfiguration>(Json.SerializerOptions);
            if (deserializeRet == null)
            {
                return;
            }

            // We want to update work value if:
            // 1. Have we just pushed a work LaunchConfiguration to MissionControl (so work copy should be
            //    equivalent or the result of small modifications after the push).
            // 2. It is equal to the last known launch configuration from MissionControl (was not modified by the user).
            bool updateWorkValue = m_JustPushed || m_WorkValue.Equals(m_MissionControlValue);
            m_JustPushed = false;

            try
            {
                m_ValueChangesAllowed = true;
                m_MissionControlValue.DeepCopyFrom(deserializeRet);
                m_MissionControlValue.SignalChanges();
            }
            finally
            {
                m_ValueChangesAllowed = false;
            }

            if (updateWorkValue)
            {
                m_WorkValue.DeepCopyFrom(deserializeRet);
                m_WorkValue.SignalChanges();
                m_WorkValueStartingPoint.DeepCopyFrom(deserializeRet);
            }
        }

        /// <summary>
        /// Used to detect (when in debug) that <see cref="ReadOnlyMissionControlValue"/> changed by code that shouldn't
        /// modify it.
        /// </summary>
        /// <param name="obj">The object that changed.</param>
        void ReadOnlyObjectChanged(ObservableObject obj)
        {
            Debug.Assert(m_ValueChangesAllowed);
        }

        readonly HttpClient m_HttpClient;

        /// <summary>
        /// The current mirrored MissionControl's <see cref="LaunchConfiguration"/>.
        /// </summary>
        LaunchConfiguration m_MissionControlValue = new();
        /// <summary>
        /// Value of <see cref="ReadOnlyMissionControlValue"/> when <see cref="WorkValue"/> was first modified.
        /// </summary>
        LaunchConfiguration m_WorkValueStartingPoint = new();
        /// <summary>
        /// The current <see cref="LaunchConfiguration"/> as modified by the user.
        /// </summary>
        LaunchConfiguration m_WorkValue = new();
        /// <summary>
        /// Have we just pushed something to MissionControl (so we must overwrite everything on next update).
        /// </summary>
        bool m_JustPushed;
        /// <summary>
        /// Temporarily set to true while we are setting m_Value.
        /// </summary>
        bool m_ValueChangesAllowed;
    }
}
