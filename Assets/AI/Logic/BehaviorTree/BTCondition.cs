using System;

namespace SoR.AI
{
    public class BTCondition : BTNode
    {
        public Func<bool> Condition;

        public BTCondition() { }

        public BTCondition(string name, Func<bool> condition)
        {
            Name = name;
            Condition = condition;
        }

        public override BTStatus Evaluate()
        {
            if (Condition == null)
                return BTStatus.Failure;

            return Condition.Invoke() ? BTStatus.Success : BTStatus.Failure;
        }
    }
}
