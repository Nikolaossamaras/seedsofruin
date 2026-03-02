using SoR.Core;
using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerDodgeState : IState
    {
        private readonly PlayerController _controller;

        private Vector3 _dodgeDirection;
        private float _dodgeTimer;
        private float _dodgeDuration;

        public PlayerDodgeState(PlayerController controller)
        {
            _controller = controller;
        }

        public void Enter()
        {
            _controller.View.PlayAnimation("Dodge");
            _controller.IsInvincible = true;

            _dodgeDirection = _controller.InputDirection.sqrMagnitude > 0.01f
                ? _controller.InputDirection.normalized
                : _controller.Movement.transform.forward;

            // Calculate duration from distance and speed.
            float speed = _controller.Movement.DodgeSpeed;
            float distance = _controller.Movement.DodgeDistance;
            _dodgeDuration = speed > 0f ? distance / speed : 0.3f;
            _dodgeTimer = 0f;
        }

        public void Execute()
        {
            _dodgeTimer += Time.deltaTime;

            if (_dodgeTimer < _dodgeDuration)
            {
                _controller.Movement.Dodge(_dodgeDirection);
            }
            else
            {
                _controller.StateMachine.ChangeState(_controller.IdleState);
            }
        }

        public void Exit()
        {
            _controller.IsInvincible = false;
            _controller.StartDodgeCooldown();
        }
    }
}
