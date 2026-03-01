using UnityEngine;

namespace SoR.UI
{
    public class SummonRevealPopup : UIPopup
    {
        public override void Show(object data)
        {
            base.Show(data);
            Debug.Log("[SummonRevealPopup] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[SummonRevealPopup] Closed.");
            base.Hide();
        }
    }
}
