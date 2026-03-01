using SoR.Core;
using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerSkillState : IState
    {
        private readonly PlayerController _controller;

        private float _castTimer;
        private float _castDuration = 0.8f;
        private int _skillIndex;

        public PlayerSkillState(PlayerController controller)
        {
            _controller = controller;
        }

        public void Enter()
        {
            _skillIndex = _controller.SkillInputIndex;
            _controller.SkillInputIndex = -1;
            _castTimer = 0f;

            // TODO: check verdance cost from the skill definition and
            // deduct via PlayerStatManager. For now just play the animation.
            _controller.View.PlayAnimation("Skill");

            Debug.Log($"[PlayerSkillState] Casting skill index {_skillIndex}");
        }

        public void Execute()
        {
            _castTimer += Time.deltaTime;

            if (_castTimer >= _castDuration)
            {
                _controller.StateMachine.ChangeState(_controller.IdleState);
            }
        }

        public void Exit()
        {
            // Apply skill effect at the end of the cast.
            Debug.Log($"[PlayerSkillState] Skill {_skillIndex} effect applied.");
        }
    }
}
