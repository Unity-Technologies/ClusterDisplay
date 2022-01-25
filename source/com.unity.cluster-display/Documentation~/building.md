## Building your Project

To be able to use your Unity project in Cluster Display context, you must build it as a [Windows standalone player](https://docs.unity3d.com/Manual/BuildSettingsStandalone.html) from a Unity editor with the proper license.

Once built, the standalone player includes the Cluster-enabled features according to your project setup.

> **Important:** This standalone player is the one that you will need to make available to all cluster nodes through a further step. You must always ensure to use the exact same copy of it across the cluster.