using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/objectsUpdate")]
    public class ObjectsUpdateController : ControllerBase
    {
        public ObjectsUpdateController(ObservableObjectCatalogService observableObjectCatalog)
        {
            m_ObservableObjectCatalog = observableObjectCatalog;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            List<Task> waitOnTasks = new();
            CancellationTokenSource waitingForTooLongCancel = new();
            List<(string observableObjectName, Task<ObservableObjectUpdate> taskWithUpdate)> observableObjects = new();

            try
            {
                var queryParameters = HttpContext.Request.Query;
                for (int objectIndex = 0;; ++objectIndex)
                {
                    if (!queryParameters.TryGetValue($"name{objectIndex}", out var objectName))
                    {
                        // We reached the last object
                        break;
                    }

                    if (!m_ObservableObjectCatalog.TryGetValue(objectName, out var observableObject))
                    {
                        return BadRequest($"No observable object named {objectName} found");
                    }

                    if (!queryParameters.TryGetValue($"fromVersion{objectIndex}", out var fromVersionString))
                    {
                        return BadRequest($"Missing fromVersion{objectIndex} for {objectName}");
                    }

                    ulong fromVersion;
                    try
                    {
                        fromVersion = Convert.ToUInt64(fromVersionString);
                    }
                    catch (Exception e)
                    {
                        return BadRequest($"Failed to convert {fromVersionString} to integer: {e}");
                    }

                    var taskWithUpdate = observableObject.GetValueFromVersionAsync(fromVersion, waitingForTooLongCancel.Token);
                    waitOnTasks.Add(taskWithUpdate);
                    observableObjects.Add((objectName, taskWithUpdate));
                }

                if (!waitOnTasks.Any())
                {
                    // We are not asked for anything, so no content...
                    return NoContent();
                }

                // Wait for one of the objects to reach the desired version.
                var timeoutTask = Task.Delay(k_MaxWait, cancellationToken);
                waitOnTasks.Add(timeoutTask);
                await Task.WhenAny(waitOnTasks);

                // Try to get values
                Dictionary<string, object> ret = new();
                foreach (var (observableObjectName, taskWithUpdate) in observableObjects)
                {
                    if (taskWithUpdate.IsCompleted)
                    {
                        ret[observableObjectName] = taskWithUpdate.Result;
                    }
                }

                if (ret.Any())
                {
                    return Ok(ret);
                }
                else
                {
                    return NoContent();
                }
            }
            finally
            {
                waitingForTooLongCancel.Cancel();
            }
        }

        readonly ObservableObjectCatalogService m_ObservableObjectCatalog;

        /// <summary>
        /// Maximum amount of time we want to wait for the request objects value before returning NoContent (to avoid
        /// having a REST call blocked for too long as some middleware (like Azure) does not like applications hosted
        /// on Azure).
        /// </summary>
        static readonly TimeSpan k_MaxWait = TimeSpan.FromMinutes(3);
    }
}
