using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SoR.UI
{
    public class CompanionHUDPanel : MonoBehaviour
    {
        [SerializeField] private Image _portrait;
        [SerializeField] private Image _healthFill;
        [SerializeField] private TextMeshProUGUI _nameText;

        public Image Portrait => _portrait;
        public Image HealthFill => _healthFill;
        public TextMeshProUGUI NameText => _nameText;

        /// <summary>
        /// Sets the companion HUD display info.
        /// </summary>
        /// <param name="name">The companion's name.</param>
        /// <param name="portrait">The companion's portrait sprite.</param>
        /// <param name="healthPercent">Health as a 0-1 normalized value.</param>
        public void SetCompanionInfo(string name, Sprite portrait, float healthPercent)
        {
            if (_nameText != null)
                _nameText.text = name;

            if (_portrait != null && portrait != null)
                _portrait.sprite = portrait;

            if (_healthFill != null)
                _healthFill.fillAmount = Mathf.Clamp01(healthPercent);
        }
    }
}
