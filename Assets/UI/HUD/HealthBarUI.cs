using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SoR.UI
{
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private Image _fillImage;
        [SerializeField] private Image _delayedFillImage;
        [SerializeField] private float _delayedFillSpeed = 0.5f;

        private float _targetFill;
        private Coroutine _delayedFillCoroutine;

        public Image FillImage => _fillImage;

        /// <summary>
        /// Sets the fill amount of the health bar.
        /// </summary>
        /// <param name="normalized">Normalized fill value from 0 to 1.</param>
        public void SetFill(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            _targetFill = normalized;

            if (_fillImage != null)
            {
                _fillImage.fillAmount = normalized;
            }

            // Optional delayed fill (damage bar effect)
            if (_delayedFillImage != null)
            {
                if (_delayedFillCoroutine != null)
                    StopCoroutine(_delayedFillCoroutine);

                _delayedFillCoroutine = StartCoroutine(AnimateDelayedFill());
            }
        }

        private IEnumerator AnimateDelayedFill()
        {
            // Brief pause before the delayed bar starts catching up
            yield return new WaitForSeconds(0.4f);

            while (_delayedFillImage != null &&
                   Mathf.Abs(_delayedFillImage.fillAmount - _targetFill) > 0.001f)
            {
                _delayedFillImage.fillAmount = Mathf.MoveTowards(
                    _delayedFillImage.fillAmount,
                    _targetFill,
                    _delayedFillSpeed * Time.deltaTime
                );
                yield return null;
            }

            if (_delayedFillImage != null)
                _delayedFillImage.fillAmount = _targetFill;

            _delayedFillCoroutine = null;
        }
    }
}
