﻿using UnityEngine;

namespace PhysSound
{
    [AddComponentMenu("PhysSound/PhysSound Object Lite")]
    public partial class PhysSoundObjectLite : PhysSoundObjectBase
    {
        void Update()
        {
            if (SoundMaterial == null)
                return;

            if (ImpactAudio && !ImpactAudio.isPlaying)
                ImpactAudio.Stop();

            _kinematicVelocity = (transform.position - _prevPosition) / Time.deltaTime;
            _prevPosition = transform.position;

            _kinematicAngularVelocity = Quaternion.Angle(_prevRotation, transform.rotation) / Time.deltaTime / 45f;
            _prevRotation = transform.rotation;
        }

        /// <summary>
        /// Initializes the PhysSoundObject. Use this if you adding a PhysSoundObject component to an object at runtime.
        /// </summary>
        public override void Initialize()
        {
#if PHYS_SOUND_3D
            _r = GetComponent<Rigidbody>();
#endif
#if PHYS_SOUND_3D
            _r2D = GetComponent<Rigidbody2D>();
#endif

            if (AutoCreateSources)
            {
                baseImpactVol = ImpactAudio.volume;
                baseImpactPitch = ImpactAudio.pitch;

                ImpactAudio.loop = false;
            }
            else if (ImpactAudio)
            {
                ImpactAudio.loop = false;
                baseImpactVol = ImpactAudio.volume;
                baseImpactPitch = ImpactAudio.pitch;
            }

            if (PlayClipAtPoint)
                PhysSoundTempAudioPool.Create();
            else if (ImpactAudio != null && !ImpactAudio.isActiveAndEnabled)
                ImpactAudio = PhysSoundTempAudioPool.GetAudioSourceCopy(ImpactAudio, gameObject);
        }

        /// <summary>
        /// Enables or Disables this script along with its associated AudioSources.
        /// </summary>
        public override void SetEnabled(bool enable)
        {
            if (enable && this.enabled == false)
            {
                ImpactAudio.enabled = true;
                this.enabled = true;
            }
            else if (!enable && this.enabled == true)
            {
                if (ImpactAudio)
                {
                    ImpactAudio.Stop();
                    ImpactAudio.enabled = false;
                }

                this.enabled = false;
            }
        }
    }
}