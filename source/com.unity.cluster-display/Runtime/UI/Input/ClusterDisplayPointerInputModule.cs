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

            public Vector2 screenPosition;
            public Vector2 previousScreenPosition;
            public Vector2 deltaScreenPosition;

            public SelectionState selectionState;
        }

        private PointerInputData cachedPointerInputData;
        private PointerEventData pointerEventData;

        [ClusterRPC]
        public void CachePointerState (PointerInputData pointerInputData)
        {
            this.cachedPointerInputData = pointerInputData;

            pointerEventData.position = pointerInputData.screenPosition;
            pointerEventData.delta = pointerInputData.deltaScreenPosition;
            pointerEventData.button = PointerEventData.InputButton.Left;
        }

        private void ProcessEmitterInput ()
        {
            cachedPointerInputData.previousScreenPosition = cachedPointerInputData.screenPosition;
            cachedPointerInputData.screenPosition = Input.mousePosition;
            cachedPointerInputData.deltaScreenPosition = (Vector2)Input.mousePosition - cachedPointerInputData.previousScreenPosition;

            PointerInputData.SelectionState selectionState = PointerInputData.SelectionState.None;
            if (Input.GetMouseButtonDown(0))
                selectionState = PointerInputData.SelectionState.Down;
            else if (Input.GetMouseButton(0))
                selectionState = PointerInputData.SelectionState.Hold;
            else if (Input.GetMouseButtonUp(0))
                selectionState = PointerInputData.SelectionState.Up;
            cachedPointerInputData.selectionState = selectionState;

            CachePointerState(cachedPointerInputData);
        }

        private void ProcessPosition ()
        {
            if (ClusterDisplayState.IsEmitter)
                ProcessEmitterInput();
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
                if (cachedPointerInputData.selectionState == PointerInputData.SelectionState.Down)
                    ExecuteEvents.ExecuteHierarchy(handler, pointerEventData, ExecuteEvents.pointerDownHandler);
                else if (cachedPointerInputData.selectionState == PointerInputData.SelectionState.Hold)
                    ExecuteEvents.ExecuteHierarchy(handler, pointerEventData, ExecuteEvents.pointerClickHandler);
                else if (cachedPointerInputData.selectionState == PointerInputData.SelectionState.Up)
                    ExecuteEvents.ExecuteHierarchy(handler, pointerEventData, ExecuteEvents.pointerUpHandler);
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
