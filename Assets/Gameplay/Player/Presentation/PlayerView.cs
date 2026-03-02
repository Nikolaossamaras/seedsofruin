using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerView : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        private ProceduralAnimator _proceduralAnimator;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        public void SetProceduralAnimator(ProceduralAnimator pa)
        {
            _proceduralAnimator = pa;
        }

        public void PlayAnimation(string animName)
        {
            if (_animator != null)
            {
                _animator.Play(animName);
                return;
            }

            _proceduralAnimator?.SetAnimation(animName);
        }

        public void SetMoveBlend(float speed)
        {
            if (_animator != null)
            {
                _animator.SetFloat(SpeedParam, speed);
                return;
            }

            _proceduralAnimator?.SetMoveBlend(speed);
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
