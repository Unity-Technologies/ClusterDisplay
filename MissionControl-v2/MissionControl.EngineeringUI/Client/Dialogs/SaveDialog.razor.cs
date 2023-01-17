using Microsoft.AspNetCore.Components;
using Radzen;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs
{
    public partial class SaveDialog
    {
        [Parameter]
        public SaveMissionCommand Command { get; set; } = new() { Identifier = Guid.NewGuid() };

        [Inject]
        DialogService DialogService { get; set; } = default!;

        void OnOk()
        {
            DialogService.Close(Command);
        }

        void OnCancel()
        {
            DialogService.Close();
        }
    }
}
