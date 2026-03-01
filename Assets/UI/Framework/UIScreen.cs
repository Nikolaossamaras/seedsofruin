using System.Collections;
using UnityEngine;

namespace SoR.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [SerializeField] private float _fadeDuration = 0.25f;

        private CanvasGroup _canvasGroup;

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null)
                    _canvasGroup = GetComponent<CanvasGroup>();
                return _canvasGroup;
            }
        }

        public bool IsVisible { get; private set; }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            IsVisible = true;
            StartCoroutine(FadeIn());
        }

        public virtual void Hide()
        {
            StartCoroutine(FadeOutAndDeactivate());
        }

        protected virtual void OnScreenShown()
        {
            Debug.Log($"[UIScreen] {GetType().Name} shown.");
        }

        protected virtual void OnScreenHidden()
        {
            Debug.Log($"[UIScreen] {GetType().Name} hidden.");
        }

        private IEnumerator FadeIn()
        {
            CanvasGroup.alpha = 0f;
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                CanvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }

            CanvasGroup.alpha = 1f;
            CanvasGroup.interactable = true;
            OnScreenShown();
        }

        private IEnumerator FadeOutAndDeactivate()
        {
            CanvasGroup.interactable = false;

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                CanvasGroup.alpha = Mathf.Clamp01(1f - elapsed / _fadeDuration);
                yield return null;
            }

            CanvasGroup.alpha = 0f;
            CanvasGroup.blocksRaycasts = false;
            IsVisible = false;
            gameObject.SetActive(false);
            OnScreenHidden();
        }
    }
}
