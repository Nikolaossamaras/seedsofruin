using UnityEngine;
using UnityEngine.UI;

namespace SoR.UI
{
    public class MinimapUI : MonoBehaviour
    {
        [SerializeField] private RawImage _minimapImage;

        public RawImage MinimapImage => _minimapImage;

        /// <summary>
        /// Updates the minimap based on the player's world position.
        /// Placeholder implementation.
        /// </summary>
        /// <param name="worldPos">The player's current world position.</param>
        public void UpdatePlayerPosition(Vector3 worldPos)
        {
            // Placeholder: In a full implementation, this would update the minimap
            // camera position or UV offset to center on the player.
            Debug.Log($"[Minimap] Player position updated: {worldPos}");
        }
    }
}
