using UnityEngine;

namespace SoR.Core
{
    public abstract class GameEventListener<T> : MonoBehaviour where T : IGameEvent
    {
        protected virtual void OnEnable()
        {
            EventBus.Subscribe<T>(OnEventRaised);
        }

        protected virtual void OnDisable()
        {
            EventBus.Unsubscribe<T>(OnEventRaised);
        }

        public abstract void OnEventRaised(T evt);
    }
}
