using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.Graphics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unity.ClusterDisplay.Helpers
{
    public class ClusterDisplayUIRaycaster : GraphicRaycaster
    {
        readonly List<Graphic> s_SortedGraphics = new List<Graphic>();

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
                    ClusterDebug.LogError("There is no cluster display camera to fire UI raycasts from!");
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

            var canvasGraphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
            if (canvasGraphics == null || canvasGraphics.Count == 0)
                return;

            // Ray ray = eventCamera.ClusterDisplayScreenPointToRay(pointerEventData.position);
            Ray ray = eventCamera.ScreenPointToRay(eventData.position);
            Debug.DrawRay(ray.origin, ray.direction * 100, Color.red, 0.1f);

            float hitDistance = float.MaxValue;
            float projectionDirection = ray.direction.z;
            float distanceToClipPlane = Mathf.Approximately(0.0f, projectionDirection)
                ? Mathf.Infinity
                : Mathf.Abs((eventCamera.farClipPlane - eventCamera.nearClipPlane) / projectionDirection);

            if (blockingObjects == BlockingObjects.ThreeD || blockingObjects == BlockingObjects.All)
            {
                var hits = Physics.RaycastAll(ray, distanceToClipPlane, (int)m_BlockingMask);
                if (hits.Length > 0)
                    hitDistance = hits[0].distance;
            }

            if (blockingObjects == BlockingObjects.TwoD || blockingObjects == BlockingObjects.All)
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

                if (ignoreReversedGraphics)
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
        }
    }
}
