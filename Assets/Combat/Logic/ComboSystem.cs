using UnityEngine;

namespace SoR.Combat
{
    public class ComboSystem : MonoBehaviour
    {
        [SerializeField] private float comboResetTime = 1.5f;

        private int _currentComboStep;
        private float _comboResetTimer;

        private static readonly float[] ComboMultipliers = { 1.0f, 1.1f, 1.2f, 1.5f };

        public int CurrentComboStep => _currentComboStep;

        /// <summary>
        /// Advances the combo by one step and returns the combo multiplier for this hit.
        /// The final hit in the sequence is the finisher (1.5x multiplier).
        /// </summary>
        public float AdvanceCombo()
        {
            int index = Mathf.Clamp(_currentComboStep, 0, ComboMultipliers.Length - 1);
            float multiplier = ComboMultipliers[index];

            _currentComboStep++;
            _comboResetTimer = comboResetTime;

            if (_currentComboStep >= ComboMultipliers.Length)
            {
                ResetCombo();
            }

            return multiplier;
        }

        /// <summary>
        /// Resets the combo back to step 0.
        /// </summary>
        public void ResetCombo()
        {
            _currentComboStep = 0;
            _comboResetTimer = 0f;
        }

        private void Update()
        {
            if (_comboResetTimer > 0f)
            {
                _comboResetTimer -= Time.deltaTime;
                if (_comboResetTimer <= 0f)
                {
                    ResetCombo();
                }
            }
        }
    }
}
