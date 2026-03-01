using System.Collections.Generic;
using SoR.Core;

namespace SoR.Systems.Gacha
{
    public readonly struct GachaPullCompletedEvent : IGameEvent
    {
        public readonly GachaPullResult Result;

        public GachaPullCompletedEvent(GachaPullResult result)
        {
            Result = result;
        }
    }

    public readonly struct GachaMultiPullCompletedEvent : IGameEvent
    {
        public readonly List<GachaPullResult> Results;

        public GachaMultiPullCompletedEvent(List<GachaPullResult> results)
        {
            Results = results;
        }
    }

    public readonly struct AccordEssenceGainedEvent : IGameEvent
    {
        public readonly int Amount;

        public AccordEssenceGainedEvent(int amount)
        {
            Amount = amount;
        }
    }
}
