using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using System.Reflection;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global -> Instantiated through routing
    public partial class Missions: IDisposable
    {
        [Inject]
        MissionsService MissionsService { get; set; } = default!;
        [Inject]
        LaunchConfigurationService LaunchConfigurationService { get; set; } = default!;
        [Inject]
        MissionControlStatusService MissionControlStatus { get; set; } = default!;
        [Inject]
        AssetsService AssetsService { get; set; } = default!;
        [Inject]
        MissionCommandsService MissionCommandsService { get; set; } = default!;
        [Inject]
        DialogService DialogService { get; set; } = default!;
        [Inject]
        NotificationService NotificationService { get; set; } = default!;

        RadzenDataGrid<SavedMissionSummary> m_MissionsGrid = default!;
        IList<SavedMissionSummary>? m_SelectedSnapshots;

        bool HasSelection => MissionsService.Collection.Values.Any(i => m_SelectedSnapshots?.Contains(i) ?? false);

        void SelectAllOrNone(bool all)
        {
            if (all)
            {
                m_SelectedSnapshots = MissionsService.Collection.Values.ToList();
            }
            else
            {
                m_SelectedSnapshots = null;
            }
        }

        protected override void OnInitialized()
        {
            MissionsService.Collection.SomethingChanged += MissionsChanged;
            LaunchConfigurationService.ReadOnlyMissionControlValue.ObjectChanged += LaunchConfigurationChanged;
            LaunchConfigurationService.WorkValue.ObjectChanged += LaunchConfigurationChanged;
            MissionControlStatus.ObjectChanged += StatusChanged;
            AssetsService.Collection.SomethingChanged += AssetsChanged;
        }

        async Task Load(SavedMissionSummary mission)
        {
            await MissionCommandsService.LoadMissionAsync(mission.Id);
            NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Loaded snapshot \"{mission.Description.Name}\" into the current launch configuration.");
        }

        async Task Delete(SavedMissionSummary mission)
        {
            var ret = await DialogService.CustomConfirm($"Do you really want to delete {mission.Description.Name}?",
                "Confirm deletion", new () { OkButtonText = "Yes", CancelButtonText = "No" });
            if (!ret)
            {
                return;
            }

            await MissionsService.DeleteAsync(mission.Id);
            NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Deleted snapshot \"{mission.Description.Name}\"");
        }

        async Task DeleteSelectedSnapshots()
        {
            if (!HasSelection) return;

            var ret = await DialogService.CustomConfirm($"Do you want to delete the selected snapshots?",
    "Confirm deletion", new() { OkButtonText = "Yes", CancelButtonText = "No" });
            if (!ret)
            {
                return;
            }

            await Task.WhenAll(m_SelectedSnapshots!.Select(i => MissionsService.DeleteAsync(i.Id)));
            SelectAllOrNone(all: false);
            NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Snapshots deleted");
        }

        public void Dispose()
        {
            AssetsService.Collection.SomethingChanged -= AssetsChanged;
            LaunchConfigurationService.ReadOnlyMissionControlValue.ObjectChanged -= LaunchConfigurationChanged;
            LaunchConfigurationService.WorkValue.ObjectChanged -= LaunchConfigurationChanged;
            MissionControlStatus.ObjectChanged -= StatusChanged;
            MissionsService.Collection.SomethingChanged -= MissionsChanged;
        }

        void MissionsChanged(IReadOnlyIncrementalCollection obj)
        {
            StateHasChanged();
            m_MissionsGrid.Reload();
        }

        void LaunchConfigurationChanged(ObservableObject obj)
        {
            StateHasChanged();
        }

        void StatusChanged(ObservableObject obj)
        {
            StateHasChanged();
        }

        void AssetsChanged(IReadOnlyIncrementalCollection obj)
        {
            StateHasChanged();
            m_MissionsGrid.Reload();
        }
    }
}
