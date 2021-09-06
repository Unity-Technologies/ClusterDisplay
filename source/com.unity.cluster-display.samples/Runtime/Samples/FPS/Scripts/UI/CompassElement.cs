using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.UI
{
    public class CompassElement : MonoBehaviour
    {
        [Tooltip("The marker on the compass for this element")]
        public CompassMarker CompassMarkerPrefab;

        [Tooltip("Text override for the marker, if it's a direction")]
        public string TextDirection;

        Compass m_Compass;

        void Awake()
        {
            m_Compass = FindObjectOfType<Compass>();
            DebugUtility.HandleErrorIfNullFindObject<Compass, CompassElement>(m_Compass, this);

            var markerInstance = Instantiate(CompassMarkerPrefab);

            markerInstance.Initialize(this, TextDirection);
            m_Compass.RegisterCompassElement(transform, markerInstance);
        }

        void OnDestroy()
        {
            m_Compass.UnregisterCompassElement(transform);
        }
    }
}