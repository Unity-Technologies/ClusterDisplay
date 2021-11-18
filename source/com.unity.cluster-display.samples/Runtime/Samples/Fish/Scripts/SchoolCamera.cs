using UnityEngine;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.RPC;
using Unity.ClusterDisplay.RPC.Wrappers;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(CameraWrapper))]
public class SchoolCamera : SingletonMonoBehaviour<SchoolCamera>
{
    [SerializeField] private Camera m_Camera;
    [SerializeField] private CameraWrapper m_CameraWrapper;

    public Camera Camera => m_Camera;

    [SerializeField] private float m_PitchSpeed = 2f;
    [SerializeField] private float m_YawSpeed = 5f;

    [SerializeField] private float m_OffsetDistance = 0.5f;

    private float m_Pitch;
    private float m_Yaw;

    private Vector3 m_LookAtDirection;

    [SerializeField] private School school;

    private void OnValidate() => m_Camera = GetComponent<Camera>();

    private void Update()
    {
        if (m_Camera == null)
            return;

        var schoolBounds = school.SchoolBounds;
        Vector3 min = Vector3.Min(FishUtils.CheckForNANs(schoolBounds.min), -Vector3.one), max = Vector3.Max(FishUtils.CheckForNANs(schoolBounds.max), Vector3.one);
        Vector3 center = FishUtils.CheckForNANs(schoolBounds.center);

        var pos = center + m_LookAtDirection.normalized * (max - min).magnitude * m_OffsetDistance;
        var targetCameraPos = Vector3.Lerp(m_Camera.transform.position, pos, Time.deltaTime / 0.05f);

        m_Camera.transform.position = targetCameraPos;
        m_Camera.transform.rotation = Quaternion.Lerp(m_Camera.transform.rotation, Quaternion.LookRotation((center - targetCameraPos).normalized, Vector3.up), Time.deltaTime / 0.05f);

        if (ClusterDisplayState.IsEmitter)
        {
            float keyboardPitch = ((Input.GetKey(KeyCode.DownArrow) ? -1f : Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) * Time.deltaTime * 45f);
            float keyboardYaw = ((Input.GetKey(KeyCode.LeftArrow) ? -1f : Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) * Time.deltaTime * 45f);

            float deltaPitch = (Input.GetAxis("Mouse Y") + keyboardPitch + Random.Range(-0.0001f, 0.0001f)) * m_PitchSpeed;
            float deltaYaw = (Input.GetAxis("Mouse X") + keyboardYaw + Random.Range(-0.0001f, 0.0001f)) * m_YawSpeed;

            float keyboardZoom = ((Input.GetKey(KeyCode.W) ? -1f : Input.GetKey(KeyCode.S) ? 1f : 0f) * Time.deltaTime * 20f);
            m_CameraWrapper.fieldOfView = m_CameraWrapper.fieldOfView + keyboardZoom;

            if (deltaPitch != 0f || deltaYaw != 0f)
                SetLookAtDirection(m_Pitch + deltaPitch, m_Yaw + deltaYaw);
        }
    }

    [ClusterRPC]
    public void SetLookAtDirection (float pitch, float yaw)
    {
        DeterministicUtils.LogCall(pitch, yaw);
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        if (yaw < -360.0f)
            yaw = 360.0f;
        else if (yaw > 360.0f)
            yaw = -360.0f;

        m_Pitch = pitch;
        m_Yaw = yaw;

        var currentRotation = Quaternion.LookRotation(m_LookAtDirection, Vector3.up);
        currentRotation = Quaternion.AngleAxis(yaw, Vector3.up);
        currentRotation *= Quaternion.AngleAxis(pitch, Vector3.right);
        m_LookAtDirection = currentRotation * Vector3.forward;
    }
}
