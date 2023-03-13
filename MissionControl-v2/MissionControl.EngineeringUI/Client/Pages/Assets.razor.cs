using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using System.Numerics;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global -> Instantiated through routing
    public partial class Assets: IDisposable
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

        IList<Asset>? m_SelectedAsset;
        Asset? SelectedAsset => m_SelectedAsset?.FirstOrDefault();

        Dictionary<Guid, int> AssetsUsageCount { get; }= new();

        RadzenDataGrid<Asset> m_AssetsGrid = default!;

        async Task AddAsset()
        {
            var ret = await DialogService.OpenAsync<Dialogs.PostAsset>($"Add a new Asset", new Dictionary<string, object>(),
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
            var ret = await DialogService.Confirm($"Do you want to delete \"{asset.Name}\"?",
                "Confirm deletion", new () { OkButtonText = "Yes", CancelButtonText = "No" });
            if (!ret.HasValue || !ret.Value)
            {
                return;
            }

            await AssetsService.DeleteAsync(asset.Id);
            SelectAllOrNone(all: false);
            NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Deleted asset \"{asset.Name}\"");
        }

        async Task DeleteSelectedAssets()
        {
            if (!HasSelection) return;

            var ret = await DialogService.Confirm($"Do you want to delete the selected assets?",
    "Confirm deletion", new() { OkButtonText = "Yes", CancelButtonText = "No" });
            if (!ret.HasValue || !ret.Value)
            {
                return;
            }

            await Task.WhenAll(m_SelectedAssets!.Select(i => AssetsService.DeleteAsync(i.Id)));
            SelectAllOrNone(all: false);
            NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Assets deleted");
        }

        void AssetsChanged(IReadOnlyIncrementalCollection obj)
        {
            if (m_ToSelect != Guid.Empty && AssetsService.Collection.TryGetValue(m_ToSelect, out var asset))
            {
                m_SelectedAsset = new List<Asset>{ asset };
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
