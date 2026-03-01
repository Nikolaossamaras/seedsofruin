using UnityEngine;

namespace SoR.UI
{
    public class TitleScreen : UIScreen
    {
        public override void Show()
        {
            base.Show();
            Debug.Log("[TitleScreen] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[TitleScreen] Closed.");
            base.Hide();
        }
    }
}
