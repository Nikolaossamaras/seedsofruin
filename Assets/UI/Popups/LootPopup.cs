using UnityEngine;

namespace SoR.UI
{
    public class LootPopup : UIPopup
    {
        public override void Show(object data)
        {
            base.Show(data);
            Debug.Log("[LootPopup] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[LootPopup] Closed.");
            base.Hide();
        }
    }
}
