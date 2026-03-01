using UnityEngine;

namespace SoR.UI
{
    public class DialoguePopup : UIPopup
    {
        public override void Show(object data)
        {
            base.Show(data);
            Debug.Log("[DialoguePopup] Opened.");
        }

        public override void Hide()
        {
            Debug.Log("[DialoguePopup] Closed.");
            base.Hide();
        }
    }
}
