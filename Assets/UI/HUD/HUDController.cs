using UnityEngine;

namespace SoR.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Player HUD")]
        [SerializeField] private HealthBarUI _playerHealthBar;
        [SerializeField] private HealthBarUI _verdanceBar;

        [Header("Companion HUD")]
        [SerializeField] private CompanionHUDPanel _companionPanel;

        [Header("Minimap")]
        [SerializeField] private MinimapUI _minimap;

        [Header("Quest Tracker")]
        [SerializeField] private QuestTrackerUI _questTracker;

        public void UpdateHealth(float current, float max)
        {
            if (_playerHealthBar != null)
            {
                float normalized = max > 0f ? current / max : 0f;
                _playerHealthBar.SetFill(normalized);
            }
        }

        public void UpdateVerdance(float current, float max)
        {
            if (_verdanceBar != null)
            {
                float normalized = max > 0f ? current / max : 0f;
                _verdanceBar.SetFill(normalized);
            }
        }

        public void UpdateCompanionHUD(string name, Sprite portrait, float healthPercent)
        {
            if (_companionPanel != null)
            {
                _companionPanel.SetCompanionInfo(name, portrait, healthPercent);
            }
        }

        public void ToggleMinimap()
        {
            if (_minimap != null)
            {
                _minimap.gameObject.SetActive(!_minimap.gameObject.activeSelf);
            }
        }
    }
}
