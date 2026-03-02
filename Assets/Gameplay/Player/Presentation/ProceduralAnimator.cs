using UnityEngine;

namespace SoR.Gameplay
{
    /// <summary>
    /// Transform-based procedural animations for the player visual.
    /// Attach to the visual child (e.g. capsule or loaded model).
    /// Driven by SetAnimation / SetMoveBlend — no AnimatorController required.
    /// </summary>
    public class ProceduralAnimator : MonoBehaviour
    {
        // ---- cached base pose ----
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private Vector3 _baseLocalScale;

        // ---- current state ----
        private string _currentAnim = "Idle";
        private float _animTimer;
        private float _moveBlend;

        // ---- material flash (Skill) ----
        private Renderer _renderer;
        private Color _originalColor;
        private bool _hasOriginalColor;

        // ---- animation durations ----
        private const float AttackDuration = 0.25f;
        private const float DodgeDuration = 0.35f;
        private const float SkillDuration = 0.6f;
        private const float HitDuration = 0.15f;
        private const float DeathDuration = 0.5f;
        private const float JumpDuration = 0.5f;

        private void Awake()
        {
            _baseLocalPosition = transform.localPosition;
            _baseLocalRotation = transform.localRotation;
            _baseLocalScale = transform.localScale;

            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
            {
                _originalColor = GetMainColor(_renderer.material);
                _hasOriginalColor = true;
            }
        }

        public void SetAnimation(string animName)
        {
            // Restore material color when leaving Skill
            if (_currentAnim == "Skill" && animName != "Skill")
                RestoreColor();

            _currentAnim = animName;
            _animTimer = 0f;
        }

        public void SetMoveBlend(float speed)
        {
            _moveBlend = speed;

            // Auto-switch between Walk and Run when in a movement state
            if (_currentAnim == "Move" || _currentAnim == "Walk" || _currentAnim == "Run")
                _currentAnim = speed >= 0.6f ? "Run" : "Walk";
        }

        private void Update()
        {
            _animTimer += Time.deltaTime;

            // Reset to base pose each frame, then apply offsets
            transform.localPosition = _baseLocalPosition;
            transform.localRotation = _baseLocalRotation;
            transform.localScale = _baseLocalScale;

            switch (_currentAnim)
            {
                case "Idle":        ApplyIdle();        break;
                case "Move":        ApplyWalk();        break;
                case "Walk":        ApplyWalk();        break;
                case "Run":         ApplyRun();         break;
                case "Attack":      ApplyAttack(0.35f, 10f); break;
                case "Attack1":     ApplyAttack(0.3f, 0f);   break;
                case "Attack2":     ApplyAttack(0.3f, 15f);  break;
                case "Attack3":     ApplyAttack(0.45f, -20f); break;
                case "Dodge":       ApplyDodge();       break;
                case "Jump":        ApplyJump();        break;
                case "Skill":       ApplySkill();       break;
                case "HitReaction": ApplyHitReaction(); break;
                case "Death":       ApplyDeath();       break;
            }
        }

        // ================================================================
        // Animation implementations
        // ================================================================

        private void ApplyIdle()
        {
            // Gentle vertical bob
            float bob = Mathf.Sin(_animTimer * 2f) * 0.04f;
            transform.localPosition = _baseLocalPosition + new Vector3(0f, bob, 0f);
        }

        private void ApplyWalk()
        {
            // Moderate bob + slight forward tilt + subtle side sway
            float bob = Mathf.Sin(_animTimer * 5f) * 0.05f;
            float sway = Mathf.Sin(_animTimer * 2.5f) * 1.5f;
            transform.localPosition = _baseLocalPosition + new Vector3(0f, bob, 0f);
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(5f, 0f, sway);
        }

        private void ApplyRun()
        {
            // Faster bob + more forward tilt + wider side sway + slight scale bounce
            float bob = Mathf.Sin(_animTimer * 9f) * 0.08f;
            float sway = Mathf.Sin(_animTimer * 4.5f) * 3f;
            transform.localPosition = _baseLocalPosition + new Vector3(0f, bob, 0f);
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(12f, 0f, sway);

            float scalePulse = 1f + Mathf.Sin(_animTimer * 9f) * 0.02f;
            transform.localScale = new Vector3(_baseLocalScale.x, _baseLocalScale.y * scalePulse, _baseLocalScale.z);
        }

        private void ApplyAttack(float lungeDistance, float yaw)
        {
            float t = Mathf.Clamp01(_animTimer / AttackDuration);
            // Quick lunge out then snap back
            float lunge = Mathf.Sin(t * Mathf.PI) * lungeDistance;
            transform.localPosition = _baseLocalPosition + transform.parent.InverseTransformDirection(transform.parent.forward) * lunge;
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(0f, Mathf.Sin(t * Mathf.PI) * yaw, 0f);
        }

        private void ApplyDodge()
        {
            float t = Mathf.Clamp01(_animTimer / DodgeDuration);
            // 360° forward roll (X-axis spin)
            float angle = t * 360f;
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(angle, 0f, 0f);
        }

        private void ApplyJump()
        {
            float t = Mathf.Clamp01(_animTimer / JumpDuration);
            // Parabolic arc upward + slight tuck (scale squeeze at peak)
            float arc = Mathf.Sin(t * Mathf.PI) * 0.6f;
            transform.localPosition = _baseLocalPosition + new Vector3(0f, arc, 0f);

            // Tuck at peak: slight X-axis tilt and Y-scale squeeze
            float tuck = Mathf.Sin(t * Mathf.PI);
            float scaleY = 1f - tuck * 0.1f;
            transform.localScale = new Vector3(_baseLocalScale.x, _baseLocalScale.y * scaleY, _baseLocalScale.z);
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(-tuck * 15f, 0f, 0f);
        }

        private void ApplySkill()
        {
            float t = Mathf.Clamp01(_animTimer / SkillDuration);
            // Rise upward
            float rise = Mathf.Sin(t * Mathf.PI) * 0.5f;
            transform.localPosition = _baseLocalPosition + new Vector3(0f, rise, 0f);

            // Rapid color flash pulse
            if (_renderer != null && _hasOriginalColor)
            {
                float flash = Mathf.PingPong(_animTimer * 8f, 1f);
                Color c = Color.Lerp(_originalColor, Color.white, flash);
                SetMainColor(_renderer.material, c);
            }
        }

        private void ApplyHitReaction()
        {
            float t = Mathf.Clamp01(_animTimer / HitDuration);
            // Quick backward jolt
            float jolt = Mathf.Sin(t * Mathf.PI) * -0.2f;
            transform.localPosition = _baseLocalPosition + transform.parent.InverseTransformDirection(transform.parent.forward) * jolt;
        }

        private void ApplyDeath()
        {
            float t = Mathf.Clamp01(_animTimer / DeathDuration);
            // Fall sideways to 90° (Z-axis rotation)
            float angle = Mathf.Lerp(0f, 90f, t);
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(0f, 0f, angle);
        }

        // ================================================================
        // Material helpers
        // ================================================================

        private void RestoreColor()
        {
            if (_renderer != null && _hasOriginalColor)
                SetMainColor(_renderer.material, _originalColor);
        }

        private static Color GetMainColor(Material mat)
        {
            if (mat.HasProperty("_BaseColor"))
                return mat.GetColor("_BaseColor");
            if (mat.HasProperty("_Color"))
                return mat.GetColor("_Color");
            return Color.white;
        }

        private static void SetMainColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
        }
    }
}
