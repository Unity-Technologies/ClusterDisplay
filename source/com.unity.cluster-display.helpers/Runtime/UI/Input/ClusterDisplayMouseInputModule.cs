using Unity.ClusterDisplay.Graphics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Unity.ClusterDisplay.Helpers
{
    internal class ClusterDisplayMouseInputModule : ClusterDisplayPointerInputModule
    {
        public enum InputMode
        {
            DeviceSpace,
            ClusterSpace,
            ControllerSpace,
        }

        [SerializeField] private InputMode m_InputMode = InputMode.ControllerSpace;

        public Vector2 ControllerSpaceDimension { set => m_ControllerSpaceDimension = value; }
        private Vector2 m_ControllerSpaceDimension;

        public override Vector2 GetPointerScreenSpacePosition()
        {
            if (!ClusterCameraController.TryGetContextCamera(out var contextCamera))
                return Vector2.zero;

            switch (m_InputMode)
            {
                case InputMode.DeviceSpace:
                    return Input.mousePosition;

                case InputMode.ClusterSpace:
                    return contextCamera.DeviceScreenPositionToClusterScreenPosition(Input.mousePosition);

                case InputMode.ControllerSpace:
                {
                    if (m_ControllerSpaceDimension == Vector2.zero)
                        return contextCamera.NCCToClusterScreenPosition(Vector2.one * 0.5f);

                    var ncc = new Vector2((Input.mousePosition.x / m_ControllerSpaceDimension.x) * 2f - 1f, (Input.mousePosition.y / m_ControllerSpaceDimension.y) * 2f - 1f);
                    var clusterScreenPosition = contextCamera.NCCToClusterScreenPosition(ncc);
                    return clusterScreenPosition;
                }

                default:
                    Debug.LogError($"Unhandled input mode: \"{m_InputMode}\".");
                    return Vector2.zero;
            }
        }

        public override PointerEventData.FramePressState GetPressState()
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
                return PointerEventData.FramePressState.Pressed;
            else if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space))
                return PointerEventData.FramePressState.NotChanged;
            else if (Input.GetMouseButtonUp(0) || Input.GetKeyUp(KeyCode.Space))
                return PointerEventData.FramePressState.Released;

            return PointerEventData.FramePressState.NotChanged;
        }

        public override Vector2 GetScrollDelta() =>
            Input.mouseScrollDelta;
    }
}
