using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SoR.UI.Crafting
{
    public class CraftingTimingUI : MonoBehaviour
    {
        private const float SHRINK_DURATION = 2f;
        private const float START_SIZE = 300f;
        private const float TARGET_SIZE = 100f;
        private const float FADE_DURATION = 0.25f;
        private const float RESULT_HOLD = 0.8f;

        // Timing zone thresholds (ring size when clicked)
        private const float PERFECT_THRESHOLD = 55f;
        private const float GOOD_THRESHOLD = 80f;
        private const float OK_THRESHOLD = 130f;

        private GameObject _overlayRoot;
        private CanvasGroup _canvasGroup;
        private Image _targetCircle;
        private Image _shrinkingRing;
        private Text _resultText;
        private Text _instructionText;
        private Font _font;
        private Coroutine _activeCoroutine;
        private bool _inputLocked;
        private float _currentRingSize;
        private bool _waitingForInput;

        public static CraftingTimingUI Create(Canvas canvas, Font font)
        {
            // Overlay root — fullscreen dark background
            var overlayGo = new GameObject("CraftingTimingOverlay");
            overlayGo.transform.SetParent(canvas.transform, false);
            var overlayRt = overlayGo.AddComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;

            var overlayImg = overlayGo.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.85f);

            var cg = overlayGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var ui = overlayGo.AddComponent<CraftingTimingUI>();
            ui._overlayRoot = overlayGo;
            ui._canvasGroup = cg;
            ui._font = font;

            // Target circle (fixed, green, center)
            var targetGo = new GameObject("TargetCircle");
            targetGo.transform.SetParent(overlayGo.transform, false);
            var targetRt = targetGo.AddComponent<RectTransform>();
            targetRt.anchorMin = new Vector2(0.5f, 0.5f);
            targetRt.anchorMax = new Vector2(0.5f, 0.5f);
            targetRt.sizeDelta = new Vector2(TARGET_SIZE, TARGET_SIZE);
            ui._targetCircle = targetGo.AddComponent<Image>();
            ui._targetCircle.sprite = CreateCircleSprite(64, true);
            ui._targetCircle.color = new Color(0.3f, 0.8f, 0.3f, 0.5f);

            // Shrinking ring (gold, starts large)
            var ringGo = new GameObject("ShrinkingRing");
            ringGo.transform.SetParent(overlayGo.transform, false);
            var ringRt = ringGo.AddComponent<RectTransform>();
            ringRt.anchorMin = new Vector2(0.5f, 0.5f);
            ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(START_SIZE, START_SIZE);
            ui._shrinkingRing = ringGo.AddComponent<Image>();
            ui._shrinkingRing.sprite = CreateCircleSprite(64, false);
            ui._shrinkingRing.color = new Color(1f, 0.85f, 0.3f, 0.9f);

            // Instruction text
            var instrGo = new GameObject("InstructionText");
            instrGo.transform.SetParent(overlayGo.transform, false);
            var instrRt = instrGo.AddComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, -120f);
            instrRt.sizeDelta = new Vector2(400f, 40f);
            ui._instructionText = instrGo.AddComponent<Text>();
            ui._instructionText.font = font;
            ui._instructionText.fontSize = 20;
            ui._instructionText.color = new Color(0.8f, 0.8f, 0.8f);
            ui._instructionText.alignment = TextAnchor.MiddleCenter;
            ui._instructionText.text = "Click or press Space!";

            // Result text
            var resultGo = new GameObject("ResultText");
            resultGo.transform.SetParent(overlayGo.transform, false);
            var resultRt = resultGo.AddComponent<RectTransform>();
            resultRt.anchorMin = new Vector2(0.5f, 0.5f);
            resultRt.anchorMax = new Vector2(0.5f, 0.5f);
            resultRt.anchoredPosition = new Vector2(0f, 120f);
            resultRt.sizeDelta = new Vector2(400f, 50f);
            ui._resultText = resultGo.AddComponent<Text>();
            ui._resultText.font = font;
            ui._resultText.fontSize = 32;
            ui._resultText.fontStyle = FontStyle.Bold;
            ui._resultText.color = Color.white;
            ui._resultText.alignment = TextAnchor.MiddleCenter;
            ui._resultText.text = "";

            overlayGo.SetActive(false);
            return ui;
        }

        /// <summary>
        /// Begins the timing minigame. Callback receives timingBonus (0.0-1.0).
        /// </summary>
        public void Begin(Action<float> callback)
        {
            _overlayRoot.SetActive(true);
            _inputLocked = false;
            _waitingForInput = false;
            _resultText.text = "";
            _instructionText.text = "Click or press Space!";

            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(TimingRoutine(callback));
        }

        public void Dismiss()
        {
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }

            if (_overlayRoot != null)
                Destroy(_overlayRoot);

            Destroy(this);
        }

        private void Update()
        {
            if (!_waitingForInput || _inputLocked) return;

            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                _inputLocked = true;
            }
        }

        private IEnumerator TimingRoutine(Action<float> callback)
        {
            var ringRt = _shrinkingRing.GetComponent<RectTransform>();

            // Fade in
            float fadeElapsed = 0f;
            _canvasGroup.alpha = 0f;
            while (fadeElapsed < FADE_DURATION)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(fadeElapsed / FADE_DURATION);
                yield return null;
            }
            _canvasGroup.alpha = 1f;

            // Brief pause before starting
            yield return new WaitForSecondsRealtime(0.3f);

            // Shrink ring
            _waitingForInput = true;
            _inputLocked = false;
            float elapsed = 0f;
            _currentRingSize = START_SIZE;

            while (elapsed < SHRINK_DURATION)
            {
                if (_inputLocked) break;

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SHRINK_DURATION);
                _currentRingSize = Mathf.Lerp(START_SIZE, 0f, t);
                ringRt.sizeDelta = new Vector2(_currentRingSize, _currentRingSize);
                yield return null;
            }

            _waitingForInput = false;

            // Determine result based on ring size at click moment
            float timingBonus;
            string resultLabel;
            Color resultColor;

            if (!_inputLocked)
            {
                // Player didn't click — Miss
                timingBonus = 0f;
                resultLabel = "Miss!";
                resultColor = new Color(0.5f, 0.5f, 0.5f);
            }
            else if (_currentRingSize <= PERFECT_THRESHOLD)
            {
                timingBonus = 1.0f;
                resultLabel = "Perfect!";
                resultColor = new Color(1f, 0.95f, 0.3f);
            }
            else if (_currentRingSize <= GOOD_THRESHOLD)
            {
                timingBonus = 0.75f;
                resultLabel = "Good!";
                resultColor = new Color(0.3f, 1f, 0.5f);
            }
            else if (_currentRingSize <= OK_THRESHOLD)
            {
                timingBonus = 0.5f;
                resultLabel = "OK";
                resultColor = new Color(0.7f, 0.85f, 1f);
            }
            else
            {
                timingBonus = 0.25f;
                resultLabel = "Early";
                resultColor = new Color(0.8f, 0.5f, 0.3f);
            }

            // Show result
            _resultText.text = resultLabel;
            _resultText.color = resultColor;
            _instructionText.text = "";

            // Freeze ring at current position
            ringRt.sizeDelta = new Vector2(
                Mathf.Max(_currentRingSize, 0f),
                Mathf.Max(_currentRingSize, 0f));

            // Hold result display
            yield return new WaitForSecondsRealtime(RESULT_HOLD);

            // Fade out
            fadeElapsed = 0f;
            while (fadeElapsed < FADE_DURATION)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(fadeElapsed / FADE_DURATION);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
            _overlayRoot.SetActive(false);

            _activeCoroutine = null;
            callback?.Invoke(timingBonus);
        }

        /// <summary>
        /// Creates a procedural circle sprite (filled or ring outline).
        /// </summary>
        private static Sprite CreateCircleSprite(int size, bool filled)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = center - 1f;
            float ringWidth = filled ? radius : Mathf.Max(size / 16f, 2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (filled)
                    {
                        float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        float innerEdge = radius - ringWidth;
                        float outerAlpha = Mathf.Clamp01(radius - dist + 0.5f);
                        float innerAlpha = Mathf.Clamp01(dist - innerEdge + 0.5f);
                        float alpha = outerAlpha * innerAlpha;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
