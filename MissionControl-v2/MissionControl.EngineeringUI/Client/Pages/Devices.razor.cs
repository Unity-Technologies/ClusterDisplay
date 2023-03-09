using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using System.Xml.Linq;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Pages
{
    public partial class Devices : IDisposable
    {
        [Inject]
        ComplexesService Complexes { get; set; } = default!;
        [Inject]
        DialogService DialogService { get; set; } = default!;
        [Inject]
        NotificationService NotificationService { get; set; } = default!;

        [Inject]
        MissionControlStatusService MissionControlStatus { get; set; } = default!;
        protected RadzenDataGrid<LaunchComplex> m_ComplexesGrid = default!;

        IList<LaunchComplex>? m_SelectedComplexes;

        bool HasSelection => Complexes.Collection.Values.Any(i => m_SelectedComplexes?.Contains(i) ?? false);

        void SelectAllOrNone(bool all)
        {
            if (all)
            {
                m_SelectedComplexes = Complexes.Collection.Values.ToList();                
            }
            else
            {
                m_SelectedComplexes = null;
            }
        }

        protected override void OnInitialized()
        {
            Complexes.Collection.SomethingChanged += ComplexesChanged;
        }

        protected async Task AddLaunchComplex()
        {
            await DialogService.OpenAsync<Dialogs.EditLaunchComplex>($"Add a new Launch Complex",
               new Dictionary<string, object>(),
               new DialogOptions() { Width = "60%", Height = "80%", Resizable = true, Draggable = true });
        }

        protected async Task EditLaunchComplex(LaunchComplex launchComplex)
        {
            await DialogService.OpenAsync<Dialogs.EditLaunchComplex>($"Edit {launchComplex.Name}",
               new Dictionary<string, object>() { { "ToEdit", launchComplex.DeepClone() } },
               new DialogOptions() { Width = "60%", Height = "80%", Resizable = true, Draggable = true });
        }

        protected async Task DeleteLaunchComplex(LaunchComplex launchComplex)
        {
            var ret = await DialogService.Confirm($"Do you want to delete \"{launchComplex.Name}\"?",
                "Confirm deletion", new() { OkButtonText = "Yes", CancelButtonText = "No" });
            if (!ret.HasValue || !ret.Value)
            {
                return;
            }

            await Complexes.DeleteAsync(launchComplex.Id);
            SelectAllOrNone(all: false);
            NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Node \"{launchComplex.Name}\" was deleted.");
        }

        protected async Task DeleteSelectedLaunchComplex()
        {
            if (!HasSelection) return;
            string names = String.Join("; ", m_SelectedComplexes!.Select(i => i.Name) ?? Enumerable.Empty<string>());

            var ret = await DialogService.Confirm($"Do you want to delete the following complexes? {names}",
    "Confirm deletion", new() { OkButtonText = "Yes", CancelButtonText = "No" });
            if (!ret.HasValue || !ret.Value)
            {
                return;
            }

            await Task.WhenAll(m_SelectedComplexes!.Select(i => Complexes.DeleteAsync(i.Id)));
            SelectAllOrNone(all: false);
            NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Nodes deleted");
        }

        public void Dispose()
        {
            Complexes.Collection.SomethingChanged -= ComplexesChanged;
        }

        void ComplexesChanged(IReadOnlyIncrementalCollection obj)
        {
            StateHasChanged();
            m_ComplexesGrid.Reload();
        }
    }
}
