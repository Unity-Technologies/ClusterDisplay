using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.ClusterDisplay.Graphics;

public static class FishUtils
{
    public static Vector3 GetWorldInteractionPosition ()
    {
        if (!SchoolCamera.TryGetInstance(out var schoolCamera) || !School.TryGetInstance(out var school))
            return Vector3.zero;

        var ncc = new Vector2((Input.mousePosition.x / 1280f) * 2f - 1f, (Input.mousePosition.y / 720f) * 2f - 1f);
        var clusterScreenPosition = Vector2.one * 0.5f;
        // var clusterScreenPosition = schoolCamera.Camera.NCCToClusterScreenPosition(ncc);
        var worldRay = schoolCamera.Camera.ScreenPointToRay(new Vector3(clusterScreenPosition.x, clusterScreenPosition.y, 1f));

        return worldRay.origin + worldRay.direction.normalized * (school.Center - schoolCamera.transform.position).magnitude;
    }

    public static Vector3 CheckForNANs (Vector3 vector)
    {
        if (float.IsNaN(vector.x))
            vector.x = 0f;
        if (float.IsNaN(vector.y))
            vector.y = 0f;
        if (float.IsNaN(vector.z))
            vector.z = 0f;
        return vector;
    }

}
