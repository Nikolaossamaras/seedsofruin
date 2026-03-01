using UnityEngine;
using SoR.Core;

namespace SoR.Input
{
    public readonly struct MoveInputEvent : IGameEvent
    {
        public readonly Vector2 Direction;

        public MoveInputEvent(Vector2 direction)
        {
            Direction = direction;
        }
    }

    public readonly struct AttackInputEvent : IGameEvent { }

    public readonly struct DodgeInputEvent : IGameEvent { }

    public readonly struct SkillInputEvent : IGameEvent
    {
        public readonly int SkillIndex;

        public SkillInputEvent(int skillIndex)
        {
            SkillIndex = skillIndex;
        }
    }

    public readonly struct InteractInputEvent : IGameEvent { }
}
