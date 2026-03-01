using SoR.Core;
using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    public class HitVFXController : MonoBehaviour
    {
        [Header("VFX Prefabs by Element")]
        [SerializeField] private GameObject defaultHitVFX;
        [SerializeField] private GameObject verdantHitVFX;
        [SerializeField] private GameObject pyroHitVFX;
        [SerializeField] private GameObject hydroHitVFX;
        [SerializeField] private GameObject voltHitVFX;
        [SerializeField] private GameObject umbralHitVFX;
        [SerializeField] private GameObject cryoHitVFX;
        [SerializeField] private GameObject geoHitVFX;

        private void OnEnable()
        {
            EventBus.Subscribe<DamageDealtEvent>(OnDamageDealt);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
        }

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            GameObject vfxPrefab = GetVFXPrefab(evt.Element);
            if (vfxPrefab == null) return;

            GameObject instance = Instantiate(vfxPrefab, evt.HitPoint, Quaternion.identity);

            // Auto-destroy after particle system finishes.
            ParticleSystem particles = instance.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                float totalDuration = particles.main.duration + particles.main.startLifetime.constantMax;
                Destroy(instance, totalDuration);
            }
            else
            {
                // Fallback: destroy after a default time if no particle system is found.
                Destroy(instance, 2f);
            }
        }

        private GameObject GetVFXPrefab(Element element)
        {
            return element switch
            {
                Element.Verdant => verdantHitVFX != null ? verdantHitVFX : defaultHitVFX,
                Element.Pyro => pyroHitVFX != null ? pyroHitVFX : defaultHitVFX,
                Element.Hydro => hydroHitVFX != null ? hydroHitVFX : defaultHitVFX,
                Element.Volt => voltHitVFX != null ? voltHitVFX : defaultHitVFX,
                Element.Umbral => umbralHitVFX != null ? umbralHitVFX : defaultHitVFX,
                Element.Cryo => cryoHitVFX != null ? cryoHitVFX : defaultHitVFX,
                Element.Geo => geoHitVFX != null ? geoHitVFX : defaultHitVFX,
                _ => defaultHitVFX
            };
        }
    }
}
