using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// <see cref="Command"/> asking the HangarBay to shutdown.  Use with care as the only way to restart it is to some
    /// manual interventions on the computer running it, designed to be sued as part of automated testing.
    /// </summary>
    public class ShutdownCommand: Command
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ShutdownCommand()
        {
            Type = CommandType.Shutdown;
        }

        public override bool Equals(Object? obj)
        {
            return obj.GetType() == typeof(ShutdownCommand);
        }

        public override int GetHashCode()
        {
            return typeof(ShutdownCommand).GetHashCode();
        }
    }
}
