﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.UI.GraphicRaycaster;

namespace Unity.ClusterDisplay.Graphics
{
    public class ClusterDisplayUIRaycaster : BaseRaycaster
    {
        readonly List<Graphic> s_SortedGraphics = new List<Graphic>();

        private static int RaycastComparer(RaycastResult lhs, RaycastResult rhs)
        {
            if (lhs.module != rhs.module)
            {
                var lhsEventCamera = lhs.module.eventCamera;
                var rhsEventCamera = rhs.module.eventCamera;
                if (lhsEventCamera != null && rhsEventCamera != null && lhsEventCamera.depth != rhsEventCamera.depth)
                {
                    // need to reverse the standard compareTo
                    if (lhsEventCamera.depth < rhsEventCamera.depth)
                        return 1;
                    if (lhsEventCamera.depth == rhsEventCamera.depth)
                        return 0;

                    return -1;
                }

                if (lhs.module.sortOrderPriority != rhs.module.sortOrderPriority)
                    return rhs.module.sortOrderPriority.CompareTo(lhs.module.sortOrderPriority);

                if (lhs.module.renderOrderPriority != rhs.module.renderOrderPriority)
                    return rhs.module.renderOrderPriority.CompareTo(lhs.module.renderOrderPriority);
            }

            if (lhs.sortingLayer != rhs.sortingLayer)
            {
                // Uses the layer value to properly compare the relative order of the layers.
                var rid = SortingLayer.GetLayerValueFromID(rhs.sortingLayer);
                var lid = SortingLayer.GetLayerValueFromID(lhs.sortingLayer);
                return rid.CompareTo(lid);
            }

            if (lhs.sortingOrder != rhs.sortingOrder)
                return rhs.sortingOrder.CompareTo(lhs.sortingOrder);

            // comparing depth only makes sense if the two raycast results have the same root canvas (case 912396)
            if (lhs.depth != rhs.depth)
                return rhs.depth.CompareTo(lhs.depth);

            if (lhs.distance != rhs.distance)
                return lhs.distance.CompareTo(rhs.distance);

            return lhs.index.CompareTo(rhs.index);
        }

        private readonly Comparison<RaycastResult> s_RaycastComparer = RaycastComparer;

        [SerializeField] protected LayerMask m_BlockingMask = -1;
        [SerializeField] private BlockingObjects m_BlockingObjects = BlockingObjects.None;
        [SerializeField] private bool m_IgnoreReversedGraphics = true;

        private Canvas m_Canvas;
        private Canvas canvas
        {
            get
            {
                if (m_Canvas != null)
                    return m_Canvas;

                m_Canvas = GetComponent<Canvas>();
                return m_Canvas;
            }
        }

        public override Camera eventCamera
        {
            get
            {
                if (!ClusterCameraController.TryGetContextCamera(out var contextCamera))
                {
                    Debug.LogError("There is no cluster display camera to fire UI raycasts from!");
                    return null;
                }

                return contextCamera;
            }
        }

        // This code was lifted and refactored from EventSystem.RaycastAll and GraphicRaycaster.Raycast and the purpose of refactoring
        // was to remove several restrictions that would have prevented UI working properly in cluster
        // display including the following:
        // 1. Returning no raycasts if the screen point is outside the bounds of the screen.
        // 2. Removing support for multi-display for now.
        // 3. Event camera is always present.
        // 4. Simplified for easier management.
        // I think long term, it makes sense to maybe refactor UGUI to support cluster display in a better way by refactoring the source
        // methods themselves. This could be potentially done by splitting up the RaycastAll and Raycast methods in GraphicsRaycaster
        // and allow them to be called externally.
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            List<Graphic> m_RaycastResults = new List<Graphic>();
            resultAppendList.Clear();

            var canvasGraphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
            if (canvasGraphics == null || canvasGraphics.Count == 0)
                return;

            // Ray ray = eventCamera.ClusterDisplayScreenPointToRay(pointerEventData.position);
            Ray ray = eventCamera.ScreenPointToRay(eventData.position);

            float hitDistance = float.MaxValue;
            float projectionDirection = ray.direction.z;
            float distanceToClipPlane = Mathf.Approximately(0.0f, projectionDirection)
                ? Mathf.Infinity
                : Mathf.Abs((eventCamera.farClipPlane - eventCamera.nearClipPlane) / projectionDirection);

            if (m_BlockingObjects == BlockingObjects.ThreeD || m_BlockingObjects == BlockingObjects.All)
            {
                var hits = Physics.RaycastAll(ray, distanceToClipPlane, (int)m_BlockingMask);
                if (hits.Length > 0)
                    hitDistance = hits[0].distance;
            }

            if (m_BlockingObjects == BlockingObjects.TwoD || m_BlockingObjects == BlockingObjects.All)
            {
                var hits = Physics2D.GetRayIntersectionAll(ray, distanceToClipPlane, (int)m_BlockingMask);
                if (hits.Length > 0)
                    hitDistance = hits[0].distance;
            }

            m_RaycastResults.Clear();
            int totalCount = canvasGraphics.Count;
            for (int i = 0; i < totalCount; ++i)
            {
                Graphic graphic = canvasGraphics[i];

                // -1 means it hasn't been processed by the canvas, which means it isn't actually drawn
                if (graphic.depth == -1 || !graphic.raycastTarget || graphic.canvasRenderer.cull)
                    continue;

                if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, eventData.position, eventCamera))
                    continue;

                if (graphic.Raycast(eventData.position, eventCamera))
                    s_SortedGraphics.Add(graphic);
            }

            s_SortedGraphics.Sort((g1, g2) => g2.depth.CompareTo(g1.depth));
            m_RaycastResults.AddRange(s_SortedGraphics);
            s_SortedGraphics.Clear();

            for (var index = 0; index < m_RaycastResults.Count; index++)
            {
                var go = m_RaycastResults[index].gameObject;
                bool appendGraphic = true;

                if (m_IgnoreReversedGraphics)
                {
                    var cameraFoward = eventCamera.transform.rotation * Vector3.forward;
                    var dir = go.transform.rotation * Vector3.forward;
                    appendGraphic = Vector3.Dot(cameraFoward, dir) > 0;
                }

                if (appendGraphic)
                {
                    Vector3 transForward = go.transform.forward;
                    float distance = (Vector3.Dot(transForward, go.transform.position - ray.origin) / Vector3.Dot(transForward, ray.direction));

                    if (distance < 0 || distance >= hitDistance)
                        continue;

                    var castResult = new RaycastResult
                    {
                        module = this,
                        gameObject = go,
                        distance = distance,
                        screenPosition = eventData.position,
                        index = resultAppendList.Count,
                        depth = m_RaycastResults[index].depth,
                        sortingLayer = canvas.sortingLayerID,
                        sortingOrder = canvas.sortingOrder,
                        worldPosition = ray.origin + ray.direction * distance,
                        worldNormal = -transForward
                    };

                    resultAppendList.Add(castResult);
                }
            }

            resultAppendList.Sort(s_RaycastComparer);
        }
    }
}
