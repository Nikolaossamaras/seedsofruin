using UnityEngine;

namespace SoR.UI
{
    public class CraftingScreen : UIScreen
    {
        public override void Show()
        {
            base.Show();
            Debug.Log("[CraftingScreen] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[CraftingScreen] Closed.");
            base.Hide();
        }
    }
}
