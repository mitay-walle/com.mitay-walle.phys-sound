using UnityEngine;
using System.Collections.Generic;

namespace PhysSound
{
    [AddComponentMenu("PhysSound/PhysSound Object Lite")]
    public class PhysSoundObjectLite : PhysSoundObjectBase
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
            _r = GetComponent<Rigidbody>();
            _r2D = GetComponent<Rigidbody2D>();

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

        #region 3D Collision Messages

        Vector3 contactNormal, contactPoint, relativeVelocity;

        void OnCollisionEnter(Collision c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            contactNormal = c.contacts[0].normal;
            contactPoint = c.contacts[0].point;
            relativeVelocity = c.relativeVelocity;

            playImpactSound(c.collider.gameObject, relativeVelocity, contactNormal, contactPoint);

            _setPrevVelocity = true;
        }

        #endregion

        #region 3D Trigger Messages

        void OnTriggerEnter(Collider c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || !HitsTriggers)
                return;

            playImpactSound(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position);
        }

        #endregion

        #region 2D Collision Messages

        void OnCollisionEnter2D(Collision2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            playImpactSound(c.collider.gameObject, c.relativeVelocity, c.contacts[0].normal, c.contacts[0].point);

            _setPrevVelocity = true;
        }

        #endregion

        #region 2D Trigger Messages

        void OnTriggerEnter2D(Collider2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            playImpactSound(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position);
        }

        #endregion
    }
}