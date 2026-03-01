using SoR.Core;

namespace SoR.Progression.Events
{
    public readonly struct LevelUpEvent : IGameEvent
    {
        public readonly int NewLevel;
        public readonly int StatPointsGained;

        public LevelUpEvent(int newLevel, int statPointsGained)
        {
            NewLevel = newLevel;
            StatPointsGained = statPointsGained;
        }
    }

    public readonly struct TitleUnlockedEvent : IGameEvent
    {
        public readonly string TitleId;

        public TitleUnlockedEvent(string titleId)
        {
            TitleId = titleId;
        }
    }

    public readonly struct ClassEvolutionUnlockedEvent : IGameEvent
    {
        public readonly string EvolutionId;

        public ClassEvolutionUnlockedEvent(string evolutionId)
        {
            EvolutionId = evolutionId;
        }
    }

    public readonly struct AchievementUnlockedEvent : IGameEvent
    {
        public readonly string AchievementId;

        public AchievementUnlockedEvent(string achievementId)
        {
            AchievementId = achievementId;
        }
    }
}
