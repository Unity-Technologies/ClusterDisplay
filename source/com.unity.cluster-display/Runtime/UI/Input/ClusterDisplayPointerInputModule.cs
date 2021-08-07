using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay
{
    public class ClusterDisplayPointerInputModule : PointerInputModule
    {

        public struct PointerInputData
        {
            public enum SelectionState : ushort
            {
                None,
                Down,
                Hold,
                Up
            }

            public Vector2 cachedPointerPosition;
            public Vector2 previousPointerPosition;
            public Vector2 deltaPointerPosition;

            public SelectionState cachedSelectionState;
        }

        private PointerInputData cachedPointerInputData;

        private PointerEventData pointerEventData;

        [ClusterRPC]
        public void CachePointerState (PointerInputData pointerInputData)
        {
            this.cachedPointerInputData = pointerInputData;
        }

        private void ProcessMasterInput ()
        {
            PointerInputData.SelectionState selectionState = PointerInputData.SelectionState.None;

            cachedPointerInputData.previousPointerPosition = cachedPointerInputData.cachedPointerPosition;
            cachedPointerInputData.cachedPointerPosition = Input.mousePosition;
            cachedPointerInputData.deltaPointerPosition = (Vector2)Input.mousePosition - cachedPointerInputData.previousPointerPosition;

            if (Input.GetMouseButtonDown(0))
                selectionState = PointerInputData.SelectionState.Down;
            else if (Input.GetMouseButton(0))
                selectionState = PointerInputData.SelectionState.Hold;
            else if (Input.GetMouseButtonUp(0))
                selectionState = PointerInputData.SelectionState.Up;

            cachedPointerInputData.cachedSelectionState = selectionState;

            CachePointerState(cachedPointerInputData);
        }

        private void ProcessPosition ()
        {
            if (ClusterDisplayState.IsMaster)
                ProcessMasterInput();
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
