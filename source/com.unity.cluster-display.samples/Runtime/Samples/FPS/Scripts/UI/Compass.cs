using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.UI
{
    public class Compass : MonoBehaviour
    {
        public RectTransform CompasRect;
        public float VisibilityAngle = 180f;
        public float HeightDifferenceMultiplier = 2f;
        public float MinScale = 0.5f;
        public float DistanceMinScale = 50f;
        public float CompasMarginRatio = 0.8f;

        public GameObject MarkerDirectionPrefab;

        Transform m_PlayerTransform;
        Dictionary<Transform, CompassMarker> m_ElementsDictionnary = new Dictionary<Transform, CompassMarker>();

        float m_WidthMultiplier;
        float m_HeightOffset;

        void Awake()
        {
            PlayerCharacterController playerCharacterController = FindObjectOfType<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullFindObject<PlayerCharacterController, Compass>(playerCharacterController,
                this);
            m_PlayerTransform = playerCharacterController.transform;

            m_WidthMultiplier = CompasRect.rect.width / VisibilityAngle;
            m_HeightOffset = -CompasRect.rect.height / 2;
        }

        void Update()
        {
            // this is all very WIP, and needs to be reworked
            foreach (var element in m_ElementsDictionnary)
            {
                float distanceRatio = 1;
                float heightDifference = 0;
                float angle;

                if (element.Value.IsDirection)
                {
                    angle = Vector3.SignedAngle(m_PlayerTransform.forward,
                        element.Key.transform.localPosition.normalized, Vector3.up);
                }
                else
                {
                    Vector3 targetDir = (element.Key.transform.position - m_PlayerTransform.position).normalized;
                    targetDir = Vector3.ProjectOnPlane(targetDir, Vector3.up);
                    Vector3 playerForward = Vector3.ProjectOnPlane(m_PlayerTransform.forward, Vector3.up);
                    angle = Vector3.SignedAngle(playerForward, targetDir, Vector3.up);

                    Vector3 directionVector = element.Key.transform.position - m_PlayerTransform.position;

                    heightDifference = (directionVector.y) * HeightDifferenceMultiplier;
                    heightDifference = Mathf.Clamp(heightDifference, -CompasRect.rect.height / 2 * CompasMarginRatio,
                        CompasRect.rect.height / 2 * CompasMarginRatio);

                    distanceRatio = directionVector.magnitude / DistanceMinScale;
                    distanceRatio = Mathf.Clamp01(distanceRatio);
                }

                if (angle > -VisibilityAngle / 2 && angle < VisibilityAngle / 2)
                {
                    element.Value.CanvasGroup.alpha = 1;
                    element.Value.CanvasGroup.transform.localPosition = new Vector2(m_WidthMultiplier * angle,
                        heightDifference + m_HeightOffset);
                    element.Value.CanvasGroup.transform.localScale =
                        Vector3.one * Mathf.Lerp(1, MinScale, distanceRatio);
                }
                else
                {
                    element.Value.CanvasGroup.alpha = 0;
                }
            }
        }

        public void RegisterCompassElement(Transform element, CompassMarker marker)
        {
            marker.transform.SetParent(CompasRect);

            m_ElementsDictionnary.Add(element, marker);
        }

        public void UnregisterCompassElement(Transform element)
        {
            if (m_ElementsDictionnary.TryGetValue(element, out CompassMarker marker) && marker.CanvasGroup != null)
                Destroy(marker.CanvasGroup.gameObject);
            m_ElementsDictionnary.Remove(element);
        }
    }
}