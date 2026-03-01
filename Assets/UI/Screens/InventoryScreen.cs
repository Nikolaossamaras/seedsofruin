using UnityEngine;

namespace SoR.UI
{
    public class InventoryScreen : UIScreen
    {
        public override void Show()
        {
            base.Show();
            Debug.Log("[InventoryScreen] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[InventoryScreen] Closed.");
            base.Hide();
        }
    }
}
