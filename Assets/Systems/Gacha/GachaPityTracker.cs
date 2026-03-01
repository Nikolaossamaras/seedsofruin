using System;

namespace SoR.Systems.Gacha
{
    [Serializable]
    public class GachaPityTracker
    {
        public int TotalPulls;
        public int PullsSinceLegendary;
        public int PullsSinceMythic;
        public bool LostLastFiftyFifty;

        public GachaPityTracker()
        {
            TotalPulls = 0;
            PullsSinceLegendary = 0;
            PullsSinceMythic = 0;
            LostLastFiftyFifty = false;
        }

        public void IncrementPull()
        {
            TotalPulls++;
            PullsSinceLegendary++;
            PullsSinceMythic++;
        }

        public void ResetLegendaryPity()
        {
            PullsSinceLegendary = 0;
        }

        public void ResetMythicPity()
        {
            PullsSinceMythic = 0;
        }
    }
}
