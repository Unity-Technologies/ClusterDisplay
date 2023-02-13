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

        protected RadzenDataGrid<LaunchComplex> m_ComplexesGrid = default!;

        protected IList<LaunchComplex>? SelectedLaunchComplexes;

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

        protected async Task EditLaunchComplex(string name)
        {
            var selected = SelectedLaunchComplexes?.FirstOrDefault();
            if (selected == null)
            {
                return;
            }
            await DialogService.OpenAsync<Dialogs.EditLaunchComplex>($"Edit {name}",
               new Dictionary<string, object>() { { "ToEdit", selected.DeepClone() } },
               new DialogOptions() { Width = "60%", Height = "80%", Resizable = true, Draggable = true });
            Console.WriteLine("Selected:::", selected);

        }
        // async Task OpenOrder(int orderId)
        // {
        //   await DialogService.OpenAsync<DialogCardPage>($"Order {orderId}",
        //          new Dictionary<string, object>() { { "OrderID", orderId } },
        //          new DialogOptions() { Width = "700px", Height = "520px" });
        // }

        // new Dictionary<string, object>() { { "ToEdit", selected.DeepClone() } },

        protected async Task DeleteLaunchComplex(string name)
        {
            var selected = SelectedLaunchComplexes?.FirstOrDefault();
            if (selected == null)
            {
                return;
            }

            var ret = await DialogService.Confirm($"Do you want to delete \"{name}\"?",
                "Confirm deletion", new() { OkButtonText = "Yes", CancelButtonText = "No" });
            if (!ret.HasValue || !ret.Value)
            {
                return;
            }

            await Complexes.DeleteAsync(selected.Id);
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
