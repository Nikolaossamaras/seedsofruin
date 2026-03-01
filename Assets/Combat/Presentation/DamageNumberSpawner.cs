using SoR.Core;
using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    public class DamageNumberSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject damageNumberPrefab;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color critColor = Color.yellow;
        [SerializeField] private Color verdantColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color pyroColor = new Color(1f, 0.4f, 0.1f);
        [SerializeField] private Color hydroColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] private Color voltColor = new Color(0.9f, 0.9f, 0.2f);
        [SerializeField] private Color umbralColor = new Color(0.5f, 0.1f, 0.6f);
        [SerializeField] private Color cryoColor = new Color(0.6f, 0.9f, 1f);
        [SerializeField] private Color geoColor = new Color(0.7f, 0.5f, 0.3f);

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1.5f, 0f);

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
            if (damageNumberPrefab == null) return;

            Vector3 spawnPos = evt.HitPoint + spawnOffset;

            // Add slight random offset to prevent overlap.
            spawnPos.x += Random.Range(-0.3f, 0.3f);
            spawnPos.y += Random.Range(0f, 0.3f);

            GameObject instance = Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);

            // Attempt to set the text and color on the spawned object.
            TMPro.TextMeshPro textMesh = instance.GetComponent<TMPro.TextMeshPro>();
            if (textMesh == null)
            {
                textMesh = instance.GetComponentInChildren<TMPro.TextMeshPro>();
            }

            if (textMesh != null)
            {
                textMesh.text = Mathf.RoundToInt(evt.Amount).ToString();
                textMesh.color = GetDamageColor(evt.Element, evt.IsCrit);

                if (evt.IsCrit)
                {
                    textMesh.fontSize *= 1.3f;
                }
            }
        }

        private Color GetDamageColor(Element element, bool isCrit)
        {
            if (isCrit) return critColor;

            return element switch
            {
                Element.Verdant => verdantColor,
                Element.Pyro => pyroColor,
                Element.Hydro => hydroColor,
                Element.Volt => voltColor,
                Element.Umbral => umbralColor,
                Element.Cryo => cryoColor,
                Element.Geo => geoColor,
                _ => normalColor
            };
        }
    }
}
