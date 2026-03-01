using SoR.Shared;
using UnityEngine;

namespace SoR.Gameplay
{
    [CreateAssetMenu(fileName = "New PlayerStats", menuName = "SoR/Gameplay/PlayerStats")]
    public class PlayerStatsSO : ScriptableObject
    {
        [Header("Identity")]
        public string CharacterName = "Alder";

        [Header("Base Stats")]
        public StatBlock BaseStats;

        [Header("Resource Pools")]
        public float BaseHealth = 1000f;
        public float BaseVerdance = 100f;

        [Header("Movement")]
        public float MoveSpeed = 6f;
        public float DodgeSpeed = 14f;
        public float DodgeDistance = 4f;
    }
}
