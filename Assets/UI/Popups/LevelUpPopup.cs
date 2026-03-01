using UnityEngine;

namespace SoR.UI
{
    public class LevelUpPopup : UIPopup
    {
        public override void Show(object data)
        {
            base.Show(data);
            Debug.Log("[LevelUpPopup] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[LevelUpPopup] Closed.");
            base.Hide();
        }
    }
}
