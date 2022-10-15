using UnityEngine;

namespace PhysSound
{
    public class PhysSoundTempAudio : MonoBehaviour
    {
        private AudioSource _audioSource;
        public AudioSource Audio
        {
            get { return _audioSource; }
        }

        public void Initialize(PhysSoundTempAudioPool pool)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();

            transform.SetParent(pool.transform);
            gameObject.SetActive(false);
        }

        public void PlayClip(AudioClip clip, Vector3 point, AudioSource template, float volume, float pitch)
        {
            PhysSoundTempAudioPool.CopyAudioSource(template, _audioSource);

            transform.position = point;

            _audioSource.clip = clip;
            _audioSource.volume = volume;
            _audioSource.pitch = pitch;

            gameObject.SetActive(true);

            _audioSource.Play();
        }

        void Update()
        {
            if (!_audioSource.isPlaying)
            {
                transform.position = Vector3.zero;
                gameObject.SetActive(false);
            }
        }
    }
}