namespace SoR.AI
{
    public enum BTStatus { Running, Success, Failure }

    public abstract class BTNode
    {
        public string Name { get; set; }
        protected EnemyAIController Controller;

        public void Initialize(EnemyAIController controller) => Controller = controller;
        public abstract BTStatus Evaluate();
        public virtual void Reset() { }
    }
}
