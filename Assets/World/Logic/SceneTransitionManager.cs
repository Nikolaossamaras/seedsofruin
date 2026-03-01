using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoR.Core;

namespace SoR.World
{
    public class SceneTransitionManager : MonoBehaviour, IService
    {
        [SerializeField] private CanvasGroup _fadeCanvasGroup;
        [SerializeField] private float _fadeDuration = 0.5f;

        public bool IsTransitioning { get; private set; }
        public Action OnTransitionComplete;

        public void Initialize() { }
        public void Dispose() { }

        /// <summary>
        /// Transitions to a region by loading its scene asynchronously.
        /// Fades the screen to black, loads the scene, then fades back in.
        /// </summary>
        /// <param name="regionId">The scene path or region ID to load.</param>
        public void TransitionToRegion(string regionId)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning("[SceneTransition] Already transitioning, ignoring request.");
                return;
            }

            StartCoroutine(TransitionCoroutine(regionId));
        }

        private IEnumerator TransitionCoroutine(string scenePath)
        {
            IsTransitioning = true;

            // Fade to black
            yield return StartCoroutine(Fade(0f, 1f));

            // Load the scene asynchronously
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(scenePath);
            if (loadOp != null)
            {
                while (!loadOp.isDone)
                {
                    yield return null;
                }
            }

            // Fade back in
            yield return StartCoroutine(Fade(1f, 0f));

            IsTransitioning = false;
            OnTransitionComplete?.Invoke();
            Debug.Log($"[SceneTransition] Transition to '{scenePath}' complete.");
        }

        private IEnumerator Fade(float from, float to)
        {
            if (_fadeCanvasGroup == null)
                yield break;

            float elapsed = 0f;
            _fadeCanvasGroup.alpha = from;

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
                yield return null;
            }

            _fadeCanvasGroup.alpha = to;
        }
    }
}
