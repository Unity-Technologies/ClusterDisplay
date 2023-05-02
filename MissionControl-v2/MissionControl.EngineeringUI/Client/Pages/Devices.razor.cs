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

        IList<LaunchComplex>? m_SelectedComplexes = new List<LaunchComplex>();

        bool HasSelection => m_SelectedComplexes is { } selected && Complexes.Collection.Values.Any(i => selected.Contains(i));

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
                "Confirm deletion", new() { OkButtonText = "Delete", CancelButtonText = "Cancel" });
            if (!ret)
            {
                return;
            }

            try
            {
                var response = await Complexes.DeleteAsync(launchComplex.Id);
                if (response.IsSuccessStatusCode)
                {
                    SelectAllOrNone(all: false);
                    NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Complex \"{launchComplex.Name}\" was deleted.");
                }
                else
                {
                    NotificationService.Notify(severity: NotificationSeverity.Error, summary: $"Could not delete complex \"{launchComplex.Name}\"");
                }
            }
            catch (Exception ex)
            {
                NotificationService.Notify(severity: NotificationSeverity.Error, summary: $"Error deleting complex: {ex.Message}");
            }
        }

        protected async Task DeleteSelectedLaunchComplexes()
        {
            if (!HasSelection) return;
            string names = String.Join(", ", m_SelectedComplexes!.Select(i => $"\"{i.Name}\"") ?? Enumerable.Empty<string>());

            var ret = await DialogService.CustomConfirm($"Do you want to delete {names}?", "Confirm deletion",
                new() { OkButtonText = "Delete", CancelButtonText = "Cancel", Prompt = "delete" });
            if (!ret)
            {
                return;
            }

            try
            {
                var results = await Task.WhenAll(m_SelectedComplexes!.Select(async i => (i.Name, response: await Complexes.DeleteAsync(i.Id))));
                foreach (var result in results)
                {
                    if (result.response.IsSuccessStatusCode)
                    {
                        NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Deleted complex \"{result.Name}\"");
                    }
                    else
                    {
                        NotificationService.Notify(severity: NotificationSeverity.Info, summary: $"Could not delete complex \"{result.Name}\"");
                    }
                }
                SelectAllOrNone(all: false);
            }
            catch (Exception ex)
            {
                NotificationService.Notify(severity: NotificationSeverity.Error, summary: $"Error deleting complexes: {ex.Message}");
            }
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
