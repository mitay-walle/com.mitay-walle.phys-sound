using UnityEngine;

namespace PhysSound
{
    [System.Serializable]
    public class PhysSoundAudioContainer
    {
        public PhysSoundKey Key;
        public AudioSource SlideAudio;

        private PhysSoundObject _physSoundObject;
        private float _targetVolume;
        private float _baseVol, _basePitch, _basePitchRand;

        private int _lastFrame;
        private bool _lastExit;

        private AudioSource _currAudioSource;

        private PhysSoundMaterial SoundMaterial => _physSoundObject.SoundMaterial;

        public PhysSoundAudioContainer(PhysSoundKey k)
        {
            Key = k;
        }

        /// <summary>
        /// Initializes this Audio Container for no audio pooling. Will do nothing if SlideAudio is not assigned.
        /// </summary>
        public void Initialize(PhysSoundObject obj)
        {
            if (SlideAudio == null)
                return;

            _physSoundObject = obj;

            _baseVol = SlideAudio.volume;
            _basePitch = SlideAudio.pitch;
            _basePitchRand = _basePitch;

            SlideAudio.clip = SoundMaterial.GetAudioSet(Key).Slide;
            SlideAudio.loop = true;
            SlideAudio.volume = 0;

            _currAudioSource = SlideAudio;
        }

        /// <summary>
        /// Initializes this Audio Container for audio pooling.
        /// </summary>
        public void Initialize(PhysSoundObject obj, AudioSource template)
        {
            _physSoundObject = obj;
            SlideAudio = template;

            _baseVol = template.volume;
            _basePitch = template.pitch;
            _basePitchRand = _basePitch;
        }

        /// <summary>
        /// Sets the target volume and pitch of the sliding sound effect based on the given object that was hit, velocity, and normal.
        /// </summary>
        public void SetTargetVolumeAndPitch(GameObject parentObject, GameObject otherObject, Vector3 relativeVelocity,
            Vector3 normal, bool exit, float mod = 1)
        {
            if (SlideAudio == null)
                return;

            float vol = exit || !SoundMaterial.CanCollideAudioWith(otherObject)
                ? 0
                : SoundMaterial.GetSlideVolume(relativeVelocity, normal) * _baseVol * mod;

            if (_lastFrame == Time.frameCount)
            {
                if (_lastExit != exit || _targetVolume < vol)
                    _targetVolume = vol;
            }
            else
                _targetVolume = vol;

            if (_physSoundObject.PlayClipAtPoint && _currAudioSource == null && _targetVolume > 0.001f)
            {
                _basePitchRand = _basePitch * SoundMaterial.GetScaleModPitch(parentObject.transform.localScale) +
                                 SoundMaterial.GetRandomPitch();

                _currAudioSource = PhysSoundTempAudioPool.Instance.GetSource(SlideAudio);

                if (_currAudioSource)
                {
                    _currAudioSource.clip = SoundMaterial.GetAudioSet(Key).Slide;
                    _currAudioSource.volume = _targetVolume;
                    _currAudioSource.loop = true;
                    _currAudioSource.Play();
                }
            }
            else if (_currAudioSource && !_currAudioSource.isPlaying)
            {
                _basePitchRand = _basePitch * SoundMaterial.GetScaleModPitch(parentObject.transform.localScale) +
                                 SoundMaterial.GetRandomPitch();

                _currAudioSource.loop = true;
                _currAudioSource.volume = _targetVolume;
                _currAudioSource.Play();
            }

            if (_currAudioSource)
                _currAudioSource.pitch = _basePitchRand + relativeVelocity.magnitude * SoundMaterial.SlidePitchMod;

            if (SoundMaterial.TimeScalePitch)
                _currAudioSource.pitch *= Time.timeScale;

            _lastExit = exit;
            _lastFrame = Time.frameCount;
        }

        /// <summary>
        /// Updates the associated AudioSource to match the target volume and pitch.
        /// </summary>
        public void UpdateVolume()
        {
            if (SlideAudio == null || _currAudioSource == null)
                return;

            _currAudioSource.transform.position = _physSoundObject.transform.position;
            _currAudioSource.volume = Mathf.MoveTowards(_currAudioSource.volume, _targetVolume, 0.1f);

            if (_currAudioSource.volume < 0.001f)
            {
                if (_physSoundObject.PlayClipAtPoint)
                {
                    PhysSoundTempAudioPool.Instance.ReleaseSource(_currAudioSource);
                    _currAudioSource = null;
                }
                else
                {
                    _currAudioSource.Stop();
                }
            }
        }

        /// <summary>
        /// Returns true if this Audio Container's key index is the same as the given key index.
        /// </summary>
        public bool CompareKeyIndex(PhysSoundKey k)
        {
            return k == Key;
        }

        /// <summary>
        /// Disables the associated AudioSource.
        /// </summary>
        public void Disable()
        {
            if (SlideAudio)
            {
                SlideAudio.Stop();
                SlideAudio.enabled = false;
            }
        }

        /// <summary>
        /// Enables the associated AudioSource.
        /// </summary>
        public void Enable()
        {
            if (SlideAudio)
            {
                SlideAudio.enabled = true;
            }
        }
    }
}