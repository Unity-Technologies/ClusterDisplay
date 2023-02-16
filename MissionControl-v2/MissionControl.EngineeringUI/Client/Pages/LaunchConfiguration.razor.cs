using System.Diagnostics;
using System.Numerics;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global -> Instantiated through routing
    public partial class LaunchConfiguration: IDisposable
    {
        [Inject]
        MissionControlStatusService MissionControlStatus { get; set; } = default!;
        [Inject]
        AssetsService AssetsService { get; set; } = default!;
        [Inject]
        LaunchConfigurationService LaunchConfigurationService { get; set; } = default!;
        [Inject]
        ComplexesService Complexes { get; set; } = default!;
        [Inject]
        MissionCommandsService MissionCommandsService { get; set; } = default!;
        [Inject]
        DialogService DialogService { get; set; } = default!;

        protected override void OnInitialized()
        {
            MissionControlStatus.ObjectChanged += MissionControlStatusChanged;
            AssetsService.Collection.SomethingChanged += AssetsChanged;
            LaunchConfigurationService.ReadOnlyMissionControlValue.ObjectChanged += LaunchConfigurationChanged;
            LaunchConfigurationService.WorkValue.ObjectChanged += LaunchConfigurationChanged;
            Complexes.Collection.SomethingChanged += ComplexesChanged;

            SyncParameterValuesToWorkLaunchConfiguration();
        }

        public void Dispose()
        {
            MissionControlStatus.ObjectChanged -= MissionControlStatusChanged;
            AssetsService.Collection.SomethingChanged -= AssetsChanged;
            LaunchConfigurationService.ReadOnlyMissionControlValue.ObjectChanged -= LaunchConfigurationChanged;
            LaunchConfigurationService.WorkValue.ObjectChanged -= LaunchConfigurationChanged;
            Complexes.Collection.SomethingChanged -= ComplexesChanged;
        }

        LaunchComplex? SelectedLaunchComplexes => m_SelectedLaunchComplexes?.FirstOrDefault();

        IEnumerable<LaunchCatalog.LaunchParameter> LaunchParameters
        {
            get
            {
                HashSet<string> launchableNames = new();
                if (AssetsService.Collection.TryGetValue(LaunchConfigurationService.WorkValue.AssetId, out var asset))
                {
                    foreach (var complex in Complexes.Collection.Values)
                    {
                        foreach (var launchPad in complex.LaunchPads)
                        {
                            var configuration = GetConfigurationForLaunchPad(complex, launchPad);
                            var launchable = configuration.GetEffectiveLaunchable(asset, launchPad);
                            if (launchable != null)
                            {
                                launchableNames.Add(launchable.Name);
                            }
                        }
                    }

                    var sortedLaunchableNames = launchableNames.OrderBy(n => n).ToList();
                    if (!sortedLaunchableNames.SequenceEqual(m_LaunchablesNames))
                    {
                        List<LaunchCatalog.LaunchParameter> launchableParameters = new();
                        foreach (var name in sortedLaunchableNames)
                        {
                            var launchable = asset.Launchables.FirstOrDefault(l => l.Name == name);
                            if (launchable != null)
                            {
                                launchableParameters.AddRange(launchable.GlobalParameters);
                            }
                        }
                        m_LaunchablesLaunchParameters = launchableParameters;
                        m_LaunchablesNames = sortedLaunchableNames;
                    }
                }
                else
                {
                    m_LaunchablesLaunchParameters = Enumerable.Empty<LaunchCatalog.LaunchParameter>();
                    m_LaunchablesNames = Enumerable.Empty<string>();
                }

                return m_LaunchablesLaunchParameters;
            }
        }
        List<LaunchParameterValue> LaunchParametersValue { get; set; } = new();

        RadzenDataGrid<LaunchComplex> m_ComplexesGrid = default!;
        RadzenDropDownDataGrid<Guid>? m_AssetsDropDown;
        IList<LaunchComplex>? m_SelectedLaunchComplexes;

        int GetActiveLaunchPads(Guid launchComplexId)
        {
            if (!AssetsService.Collection.TryGetValue(LaunchConfigurationService.WorkValue.AssetId, out var asset))
            {
                return 0;
            }

            if (!Complexes.Collection.TryGetValue(launchComplexId, out var complexDefinition))
            {
                return 0;
            }
            var complexConfiguration = LaunchConfigurationService.WorkValue.LaunchComplexes
                    .FirstOrDefault(lc => lc.Identifier == launchComplexId);
            if (complexConfiguration == null)
            {
                return 0;
            }

            int activeCount = 0;
            foreach (var launchPadConfiguration in complexConfiguration.LaunchPads.ToList())
            {
                var launchPad =
                    complexDefinition.LaunchPads.FirstOrDefault(c => c.Identifier == launchPadConfiguration.Identifier);
                if (launchPad != null)
                {
                    var launchable = launchPadConfiguration.GetEffectiveLaunchable(asset, launchPad);
                    if (launchable != null)
                    {
                        ++activeCount;
                    }
                    else
                    {
                        launchPadConfiguration.LaunchableName = "";
                    }
                }
            }

            return activeCount;
        }

        async Task ConfigureLaunchComplex(LaunchComplex complex)
        {
            //var selected = SelectedLaunchComplexes;
            //if (selected == null)
            //{
             //   return;
            //}

            var toEdit =
                LaunchConfigurationService.WorkValue.LaunchComplexes.FirstOrDefault(c => c.Identifier == complex.Id);
            if (toEdit == null)
            {
                toEdit = new() {Identifier = complex.Id};
                LaunchConfigurationService.WorkValue.LaunchComplexes =
                    LaunchConfigurationService.WorkValue.LaunchComplexes.Append(toEdit).ToList();
                LaunchConfigurationService.WorkValue.SignalChanges();
            }

            if (!AssetsService.Collection.TryGetValue(LaunchConfigurationService.WorkValue.AssetId, out var asset))
            {
                return;
            }

            var ret = await DialogService.OpenAsync<Dialogs.EditLaunchComplexConfiguration>($"Configure {selected.Name}",
                new Dictionary<string, object>{ {"Asset", asset}, {"Complex", selected}, {"ToEdit", toEdit} },
                new DialogOptions() { Width = "60%", Height = "80%", Resizable = true, Draggable = true });
            if (!(bool)(ret ?? false))
            {
                return;
            }

            LaunchConfigurationService.WorkValue.SignalChanges();
        }

        async Task Launch()
        {
            var hasAsset = LaunchConfigurationService.WorkValue.AssetId != Guid.Empty;
            if (!hasAsset)
            {
                return;
            }

            if (LaunchConfigurationService.WorkValueNeedsPush)
            {
                if (!await ApplyChanges())
                {
                    return;
                }
            }

            await MissionCommandsService.LaunchMissionAsync();
        }

        Task Stop()
        {
            return MissionCommandsService.StopCurrentMissionAsync();
        }

        async Task<bool> ApplyChanges()
        {
            // Check we are not overwriting someone else's work.
            if (LaunchConfigurationService.HasMissionControlValueChangedSinceWorkValueModified)
            {
                var ret = await DialogService.Confirm($"Launch configuration has be modified by an external source " +
                    $"since you started working on it.  Do you want to overwrite those changes?",
                    "Overwrite confirmation", new () { OkButtonText = "Yes", CancelButtonText = "No" });
                if (!ret.HasValue || !ret.Value)
                {
                    return false;
                }
            }

            // Let's do some cleanup before applying changes
            if (AssetsService.Collection.TryGetValue(LaunchConfigurationService.WorkValue.AssetId, out var asset))
            {
                foreach (var complexConfiguration in LaunchConfigurationService.WorkValue.LaunchComplexes.ToList())
                {
                    if (Complexes.Collection.TryGetValue(complexConfiguration.Identifier, out var complex))
                    {
                        foreach (var launchPadConfiguration in complexConfiguration.LaunchPads.ToList())
                        {
                            var launchPad =
                                complex.LaunchPads.FirstOrDefault(c => c.Identifier == launchPadConfiguration.Identifier);
                            if (launchPad != null)
                            {
                                var launchable = launchPadConfiguration.GetEffectiveLaunchable(asset, launchPad);
                                launchPadConfiguration.LaunchableName = launchable != null ? launchable.Name : "";
                            }
                            else
                            {
                                complexConfiguration.LaunchPads = complexConfiguration.LaunchPads
                                    .Where(l => l.Identifier != launchPadConfiguration.Identifier).ToList();
                            }
                        }
                    }

                    if (complex == null || !complexConfiguration.LaunchPads.Any())
                    {
                        LaunchConfigurationService.WorkValue.LaunchComplexes =
                            LaunchConfigurationService.WorkValue.LaunchComplexes
                                .Where(c => c.Identifier != complexConfiguration.Identifier).ToList();
                    }
                }
            }

            // Send to Mission Control
            await LaunchConfigurationService.PushWorkToMissionControlAsync();

            // Done
            return true;
        }

        void UndoChanges()
        {
            LaunchConfigurationService.ClearWorkValue();
        }

        void LaunchParametersValueUpdate(List<LaunchParameterValue> updatedValues)
        {
            Debug.Assert(ReferenceEquals(updatedValues, LaunchParametersValue));
            LaunchConfigurationService.WorkValue.SignalChanges();
        }

        void MissionControlStatusChanged(ObservableObject obj)
        {
            StateHasChanged();
        }

        void AssetsChanged(IReadOnlyIncrementalCollection obj)
        {
            StateHasChanged();
            m_AssetsDropDown?.Reload();
        }

        void LaunchConfigurationChanged(ObservableObject obj)
        {
            if (!LaunchConfigurationService.WorkValueNeedsPush)
            {
                // Work configuration is still follow mission control, so update out launch parameters values (that
                // will be modified by Components.LaunchParameters).
                SyncParameterValuesToWorkLaunchConfiguration();
            }
            StateHasChanged();
        }

        void ComplexesChanged(IReadOnlyIncrementalCollection obj)
        {
            StateHasChanged();
            m_ComplexesGrid.Reload();
        }

        void SyncParameterValuesToWorkLaunchConfiguration()
        {
            if (!ReferenceEquals(LaunchParametersValue, LaunchConfigurationService.WorkValue.Parameters))
            {
                LaunchParametersValue.Clear();
                LaunchParametersValue.AddRange(LaunchConfigurationService.WorkValue.Parameters);
                Debug.Assert(LaunchParametersValue.SequenceEqual(LaunchConfigurationService.WorkValue.Parameters));
                LaunchConfigurationService.WorkValue.Parameters = LaunchParametersValue;
            }
        }

        /// <summary>
        /// Returns the <see cref="LaunchPadConfiguration"/> for the requested <see cref="MissionControl.LaunchPad"/>.
        /// </summary>
        /// <param name="launchComplex"><see cref="LaunchComplex"/> the <see cref="MissionControl.LaunchPad"/> is a
        /// part of.</param>
        /// <param name="launchPad">The <see cref="MissionControl.LaunchPad"/>.</param>
        LaunchPadConfiguration GetConfigurationForLaunchPad(LaunchComplex launchComplex, MissionControl.LaunchPad launchPad)
        {
            var complexCfg = LaunchConfigurationService.WorkValue.LaunchComplexes
                .FirstOrDefault(lc => lc.Identifier == launchComplex.Id);
            if (complexCfg == null)
            {
                complexCfg = new() {Identifier = launchComplex.Id};
                LaunchConfigurationService.WorkValue.LaunchComplexes =
                    LaunchConfigurationService.WorkValue.LaunchComplexes.Append(complexCfg).ToList();
            }

            var padCfg = complexCfg.LaunchPads.FirstOrDefault(c => c.Identifier == launchPad.Identifier);
            if (padCfg == null)
            {
                padCfg = new() {Identifier = launchPad.Identifier};
                if (AssetsService.Collection.TryGetValue(LaunchConfigurationService.WorkValue.AssetId, out var asset))
                {
                    padCfg.LaunchableName = launchPad.GetCompatibleLaunchables(asset).Select(l => l.Name).FirstOrDefault("");
                }
                complexCfg.LaunchPads = complexCfg.LaunchPads.Append(padCfg).ToList();
            }

            return padCfg;
        }

        async Task SaveAs()
        {
            var ret = await DialogService.OpenAsync<Dialogs.SaveDialog>($"Save current configuration as...",
                new Dictionary<string, object>(),
                new DialogOptions() { Width = "80%", Height = "60%", Resizable = true, Draggable = true });

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

        /// <summary>
        /// Name of the <see cref="Launchable"/> of the <see cref="LaunchPad"/>s.
        /// </summary>
        IEnumerable<string> m_LaunchablesNames = Enumerable.Empty<string>();
        /// <summary>
        /// <see cref="LaunchCatalog.LaunchParameter"/> of the <see cref="Launchable"/> of the <see cref="LaunchPad"/>s.
        /// </summary>
        IEnumerable<LaunchCatalog.LaunchParameter> m_LaunchablesLaunchParameters = Enumerable.Empty<LaunchCatalog.LaunchParameter>();
    }
}
