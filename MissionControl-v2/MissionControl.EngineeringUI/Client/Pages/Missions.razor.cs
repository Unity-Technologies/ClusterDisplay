using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using System.Reflection;
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
        NavigationManager NavigationManager { get; set; } = default!;

        SavedMissionSummary? SelectedMission => m_SelectedMission?.FirstOrDefault();

        RadzenDataGrid<SavedMissionSummary> m_MissionsGrid = default!;
        IList<SavedMissionSummary>? m_SelectedMission;

        protected override void OnInitialized()
        {
            MissionsService.Collection.SomethingChanged += MissionsChanged;
            LaunchConfigurationService.ReadOnlyMissionControlValue.ObjectChanged += LaunchConfigurationChanged;
            LaunchConfigurationService.WorkValue.ObjectChanged += LaunchConfigurationChanged;
            MissionControlStatus.ObjectChanged += StatusChanged;
            AssetsService.Collection.SomethingChanged += AssetsChanged;
        }

        async Task SaveAs()
        {
            var ret = await DialogService.OpenAsync<Dialogs.SaveDialog>($"Save current configuration as...",
                new Dictionary<string, object>(),
                new DialogOptions() { Width = "60%", Height = "60%", Resizable = true, Draggable = true });

            if (ret == null)
            {
                return;
            }

            if (LaunchConfigurationService.WorkValueNeedsPush)
            {
                await LaunchConfigurationService.PushWorkToMissionControlAsync();
            }

            await MissionCommandsService.SaveMissionAsync((SaveMissionCommand)ret);
        }

        async Task Overwrite()
        {
            var selectedMission = SelectedMission;
            if (selectedMission == null)
            {
                return;
            }

            SaveMissionCommand saveCommand = new() { Identifier = selectedMission.Id };
            saveCommand.Description.DeepCopyFrom(selectedMission.Description);

            var ret = await DialogService.OpenAsync<Dialogs.SaveDialog>($"Save current configuration as...",
                new Dictionary<string, object>{ {"Command", saveCommand} },
                new DialogOptions() { Width = "60%", Height = "60%", Resizable = true, Draggable = true });

            if (ret == null)
            {
                return;
            }

            if (LaunchConfigurationService.WorkValueNeedsPush)
            {
                await LaunchConfigurationService.PushWorkToMissionControlAsync();
            }

            await MissionCommandsService.SaveMissionAsync((SaveMissionCommand)ret);
        }

        async Task Load(SavedMissionSummary mission)
        {
            //var selectedMission = SelectedMission;
           // if (selectedMission == null)
            //{
               // return;
            //}

            if (LaunchConfigurationService.WorkValueNeedsPush)
            {
                var ret = await DialogService.Confirm($"Discard non applied changes to the launch configuration?",
                    "Discard changes?", new () { OkButtonText = "Yes", CancelButtonText = "No" });
                if (!ret.HasValue || !ret.Value)
                {
                    return;
                }
                LaunchConfigurationService.ClearWorkValue();
            }

            await MissionCommandsService.LoadMissionAsync(mission.Id);
        }

        async Task LoadAndLaunch(SavedMissionSummary mission)
        {
            //var selectedMission = SelectedMission;
           // if (selectedMission == null)
           // {
               // return;
           // }

            await MissionCommandsService.LoadMissionAsync(mission.Id);

            await MissionCommandsService.LaunchMissionAsync();

            NavigationManager.NavigateTo("/launch");
        }

        async Task Delete(SavedMissionSummary mission)
        {
            //var selectedMission = SelectedMission;
            //if (selectedMission == null)
           // {
               // return;
          //  }

            var ret = await DialogService.Confirm($"Do you really want to delete {mission.Description.Name}?",
                "Confirm deletion", new () { OkButtonText = "Yes", CancelButtonText = "No" });
            if (!ret.HasValue || !ret.Value)
            {
                return;
            }

            await MissionsService.DeleteAsync(mission.Id);
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
