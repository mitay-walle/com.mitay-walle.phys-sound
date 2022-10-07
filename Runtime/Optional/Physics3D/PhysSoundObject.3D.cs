using UnityEngine;

namespace PhysSound
{
    // partial class-files should be in same assemblies
    public partial class PhysSoundObject
    {
#if PHYS_SOUND_3D
        protected Rigidbody _r;

        #region 3D Collision Messages

        // optimization: c.contacts[0] -> GetContact(0). You should avoid using this as it produces memory garbage. Use GetContact or GetContacts instead (from official documentation)

        Vector3 contactNormal, contactPoint, relativeVelocity;

        void OnCollisionEnter(Collision c)
        {
            if (alertNotInSoundZone || SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            contactNormal = c.GetContact(0).normal;
            contactPoint = c.GetContact(0).point;
            relativeVelocity = c.relativeVelocity;

            playImpactSound(c.collider.gameObject, relativeVelocity, contactNormal, contactPoint);

            _setPrevVelocity = true;
        }

        void OnCollisionStay(Collision c)
        {
            if (alertNotInSoundZone)
                return;

            count++;
            if (count >= maxStep)
            {
                count = 0;
                return;
            }

            if (breakOnCollisionStay || SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersDic == null)
                return;

            if (_setPrevVelocity)
            {
                _prevVelocity = _r.velocity;
                _setPrevVelocity = false;
            }

            Vector3 deltaVel = _r.velocity - _prevVelocity;

            if (c.contactCount > 0)
            {
                contactNormal = c.GetContact(0).normal;
                contactPoint = c.GetContact(0).point;
            }

            relativeVelocity = c.relativeVelocity;

            playImpactSound(c.collider.gameObject, deltaVel, contactNormal, contactPoint);
            setSlideTargetVolumes(c.collider.gameObject, relativeVelocity, contactNormal, contactPoint, false);

            _prevVelocity = _r.velocity;
        }

        void OnCollisionExit(Collision c)
        {
            if (alertNotInSoundZone || SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersDic == null)
                return;

            setSlideTargetVolumes(c.collider.gameObject, Vector3.zero, Vector3.zero, transform.position, true);
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

        void OnTriggerStay(Collider c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersDic == null || !HitsTriggers)
                return;

            setSlideTargetVolumes(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position, false);
        }

        void OnTriggerExit(Collider c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersDic == null || !HitsTriggers)
                return;

            setSlideTargetVolumes(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position, true);
        }

        #endregion

#endif
    }
}