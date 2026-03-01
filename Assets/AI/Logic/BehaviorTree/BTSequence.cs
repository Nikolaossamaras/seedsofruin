using System.Collections.Generic;

namespace SoR.AI
{
    public class BTSequence : BTNode
    {
        public List<BTNode> Children = new();

        public override BTStatus Evaluate()
        {
            foreach (var child in Children)
            {
                var status = child.Evaluate();

                switch (status)
                {
                    case BTStatus.Failure:
                        return BTStatus.Failure;
                    case BTStatus.Running:
                        return BTStatus.Running;
                    case BTStatus.Success:
                        continue;
                }
            }

            return BTStatus.Success;
        }

        public override void Reset()
        {
            foreach (var child in Children)
                child.Reset();
        }
    }
}
