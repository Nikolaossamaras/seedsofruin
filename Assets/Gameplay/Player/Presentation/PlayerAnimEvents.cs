using System;
using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerAnimEvents : MonoBehaviour
    {
        public Action OnAttackHitFrameCallback;
        public Action OnAttackEndCallback;
        public Action OnFootstepCallback;

        /// <summary>
        /// Called by animation event on the attack hit frame.
        /// Enables the hitbox so the attack can register damage.
        /// </summary>
        public void OnAttackHitFrame()
        {
            OnAttackHitFrameCallback?.Invoke();
        }

        /// <summary>
        /// Called by animation event when the attack animation completes.
        /// Signals that the attack sequence is finished.
        /// </summary>
        public void OnAttackEnd()
        {
            OnAttackEndCallback?.Invoke();
        }

        /// <summary>
        /// Called by animation event on footstep frames.
        /// Triggers footstep audio through the FootstepHandler.
        /// </summary>
        public void OnFootstep()
        {
            OnFootstepCallback?.Invoke();
        }
    }
}
