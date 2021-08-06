using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay
{
    public class ClusterDisplayPointerInputModule : PointerInputModule
    {
        public enum SelectionState : ushort
        {
            None,
            Down,
            Hold,
            Up
        }

        private PointerEventData pointerEventData;

        private Vector2 cachedPointerPosition;
        private Vector2 previousPointerPosition;
        private Vector2 deltaPointerPosition;

        private SelectionState cachedSelectionState;

        [ClusterRPC]
        private void CachePointerState (Vector2 pointerPosition, SelectionState selectionState)
        {
            previousPointerPosition = cachedPointerPosition;
            deltaPointerPosition = pointerPosition - previousPointerPosition;
            cachedPointerPosition = pointerPosition;

            cachedSelectionState = selectionState;
        }

        private void ProcessMasterInput ()
        {
            SelectionState selectionState = SelectionState.None;

            if (Input.GetMouseButtonDown(0))
                selectionState = SelectionState.Down;
            else if (Input.GetMouseButton(0))
                selectionState = SelectionState.Hold;
            else if (Input.GetMouseButtonUp(0))
                selectionState = SelectionState.Up;

            CachePointerState(Input.mousePosition, selectionState);
        }

        private void ProcessPosition ()
        {
            if (ClusterDisplayState.IsMaster)
                ProcessMasterInput();

            pointerEventData.position = cachedPointerPosition;
            pointerEventData.delta = deltaPointerPosition;
        }

        private void ProcessRaycasts ()
        {
            List<RaycastResult> raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerEventData, raycastResults);
            pointerEventData.pointerCurrentRaycast = FindFirstRaycast(raycastResults);
            ProcessMove(pointerEventData);
        }

        private void ProcessSelection ()
        {
            if (pointerEventData.pointerEnter != null)
            {
                var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(pointerEventData.pointerEnter);
                ExecuteEvents.ExecuteHierarchy(handler, pointerEventData, ExecuteEvents.pointerClickHandler);
            }
        }

        public override void Process()
        {
            if (pointerEventData == null)
                pointerEventData = new PointerEventData(eventSystem);

            ProcessPosition();
            ProcessRaycasts();
            ProcessSelection();
        }
    }
}
