namespace SoR.Core
{
    public interface IState
    {
        void Enter();
        void Execute();
        void Exit();
    }
}
