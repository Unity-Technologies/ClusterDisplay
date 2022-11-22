using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/incrementalCollectionsUpdate")]
    public class IncrementalCollectionsUpdateController : ControllerBase
    {
        public IncrementalCollectionsUpdateController(IncrementalCollectionCatalogService collectionsCatalog)
        {
            m_CollectionsCatalog = collectionsCatalog;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            List<Task> waitOnTasks = new();
            CancellationTokenSource waitingForTooLongCancel = new();
            List<(string collectionName, Task<object> taskWithUpdate)> observableObjects = new();

            try
            {
                var queryParameters = HttpContext.Request.Query;
                for (int objectIndex = 0; ; ++objectIndex)
                {
                    if (!queryParameters.TryGetValue($"name{objectIndex}", out var collectionName))
                    {
                        // We reached the last object
                        break;
                    }

                    if (!queryParameters.TryGetValue($"fromVersion{objectIndex}", out var fromVersionString))
                    {
                        return BadRequest($"Missing fromVersion{objectIndex} for {collectionName}");
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

                    try
                    {
                        var taskWithUpdate = m_CollectionsCatalog.GetIncrementalUpdateFromAsync(collectionName, fromVersion,
                            waitingForTooLongCancel.Token);
                        waitOnTasks.Add(taskWithUpdate);
                        observableObjects.Add((collectionName, taskWithUpdate));
                    }
                    catch (KeyNotFoundException)
                    {
                        return BadRequest($"Cannot find incremental collection {collectionName}");
                    }
                    catch (Exception e)
                    {
                        return BadRequest($"Cannot wait on incremental collection {collectionName}: {e}");
                    }
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

                // Has any of the task generated an exception?
                foreach (var (collectionName, taskWithUpdate) in observableObjects)
                {
                    try
                    {
                        if (taskWithUpdate.IsFaulted)
                        {
                            ExceptionDispatchInfo.Capture(taskWithUpdate.Exception?.InnerException!).Throw();
                        }
                    }
                    catch (KeyNotFoundException)
                    {
                        return BadRequest($"Cannot find incremental collection {collectionName}");
                    }
                    catch (Exception e)
                    {
                        return BadRequest($"Cannot wait on incremental collection {collectionName}: {e}");
                    }
                }

                // Try to get values
                Dictionary<string, object> ret = new();
                foreach (var (collectionName, taskWithUpdate) in observableObjects)
                {
                    if (taskWithUpdate.IsCompleted)
                    {
                        ret[collectionName] = taskWithUpdate.Result;
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

        readonly IncrementalCollectionCatalogService m_CollectionsCatalog;

        /// <summary>
        /// Maximum amount of time we want to wait for the request objects value before returning NoContent (to avoid
        /// having a REST call blocked for too long as some middleware (like Azure) does not like applications hosted
        /// on Azure).
        /// </summary>
        static readonly TimeSpan k_MaxWait = TimeSpan.FromMinutes(3);
    }
}
