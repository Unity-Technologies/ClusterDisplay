using System.Threading;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// <see cref="IApplicationProcess"/> that trigger the shutdown of the capcom process if requested by MissionControl.
    /// </summary>
    public class ShutdownCapcomProcess: IApplicationProcess
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="toCancel">To cancel when we detect a shutdown is requested</param>
        public ShutdownCapcomProcess(CancellationTokenSource toCancel)
        {
            m_ToCancel = toCancel;
        }

        public void Process(MissionControlMirror missionControlMirror)
        {
            if (!missionControlMirror.CapcomUplink.IsRunning)
            {
                // In theory, we could do the m_ToCancel right here.  However, when running unit tests once in a while
                // the cancel seem to happen at a time where HttpClient.GetAsync is just starting and it fails to deal
                // with the state switching at that moment.  Sleep 50 ms before canceling seem to fix the issue.  It is
                // not a perfect fix but it is probably enough as in real life MissionControl will wait for a given
                // amount of time for a graceful shutdown of capcom and then simply kill the process.
                Task.Run(async () =>
                {
                    await Task.Delay(50).ConfigureAwait(false);
                    m_ToCancel.Cancel();
                });
            }
        }

        CancellationTokenSource m_ToCancel;
    }
}
