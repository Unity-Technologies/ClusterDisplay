using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class ChargedProjectileEffectsHandler : MonoBehaviour
    {
        [Tooltip("Object that will be affected by charging scale & color changes")]
        public GameObject ChargingObject;

        [Tooltip("Scale of the charged object based on charge")]
        public MinMaxVector3 Scale;

        [Tooltip("Color of the charged object based on charge")]
        public MinMaxColor Color;

        MeshRenderer[] m_AffectedRenderers;
        ProjectileBase m_ProjectileBase;

        void OnEnable()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            DebugUtility.HandleErrorIfNullGetComponent<ProjectileBase, ChargedProjectileEffectsHandler>(
                m_ProjectileBase, this, gameObject);

            m_ProjectileBase.OnShoot += OnShoot;

            m_AffectedRenderers = ChargingObject.GetComponentsInChildren<MeshRenderer>();
            foreach (var ren in m_AffectedRenderers)
            {
                ren.sharedMaterial = Instantiate(ren.sharedMaterial);
            }
        }

        void OnShoot()
        {
            ChargingObject.transform.localScale = Scale.GetValueFromRatio(m_ProjectileBase.InitialCharge);

            foreach (var ren in m_AffectedRenderers)
            {
                ren.sharedMaterial.SetColor("_Color", Color.GetValueFromRatio(m_ProjectileBase.InitialCharge));
            }
        }
    }
}