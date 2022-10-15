using UnityEngine;

namespace PhysSound
{
    // partial class-files should be in same assemblies
    public partial class PhysSoundObject
    {
#if PHYS_SOUND_3D
        #region 3D Collision Messages

        protected Vector3 _contactNormal;
        protected Vector3 _contactPoint;
        protected Vector3 _relativeVelocity;

        void OnCollisionEnter(Collision collision)
        {
            if (_alertNotInSoundZone || SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            _contactNormal = collision.GetContact(0).normal;
            _contactPoint = collision.GetContact(0).point;
            _relativeVelocity = collision.relativeVelocity;

            PlayImpactSound(collision.collider, _relativeVelocity, _contactNormal, _contactPoint);

            SetPrevVelocity = true;
        }

        void OnCollisionStay(Collision collision)
        {
            if (_alertNotInSoundZone)
                return;

            _count++;
            if (_count >= _maxStep)
            {
                _count = 0;
                return;
            }

            if (_breakOnCollisionStay || SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            if (SetPrevVelocity)
            {
                PrevVelocity = _r.velocity;
                SetPrevVelocity = false;
            }

            Vector3 deltaVel = _r.velocity - PrevVelocity;

            if (collision.contactCount > 0)
            {
                _contactNormal = collision.GetContact(0).normal;
                _contactPoint = collision.GetContact(0).point;
            }

            _relativeVelocity = collision.relativeVelocity;

            PlayImpactSound(collision.collider, deltaVel, _contactNormal, _contactPoint);
            SetSlideTargetVolumes(collision.collider.gameObject, _relativeVelocity, _contactNormal, _contactPoint, false);

            PrevVelocity = _r.velocity;
        }

        void OnCollisionExit(Collision c)
        {
            if (_alertNotInSoundZone || SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            SetSlideTargetVolumes(c.collider.gameObject, Vector3.zero, Vector3.zero, transform.position, true);
            SetPrevVelocity = true;
        }

        #endregion

        #region 3D Trigger Messages

        void OnTriggerEnter(Collider collider)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || !HitsTriggers)
                return;

            PlayImpactSound(collider, TotalKinematicVelocity, Vector3.zero, collider.transform.position);
        }

        void OnTriggerStay(Collider collider)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null || !HitsTriggers)
                return;

            SetSlideTargetVolumes(collider.gameObject, TotalKinematicVelocity, Vector3.zero, collider.transform.position, false);
        }

        void OnTriggerExit(Collider collider)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null || !HitsTriggers)
                return;

            SetSlideTargetVolumes(collider.gameObject, TotalKinematicVelocity, Vector3.zero, collider.transform.position, true);
        }

        #endregion

#endif
    }
}