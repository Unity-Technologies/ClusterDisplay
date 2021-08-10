using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.ClusterDisplay.RPC;
using Unity.ClusterDisplay.Graphics;
using System.Runtime.InteropServices;

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

        protected static PointerEventData CopyTo (ReplicatedPointerEventData replicatedPointerEventData, EventSystem eventSystem)
        {
            var pointerEventData = new PointerEventData(eventSystem);

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

            return pointerEventData;
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

        protected static MouseState CopyTo (ReplicatedMouseState replicatedMouseState, EventSystem eventSystem)
        {
            var mouseState = new MouseState();

            var leftPointerEventData = CopyTo(replicatedMouseState.leftButtonState.eventData.buttonData, eventSystem);
            mouseState.SetButtonState(PointerEventData.InputButton.Left, replicatedMouseState.leftButtonState.eventData.buttonState, leftPointerEventData);

            return mouseState;
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

        private ReplicatedMouseState replicatedMouseState;
        private PointerEventData m_InputPointerEvent;
        private MouseState m_MouseState;

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

        private void PerformRaycast (PointerEventData leftData)
        {
            eventSystem.RaycastAll(leftData, m_RaycastResultCache);
            var raycast = FindFirstRaycast(m_RaycastResultCache);
            leftData.pointerCurrentRaycast = raycast;
            m_RaycastResultCache.Clear();
        }

        private void GetEmitterMouseEventdata (out PointerEventData leftData)
        {
            var created = GetPointerData(kMouseLeftId, out leftData, true);

            leftData.Reset();

            if (created)
                leftData.position = GetPointerScreenSpacePosition();

            Vector2 pos = GetPointerScreenSpacePosition();
            leftData.delta = pos - leftData.position;
            leftData.position = pos;

            leftData.scrollDelta = GetScrollDelta();
            leftData.button = PointerEventData.InputButton.Left;
        }

        private void GetRepeaterMouseEventData (out PointerEventData leftData, out PointerEventData.FramePressState framePressState)
        {
            // Copy the data we received from the emitter, and create a MouseState object.
            m_MouseState = CopyTo(replicatedMouseState, eventSystem);
            var buttonState = m_MouseState.GetButtonState(PointerEventData.InputButton.Left);

            leftData = buttonState.eventData.buttonData;
            framePressState = buttonState.eventData.buttonState;
        }

        protected override MouseState GetMousePointerEventData()
        {
            if (ClusterDisplayState.IsEmitter)
            {
                // Push the data to repeater nodes.
                GetEmitterMouseEventdata(out var leftData);

                CachePointerState(CopyTo(m_MouseState));
                PerformRaycast(leftData);

                m_MouseState.SetButtonState(PointerEventData.InputButton.Left, GetPressState(), leftData);
            }

            else
            {
                GetRepeaterMouseEventData(out var leftData, out var framePressState);
                PerformRaycast(leftData);

                m_MouseState.SetButtonState(PointerEventData.InputButton.Left, framePressState, leftData);
            }

            return m_MouseState;
        }

        public override void Process()
        {
            if (eventSystem.currentSelectedGameObject != null)
                ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, GetBaseEventData(), ExecuteEvents.updateSelectedHandler);

            MouseState mouseData = GetMousePointerEventData(0);
            var leftButtonData = mouseData.GetButtonState(PointerEventData.InputButton.Left).eventData;

            ProcessMousePress(leftButtonData);
            m_InputPointerEvent = leftButtonData.buttonData;

            ProcessMove(leftButtonData.buttonData);
            ProcessDrag(leftButtonData.buttonData);

            if (!Mathf.Approximately(leftButtonData.buttonData.scrollDelta.sqrMagnitude, 0.0f))
            {
                var m_CurrentFocusedGameObject = leftButtonData.buttonData.pointerCurrentRaycast.gameObject;
                var scrollHandler = ExecuteEvents.GetEventHandler<IScrollHandler>(leftButtonData.buttonData.pointerCurrentRaycast.gameObject);
                ExecuteEvents.ExecuteHierarchy(scrollHandler, leftButtonData.buttonData, ExecuteEvents.scrollHandler);
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
