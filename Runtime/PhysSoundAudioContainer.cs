using UnityEngine;

namespace PhysSound
{
    [System.Serializable]
    public class PhysSoundAudioContainer
    {
        public int KeyIndex;
        public AudioSource SlideAudio;

        private PhysSoundObject physSoundObject;
        private float _targetVolume;
        private float _baseVol, _basePitch, _basePitchRand;

        private int _lastFrame;
        private bool _lastExit;

        private AudioSource currAudioSource;

        private PhysSoundMaterial soundMaterial
        {
            get { return physSoundObject.SoundMaterial; }
        }

        public PhysSoundAudioContainer(int k)
        {
            KeyIndex = k;
        }

        /// <summary>
        /// Initializes this Audio Container for no audio pooling. Will do nothing if SlideAudio is not assigned.
        /// </summary>
        public void Initialize(PhysSoundObject obj)
        {
            if (SlideAudio == null)
                return;

            physSoundObject = obj;

            _baseVol = SlideAudio.volume;
            _basePitch = SlideAudio.pitch;
            _basePitchRand = _basePitch;

            SlideAudio.clip = soundMaterial.GetAudioSet(KeyIndex).Slide;
            SlideAudio.loop = true;
            SlideAudio.volume = 0;

            currAudioSource = SlideAudio;
        }

        /// <summary>
        /// Initializes this Audio Container for audio pooling.
        /// </summary>
        public void Initialize(PhysSoundObject obj, AudioSource template)
        {
            physSoundObject = obj;
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

            float vol = exit || !soundMaterial.CollideWith(otherObject)
                ? 0
                : soundMaterial.GetSlideVolume(relativeVelocity, normal) * _baseVol * mod;

            if (_lastFrame == Time.frameCount)
            {
                if (_lastExit != exit || _targetVolume < vol)
                    _targetVolume = vol;
            }
            else
                _targetVolume = vol;

            if (physSoundObject.PlayClipAtPoint && currAudioSource == null && _targetVolume > 0.001f)
            {
                _basePitchRand = _basePitch * soundMaterial.GetScaleModPitch(parentObject.transform.localScale) +
                                 soundMaterial.GetRandomPitch();

                currAudioSource = PhysSoundTempAudioPool.Instance.GetSource(SlideAudio);

                if (currAudioSource)
                {
                    currAudioSource.clip = soundMaterial.GetAudioSet(KeyIndex).Slide;
                    currAudioSource.volume = _targetVolume;
                    currAudioSource.loop = true;
                    currAudioSource.Play();
                }
            }
            else if (currAudioSource && !currAudioSource.isPlaying)
            {
                _basePitchRand = _basePitch * soundMaterial.GetScaleModPitch(parentObject.transform.localScale) +
                                 soundMaterial.GetRandomPitch();

                currAudioSource.loop = true;
                currAudioSource.volume = _targetVolume;
                currAudioSource.Play();
            }

            if (currAudioSource)
                currAudioSource.pitch = _basePitchRand + relativeVelocity.magnitude * soundMaterial.SlidePitchMod;

            if (soundMaterial.TimeScalePitch)
                currAudioSource.pitch *= Time.timeScale;

            _lastExit = exit;
            _lastFrame = Time.frameCount;
        }

        /// <summary>
        /// Updates the associated AudioSource to match the target volume and pitch.
        /// </summary>
        public void UpdateVolume()
        {
            if (SlideAudio == null || currAudioSource == null)
                return;

            currAudioSource.transform.position = physSoundObject.transform.position;
            currAudioSource.volume = Mathf.MoveTowards(currAudioSource.volume, _targetVolume, 0.1f);

            if (currAudioSource.volume < 0.001f)
            {
                if (physSoundObject.PlayClipAtPoint)
                {
                    PhysSoundTempAudioPool.Instance.ReleaseSource(currAudioSource);
                    currAudioSource = null;
                }
                else
                {
                    currAudioSource.Stop();
                }
            }
        }

        /// <summary>
        /// Returns true if this Audio Container's key index is the same as the given key index.
        /// </summary>
        public bool CompareKeyIndex(int k)
        {
            return k == KeyIndex;
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