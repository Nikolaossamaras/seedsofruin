using UnityEngine;
using SoR.Core;

namespace SoR
{
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("System Prefabs (instantiated in order)")]
        [SerializeField] private GameObject[] _systemPrefabs;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EventBus.Clear();
            ServiceLocator.Clear();

            foreach (var prefab in _systemPrefabs)
            {
                var instance = Instantiate(prefab, transform);
                instance.name = prefab.name;
            }

            Debug.Log("[Bootstrapper] All systems initialized.");
        }
    }
}
