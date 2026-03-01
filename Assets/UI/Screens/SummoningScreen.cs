using UnityEngine;

namespace SoR.UI
{
    public class SummoningScreen : UIScreen
    {
        public override void Show()
        {
            base.Show();
            Debug.Log("[SummoningScreen] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[SummoningScreen] Closed.");
            base.Hide();
        }
    }
}
