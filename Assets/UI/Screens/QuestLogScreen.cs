using UnityEngine;

namespace SoR.UI
{
    public class QuestLogScreen : UIScreen
    {
        public override void Show()
        {
            base.Show();
            Debug.Log("[QuestLogScreen] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[QuestLogScreen] Closed.");
            base.Hide();
        }
    }
}
