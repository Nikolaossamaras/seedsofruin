using System;

namespace SoR.Core
{
    public class Timer
    {
        public float Duration { get; private set; }
        public float Remaining { get; private set; }
        public bool IsComplete => Remaining <= 0f;

        public Action OnComplete;

        public void Start(float duration)
        {
            Duration = duration;
            Remaining = duration;
        }

        public void Tick(float deltaTime)
        {
            if (IsComplete) return;

            Remaining -= deltaTime;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                OnComplete?.Invoke();
            }
        }

        public void Reset()
        {
            Remaining = Duration;
        }
    }
}
