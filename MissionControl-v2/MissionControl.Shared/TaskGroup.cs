using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Used to create a group of task that should all complete or if not there is no need on finishing them.
    /// </summary>
    public class TaskGroup
    {
        /// <summary>
        /// Add a task to the list of tasks to wait on.
        /// </summary>
        /// <param name="task"></param>
        public void Add(Task task)
        {
            m_ToWaitOn.Add(WaitOnOrCancel(task));
        }

        /// <summary>
        /// CancellationToken to be used for task so that they can abort their execution of a task of the group fails.
        /// </summary>
        public CancellationToken CancellationToken => m_CancellationTokenSource.Token;

        /// <summary>
        /// Returns the list of tasks added to the group with the <see cref="Add"/> method.
        /// </summary>
        public IEnumerable<Task> Tasks => m_AddedTasks;

        /// <summary>
        /// Returns the list of tasks to wait on (most likely with Task.WhenAll).
        /// </summary>
        public IEnumerable<Task> ToWaitOn => m_ToWaitOn;

        /// <summary>
        /// Force canceling the tasks (even if none of the tasks have failed).
        /// </summary>
        public void ForceCancel()
        {
            m_CancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Helper that will detect exception in a task and cancel the other if triggered.
        /// </summary>
        /// <param name="task">The task</param>
        async Task WaitOnOrCancel(Task task)
        {
            m_AddedTasks.Add(task);
            try
            {
                await task;
            }
            catch (Exception)
            {
                m_CancellationTokenSource.Cancel();
                throw;
            }
        }

        /// <summary>
        /// Used to cancel the tasks of the group as soon as one fails.
        /// </summary>
        CancellationTokenSource m_CancellationTokenSource = new();

        /// <summary>
        /// The list of tasks that was added.
        /// </summary>
        List<Task> m_AddedTasks = new();

        /// <summary>
        /// The list of tasks we will wait on.
        /// </summary>
        List<Task> m_ToWaitOn = new();
    }
}
