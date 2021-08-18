using System.Runtime.InteropServices;
using Unity.ClusterDisplay.Graphics;
using Unity.ClusterDisplay.RPC;
using UnityEngine;
using UnityEngine.EventSystems;
using static UnityEngine.UI.GraphicRaycaster;

namespace Unity.ClusterDisplay
{
    public abstract class ClusterDisplayPointerInputModule : PointerInputModule
    {
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct ReplicatedPointerEventData
        {
            [FieldOffset(0)] public int pointerId;
            [FieldOffset(4)] public Vector2 position;
            [FieldOffset(12)] public Vector2 delta;
            [FieldOffset(20)] public Vector2 scrollDelta;
            [FieldOffset(28)] public PointerEventData.InputButton button;
        }

        [StructLayout(LayoutKind.Explicit, Size = 36)]
        public struct ReplicatedMouseButtonEventData
        {
            
            [FieldOffset(0)] public PointerEventData.FramePressState buttonState;
            [FieldOffset(4)] public ReplicatedPointerEventData buttonData;
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        public struct ReplicatedButtonState
        {
            
            [FieldOffset(0)] public PointerEventData.InputButton button;
            [FieldOffset(4)] public ReplicatedMouseButtonEventData eventData;
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        public struct ReplicatedMouseState
        {
            [FieldOffset(0)] public ReplicatedButtonState leftButtonState;

        }

        private ReplicatedMouseState replicatedMouseState;
        private MouseState m_CachedMouseState = new MouseState();
        private PointerEventData m_CachedPointerEventData;

        // This is where we receive the input data from the emitter, we want to receive
        // this before we start processing UI events, so we executes this before Update.
        [ClusterRPC(RPCExecutionStage.BeforeUpdate)]
        public void ApplyPointerData (ReplicatedMouseState replicatedMouseState) => 
            this.replicatedMouseState = replicatedMouseState;

        public abstract Vector2 GetPointerScreenSpacePosition();
        public abstract Vector2 GetScrollDelta();
        public abstract PointerEventData.FramePressState GetPressState();

        public override bool ShouldActivateModule() => true;
        public override bool IsModuleSupported() => true;

        protected void DeserializePointerEventData (ReplicatedPointerEventData replicatedPointerEventData, EventSystem eventSystem)
        {
            m_CachedPointerEventData.pointerId = replicatedPointerEventData.pointerId;
            m_CachedPointerEventData.position = replicatedPointerEventData.position;
            m_CachedPointerEventData.delta = replicatedPointerEventData.delta;
            m_CachedPointerEventData.scrollDelta = replicatedPointerEventData.scrollDelta;
            m_CachedPointerEventData.button = replicatedPointerEventData.button;
        }

        protected void DeserializeForRepeat (ReplicatedMouseState replicatedMouseState, EventSystem eventSystem)
        {
            DeserializePointerEventData(replicatedMouseState.leftButtonState.eventData.buttonData, eventSystem);
            m_CachedMouseState.SetButtonState(PointerEventData.InputButton.Left, replicatedMouseState.leftButtonState.eventData.buttonState, m_CachedPointerEventData);
        }

        protected ReplicatedPointerEventData SerializePointerEventData ()
        {
            var replicatedPointerEventData = new ReplicatedPointerEventData();

            replicatedPointerEventData.pointerId = m_CachedPointerEventData.pointerId;
            replicatedPointerEventData.position = m_CachedPointerEventData.position;
            replicatedPointerEventData.delta = m_CachedPointerEventData.delta;
            replicatedPointerEventData.scrollDelta = m_CachedPointerEventData.scrollDelta;
            replicatedPointerEventData.button = m_CachedPointerEventData.button;

            return replicatedPointerEventData;
        }

        protected ReplicatedMouseState SerializeForEmit ()
        {
            var replicatedMouseState = new ReplicatedMouseState();

            var leftButtonState = m_CachedMouseState.GetButtonState(PointerEventData.InputButton.Left);
            replicatedMouseState.leftButtonState.button = leftButtonState.button;
            replicatedMouseState.leftButtonState.eventData.buttonState = leftButtonState.eventData.buttonState;
            replicatedMouseState.leftButtonState.eventData.buttonData = SerializePointerEventData();

            return replicatedMouseState;
        }

        public override void UpdateModule()
        {
            /*
            if (m_InputPointerEvent != null && m_InputPointerEvent.pointerDrag != null && m_InputPointerEvent.dragging)
                ReleaseMouse(m_InputPointerEvent, m_InputPointerEvent.pointerCurrentRaycast.gameObject);
            m_InputPointerEvent = null;
            */
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
                if (newPressed == null)
                    newPressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

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
                pointerEvent.clickTime = time;
                pointerEvent.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(currentOverGo);

                if (pointerEvent.pointerDrag != null)
                    ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.initializePotentialDrag);
            }

            if (data.ReleasedThisFrame())
                ReleaseMouse(pointerEvent, currentOverGo);
        }

        private void PerformRaycast (PointerEventData pointerEventData)
        {
            if (!ClusterCameraController.TryGetContextCamera(out var contextCamera))
                return;

            eventSystem.RaycastAll(pointerEventData, m_RaycastResultCache);
            
            var raycast = FindFirstRaycast(m_RaycastResultCache);
            pointerEventData.pointerCurrentRaycast = raycast;
            #if UNITY_EDITOR
            if (raycast.gameObject != null)
                UnityEditor.Selection.objects = new[] { raycast.gameObject };
            #endif
            m_RaycastResultCache.Clear();
        }

        private void PollEmitterPointerEventData ()
        {
            if (m_CachedPointerEventData == null)
            {
                m_CachedPointerEventData = new PointerEventData(eventSystem);
                m_CachedPointerEventData.position = GetPointerScreenSpacePosition();
            }

            Vector2 pos = GetPointerScreenSpacePosition();
            m_CachedPointerEventData.delta = pos - m_CachedPointerEventData.position;
            m_CachedPointerEventData.position = pos;

            m_CachedPointerEventData.scrollDelta = GetScrollDelta();
            m_CachedPointerEventData.button = PointerEventData.InputButton.Left;
        }

        private void PollRepeaterPointerEventData (out PointerEventData.FramePressState framePressState)
        {
            // Copy the data we received from the emitter, and create a MouseState object.
            if (m_CachedPointerEventData == null)
                m_CachedPointerEventData = new PointerEventData(eventSystem);

            DeserializeForRepeat(replicatedMouseState, eventSystem);
            var buttonState = m_CachedMouseState.GetButtonState(PointerEventData.InputButton.Left);

            framePressState = buttonState.eventData.buttonState;
        }

        private void PollInput()
        {
            if (ClusterDisplayState.IsEmitter)
            {
                // Push the data to repeater nodes.
                PollEmitterPointerEventData();
                m_CachedMouseState.SetButtonState(PointerEventData.InputButton.Left, GetPressState(), m_CachedPointerEventData);
                ApplyPointerData(SerializeForEmit());
                PerformRaycast(m_CachedPointerEventData);

                m_CachedMouseState.SetButtonState(PointerEventData.InputButton.Left, GetPressState(), m_CachedPointerEventData);
            }

            else
            {
                PollRepeaterPointerEventData(out var framePressState);
                PerformRaycast(m_CachedPointerEventData);

                m_CachedMouseState.SetButtonState(PointerEventData.InputButton.Left, framePressState, m_CachedPointerEventData);
            }
        }

        public override void Process()
        {
            if (eventSystem.currentSelectedGameObject != null)
                ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, GetBaseEventData(), ExecuteEvents.updateSelectedHandler);

            var pointerEventData = m_CachedMouseState.GetButtonState(PointerEventData.InputButton.Left).eventData;

            PollInput();
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
        }
    }
}
