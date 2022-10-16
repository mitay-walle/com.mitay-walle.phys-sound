using UnityEngine;

namespace PhysSound
{
    // partial class-files should be in same assemblies
    public partial class PhysSoundObject
    {
        #if PHYS_SOUND_2D

        #region 2D Collision Messages

        private void OnCollisionEnter2D(Collision2D c)
        {
            if (SoundMaterial == null || !enabled ||
                SoundMaterial.AudioSets.Count == 0)
                return;

            if (_collisionEvents.PlayOnCollisionEnter)
                PlayImpactSound(c.collider, c.relativeVelocity, c.GetContact(0).normal, c.GetContact(0).point);

            _setPrevVelocity = true;
        }

        public void OnCollisionStay2DInternal(Collision2D c)
        {
            if (SoundMaterial == null || !enabled ||
                SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            if (_setPrevVelocity)
            {
                _prevVelocity = _rigidbody2D.velocity;
                _setPrevVelocity = false;
            }

            Vector3 deltaVel = _rigidbody2D.velocity - (Vector2) _prevVelocity;

            if (_collisionEvents.PlayOnCollisionStay)
                PlayImpactSound(c.collider, deltaVel, c.GetContact(0).normal, c.GetContact(0).point);

            if (_collisionEvents.SlideOnCollisionStay)
            {
                SetSlideTargetVolumes(c.collider.gameObject,
                    c.relativeVelocity,
                    c.contacts[0].normal,
                    c.contacts[0].point,
                    false);
            }

            _prevVelocity = _rigidbody2D.velocity;
        }

        public void OnCollisionExit2DInternal(Collision2D c)
        {
            if (SoundMaterial == null || !enabled ||
                SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            SetSlideTargetVolumes(c.collider.gameObject, c.relativeVelocity, Vector3.up, transform.position, true);

            _setPrevVelocity = true;
        }

        #endregion

        #region 2D Trigger Messages

        private void OnTriggerEnter2D(Collider2D collider)
        {
            if (!_collisionEvents.PlayOnTriggerEnter || SoundMaterial == null || !enabled ||
                SoundMaterial.AudioSets.Count == 0)
                return;

            PlayImpactSound(collider, TotalKinematicVelocity, Vector3.zero, collider.transform.position);
        }

        public void OnTriggerStay2DInternal(Collider2D collider)
        {
            if (SoundMaterial == null || !enabled ||
                SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            SetSlideTargetVolumes(collider.gameObject,
                TotalKinematicVelocity,
                Vector3.zero,
                collider.transform.position,
                false);
        }

        public void OnTriggerExit2DInternal(Collider2D c)
        {
            if (SoundMaterial == null || !enabled ||
                SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            SetSlideTargetVolumes(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position, true);
        }

        #endregion

#endif
    }
}