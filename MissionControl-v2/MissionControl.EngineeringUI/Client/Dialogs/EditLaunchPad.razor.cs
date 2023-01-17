using Microsoft.AspNetCore.Components;
using Radzen;
using System.Net.Http.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs
{
    public partial class EditLaunchPad
    {
        [Parameter]
        public MissionControl.LaunchPad ToEdit { get; set; } = new();
        [Parameter]
        public LaunchComplex ParentComplex { get; set; } = new(Guid.Empty);

        [Inject]
        DialogService DialogService { get; set; } = default!;
        [Inject]
        HttpClient HttpClient { get; set; } = default!;

        protected override void OnInitialized()
        {
            Edited = ToEdit.DeepClone();
            if (Edited.Identifier == Guid.Empty) // For now let's assume every launch pad are cluster nodes
            {
                Edited.SuitableFor = new[] { "clusterNode" };
            }
            EditedLaunchPadEndpoint = Edited.Endpoint.ToString();
        }

        MissionControl.LaunchPad Edited { get; set; } = new();

        string EditedLaunchPadEndpoint {
            get => m_EditedLaunchPadEndpoint;
            set
            {
                if (m_EditedLaunchPadEndpoint != value)
                {
                    m_EditedLaunchPadEndpoint = value;
                    if (Uri.TryCreate(m_EditedLaunchPadEndpoint, UriKind.Absolute, out var parsedUri) &&
                        parsedUri.Scheme == "http")
                    {
                        EditedLaunchPadEndpointErrorMessage = "";
                    }
                    else
                    {
                        EditedLaunchPadEndpointErrorMessage = $"Uri format invalid, must be http://#.#.#.#:#/.";
                    }
                }
            }
        }
        string m_EditedLaunchPadEndpoint = "";

        string EditedLaunchPadEndpointErrorMessage { get; set; } = "";
        string EditedLaunchPadIdentifierErrorMessage { get; set; } = "";

        bool IsValid => EditedLaunchPadEndpointErrorMessage == "" &&
            EditedLaunchPadIdentifierErrorMessage == "" && Edited.Identifier != Guid.Empty;

        Task OnValidateLaunchPadEndpoint()
        {
            return DialogService.ShowBusy($"Contacting {EditedLaunchPadEndpoint}...", async () =>
            {
                // Contact the hangar bay, get its config and identifier
                try
                {
                    Edited.Identifier = ToEdit.Identifier;

                    var launchPadConfig = await HttpClient.GetFromJsonAsync<LaunchPad.Config>(
                        new Uri(new Uri(EditedLaunchPadEndpoint), "api/v1/config"));
                    if (ToEdit.Identifier != Guid.Empty && launchPadConfig.Identifier != ToEdit.Identifier)
                    {
                        EditedLaunchPadIdentifierErrorMessage = $"Launchpad identifier changed from " +
                            $"{ToEdit.Identifier} to {launchPadConfig.Identifier} indicating this is not " +
                            $"the same launchpad.  Delete the launchpad and create a new one instead.";
                    }
                    else if (ToEdit.Identifier == Guid.Empty &&
                             ParentComplex.LaunchPads.Any(lp => lp.Identifier == launchPadConfig.Identifier))
                    {
                        EditedLaunchPadIdentifierErrorMessage = $"This launchpad is already present in the launch " +
                            $"complex.";
                    }
                    else
                    {
                        Edited.Identifier = launchPadConfig.Identifier;
                        EditedLaunchPadIdentifierErrorMessage = "";
                    }
                }
                catch (Exception)
                {
                    EditedLaunchPadIdentifierErrorMessage = $"Failed to contact {EditedLaunchPadEndpoint}.";
                }
            });
        }

        async Task OnOk()
        {
            await OnValidateLaunchPadEndpoint();
            if (!IsValid)
            {
                return;
            }

            Edited.Endpoint = new Uri(EditedLaunchPadEndpoint);

            DialogService.Close(Edited);
        }

        void OnCancel()
        {
            DialogService.Close();
        }
    }
}
