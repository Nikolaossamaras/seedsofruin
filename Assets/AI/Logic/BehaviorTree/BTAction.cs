using System;

namespace SoR.AI
{
    public class BTAction : BTNode
    {
        public Func<BTStatus> Action;

        public BTAction() { }

        public BTAction(string name, Func<BTStatus> action)
        {
            Name = name;
            Action = action;
        }

        public override BTStatus Evaluate()
        {
            if (Action == null)
                return BTStatus.Failure;

            return Action.Invoke();
        }
    }
}
