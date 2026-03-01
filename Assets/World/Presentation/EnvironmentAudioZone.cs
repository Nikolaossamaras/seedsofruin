using UnityEngine;

namespace SoR.World
{
    [RequireComponent(typeof(Collider))]
    public class EnvironmentAudioZone : MonoBehaviour
    {
        [SerializeField] private string _ambientSoundId;
        [SerializeField] private float _volume = 1f;

        public string AmbientSoundId => _ambientSoundId;
        public float Volume => _volume;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();

            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                StartAmbient();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                StopAmbient();
            }
        }

        private void StartAmbient()
        {
            if (_audioSource != null && !_audioSource.isPlaying)
            {
                _audioSource.volume = _volume;
                _audioSource.Play();
            }

            Debug.Log($"[AudioZone] Started ambient sound: {_ambientSoundId}");
        }

        private void StopAmbient()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }

            Debug.Log($"[AudioZone] Stopped ambient sound: {_ambientSoundId}");
        }
    }
}
