using System.Collections;
using UnityEngine;

namespace SoR.UI
{
    public static class UIAnimator
    {
        /// <summary>
        /// Fades a CanvasGroup from its current alpha to 1 over the specified duration.
        /// </summary>
        public static IEnumerator FadeIn(CanvasGroup canvasGroup, float duration)
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// Fades a CanvasGroup from its current alpha to 0 over the specified duration.
        /// </summary>
        public static IEnumerator FadeOut(CanvasGroup canvasGroup, float duration)
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// Scales a Transform from zero to its original scale over the specified duration.
        /// </summary>
        public static IEnumerator ScaleIn(Transform target, float duration)
        {
            if (target == null) yield break;

            Vector3 targetScale = target.localScale;
            float elapsed = 0f;

            target.localScale = Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                // Ease out back for a slight overshoot effect
                float easedT = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
                target.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, easedT);
                yield return null;
            }

            target.localScale = targetScale;
        }

        /// <summary>
        /// Scales a Transform from its current scale to zero over the specified duration.
        /// </summary>
        public static IEnumerator ScaleOut(Transform target, float duration)
        {
            if (target == null) yield break;

            Vector3 startScale = target.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                target.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            target.localScale = Vector3.zero;
        }
    }
}
