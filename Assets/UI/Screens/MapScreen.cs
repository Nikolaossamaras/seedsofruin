using UnityEngine;

namespace SoR.UI
{
    public class MapScreen : UIScreen
    {
        public override void Show()
        {
            base.Show();
            Debug.Log("[MapScreen] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[MapScreen] Closed.");
            base.Hide();
        }
    }
}
