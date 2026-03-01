using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoR.Gameplay
{
    public class CompanionPortraitUI : MonoBehaviour
    {
        [SerializeField] private Image _portraitImage;
        [SerializeField] private TextMeshProUGUI _nameText;

        public void SetCompanion(CompanionDefinitionSO companion)
        {
            if (companion == null)
            {
                Clear();
                return;
            }

            if (_portraitImage != null)
            {
                _portraitImage.sprite = companion.Portrait;
                _portraitImage.enabled = companion.Portrait != null;
            }

            if (_nameText != null)
            {
                _nameText.text = companion.CompanionName;
            }
        }

        public void Clear()
        {
            if (_portraitImage != null)
            {
                _portraitImage.sprite = null;
                _portraitImage.enabled = false;
            }

            if (_nameText != null)
            {
                _nameText.text = string.Empty;
            }
        }
    }
}
