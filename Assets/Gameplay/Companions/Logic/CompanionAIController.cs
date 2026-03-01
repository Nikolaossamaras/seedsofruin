using UnityEngine;

namespace SoR.Gameplay
{
    public class CompanionAIController : MonoBehaviour
    {
        public enum Stance
        {
            Aggressive,
            Defensive,
            Tactical
        }

        [Header("Definition")]
        [SerializeField] private CompanionDefinitionSO _definition;

        [Header("Behavior")]
        [SerializeField] private Stance _currentStance = Stance.Tactical;
        [SerializeField] private float _followDistance = 3f;

        [Header("References")]
        [SerializeField] private Transform _followTarget;

        public CompanionDefinitionSO Definition => _definition;
        public Stance CurrentStance => _currentStance;

        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
        }

        public void SetStance(Stance stance)
        {
            _currentStance = stance;
        }

        private void Update()
        {
            if (_followTarget == null)
                return;

            UpdateFollowBehavior();
            UpdateCombatBehavior();
        }

        private void UpdateFollowBehavior()
        {
            Vector3 toTarget = _followTarget.position - transform.position;
            float distance = toTarget.magnitude;

            if (distance > _followDistance)
            {
                Vector3 direction = toTarget.normalized;
                float speed = _definition != null
                    ? _definition.BaseStats.Agility * 0.5f + 3f
                    : 4f;

                transform.position += direction * (speed * Time.deltaTime);

                if (direction.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(direction),
                        Time.deltaTime * 8f);
                }
            }
        }

        private void UpdateCombatBehavior()
        {
            switch (_currentStance)
            {
                case Stance.Aggressive:
                    // TODO: seek nearest enemy, use active skill when available.
                    break;

                case Stance.Defensive:
                    // TODO: stay near player, prioritize healing/shielding skills.
                    break;

                case Stance.Tactical:
                    // TODO: balanced approach, use skills based on situation.
                    break;
            }
        }
    }
}
