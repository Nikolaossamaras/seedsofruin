using UnityEngine;

namespace SoR.Gameplay
{
    public class CompanionView : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private Renderer _renderer;
        [SerializeField] private Animator _animator;

        [Header("VFX")]
        [SerializeField] private ParticleSystem _skillVFX;

        public void PlayAnimation(string anim)
        {
            if (_animator == null)
                return;

            _animator.Play(anim);
        }

        public void PlaySkillVFX()
        {
            if (_skillVFX != null)
            {
                _skillVFX.Play();
            }
            else
            {
                Debug.Log("[CompanionView] Skill VFX not assigned.");
            }
        }
    }
}
