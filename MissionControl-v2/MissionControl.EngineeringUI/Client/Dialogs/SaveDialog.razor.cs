using Microsoft.AspNetCore.Components;
using Radzen;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs
{
    public partial class SaveDialog
    {
        [Parameter]
        public IEnumerable<SavedMissionSummary> Overwritables { get; set; } = Enumerable.Empty<SavedMissionSummary>();

        [Parameter]
        public SaveMissionCommand Command { get; set; } = new() { Identifier = Guid.NewGuid() };

        [Inject]
        DialogService DialogService { get; set; } = default!;

        SavedMissionSummary? m_SelectedForOverwrite { get; set; }

        string SaveButtonText => m_SelectedForOverwrite == null ? "Save" : "Overwrite";

        protected override void OnInitialized()
        {
            base.OnInitialized();
            m_SelectedForOverwrite = Overwritables?.FirstOrDefault();

            OverwriteSelectionChanged();
        }

        void OverwriteSelectionChanged()
        {
            if (m_SelectedForOverwrite == null) return;

            Command.Identifier = m_SelectedForOverwrite.Id;
            Command.Description = m_SelectedForOverwrite.Description;
        }

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
