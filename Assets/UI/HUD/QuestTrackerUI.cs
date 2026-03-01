using UnityEngine;
using TMPro;

namespace SoR.UI
{
    public class QuestTrackerUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _questNameText;
        [SerializeField] private TextMeshProUGUI _objectiveText;

        public TextMeshProUGUI QuestNameText => _questNameText;
        public TextMeshProUGUI ObjectiveText => _objectiveText;

        /// <summary>
        /// Sets the currently tracked quest display.
        /// </summary>
        /// <param name="questName">Name of the quest.</param>
        /// <param name="objectiveDesc">Description of the current objective.</param>
        /// <param name="current">Current progress count.</param>
        /// <param name="required">Required progress count to complete.</param>
        public void SetTrackedQuest(string questName, string objectiveDesc, int current, int required)
        {
            if (_questNameText != null)
                _questNameText.text = questName;

            if (_objectiveText != null)
                _objectiveText.text = $"{objectiveDesc} ({current}/{required})";
        }
    }
}
