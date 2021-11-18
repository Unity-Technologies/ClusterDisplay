using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.ClusterDisplay.Helpers
{
    [RequireComponent(typeof(Canvas))]
    public class ClusterDisplayCanvasPointerManager : MonoBehaviour
    {
        private static readonly List<ClusterDisplayCanvasPointerManager> k_PointerManagers = new List<ClusterDisplayCanvasPointerManager>();
        private Canvas m_Canvas;
        
        [SerializeField] private Texture2D m_PointerTexture = null;
        private RawImage m_PointerRawImage;

        private bool toggled
        {
            set
            {
                if (m_PointerRawImage == null)
                    return;

                m_PointerRawImage.gameObject.SetActive(value);
            }
        }

        private bool CacheCanvas()
        {
            if (m_Canvas == null)
            {
                m_Canvas = GetComponent<Canvas>();
                if (m_Canvas == null)
                    ClusterDebug.LogError($"There is no {nameof(Canvas)} that {nameof(ClusterDisplayCanvasPointerManager)} can use attached to: \"{gameObject.name}\".");
            }
            
            return m_Canvas != null;
        }

        private void Awake()
        {
            if (!CacheCanvas())
                return;
            
            if (!k_PointerManagers.Contains(this))
                k_PointerManagers.Add(this);
        }

        private Vector3 m_PointerPosition;
        private Vector3 pointerPosition
        {
            set
            {
                if (!CacheCanvas())
                    return;
                
                if (m_PointerRawImage == null)
                {
                    var pointerGo = new GameObject("Pointer");
                    pointerGo.transform.SetParent(m_Canvas.transform);
                    
                    m_PointerRawImage = pointerGo.AddComponent<RawImage>();
                    m_PointerRawImage.texture = m_PointerTexture;
                    m_PointerRawImage.raycastTarget = false;
                }

                m_PointerRawImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1f);
                m_PointerRawImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1f);
                m_PointerRawImage.rectTransform.position = value;
                m_PointerPosition = value;
            }
        }

        private static void ForeachPointer (Action<ClusterDisplayCanvasPointerManager> callback)
        {
            for (int i = 0; i < k_PointerManagers.Count; i++)
            {
                if (k_PointerManagers[i] == null)
                {
                    k_PointerManagers.RemoveAt(i);
                    continue;
                }

                callback(k_PointerManagers[i]);
            }
        }

        public static void PropagatePointerActiveState (bool enabled) =>
            ForeachPointer((pointerManager) => pointerManager.toggled = enabled);

        public static void PropagatePointerPosition(Vector3 position) =>
            ForeachPointer((pointerManager) => pointerManager.pointerPosition = position);
    }
}