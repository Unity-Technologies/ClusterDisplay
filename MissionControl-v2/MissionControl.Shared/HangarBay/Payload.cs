using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// Information about a Payload
    /// </summary>
    public class Payload
    {
        /// <summary>
        /// List of files composing the Payload
        /// </summary>
        public IEnumerable<PayloadFile> Files { get; set; } = Enumerable.Empty<PayloadFile>();
    }
}
