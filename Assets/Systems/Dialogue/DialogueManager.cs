using SoR.Core;
using UnityEngine;

namespace SoR.Systems.Dialogue
{
    /// <summary>
    /// Manages dialogue flow. This is a placeholder implementation;
    /// the actual system would wrap Yarn Spinner or a similar dialogue framework.
    /// </summary>
    public class DialogueManager : MonoBehaviour, IService
    {
        public bool IsDialogueActive { get; private set; }

        private string _currentDialogueId;

        public void Initialize()
        {
            IsDialogueActive = false;
            Debug.Log("[DialogueManager] Initialized.");
        }

        public void Dispose()
        {
            if (IsDialogueActive)
                EndDialogue();
        }

        private void Awake()
        {
            ServiceLocator.Register<DialogueManager>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Release<DialogueManager>();
        }

        /// <summary>
        /// Starts a dialogue sequence with the given dialogue ID.
        /// In a full implementation, this would load the dialogue tree from Yarn Spinner.
        /// </summary>
        public void StartDialogue(string dialogueId)
        {
            if (IsDialogueActive)
            {
                Debug.LogWarning("[DialogueManager] Dialogue is already active. End current dialogue first.");
                return;
            }

            if (string.IsNullOrEmpty(dialogueId))
            {
                Debug.LogWarning("[DialogueManager] Cannot start dialogue with null/empty ID.");
                return;
            }

            _currentDialogueId = dialogueId;
            IsDialogueActive = true;

            EventBus.Raise(new DialogueStartedEvent(dialogueId));
            Debug.Log($"[DialogueManager] Started dialogue: {dialogueId}");

            // Placeholder: In a real implementation, the dialogue runner would
            // emit DialogueLineEvent and DialogueChoiceEvent as lines are processed.
        }

        /// <summary>
        /// Advances the dialogue to the next line or choice.
        /// Placeholder: in a full implementation this would step the Yarn Spinner runner.
        /// </summary>
        public void AdvanceDialogue()
        {
            if (!IsDialogueActive)
            {
                Debug.LogWarning("[DialogueManager] No active dialogue to advance.");
                return;
            }

            // Placeholder: the real implementation would call runner.Continue()
            // and emit the appropriate DialogueLineEvent or DialogueChoiceEvent.
            Debug.Log("[DialogueManager] Advanced dialogue.");
        }

        /// <summary>
        /// Ends the current dialogue sequence.
        /// </summary>
        public void EndDialogue()
        {
            if (!IsDialogueActive)
                return;

            string dialogueId = _currentDialogueId;
            _currentDialogueId = null;
            IsDialogueActive = false;

            EventBus.Raise(new DialogueEndedEvent(dialogueId));
            Debug.Log($"[DialogueManager] Ended dialogue: {dialogueId}");
        }
    }
}
