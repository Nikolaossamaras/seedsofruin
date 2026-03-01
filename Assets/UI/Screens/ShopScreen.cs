using UnityEngine;

namespace SoR.UI
{
    public class ShopScreen : UIScreen
    {
        public override void Show()
        {
            base.Show();
            Debug.Log("[ShopScreen] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[ShopScreen] Closed.");
            base.Hide();
        }
    }
}
