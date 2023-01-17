using Microsoft.AspNetCore.Components;
using Radzen;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs
{
    public partial class EditLaunchParameter
    {
        [Parameter]
        public LaunchCatalog.LaunchParameter Parameter { get; set; } = new();
        [Parameter]
        public LaunchParameterValue ToEdit { get; set; } = new();

        [Inject]
        DialogService DialogService { get; set; } = default!;

        protected override void OnInitialized()
        {
            Edited = ToEdit.DeepClone();
        }

        LaunchParameterValue Edited { get; set; } = new();

        bool EditedValueBoolean { get => Convert.ToBoolean(Edited.Value); set => Edited.Value = Convert.ToBoolean(value); }
        int EditedValueInt32 { get => Convert.ToInt32(Edited.Value); set => Edited.Value = Convert.ToInt32(value); }
        float EditedValueSingle { get => Convert.ToSingle(Edited.Value); set => Edited.Value = Convert.ToSingle(value); }
        string EditedValueString { get => Convert.ToString(Edited.Value) ?? ""; set => Edited.Value = Convert.ToString(value); }

        void OnOk()
        {
            DialogService.Close(Edited);
        }

        void OnCancel()
        {
            DialogService.Close();
        }
    }
}
