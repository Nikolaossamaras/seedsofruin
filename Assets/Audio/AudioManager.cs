using UnityEngine;
using SoR.Core;

namespace SoR.Audio
{
    public class AudioManager : MonoBehaviour, IService
    {
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;

        [Range(0f, 1f)]
        [SerializeField] private float _musicVolume = 0.8f;

        [Range(0f, 1f)]
        [SerializeField] private float _sfxVolume = 1f;

        public float MusicVolume
        {
            get => _musicVolume;
            private set => _musicVolume = Mathf.Clamp01(value);
        }

        public float SFXVolume
        {
            get => _sfxVolume;
            private set => _sfxVolume = Mathf.Clamp01(value);
        }

        public void Initialize()
        {
            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.loop = true;
                _musicSource.playOnAwake = false;
            }

            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.loop = false;
                _sfxSource.playOnAwake = false;
            }

            _musicSource.volume = _musicVolume;
            _sfxSource.volume = _sfxVolume;

            Debug.Log("[AudioManager] Initialized.");
        }

        public void Dispose() { }

        /// <summary>
        /// Plays a sound effect at the specified world position.
        /// </summary>
        public void PlaySFX(string sfxId, Vector3 position)
        {
            // Placeholder: In a full implementation, this would look up the clip from SFXLibrarySO
            Debug.Log($"[Audio] Playing SFX '{sfxId}' at {position}");
            if (_sfxSource != null)
            {
                AudioSource.PlayClipAtPoint(_sfxSource.clip, position, _sfxVolume);
            }
        }

        /// <summary>
        /// Plays background music by ID.
        /// </summary>
        public void PlayMusic(string musicId)
        {
            Debug.Log($"[Audio] Playing music: {musicId}");
            if (_musicSource != null)
            {
                _musicSource.volume = _musicVolume;
                _musicSource.Play();
            }
        }

        /// <summary>
        /// Stops the currently playing music with a fade out.
        /// </summary>
        public void StopMusic(float fadeTime)
        {
            Debug.Log($"[Audio] Stopping music with {fadeTime}s fade.");
            if (_musicSource != null)
            {
                StartCoroutine(FadeOutMusic(fadeTime));
            }
        }

        public void SetMusicVolume(float vol)
        {
            MusicVolume = vol;
            if (_musicSource != null)
                _musicSource.volume = _musicVolume;
        }

        public void SetSFXVolume(float vol)
        {
            SFXVolume = vol;
            if (_sfxSource != null)
                _sfxSource.volume = _sfxVolume;
        }

        private System.Collections.IEnumerator FadeOutMusic(float duration)
        {
            float startVolume = _musicSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            _musicSource.Stop();
            _musicSource.volume = _musicVolume;
        }
    }
}
