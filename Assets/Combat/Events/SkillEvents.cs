using SoR.Core;
using UnityEngine;

namespace SoR.Combat
{
    public readonly struct SkillUsedEvent : IGameEvent
    {
        public readonly GameObject Caster;
        public readonly SkillDefinitionSO Skill;
        public readonly Vector3 Position;

        public SkillUsedEvent(GameObject caster, SkillDefinitionSO skill, Vector3 position)
        {
            Caster = caster;
            Skill = skill;
            Position = position;
        }
    }

    public readonly struct SkillCooldownStartedEvent : IGameEvent
    {
        public readonly SkillDefinitionSO Skill;
        public readonly float Duration;

        public SkillCooldownStartedEvent(SkillDefinitionSO skill, float duration)
        {
            Skill = skill;
            Duration = duration;
        }
    }

    public readonly struct SkillCooldownCompleteEvent : IGameEvent
    {
        public readonly SkillDefinitionSO Skill;

        public SkillCooldownCompleteEvent(SkillDefinitionSO skill)
        {
            Skill = skill;
        }
    }
}
