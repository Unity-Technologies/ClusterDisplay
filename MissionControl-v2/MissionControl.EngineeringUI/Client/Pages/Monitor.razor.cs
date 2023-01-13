using System;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global -> Instantiated through routing
    public partial class Monitor: IDisposable
    {
        [Inject]
        MissionControlStatusService MissionControlStatus { get; set; } = default!;
        [Inject]
        ComplexesService ComplexesService { get; set; } = default!;
        [Inject]
        LaunchPadsStatusService LaunchPadsStatusService { get; set; } = default!;
        [Inject]
        LaunchPadsHealthService LaunchPadsHealthService { get; set; } = default!;
        [Inject]
        MissionParametersService MissionParametersService { get; set; } = default!;

        class LaunchPadStatusEntry
        {
            public MissionControl.LaunchPad Definition { get; init; } = new();
            public LaunchPadStatus? Status { get; set; }
            public LaunchPadHealth? Health { get; set; }
            public void Clear()
            {
                Status = null;
                Health = null;
            }

            public class Cell
            {
                public object Value { get; init; } = "";
                public string Class { get; set; } = "";
                public string BackgroundColor { get; set; } = "";
                public string PropagateBackgroundUntil { get; set; } = "";
            }

            public Dictionary<string, Cell> Cells { get; } = new();

            public List<MissionParameter> MissionParameters { get; private set; } = new();

            public void FinalizeUpdate(State missionControlState, IEnumerable<MissionParameter> missionParameters)
            {
                Cells.Clear();

                Cells["Name"] = new Cell() { Value = Definition.Name };
                if (Status is {IsDefined: true})
                {
                    Cells["Version"] = new Cell() { Value = Status.Version };
                    Cells["Started at"] = new Cell() { Value = Status.StartTime };
                    Cells["State"] = new Cell() { Value = Status.State };
                    foreach (var dynamicStatus in Status.DynamicEntries)
                    {
                        Cells[dynamicStatus.Name] = new() { Value = dynamicStatus.Value };
                    }

                    if (Health != null)
                    {
                        Cells["CPU %"] = new Cell() { Value = Health.CpuUtilization.ToString("P2") };
                        Cells["Memory usage (GB)"] = new Cell() { Value = ToGigabytes(Health.MemoryUsage) };
                        Cells["Memory installed (GB)"] = new Cell() { Value = ToGigabytes(Health.MemoryInstalled) };
                    }
                    else
                    {
                        Cells["CPU %"] = new Cell() {
                            Value = "Missing health",
                            BackgroundColor = "rz-danger-light",
                            Class = "rz-color-white"
                        };
                    }

                    if (!IsLaunchPadStatusValid(Status.State, missionControlState))
                    {
                        Cells["State"].BackgroundColor = "rz-warning-light";
                        Cells["State"].Class = "rz-color-white";
                        Cells["State"].PropagateBackgroundUntil = "CPU %";
                    }
                }
                else
                {
                    if (Status != null && Status.UpdateError != "")
                    {
                        Cells["Version"] = new Cell() { Value = Status.UpdateError };
                    }
                    else
                    {
                        Cells["Version"] = new Cell() { Value = "Missing status" };
                    }
                    Cells["Version"].BackgroundColor = "rz-danger-light";
                    Cells["Version"].Class = "rz-color-white";
                }

                string launchPadIdAsString = Definition.Identifier.ToString();
                MissionParameters = missionParameters.Where(p => p.Group == launchPadIdAsString).ToList();
            }

            static string ToGigabytes(long value)
            {
                return (value / (double)(1024 * 1024 * 1024)).ToString("0.00");
            }

            static bool IsLaunchPadStatusValid(LaunchPad.State launchpadState, State missionControlState)
            {
                switch (missionControlState)
                {
                    case State.Idle:
                        return launchpadState is LaunchPad.State.Idle or LaunchPad.State.Over;
                    case State.Preparing:
                        return !(launchpadState is LaunchPad.State.Launched or LaunchPad.State.Over);
                    case State.Launched:
                    case State.Failure:
                    default:
                        return launchpadState is LaunchPad.State.Idle or LaunchPad.State.Launched;
                }
            }
        }
        List<LaunchPadStatusEntry> LaunchPadStatusEntries { get; } = new();

        List<string> LaunchPadStatusColumns { get; } = new();

        RadzenDataGrid<LaunchPadStatusEntry>? m_LaunchapdsStatusGrid;

        protected override void OnInitialized()
        {
            MissionControlStatus.ObjectChanged += MissionControlStatusChanged;
            ComplexesService.Collection.SomethingChanged += ComplexesChanged;
            LaunchPadsStatusService.Collection.SomethingChanged += LaunchPadsStatusChanged;
            LaunchPadsHealthService.Collection.SomethingChanged += LaunchPadsHealthChanged;
            MissionParametersService.Collection.SomethingChanged += MissionParametersChanged;

            m_HealthUpdateToken = LaunchPadsHealthService.InUse();

            UpdateLaunchpads();
        }

        void RowRender(RowRenderEventArgs<LaunchPadStatusEntry> args)
        {
            bool previousExpandable = args.Expandable;
            args.Expandable = args.Data.MissionParameters.Any();
            if (previousExpandable && !args.Expandable && ReferenceEquals(args.Data, m_ExpandedRow))
            {
                m_NeedToClearExpanded = true;
                // Not nice looking but we need to trigger a refresh "soon", but not immediately...
                // The delay will cause to check again shortly and the InvokeAsync will ensure that we do it from the
                // main blazor rendering thread (at a time where we are allowed to start a refresh).
                _ = Task.Delay(100).ContinueWith(_ => InvokeAsync(UpdateLaunchpads));
            }
        }

        void CellRender(DataGridCellRenderEventArgs<LaunchPadStatusEntry> args)
        {
            if (args.Data.Cells.TryGetValue(args.Column.Title, out var cell))
            {
                if (cell.BackgroundColor != "")
                {
                    args.Attributes.Add("style", $"background-color: var(--{cell.BackgroundColor});");
                    int colSpan = GetVisibleColumnsInRange(args.Column, cell.PropagateBackgroundUntil);
                    args.Attributes.Add("colspan", colSpan);
                }
            }
        }

        int GetVisibleColumnsInRange(RadzenDataGridColumn<LaunchPadStatusEntry> from, string to)
        {
            var columns = m_LaunchapdsStatusGrid?.ColumnsCollection ?? new List<RadzenDataGridColumn<LaunchPadStatusEntry>>();
            for (int columnIndex = 0; columnIndex < columns.Count; ++columnIndex)
            {
                if (columns[columnIndex].Title == from.Title)
                {
                    int ret = 0;
                    for (; columnIndex < columns.Count; ++columnIndex)
                    {
                        if (columns[columnIndex].Title == to)
                        {
                            break;
                        }
                        if (columns[columnIndex].GetVisible())
                        {
                            ++ret;
                        }
                    }
                    return ret;
                }
            }
            return 1;
        }

        void RowExpand(LaunchPadStatusEntry expandedRow)
        {
            m_ExpandedRow = expandedRow;
        }

        public void Dispose()
        {
            MissionControlStatus.ObjectChanged -= MissionControlStatusChanged;
            ComplexesService.Collection.SomethingChanged -= ComplexesChanged;
            LaunchPadsStatusService.Collection.SomethingChanged -= LaunchPadsStatusChanged;
            LaunchPadsHealthService.Collection.SomethingChanged -= LaunchPadsHealthChanged;
            MissionParametersService.Collection.SomethingChanged -= MissionParametersChanged;
            m_HealthUpdateToken?.Dispose();
        }

        void MissionControlStatusChanged(ObservableObject obj)
        {
            StateHasChanged();
        }

        void ComplexesChanged(IReadOnlyIncrementalCollection obj)
        {
            UpdateLaunchpads();
        }

        void LaunchPadsStatusChanged(IReadOnlyIncrementalCollection obj)
        {
            UpdateLaunchpads();
        }

        void LaunchPadsHealthChanged(IReadOnlyIncrementalCollection obj)
        {
            UpdateLaunchpads();
        }

        void MissionParametersChanged(IReadOnlyIncrementalCollection obj)
        {
            UpdateLaunchpads();
        }

        void UpdateLaunchpads()
        {
            Dictionary<Guid, LaunchPadStatusEntry> oldEntries = new();
            foreach (var oldEntry in LaunchPadStatusEntries)
            {
                oldEntries[oldEntry.Definition.Identifier] = oldEntry;
                oldEntry.Clear();
            }

            // Update the list of entries based on the launchapds of every launch complex.
            foreach (var complex in ComplexesService.Collection.Values)
            {
                foreach (var launchPad in complex.LaunchPads)
                {
                    if (oldEntries.ContainsKey(launchPad.Identifier))
                    {
                        oldEntries.Remove(launchPad.Identifier);
                    }
                    else
                    {
                        LaunchPadStatusEntries.Add(new() { Definition = launchPad });
                    }
                }
            }
            foreach (var entry in LaunchPadStatusEntries.ToArray())
            {
                if (oldEntries.ContainsKey(entry.Definition.Identifier))
                {
                    LaunchPadStatusEntries.Remove(entry);
                }
            }

            // Get the status for each entry
            foreach (var entry in LaunchPadStatusEntries)
            {
                if (LaunchPadsStatusService.Collection.TryGetValue(entry.Definition.Identifier, out var status))
                {
                    entry.Status = status;
                }
            }

            // Get the health for each entry
            foreach (var entry in LaunchPadStatusEntries)
            {
                if (LaunchPadsHealthService.Collection.TryGetValue(entry.Definition.Identifier, out var health))
                {
                    entry.Health = health;
                }
            }

            LaunchPadStatusColumns.Clear();
            LaunchPadStatusColumns.Add("Name");

            // Build the list of status columns
            LaunchPadStatusColumns.Add("Version");
            LaunchPadStatusColumns.Add("Started at");
            LaunchPadStatusColumns.Add("State");
            HashSet<string> dynamicStatuses = new();
            foreach (var entry in LaunchPadStatusEntries)
            {
                if (entry.Status != null)
                {
                    foreach (var dynamicStatus in entry.Status.DynamicEntries)
                    {
                        if (dynamicStatuses.Add(dynamicStatus.Name))
                        {
                            LaunchPadStatusColumns.Add(dynamicStatus.Name);
                        }
                    }
                }
            }

            // Build the list of health columns
            LaunchPadStatusColumns.Add("CPU %");
            LaunchPadStatusColumns.Add("Memory usage (GB)");
            LaunchPadStatusColumns.Add("Memory installed (GB)");

            // Prepare the entries for the grid
            foreach (var entry in LaunchPadStatusEntries)
            {
                entry.FinalizeUpdate(MissionControlStatus.State, MissionParametersService.Collection.Values);
            }

            // Reload the grid
            m_LaunchapdsStatusGrid?.Reload();
            if (m_NeedToClearExpanded)
            {
                m_LaunchapdsStatusGrid?.ExpandRow(null!);
                m_NeedToClearExpanded = false;
            }
        }

        IDisposable? m_HealthUpdateToken;
        bool m_NeedToClearExpanded;
        LaunchPadStatusEntry? m_ExpandedRow;
    }
}
