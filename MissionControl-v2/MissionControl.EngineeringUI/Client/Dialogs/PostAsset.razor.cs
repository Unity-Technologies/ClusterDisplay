using Microsoft.AspNetCore.Components;
using Radzen;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs
{
    // ReSharper disable once ClassNeverInstantiated.Global -> It is, don't know why it does not detect it...
    public partial class PostAsset
    {
        [Inject]
        AssetsService Assets { get; set; } = default!;
        [Inject]
        DialogService DialogService { get; set; } = default!;

        AssetPost Asset { get; set; } = new();

        async Task OnOk()
        {
            Guid addedId = Guid.Empty;
            string errorMessage = "";
            await DialogService.ShowBusy("Posting asset, can take a long time...", async () =>
            {
                try
                {
                    addedId = await Assets.PostAsync(Asset);
                }
                catch (InvalidOperationException e)
                {
                    errorMessage = e.Message;
                }
                catch (Exception e)
                {
                    errorMessage = e.Message;
                }
            });

            if (errorMessage != "")
            {
                await DialogService.Alert(errorMessage);
                return;
            }

            DialogService.Close(addedId);
        }

        void OnCancel()
        {
            DialogService.Close();
        }
    }
}
