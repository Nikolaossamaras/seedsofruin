using SoR.Core;
using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerDeathState : IState
    {
        private readonly PlayerController _controller;

        public PlayerDeathState(PlayerController controller)
        {
            _controller = controller;
        }

        public void Enter()
        {
            _controller.View.TriggerDeath();

            // Disable all player input.
            _controller.InputDirection = Vector3.zero;
            _controller.AttackInputPressed = false;
            _controller.DodgeInputPressed = false;
            _controller.JumpInputPressed = false;
            _controller.SkillInputIndex = -1;

            Debug.Log("[PlayerDeathState] Player has died.");
        }

        public void Execute()
        {
            // Death is a terminal state. No transitions out.
        }

        public void Exit()
        {
            // Nothing to clean up.
        }
    }
}
