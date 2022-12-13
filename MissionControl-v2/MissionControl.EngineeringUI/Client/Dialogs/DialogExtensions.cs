using Microsoft.AspNetCore.Components.Rendering;
using Radzen;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs
{
    /// <summary>
    /// <see cref="DialogService"/> extensions.
    /// </summary>
    public static class DialogExtensions
    {
        /// <summary>
        /// Show a busy dialog while doing some processing.
        /// </summary>
        /// <param name="dialogService">The service showing dialogs.</param>
        /// <param name="message">The message to show while waiting...</param>
        /// <param name="toExecute">Work to execute in background.</param>
        public static async Task ShowBusy(this DialogService dialogService, string message, Func<Task> toExecute)
        {
            var showDialogTask = dialogService.OpenAsync("", _ =>
            {
                void Content(RenderTreeBuilder b)
                {
                    b.OpenElement(0, "div");
                    b.AddAttribute(1, "class", "row");

                    b.OpenElement(2, "div");
                    b.AddAttribute(3, "class", "col-md-12");

                    b.AddContent(4, message);

                    b.CloseElement();
                    b.CloseElement();
                }

                return Content;
            }, new DialogOptions() { ShowTitle = false, Style = "min-height:auto;min-width:auto;width:auto", CloseDialogOnEsc = false });

            try
            {
                await toExecute();
            }
            finally
            {
                dialogService.Close();

                await showDialogTask;
            }
        }
    }
}
