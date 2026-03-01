using UnityEngine;

namespace SoR.Shared
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract { get; }
        void Interact(GameObject interactor);
    }
}
