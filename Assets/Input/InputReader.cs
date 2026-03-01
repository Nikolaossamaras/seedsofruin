using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SoR.Input
{
    [CreateAssetMenu(menuName = "SoR/Input/InputReader")]
    public class InputReader : ScriptableObject
    {
        public event Action<Vector2> OnMoveInput;
        public event Action OnAttackPressed;
        public event Action OnDodgePressed;
        public event Action<int> OnSkillPressed;
        public event Action OnInteractPressed;
        public event Action OnMenuToggled;
        public event Action OnPausePressed;

        public void OnMove(InputAction.CallbackContext ctx)
        {
            OnMoveInput?.Invoke(ctx.ReadValue<Vector2>());
        }

        public void OnAttack(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                OnAttackPressed?.Invoke();
        }

        public void OnDodge(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                OnDodgePressed?.Invoke();
        }

        public void OnSkill1(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                OnSkillPressed?.Invoke(0);
        }

        public void OnSkill2(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                OnSkillPressed?.Invoke(1);
        }

        public void OnSkill3(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                OnSkillPressed?.Invoke(2);
        }

        public void OnSkill4(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                OnSkillPressed?.Invoke(3);
        }

        public void OnInteract(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                OnInteractPressed?.Invoke();
        }

        public void OnMenu(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                OnMenuToggled?.Invoke();
        }

        public void OnPause(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                OnPausePressed?.Invoke();
        }
    }
}
