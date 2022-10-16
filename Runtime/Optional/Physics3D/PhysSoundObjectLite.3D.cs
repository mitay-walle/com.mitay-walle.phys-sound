using UnityEngine;

namespace PhysSound
{
    public partial class PhysSoundObjectLite
    {
 #if PHYS_SOUND_3D

        #region 3D Collision Messages

        private Vector3 _contactNormal;
        private Vector3 _contactPoint;
        private Vector3 _relativeVelocity;

        void OnCollisionEnter(Collision collision)
        {
            if (SoundMaterial == null || !enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            _contactNormal = collision.GetContact(0).normal;
            _contactPoint = collision.GetContact(0).point;
            _relativeVelocity = collision.relativeVelocity;

            if (_collisionEvents.PlayOnCollisionEnter)
                PlayImpactSound(collision.collider, _relativeVelocity, _contactNormal, _contactPoint);

            _setPrevVelocity = true;
        }

        #endregion

        #region 3D Trigger Messages

        void OnTriggerEnter(Collider collider)
        {
            if (!_collisionEvents.PlayOnTriggerEnter || SoundMaterial == null || !enabled ||
                SoundMaterial.AudioSets.Count == 0)
                return;

            PlayImpactSound(collider, TotalKinematicVelocity, Vector3.zero, collider.transform.position);
        }

        #endregion

#endif
    }
}