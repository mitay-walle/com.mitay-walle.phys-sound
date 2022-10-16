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

            if (_collisionEvents.PlayOnCollisionEnter)
                PlayImpactSound(collision.collider, _relativeVelocity, _contactNormal, _contactPoint);

            _setPrevVelocity = true;
        }

        public void OnCollisionStayInternal(Collision collision)
        {
            if (!_collisionEvents.SlideOnCollisionStay && !_collisionEvents.PlayOnCollisionStay || _alertNotInSoundZone)
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

            Vector3 velocity = default;

            if (_rigidbody) velocity = _rigidbody.velocity;
            if (_articulationBody) velocity = _articulationBody.velocity;
            
            if (_setPrevVelocity)
            {
                _prevVelocity = velocity;
                _setPrevVelocity = false;
            }

            Vector3 deltaVel = velocity - _prevVelocity;

            if (collision.contactCount > 0)
            {
                _contactNormal = collision.GetContact(0).normal;
                _contactPoint = collision.GetContact(0).point;
            }

            _relativeVelocity = collision.relativeVelocity;

            if (_collisionEvents.PlayOnCollisionStay)
            {
                PlayImpactSound(collision.collider, deltaVel, _contactNormal, _contactPoint);
            }

            SetSlideTargetVolumes(collision.collider.gameObject,
                _relativeVelocity,
                _contactNormal,
                _contactPoint,
                false);

            _prevVelocity = velocity;
        }

        public void OnCollisionExitInternal(Collision c)
        {
            if (_alertNotInSoundZone || SoundMaterial == null || !this.enabled ||
                SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            SetSlideTargetVolumes(c.collider.gameObject, Vector3.zero, Vector3.zero, transform.position, true);
            _setPrevVelocity = true;
        }

        #endregion

        #region 3D Trigger Messages

        void OnTriggerEnter(Collider collider)
        {
            if (!_collisionEvents.PlayOnTriggerEnter || SoundMaterial == null || !this.enabled ||
                SoundMaterial.AudioSets.Count == 0)
                return;

            PlayImpactSound(collider, TotalKinematicVelocity, Vector3.zero, collider.transform.position);
        }

        public void OnTriggerStayInternal(Collider collider)
        {
            if (SoundMaterial == null || !this.enabled ||
                SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            SetSlideTargetVolumes(collider.gameObject,
                TotalKinematicVelocity,
                Vector3.zero,
                collider.transform.position,
                false);
        }

        public void OnTriggerExitInternal(Collider collider)
        {
            if (SoundMaterial == null || !this.enabled ||
                SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            SetSlideTargetVolumes(collider.gameObject,
                TotalKinematicVelocity,
                Vector3.zero,
                collider.transform.position,
                true);
        }

        #endregion

#endif
    }
}