using UnityEngine;

namespace SoR.Testing
{
    /// <summary>
    /// Simplified procedural animator for companion visuals.
    /// 3 states: Idle, Walk, Attack. Mirrors ProceduralAnimator patterns.
    /// </summary>
    public class CompanionProceduralAnimator : MonoBehaviour
    {
        // ---- cached base pose ----
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private Vector3 _baseLocalScale;

        // ---- current state ----
        private string _currentAnim = "Idle";
        private float _animTimer;

        // ---- material flash ----
        private Renderer _renderer;
        private Color _originalColor;
        private bool _hasOriginalColor;

        // ---- constants ----
        private const float AttackDuration = 0.3f;

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
            // Restore color when leaving Attack
            if (_currentAnim == "Attack" && animName != "Attack")
                RestoreColor();

            _currentAnim = animName;
            _animTimer = 0f;
        }

        private void Update()
        {
            _animTimer += Time.deltaTime;

            // Reset to base pose each frame
            transform.localPosition = _baseLocalPosition;
            transform.localRotation = _baseLocalRotation;
            transform.localScale = _baseLocalScale;

            switch (_currentAnim)
            {
                case "Idle":   ApplyIdle();   break;
                case "Walk":   ApplyWalk();   break;
                case "Attack": ApplyAttack(); break;
            }
        }

        // ================================================================
        // Animation implementations
        // ================================================================

        private void ApplyIdle()
        {
            // Gentle vertical bob
            float bob = Mathf.Sin(_animTimer * 2f) * 0.03f;
            transform.localPosition = _baseLocalPosition + new Vector3(0f, bob, 0f);
        }

        private void ApplyWalk()
        {
            // Bob + forward tilt + side sway
            float bob = Mathf.Sin(_animTimer * 6f) * 0.06f;
            float sway = Mathf.Sin(_animTimer * 3f) * 2f;
            transform.localPosition = _baseLocalPosition + new Vector3(0f, bob, 0f);
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(5f, 0f, sway);
        }

        private void ApplyAttack()
        {
            float t = Mathf.Clamp01(_animTimer / AttackDuration);

            // Forward lunge
            float lunge = Mathf.Sin(t * Mathf.PI) * 0.3f;
            transform.localPosition = _baseLocalPosition
                + transform.parent.InverseTransformDirection(transform.parent.forward) * lunge;

            // Brief white color flash
            if (_renderer != null && _hasOriginalColor)
            {
                float flash = Mathf.Sin(t * Mathf.PI);
                Color c = Color.Lerp(_originalColor, Color.white, flash * 0.6f);
                SetMainColor(_renderer.material, c);
            }

            // Auto-return to Idle when done
            if (t >= 1f)
            {
                RestoreColor();
                _currentAnim = "Idle";
                _animTimer = 0f;
            }
        }

        // ================================================================
        // Material helpers
        // ================================================================

        private void RestoreColor()
        {
            if (_renderer != null && _hasOriginalColor)
                SetMainColor(_renderer.material, _originalColor);
        }

        public static Color GetMainColor(Material mat)
        {
            if (mat.HasProperty("_BaseColor"))
                return mat.GetColor("_BaseColor");
            if (mat.HasProperty("_Color"))
                return mat.GetColor("_Color");
            return Color.white;
        }

        public static void SetMainColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
        }
    }
}
