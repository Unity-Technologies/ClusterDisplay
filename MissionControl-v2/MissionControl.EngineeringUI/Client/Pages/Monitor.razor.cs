using System;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global -> Instantiated through routing
    public partial class Monitor
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
                        Cells["Memory usage (GiB)"] = new Cell() { Value = ToGibibytes(Health.MemoryUsage) };
                        Cells["Memory installed (GiB)"] = new Cell() { Value = ToGibibytes(Health.MemoryInstalled) };
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
            static string ToGibibytes(long value) => (value / (double)(1024 * 1024 * 1024)).ToString("0.00");


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
    }
}
