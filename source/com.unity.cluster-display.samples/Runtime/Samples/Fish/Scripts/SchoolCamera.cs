using UnityEngine;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.RPC;

[RequireComponent(typeof(Camera))]
public class SchoolCamera : SingletonMonoBehaviour<SchoolCamera>
{
    [SerializeField] private Camera m_Camera;
    public Camera Camera => m_Camera;

    [SerializeField] private float m_PitchSpeed = 2f;
    [SerializeField] private float m_YawSpeed = 5f;

    [SerializeField] private float m_OffsetDistance = 0.5f;

    private float m_Pitch;
    private float m_Yaw;

    private Vector3 m_LookAtDirection;

    private void OnValidate() => m_Camera = GetComponent<Camera>();

    private void Update()
    {
        if (m_Camera == null)
            return;

        if (!School.TryGetInstance(out var school))
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
            if (Input.GetMouseButton(1))
            {
                float deltaPitch = Input.GetAxis("Mouse Y") * m_PitchSpeed;
                float deltaYaw = Input.GetAxis("Mouse X") * m_YawSpeed;

                if (deltaPitch != 0f || deltaYaw != 0f)
                    SetLookAtDirection(m_Pitch + deltaPitch, m_Yaw + deltaYaw);
            }
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
