using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Unity.ClusterDisplay.Graphics
{
    public class ClusterDisplayMouseInputModule : ClusterDisplayPointerInputModule
    {
        public override Vector2 GetPointerScreenSpacePosition()
        {
            if (!ClusterCameraController.TryGetContextCamera(out var contextCamera))
                return Vector2.zero;
            return contextCamera.ScreenPointToClusterDisplayScreenPoint(Input.mousePosition);
        }

        public override PointerEventData.FramePressState GetPressState()
        {
            if (Input.GetMouseButtonDown(0))
                return PointerEventData.FramePressState.Pressed;
            else if (Input.GetMouseButton(0))
                return PointerEventData.FramePressState.NotChanged;
            else if (Input.GetMouseButtonUp(0))
                return PointerEventData.FramePressState.Released;

            return PointerEventData.FramePressState.NotChanged;
        }

        public override Vector2 GetScrollDelta() =>
            Input.mouseScrollDelta;
    }
}
