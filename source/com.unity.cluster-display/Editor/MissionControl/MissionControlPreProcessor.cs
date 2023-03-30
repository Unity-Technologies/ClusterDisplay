using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Unity.ClusterDisplay
{
    public class MissionControlPreProcessor: IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            MissionControlParameters.Instance.Clear();
        }
    }
}
