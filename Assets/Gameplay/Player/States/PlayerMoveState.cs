using SoR.Core;
using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerMoveState : IState
    {
        private readonly PlayerController _controller;

        public PlayerMoveState(PlayerController controller)
        {
            _controller = controller;
        }

        public void Enter()
        {
        }

        public void Execute()
        {
            Vector3 input = _controller.InputDirection;

            // Transition to attack.
            if (_controller.AttackInputPressed)
            {
                _controller.AttackInputPressed = false;
                _controller.StateMachine.ChangeState(_controller.AttackState);
                return;
            }

            // Transition to dodge.
            if (_controller.DodgeInputPressed)
            {
                _controller.DodgeInputPressed = false;
                _controller.StateMachine.ChangeState(_controller.DodgeState);
                return;
            }

            // Transition to idle when no input.
            if (input.sqrMagnitude < 0.01f)
            {
                _controller.StateMachine.ChangeState(_controller.IdleState);
                return;
            }

            // Apply movement.
            _controller.Movement.Move(input);
            _controller.View.SetMoveBlend(input.magnitude);
        }

        public void Exit()
        {
        }
    }
}
