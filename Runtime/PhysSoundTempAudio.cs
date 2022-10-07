using UnityEngine;

namespace PhysSound
{
    public class PhysSoundTempAudio : MonoBehaviour
    {
        private AudioSource audioSource;
        public AudioSource Audio
        {
            get { return audioSource; }
        }

        public void Initialize(PhysSoundTempAudioPool pool)
        {
            audioSource = gameObject.AddComponent<AudioSource>();

            transform.SetParent(pool.transform);
            gameObject.SetActive(false);
        }

        public void PlayClip(AudioClip clip, Vector3 point, AudioSource template, float volume, float pitch)
        {
            PhysSoundTempAudioPool.CopyAudioSource(template, audioSource);

            transform.position = point;

            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.pitch = pitch;

            gameObject.SetActive(true);

            audioSource.Play();
        }

        void Update()
        {
            if (!audioSource.isPlaying)
            {
                transform.position = Vector3.zero;
                gameObject.SetActive(false);
            }
        }
    }
}