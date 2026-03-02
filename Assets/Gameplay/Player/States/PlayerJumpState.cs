using SoR.Core;
using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerJumpState : IState
    {
        private readonly PlayerController _controller;

        private float _verticalVelocity;
        private float _jumpTimer;

        public PlayerJumpState(PlayerController controller)
        {
            _controller = controller;
        }

        public void Enter()
        {
            _controller.View.PlayAnimation("Jump");
            _verticalVelocity = _controller.JumpForce;
            _jumpTimer = 0f;
        }

        public void Execute()
        {
            _jumpTimer += Time.deltaTime;

            // Apply gravity
            _verticalVelocity += Physics.gravity.y * Time.deltaTime;

            // Allow horizontal drift while airborne
            Vector3 input = _controller.InputDirection;
            Vector3 move = input.normalized * (_controller.BaseData.MoveSpeed * 0.5f);
            move.y = _verticalVelocity;

            _controller.Movement.GetCharacterController().Move(move * Time.deltaTime);

            if (input.sqrMagnitude > 0.01f)
                _controller.Movement.FaceDirection(input);

            // Land when grounded (after a small grace period so we don't land immediately)
            if (_jumpTimer > 0.1f && _controller.Movement.IsGrounded)
            {
                _controller.StateMachine.ChangeState(_controller.IdleState);
            }
        }

        public void Exit()
        {
        }
    }
}
