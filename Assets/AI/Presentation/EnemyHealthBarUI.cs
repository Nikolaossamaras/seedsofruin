using UnityEngine;
using UnityEngine.UI;
using SoR.Core;

namespace SoR.AI
{
    public class EnemyHealthBarUI : MonoBehaviour
    {
        [SerializeField] private Canvas _worldCanvas;
        [SerializeField] private Image _fillImage;
        [SerializeField] private float _hideDelay = 3f;

        private EnemyAIController _controller;
        private Camera _mainCamera;
        private float _hideTimer;
        private bool _isVisible;

        private void Awake()
        {
            _controller = GetComponentInParent<EnemyAIController>();
            _mainCamera = Camera.main;
            SetVisible(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyDamagedEvent>(HandleDamaged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDamagedEvent>(HandleDamaged);
        }

        private void HandleDamaged(EnemyDamagedEvent evt)
        {
            if (_controller == null) return;
            if (evt.Enemy != _controller.gameObject) return;

            SetVisible(true);
            _hideTimer = _hideDelay;
            UpdateFill();
        }

        private void Update()
        {
            if (_isVisible)
            {
                _hideTimer -= Time.deltaTime;

                if (_hideTimer <= 0f)
                {
                    SetVisible(false);
                }
                else
                {
                    BillboardToCamera();
                    UpdateFill();
                }
            }
        }

        private void UpdateFill()
        {
            if (_controller == null || _fillImage == null) return;

            float maxHealth = _controller.MaxHealth;
            if (maxHealth <= 0f) return;

            _fillImage.fillAmount = Mathf.Clamp01(_controller.CurrentHealth / maxHealth);
        }

        private void BillboardToCamera()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            if (_worldCanvas != null)
            {
                _worldCanvas.transform.forward = _mainCamera.transform.forward;
            }
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;

            if (_worldCanvas != null)
                _worldCanvas.enabled = visible;
        }
    }
}
