using SoR.Core;

namespace SoR.Gameplay
{
    public class PlayerIdleState : IState
    {
        private readonly PlayerController _controller;

        public PlayerIdleState(PlayerController controller)
        {
            _controller = controller;
        }

        public void Enter()
        {
            _controller.View.PlayAnimation("Idle");
            _controller.View.SetMoveBlend(0f);
        }

        public void Execute()
        {
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

            // Transition to skill.
            if (_controller.SkillInputIndex >= 0)
            {
                _controller.StateMachine.ChangeState(_controller.SkillState);
                return;
            }

            // Transition to movement.
            if (_controller.InputDirection.sqrMagnitude > 0.01f)
            {
                _controller.StateMachine.ChangeState(_controller.MoveState);
            }
        }

        public void Exit()
        {
        }
    }
}
