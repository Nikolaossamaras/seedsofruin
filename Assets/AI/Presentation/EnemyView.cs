using UnityEngine;
using SoR.Core;
using SoR.Combat;

namespace SoR.AI
{
    public class EnemyView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private MeshRenderer _meshRenderer;
        [SerializeField] private Animator _animator;

        private static readonly int HitTrigger = Animator.StringToHash("Hit");
        private static readonly int DeathTrigger = Animator.StringToHash("Death");
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");

        private Color _originalColor;

        private void Awake()
        {
            if (_spriteRenderer != null)
                _originalColor = _spriteRenderer.color;
            else if (_meshRenderer != null && _meshRenderer.material != null)
                _originalColor = _meshRenderer.material.color;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyDamagedEvent>(HandleDamaged);
            EventBus.Subscribe<EnemyKilledEvent>(HandleKilled);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDamagedEvent>(HandleDamaged);
            EventBus.Unsubscribe<EnemyKilledEvent>(HandleKilled);
        }

        private void HandleDamaged(EnemyDamagedEvent evt)
        {
            if (evt.Enemy != gameObject) return;
            PlayHitReaction();
        }

        private void HandleKilled(EnemyKilledEvent evt)
        {
            if (evt.Enemy != gameObject) return;
            PlayDeathAnimation();
        }

        public void PlayHitReaction()
        {
            if (_animator != null)
                _animator.SetTrigger(HitTrigger);
        }

        public void PlayDeathAnimation()
        {
            if (_animator != null)
                _animator.SetTrigger(DeathTrigger);
        }

        public void PlayAttackAnimation()
        {
            if (_animator != null)
                _animator.SetTrigger(AttackTrigger);
        }

        public void SetTint(Color color)
        {
            if (_spriteRenderer != null)
                _spriteRenderer.color = color;
            else if (_meshRenderer != null && _meshRenderer.material != null)
                _meshRenderer.material.color = color;
        }

        public void ResetTint()
        {
            SetTint(_originalColor);
        }
    }
}
