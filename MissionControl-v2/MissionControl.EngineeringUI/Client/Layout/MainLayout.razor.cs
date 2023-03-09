using Microsoft.AspNetCore.Components;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Layout
{
    public partial class MainLayout: IDisposable
    {
        [Inject]
        UiGlobal UiGlobal { get; set; } = default!;
        [Inject]
        MissionControlStatusService MissionControlStatus { get; set; } = default!;
        [Inject]
        LaunchConfigurationService LaunchConfigurationService { get; set; } = default!;
        [Inject]
        MissionCommandsService MissionCommandsService { get; set; } = default!;

        protected override void OnInitialized()
        {
            MissionControlStatus.ObjectChanged += MissionControlStatusChanged;
        }

        public void Dispose()
        {
            MissionControlStatus.ObjectChanged -= MissionControlStatusChanged;
        }

        void MissionControlStatusChanged(ObservableObject obj)
        {
            StateHasChanged();
        }
    }
}
