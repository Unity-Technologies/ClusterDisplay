using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.ClusterDisplay.Graphics;
using Unity.ClusterDisplay.RPC;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.UI.GraphicRaycaster;

namespace Unity.ClusterDisplay
{
    public abstract class ClusterDisplayPointerInputModule : PointerInputModule
    {
        [StructLayout(LayoutKind.Explicit, Size = 58)]
        public struct ReplicatedPointerEventData
        {
            
            [FieldOffset(0)] public int pointerId;
            [FieldOffset(4)] public Vector2 position;
            [FieldOffset(12)] public Vector2 previousPosition;
            [FieldOffset(20)] public Vector2 delta;
            [FieldOffset(28)] public Vector2 pressPosition;
            [FieldOffset(36)] public float clickTime;
            [FieldOffset(40)] public int clickCount;
            [FieldOffset(44)] public Vector2 scrollDelta;
            [FieldOffset(52)] public bool useDragThreshold;
            [FieldOffset(53)] public bool dragging;
            [FieldOffset(54)] public PointerEventData.InputButton button;
        }

        [StructLayout(LayoutKind.Explicit, Size = 62)]
        public struct ReplicatedMouseButtonEventData
        {
            
            [FieldOffset(0)] public PointerEventData.FramePressState buttonState;
            [FieldOffset(4)] public ReplicatedPointerEventData buttonData;
        }

        [StructLayout(LayoutKind.Explicit, Size = 66)]
        public struct ReplicatedButtonState
        {
            
            [FieldOffset(0)] public ReplicatedMouseButtonEventData eventData;
            [FieldOffset(62)] public PointerEventData.InputButton button;
        }

        [StructLayout(LayoutKind.Explicit, Size = 66)]
        public struct ReplicatedMouseState
        {
            [FieldOffset(0)] public ReplicatedButtonState leftButtonState;

        }

        private ReplicatedMouseState replicatedMouseState;
        private PointerEventData m_InputPointerEvent;

        private MouseState m_MouseState = new MouseState();
        private PointerEventData m_PointerEventData;

        // This is where we receive the input data from the emitter, we want to receive
        // this before we start processing UI events, so we executes this before Update.
        [ClusterRPC(RPCExecutionStage.BeforeUpdate)]
        public void CachePointerState (ReplicatedMouseState replicatedMouseState) => 
            this.replicatedMouseState = replicatedMouseState;

        public abstract Vector2 GetPointerScreenSpacePosition();
        public abstract Vector2 GetScrollDelta();
        public abstract PointerEventData.FramePressState GetPressState();

        public override bool ShouldActivateModule() => true;
        public override bool IsModuleSupported() => true;

        protected static void CopyTo (PointerEventData pointerEventData, ReplicatedPointerEventData replicatedPointerEventData, EventSystem eventSystem)
        {
            pointerEventData.pointerId = replicatedPointerEventData.pointerId;
            pointerEventData.position = replicatedPointerEventData.position;
            pointerEventData.delta = replicatedPointerEventData.delta;
            pointerEventData.pressPosition = replicatedPointerEventData.pressPosition;
            pointerEventData.clickTime = replicatedPointerEventData.clickTime;
            pointerEventData.clickCount = replicatedPointerEventData.clickCount;
            pointerEventData.scrollDelta = replicatedPointerEventData.scrollDelta;
            pointerEventData.useDragThreshold = replicatedPointerEventData.useDragThreshold;
            pointerEventData.dragging = replicatedPointerEventData.dragging;
            pointerEventData.button = replicatedPointerEventData.button;
        }

        protected static ReplicatedPointerEventData CopyTo (PointerEventData pointerEventData)
        {
            var replicatedPointerEventData = new ReplicatedPointerEventData();

            replicatedPointerEventData.pointerId = pointerEventData.pointerId;
            replicatedPointerEventData.position = pointerEventData.position;
            replicatedPointerEventData.delta = pointerEventData.delta;
            replicatedPointerEventData.pressPosition = pointerEventData.pressPosition;
            replicatedPointerEventData.clickTime = pointerEventData.clickTime;
            replicatedPointerEventData.clickCount = pointerEventData.clickCount;
            replicatedPointerEventData.scrollDelta = pointerEventData.scrollDelta;
            replicatedPointerEventData.useDragThreshold = pointerEventData.useDragThreshold;
            replicatedPointerEventData.dragging = pointerEventData.dragging;
            replicatedPointerEventData.button = pointerEventData.button;

            return replicatedPointerEventData;
        }

        protected static void CopyTo (MouseState mouseState, PointerEventData pointerEventData, ReplicatedMouseState replicatedMouseState, EventSystem eventSystem)
        {
            CopyTo(pointerEventData, replicatedMouseState.leftButtonState.eventData.buttonData, eventSystem);
            mouseState.SetButtonState(PointerEventData.InputButton.Left, replicatedMouseState.leftButtonState.eventData.buttonState, pointerEventData);
        }

        protected static ReplicatedMouseState CopyTo (MouseState mouseState)
        {
            var replicatedMouseState = new ReplicatedMouseState();

            var leftButtonState = mouseState.GetButtonState(PointerEventData.InputButton.Left);
            replicatedMouseState.leftButtonState.button = leftButtonState.button;
            replicatedMouseState.leftButtonState.eventData.buttonState = leftButtonState.eventData.buttonState;
            replicatedMouseState.leftButtonState.eventData.buttonData = CopyTo(leftButtonState.eventData.buttonData);

            return replicatedMouseState;
        }

        [SerializeField] private Canvas canvas;
        [SerializeField] protected LayerMask m_BlockingMask = -1;
        [SerializeField] private BlockingObjects m_BlockingObjects = BlockingObjects.None;
        [SerializeField] private bool m_IgnoreReversedGraphics = true;

        public override void UpdateModule()
        {
            if (m_InputPointerEvent != null && m_InputPointerEvent.pointerDrag != null && m_InputPointerEvent.dragging)
                ReleaseMouse(m_InputPointerEvent, m_InputPointerEvent.pointerCurrentRaycast.gameObject);
            m_InputPointerEvent = null;
        }

        public override void ActivateModule()
        {
            var toSelect = eventSystem.currentSelectedGameObject;
            if (toSelect == null)
                toSelect = eventSystem.firstSelectedGameObject;

            eventSystem.SetSelectedGameObject(toSelect, GetBaseEventData());
        }

        protected void ProcessMousePress(MouseButtonEventData data)
        {
            var pointerEvent = data.buttonData;
            var currentOverGo = pointerEvent.pointerCurrentRaycast.gameObject;

            if (data.PressedThisFrame())
            {
                pointerEvent.eligibleForClick = true;
                pointerEvent.delta = Vector2.zero;
                pointerEvent.dragging = false;
                pointerEvent.useDragThreshold = true;
                pointerEvent.pressPosition = pointerEvent.position;
                pointerEvent.pointerPressRaycast = pointerEvent.pointerCurrentRaycast;

                DeselectIfSelectionChanged(currentOverGo, pointerEvent);

                var newPressed = ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.pointerDownHandler);
                var newClick = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

                if (newPressed == null)
                    newPressed = newClick;
                float time = Time.unscaledTime;

                if (newPressed == pointerEvent.lastPress)
                {
                    var diffTime = time - pointerEvent.clickTime;
                    if (diffTime < 0.3f)
                        ++pointerEvent.clickCount;
                    else pointerEvent.clickCount = 1;
                    pointerEvent.clickTime = time;
                }

                else pointerEvent.clickCount = 1;

                pointerEvent.pointerPress = newPressed;
                pointerEvent.rawPointerPress = currentOverGo;
                pointerEvent.pointerClick = newClick;
                pointerEvent.clickTime = time;
                pointerEvent.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(currentOverGo);

                if (pointerEvent.pointerDrag != null)
                    ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.initializePotentialDrag);

                m_InputPointerEvent = pointerEvent;
            }

            if (data.ReleasedThisFrame())
                ReleaseMouse(pointerEvent, currentOverGo);
        }

        private void PerformRaycast (PointerEventData pointerEventData)
        {
            if (!ClusterCameraController.TryGetContextCamera(out var contextCamera))
                return;

            ClusterDisplayUIRaycaster.Raycast(
                canvas, 
                contextCamera, 
                m_BlockingMask,
                m_BlockingObjects,
                m_IgnoreReversedGraphics,
                pointerEventData, 
                m_RaycastResultCache);

            // eventSystem.RaycastAll(pointerEventData, m_RaycastResultCache);
            
            var raycast = FindFirstRaycast(m_RaycastResultCache);
            pointerEventData.pointerCurrentRaycast = raycast;
            m_RaycastResultCache.Clear();
        }

        private void GetEmitterMouseEventdata (out PointerEventData pointerEventData)
        {
            var created = GetPointerData(kMouseLeftId, out pointerEventData, true);
            pointerEventData.Reset();

            if (created)
                pointerEventData.position = GetPointerScreenSpacePosition();

            Vector2 pos = GetPointerScreenSpacePosition();
            pointerEventData.delta = pos - pointerEventData.position;
            pointerEventData.position = pos;

            pointerEventData.scrollDelta = GetScrollDelta();
            pointerEventData.button = PointerEventData.InputButton.Left;
        }

        private void GetRepeaterMouseEventData (out PointerEventData pointerEventData, out PointerEventData.FramePressState framePressState)
        {
            // Copy the data we received from the emitter, and create a MouseState object.
            if (m_PointerEventData == null)
                m_PointerEventData = new PointerEventData(eventSystem);
            CopyTo(m_MouseState, m_PointerEventData, replicatedMouseState, eventSystem);
            var buttonState = m_MouseState.GetButtonState(PointerEventData.InputButton.Left);

            pointerEventData = buttonState.eventData.buttonData;
            framePressState = buttonState.eventData.buttonState;
        }

        protected override MouseState GetMousePointerEventData()
        {
            if (ClusterDisplayState.IsEmitter)
            {
                // Push the data to repeater nodes.
                GetEmitterMouseEventdata(out var pointerEventData);
                m_MouseState.SetButtonState(PointerEventData.InputButton.Left, GetPressState(), pointerEventData);
                CachePointerState(CopyTo(m_MouseState));
                PerformRaycast(pointerEventData);

                m_InputPointerEvent = pointerEventData;
                m_MouseState.SetButtonState(PointerEventData.InputButton.Left, GetPressState(), pointerEventData);
            }

            else
            {
                GetRepeaterMouseEventData(out var pointerEventData, out var framePressState);
                PerformRaycast(pointerEventData);

                m_InputPointerEvent = pointerEventData;
                m_MouseState.SetButtonState(PointerEventData.InputButton.Left, framePressState, pointerEventData);
            }

            return m_MouseState;
        }

        public override void Process()
        {
            if (canvas == null)
            {
                Debug.LogError($"Unable to process UGUI input for cluster display, the Canvas property on: \"{nameof(ClusterDisplayPointerInputModule)}\" attached to: \"{gameObject.name}\" is null!");
                return;
            }

            if (eventSystem.currentSelectedGameObject != null)
                ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, GetBaseEventData(), ExecuteEvents.updateSelectedHandler);

            MouseState mouseData = GetMousePointerEventData();
            var pointerEventData = mouseData.GetButtonState(PointerEventData.InputButton.Left).eventData;

            ProcessMousePress(pointerEventData);
            ProcessMove(pointerEventData.buttonData);
            ProcessDrag(pointerEventData.buttonData);

            if (!Mathf.Approximately(pointerEventData.buttonData.scrollDelta.sqrMagnitude, 0.0f))
            {
                var m_CurrentFocusedGameObject = pointerEventData.buttonData.pointerCurrentRaycast.gameObject;
                var scrollHandler = ExecuteEvents.GetEventHandler<IScrollHandler>(pointerEventData.buttonData.pointerCurrentRaycast.gameObject);
                ExecuteEvents.ExecuteHierarchy(scrollHandler, pointerEventData.buttonData, ExecuteEvents.scrollHandler);
            }
        }

        private void ReleaseMouse(PointerEventData pointerEvent, GameObject currentOverGo)
        {
            ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);

            var pointerUpHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

            if (pointerEvent.pointerPress == pointerUpHandler && pointerEvent.eligibleForClick)
                ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerClickHandler);

            else if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
                ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.dropHandler);

            pointerEvent.eligibleForClick = false;
            pointerEvent.pointerPress = null;
            pointerEvent.rawPointerPress = null;

            if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
                ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);

            pointerEvent.dragging = false;
            pointerEvent.pointerDrag = null;

            if (currentOverGo != pointerEvent.pointerEnter)
            {
                HandlePointerExitAndEnter(pointerEvent, null);
                HandlePointerExitAndEnter(pointerEvent, currentOverGo);
            }

            m_InputPointerEvent = pointerEvent;
        }
    }
}
