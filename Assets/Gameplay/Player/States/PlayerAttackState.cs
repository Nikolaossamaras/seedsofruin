using SoR.Core;
using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerAttackState : IState
    {
        private readonly PlayerController _controller;

        private float _attackTimer;
        private float _attackDuration = 0.6f;
        private int _comboStep;
        private int _maxCombo = 3;
        public bool HitboxActive { get; private set; }
        private bool _queueNextAttack;

        public PlayerAttackState(PlayerController controller)
        {
            _controller = controller;
        }

        public void Enter()
        {
            _attackTimer = 0f;
            _comboStep = 0;
            _queueNextAttack = false;
            StartAttack();
        }

        public void Execute()
        {
            _attackTimer += Time.deltaTime;

            // Allow buffering the next attack input for combo advancement.
            if (_controller.AttackInputPressed)
            {
                _controller.AttackInputPressed = false;
                _queueNextAttack = true;
            }

            if (_attackTimer >= _attackDuration)
            {
                // Advance combo if the player queued another attack.
                if (_queueNextAttack && _comboStep < _maxCombo - 1)
                {
                    _comboStep++;
                    _queueNextAttack = false;
                    _attackTimer = 0f;
                    StartAttack();
                    return;
                }

                // Attack sequence complete; return to idle.
                _controller.StateMachine.ChangeState(_controller.IdleState);
            }
        }

        public void Exit()
        {
            HitboxActive = false;
            _comboStep = 0;
        }

        private void StartAttack()
        {
            string animName = _comboStep switch
            {
                0 => "Attack1",
                1 => "Attack2",
                2 => "Attack3",
                _ => "Attack1"
            };

            _controller.View.PlayAnimation(animName);
            HitboxActive = true;
        }
    }
}
