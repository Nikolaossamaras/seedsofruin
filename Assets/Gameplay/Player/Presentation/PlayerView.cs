using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerView : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        public void PlayAnimation(string animName)
        {
            if (_animator == null)
                return;

            _animator.Play(animName);
        }

        public void SetMoveBlend(float speed)
        {
            if (_animator == null)
                return;

            _animator.SetFloat(SpeedParam, speed);
        }

        public void TriggerHitReaction()
        {
            PlayAnimation("HitReaction");
        }

        public void TriggerDeath()
        {
            PlayAnimation("Death");
        }
    }
}
