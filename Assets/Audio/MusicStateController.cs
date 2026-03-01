using UnityEngine;
using SoR.Core;
using SoR.Combat;

namespace SoR.Audio
{
    public class MusicStateController : MonoBehaviour
    {
        public enum MusicState
        {
            Exploration,
            Combat,
            Boss,
            Menu,
            Cutscene
        }

        [SerializeField] private AudioManager _audioManager;

        private MusicState _currentState = MusicState.Exploration;

        public MusicState CurrentState => _currentState;

        private void OnEnable()
        {
            EventBus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        /// <summary>
        /// Transitions to a new music state with a crossfade.
        /// </summary>
        /// <param name="newState">The target music state.</param>
        /// <param name="crossfadeDuration">Duration of the crossfade in seconds.</param>
        public void TransitionTo(MusicState newState, float crossfadeDuration)
        {
            if (newState == _currentState)
                return;

            MusicState previousState = _currentState;
            _currentState = newState;

            if (_audioManager != null)
            {
                _audioManager.StopMusic(crossfadeDuration);
                _audioManager.PlayMusic(GetMusicIdForState(newState));
            }

            Debug.Log($"[MusicState] Transitioned from {previousState} to {newState} (crossfade: {crossfadeDuration}s)");
        }

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            if (_currentState == MusicState.Exploration)
            {
                TransitionTo(MusicState.Combat, 1f);
            }
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            // Placeholder: Return to exploration after combat ends
            // In a full implementation, this would check if all enemies are defeated
        }

        private string GetMusicIdForState(MusicState state)
        {
            return state switch
            {
                MusicState.Exploration => "music_exploration",
                MusicState.Combat => "music_combat",
                MusicState.Boss => "music_boss",
                MusicState.Menu => "music_menu",
                MusicState.Cutscene => "music_cutscene",
                _ => "music_exploration"
            };
        }
    }
}
