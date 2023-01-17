using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global -> Instantiated through routing
    public partial class Devices: IDisposable
    {
        [Inject]
        ComplexesService Complexes { get; set; } = default!;
        [Inject]
        DialogService DialogService { get; set; } = default!;

        public void Dispose()
        {
            Complexes.Collection.SomethingChanged -= ComplexesChanged;
        }

        protected override void OnInitialized()
        {
            Complexes.Collection.SomethingChanged += ComplexesChanged;
        }

        IList<LaunchComplex>? m_SelectedLaunchComplexes;
        LaunchComplex? SelectedLaunchComplexes => m_SelectedLaunchComplexes?.FirstOrDefault();

        RadzenDataGrid<LaunchComplex> m_ComplexesGrid = default!;

        async Task AddLaunchComplex()
        {
            await DialogService.OpenAsync<Dialogs.EditLaunchComplex>($"Add a new Launch Complex",
               new Dictionary<string, object>(),
               new DialogOptions() { Width = "60%", Height = "60%", Resizable = true, Draggable = true });
        }

        async Task EditLaunchComplex()
        {
            var selected = SelectedLaunchComplexes;
            if (selected == null)
            {
                return;
            }

            await DialogService.OpenAsync<Dialogs.EditLaunchComplex>($"Edit {selected.Name}",
               new Dictionary<string, object>() { { "ToEdit", selected.DeepClone() } },
               new DialogOptions() { Width = "60%", Height = "60%", Resizable = true, Draggable = true });

        }

        async Task DeleteLaunchComplex()
        {
            var selected = SelectedLaunchComplexes;
            if (selected == null)
            {
                return;
            }

            var ret = await DialogService.Confirm($"Do you want to delete \"{selected.Name}\"?",
                "Confirm deletion", new () { OkButtonText = "Yes", CancelButtonText = "No" });
            if (!ret.HasValue || !ret.Value)
            {
                return;
            }

            await Complexes.DeleteAsync(selected.Id);
        }

        void ComplexesChanged(IReadOnlyIncrementalCollection obj)
        {
            StateHasChanged();
            m_ComplexesGrid.Reload();
        }
    }
}
