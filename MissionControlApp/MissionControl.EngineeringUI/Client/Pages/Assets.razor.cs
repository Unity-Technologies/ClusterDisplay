using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global -> Instantiated through routing
    public partial class Assets : IDisposable
    {
        [Inject]
        AssetsService AssetsService { get; set; } = default!;
        [Inject]
        LaunchConfigurationService LaunchConfiguration { get; set; } = default!;
        [Inject]
        MissionsService Missions { get; set; } = default!;
        [Inject]
        DialogService DialogService { get; set; } = default!;
        [Inject]
        NotificationService NotificationService { get; set; } = default!;

        IList<Asset>? m_SelectedAssets = null;

        bool HasSelection => AssetsService.Collection.Values.Any(i => m_SelectedAssets?.Contains(i) ?? false);

        void SelectAllOrNone(bool all)
        {
            if (all)
            {
                m_SelectedAssets = AssetsService.Collection.Values.ToList();
            }
            else
            {
                m_SelectedAssets = null;
            }
        }

        public void Dispose()
        {
            AssetsService.Collection.SomethingChanged -= AssetsChanged;
            LaunchConfiguration.ReadOnlyMissionControlValue.ObjectChanged -= LaunchConfigurationChanged;
            Missions.Collection.SomethingChanged -= MissionsChanged;
        }

        protected override void OnInitialized()
        {
            AssetsService.Collection.SomethingChanged += AssetsChanged;
            LaunchConfiguration.ReadOnlyMissionControlValue.ObjectChanged += LaunchConfigurationChanged;
            Missions.Collection.SomethingChanged += MissionsChanged;
            UpdateAssetsUseCount();
        }

        Dictionary<Guid, int> AssetsUsageCount { get; } = new();

        RadzenDataGrid<Asset> m_AssetsGrid = default!;

        async Task AddAsset()
        {
            var ret = await DialogService.OpenAsync<PostAsset>($"Add a new Asset", new Dictionary<string, object>(),
               new DialogOptions() { Width = "60%", Height = "60%", Resizable = true, Draggable = true });
            if (ret == null)
            {
                return;
            }

            m_ToSelect = (Guid)ret;
            AssetsChanged(AssetsService.Collection);
        }

        async Task DeleteAsset(Asset asset)
        {
            var ret = await DialogService.CustomConfirm($"Do you want to delete \"{asset.Name}\"?",
                "Confirm deletion", new() { OkButtonText = "Delete", CancelButtonText = "Cancel" });
            if (!ret)
            {
                return;
            }

            try
            {
                var response = await AssetsService.DeleteAsync(asset.Id);
                if (response.IsSuccessStatusCode)
                {
                    SelectAllOrNone(all: false);
                    NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Deleted asset \"{asset.Name}\"");
                }
                else
                {
                    NotificationService.Notify(severity: NotificationSeverity.Error, summary: $"Could not delete asset \"{asset.Name}\"");
                }
            }
            catch (Exception ex)
            {
                NotificationService.Notify(severity: NotificationSeverity.Error, summary: $"Error deleting asset: {ex.Message}");
            }
        }

        async Task DeleteSelectedAssets()
        {
            if (!HasSelection) return;

            var ret = await DialogService.CustomConfirm($"Do you want to delete the selected assets?", "Confirm deletion",
                new() { OkButtonText = "Delete", CancelButtonText = "Cancel", Prompt = "delete" });
            if (!ret)
            {
                return;
            }

            try
            {
                var results = await Task.WhenAll(m_SelectedAssets!.Select(async i => (i.Name, response: await AssetsService.DeleteAsync(i.Id))));
                foreach (var result in results)
                {
                    if (result.response.IsSuccessStatusCode)
                    {
                        NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Deleted asset \"{result.Name}\"");
                    }
                    else
                    {
                        NotificationService.Notify(severity: NotificationSeverity.Error, summary: $"Could not delete asset \"{result.Name}\"");
                    }
                }
                SelectAllOrNone(all: false);
            }
            catch (Exception ex)
            {
                NotificationService.Notify(severity: NotificationSeverity.Error, summary: $"Error deleting assets: {ex.Message}");
            }
        }

        void AssetsChanged(IReadOnlyIncrementalCollection obj)
        {
            if (m_ToSelect != Guid.Empty && AssetsService.Collection.TryGetValue(m_ToSelect, out var asset))
            {
                m_ToSelect = Guid.Empty;
            }
            StateHasChanged();
            m_AssetsGrid.Reload();
        }

        void LaunchConfigurationChanged(ObservableObject obj)
        {
            UpdateAssetsUseCount();
            StateHasChanged();
            m_AssetsGrid.Reload();
        }

        void MissionsChanged(IReadOnlyIncrementalCollection obj)
        {
            UpdateAssetsUseCount();
            StateHasChanged();
            m_AssetsGrid.Reload();
        }

        void UpdateAssetsUseCount()
        {
            AssetsUsageCount.Clear();
            if (LaunchConfiguration.ReadOnlyMissionControlValue.AssetId != Guid.Empty)
            {
                AssetsUsageCount[LaunchConfiguration.ReadOnlyMissionControlValue.AssetId] = 1;
            }
            foreach (var mission in Missions.Collection.Values)
            {
                if (mission.AssetId == Guid.Empty)
                {
                    continue;
                }

                if (AssetsUsageCount.ContainsKey(mission.AssetId))
                {
                    ++AssetsUsageCount[mission.AssetId];
                }
                else
                {
                    AssetsUsageCount[mission.AssetId] = 1;
                }
            }
        }

        /// <summary>
        /// Identifier of the asset to select as soon as it is received.
        /// </summary>
        Guid m_ToSelect;
    }
}
