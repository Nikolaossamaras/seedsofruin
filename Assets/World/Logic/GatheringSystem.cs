using UnityEngine;
using SoR.Core;
using SoR.Systems.Inventory;

namespace SoR.World
{
    public class GatheringSystem : MonoBehaviour, IService
    {
        public void Initialize() { }
        public void Dispose() { }

        /// <summary>
        /// Attempts to gather from a harvest point. Higher harvest stat yields better drops.
        /// </summary>
        /// <param name="harvestPoint">The harvest point data.</param>
        /// <param name="harvestStat">The player's harvest stat value.</param>
        /// <returns>The item ID of the gathered item, or null if gathering failed.</returns>
        public string Gather(HarvestPointSO harvestPoint, float harvestStat)
        {
            if (harvestPoint == null || harvestPoint.PossibleItemIds == null || harvestPoint.PossibleItemIds.Length == 0)
            {
                Debug.LogWarning("[Gathering] Invalid harvest point or no possible items.");
                return null;
            }

            if (harvestStat < harvestPoint.RequiredHarvestLevel)
            {
                Debug.Log("[Gathering] Harvest stat too low for this point.");
                return null;
            }

            // Higher harvest stat gives access to rarer items (later indices)
            float statRatio = Mathf.Clamp01(harvestStat / (harvestPoint.RequiredHarvestLevel * 2f + 1f));
            int maxIndex = Mathf.Clamp(
                Mathf.FloorToInt(statRatio * harvestPoint.PossibleItemIds.Length),
                0,
                harvestPoint.PossibleItemIds.Length - 1
            );

            int selectedIndex = Random.Range(0, maxIndex + 1);
            string itemId = harvestPoint.PossibleItemIds[selectedIndex];

            EventBus.Raise(new ItemCollectedEvent(itemId, 1));
            Debug.Log($"[Gathering] Gathered item: {itemId} from {harvestPoint.PointName}");

            return itemId;
        }
    }
}
