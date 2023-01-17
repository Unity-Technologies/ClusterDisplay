using Microsoft.AspNetCore.Components;
using Radzen;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs
{
    public partial class EditLaunchPadConfiguration
    {
        [Parameter]
        public Asset Asset { get; set; } = new(Guid.Empty);
        [Parameter]
        public MissionControl.LaunchPad LaunchPad { get; set; } = new ();
        [Parameter]
        public LaunchPadConfiguration ToEdit { get; set; } = new ();

        [Inject]
        MissionControlStatusService MissionControlStatus { get; set; } = default!;
        [Inject]
        DialogService DialogService { get; set; } = default!;

        protected override void OnInitialized()
        {
            Edited = ToEdit.DeepClone();
            Edited.LaunchableName = Edited.GetEffectiveLaunchable(Asset, LaunchPad)?.Name ?? "";

            CompatibleLaunchables = LaunchPad.GetCompatibleLaunchables(Asset).Select(l => l.Name);
            LaunchParametersValue = Edited.Parameters.ToList();
        }

        LaunchPadConfiguration Edited { get; set; } = new ();
        IEnumerable<string> CompatibleLaunchables { get; set; } = Enumerable.Empty<string>();

        Launchable? CurrentLaunchable
        {
            get
            {
                if (Edited.LaunchableName != m_LaunchablesCacheFor)
                {
                    m_LaunchablesCache = Edited.GetEffectiveLaunchable(Asset, LaunchPad);
                    m_LaunchablesCacheFor = Edited.LaunchableName;
                }
                return m_LaunchablesCache;
            }
        }
        IEnumerable<LaunchCatalog.LaunchParameter> LaunchParameters =>
            CurrentLaunchable?.LaunchPadParameters ?? Enumerable.Empty<LaunchCatalog.LaunchParameter>();
        List<LaunchParameterValue> LaunchParametersValue { get; set; } = new();
        string LaunchableNameProxy // To hide a problem with Radzen putting null even if not nullable
        {
            get => Edited.LaunchableName;
            set => Edited.LaunchableName = string.IsNullOrEmpty(value) ? "" : value;
        }

        void OnOk()
        {
            Edited.Parameters = LaunchParametersValue;
            ToEdit.DeepCopyFrom(Edited);
            DialogService.Close();
        }

        void OnCancel()
        {
            DialogService.Close();
        }

        /// <summary>
        /// <see cref="LaunchPadConfiguration.LaunchableName"/> for which <see cref="m_LaunchablesCache"/> is valid.
        /// </summary>
        string m_LaunchablesCacheFor = "";
        /// <summary>
        /// <see cref="Launchable"/> for current <see cref="Edited"/>.
        /// </summary>
        Launchable? m_LaunchablesCache;
    }
}
