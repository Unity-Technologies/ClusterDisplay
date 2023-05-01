using Microsoft.AspNetCore.Components;
using System;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Options;
using Radzen;
using Radzen.Blazor;

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

        public static Task ShowError(this DialogService dialogService, string message)
        {
            DialogOptions dialogOptions = new();
            dialogOptions.Width = "75%";
            dialogOptions.Resizable = true;

            var replacedMessage = message.Replace("\n", "<br/>");

            return dialogService.OpenAsync<ShowError>("Error", new Dictionary<string, object>{ {"Message", replacedMessage} },
                dialogOptions);
        }

        public class CustomConfirmOptions
        {
            public string OkButtonText { get; set; } = "OK";
            public string CancelButtonText { get; set; } = "Cancel";
        }

        public static async Task<bool> CustomConfirm(this DialogService dialogService, string message, string title, CustomConfirmOptions confirmOptions)
        {
            var parameters = new Dictionary<string, object> { { "Message", message } };

            if (confirmOptions.OkButtonText != null)
            {
                parameters["ConfirmText"] = confirmOptions.OkButtonText;
            }
            if (confirmOptions.CancelButtonText != null)
            {
                parameters["CancelText"] = confirmOptions.CancelButtonText;
            }

            var result = await dialogService.OpenAsync<ConfirmationDialog>(title, parameters);
            // Result could be null if user clicks the close button. Treat it as "cancel" (false).
            return result is bool response && response;
        }
    }
}
