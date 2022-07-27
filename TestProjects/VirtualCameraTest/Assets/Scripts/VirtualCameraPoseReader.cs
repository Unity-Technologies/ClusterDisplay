using System;
using System.Reflection;
using Unity.LiveCapture;
using Unity.LiveCapture.CompanionApp;
using Unity.LiveCapture.VirtualCamera;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// A simple device implementation that reads Pose packets from the Virtual Camera companion app
/// and applies it to a transform, bypassing the processing of the VirtualCameraDevice script.
/// </summary>
/// <remarks>
/// This class leverages a lot of internal functionality from Unity.LiveCapture.VirtualCamera that
/// we access via reflection.
/// </remarks>
[CreateDeviceMenuItemAttribute("Virtual Camera Pose Reader")]
public class VirtualCameraPoseReader : CompanionAppDevice<IVirtualCameraClient>
{
    const string k_ClientTypeName = "Unity.LiveCapture.VirtualCamera.IVirtualCameraClientInternal";
    const string k_PoseTypeName = "Unity.LiveCapture.VirtualCamera.PoseSample";
    const string k_AssemblyName = "Unity.LiveCapture.VirtualCamera";
    static readonly Type k_ClientType = Type.GetType($"{k_ClientTypeName}, {k_AssemblyName}");
    static readonly Type k_PoseType = Type.GetType($"{k_PoseTypeName}, {k_AssemblyName}");
    static readonly EventInfo k_PoseEvent = k_ClientType?.GetEvent("PoseSampleReceived");
    static readonly FieldInfo k_PoseField = k_PoseType?.GetField("Pose");

    Delegate m_PoseEventHandler;
    Pose m_LatestPose;

    [SerializeField]
    Transform m_Actor;

    class PoseEventAdapterBase
    {
        public VirtualCameraPoseReader poseReader { get; set; }
    }

    /// <summary>
    /// Generic class used for instantiating a method with the correct signature
    /// to subscribe to PoseSampleReceived events.
    /// </summary>
    /// <typeparam name="T">The argument type of the PoseSampleReceived event.</typeparam>
    class PoseEventAdapter<T> : PoseEventAdapterBase
    {
        public void OnPoseSampleReceived(T sample)
        {
            Assert.IsNotNull(poseReader);
            var pose = (Pose)k_PoseField.GetValue(sample);
            poseReader.OnPoseSampleReceived(pose);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (k_PoseEvent is null || k_PoseField is null)
        {
            Debug.LogError("Unable to subscribe to Pose events. Did you import the correct LiveCapture package?");
            return;
        }

        // Instantiate a method that provides the appropriate signature for PoseSampleReceived.
        // this assumes that the event is an Action<T>
        var typeArgs = k_PoseEvent.EventHandlerType.GenericTypeArguments;
        var poseBridgeType = typeof(PoseEventAdapter<>).MakeGenericType(typeArgs);
        var adapter = Activator.CreateInstance(poseBridgeType) as PoseEventAdapterBase;

        Assert.IsNotNull(adapter);
        adapter.poseReader = this;

        m_PoseEventHandler = Delegate.CreateDelegate(k_PoseEvent.EventHandlerType,
            adapter,
            poseBridgeType.GetMethod("OnPoseSampleReceived") ?? throw new InvalidOperationException());
    }

    protected override void OnClientAssigned()
    {
        if (GetClient() is { } client)
        {
            k_PoseEvent.AddEventHandler(client, m_PoseEventHandler);
        }
    }

    // Update is called once per frame
    public override void UpdateDevice()
    {
        // Nothing to do
    }

    public override void LiveUpdate()
    {
        if (m_Actor == null) return;
        m_Actor.localPosition = m_LatestPose.position;
        m_Actor.localRotation = m_LatestPose.rotation;
    }

    public override void Write(ITakeBuilder takeBuilder)
    {
        throw new NotImplementedException();
    }

    void OnPoseSampleReceived(Pose pose)
    {
        Debug.Log(pose);
        m_LatestPose = pose;
    }

}
