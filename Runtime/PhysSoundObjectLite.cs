using UnityEngine;

namespace PhysSound
{
    [AddComponentMenu("PhysSound/PhysSound Object Lite")]
    public partial class PhysSoundObjectLite : PhysSoundObjectBase
    {
        private void FixedUpdate()
        {
            if (SoundMaterial == null)
                return;

            if (_impactAudio && !_impactAudio.isPlaying)
                _impactAudio.Stop();

            _kinematicVelocity = (transform.position - _prevPosition) / Time.deltaTime;
            _prevPosition = transform.position;

            _kinematicAngularVelocity = Quaternion.Angle(_prevRotation, transform.rotation) / Time.deltaTime / 45f;
            _prevRotation = transform.rotation;
        }

        /// <summary>
        /// Initializes the PhysSoundObject. Use this if you adding a PhysSoundObject component to an object at runtime.
        /// </summary>
        override protected void Initialize()
        {
#if PHYS_SOUND_3D
            _rigidbody = GetComponent<Rigidbody>();
#endif
#if PHYS_SOUND_2D
            _rigidbody2D = GetComponent<Rigidbody2D>();
#endif

            if (_autoCreateSources)
            {
                _baseImpactVol = _impactAudio.volume;
                _baseImpactPitch = _impactAudio.pitch;

                _impactAudio.loop = false;
            }
            else if (_impactAudio)
            {
                _impactAudio.loop = false;
                _baseImpactVol = _impactAudio.volume;
                _baseImpactPitch = _impactAudio.pitch;
            }

            if (PlayClipAtPoint)
                PhysSoundTempAudioPool.Create();
            else if (_impactAudio != null && !_impactAudio.isActiveAndEnabled)
                _impactAudio = PhysSoundTempAudioPool.GetAudioSourceCopy(_impactAudio, gameObject);
        }

        /// <summary>
        /// Enables or Disables this script along with its associated AudioSources.
        /// </summary>
        public override void SetEnabled(bool enable)
        {
            if (enable && this.enabled == false)
            {
                _impactAudio.enabled = true;
                this.enabled = true;
            }
            else if (!enable && this.enabled == true)
            {
                if (_impactAudio)
                {
                    _impactAudio.Stop();
                    _impactAudio.enabled = false;
                }

                this.enabled = false;
            }
        }
    }
}