using System.Collections.Generic;

namespace SoR.AI
{
    public class BTSelector : BTNode
    {
        public List<BTNode> Children = new();

        public override BTStatus Evaluate()
        {
            foreach (var child in Children)
            {
                var status = child.Evaluate();

                switch (status)
                {
                    case BTStatus.Success:
                        return BTStatus.Success;
                    case BTStatus.Running:
                        return BTStatus.Running;
                    case BTStatus.Failure:
                        continue;
                }
            }

            return BTStatus.Failure;
        }

        public override void Reset()
        {
            foreach (var child in Children)
                child.Reset();
        }
    }
}
