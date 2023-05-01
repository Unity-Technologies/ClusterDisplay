using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;
using static Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs.DialogExtensions;

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

        IList<LaunchComplex> m_SelectedComplexes = new List<LaunchComplex>();

        bool HasSelection => Complexes.Collection.Values.Any(i => m_SelectedComplexes.Contains(i));

        void SelectAllOrNone(bool all)
        {
            m_SelectedComplexes.Clear();
            if (all)
            {
                foreach (var complex in Complexes.Collection.Values)
                {
                    m_SelectedComplexes.Add(complex);
                }
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
            var ret = await DialogService.OpenAsync<Dialogs.EditLaunchComplex>($"Edit {launchComplex.Name}",
               new Dictionary<string, object>() { { "ToEdit", launchComplex.DeepClone() } },
               new DialogOptions() { Width = "60%", Height = "80%", Resizable = true, Draggable = true });
            if (ret != null)
            {
                NotificationService.Notify(NotificationSeverity.Info, $"{launchComplex.Name} updated");
            }
        }

        protected async Task DeleteLaunchComplex(LaunchComplex launchComplex)
        {
            var ret = await DialogService.CustomConfirm($"Do you want to delete \"{launchComplex.Name}\"?",
                "Confirm deletion", new() { OkButtonText = "Yes", CancelButtonText = "No" });
            if (!ret)
            {
                return;
            }

            await Complexes.DeleteAsync(launchComplex.Id);
            SelectAllOrNone(all: false);
            NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Node \"{launchComplex.Name}\" was deleted.");
        }

        protected async Task DeleteSelectedLaunchComplexes()
        {
            if (!HasSelection) return;
            string names = String.Join(", ", m_SelectedComplexes.Select(i => $"\"{i.Name}\"") ?? Enumerable.Empty<string>());

            var ret = await DialogService.CustomConfirm($"Do you want to delete {names}?",
    "Confirm deletion", new() { OkButtonText = "Yes", CancelButtonText = "No" });
            if (!ret)
            {
                return;
            }

            await Task.WhenAll(m_SelectedComplexes.Select(i => Complexes.DeleteAsync(i.Id)));
            SelectAllOrNone(all: false);
            NotificationService.Notify(severity: NotificationSeverity.Info, summary: "Nodes deleted");
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
