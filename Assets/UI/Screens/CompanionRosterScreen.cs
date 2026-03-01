using UnityEngine;

namespace SoR.UI
{
    public class CompanionRosterScreen : UIScreen
    {
        public override void Show()
        {
            base.Show();
            Debug.Log("[CompanionRosterScreen] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[CompanionRosterScreen] Closed.");
            base.Hide();
        }
    }
}
