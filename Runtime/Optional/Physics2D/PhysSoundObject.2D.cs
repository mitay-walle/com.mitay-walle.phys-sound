using UnityEngine;

namespace PhysSound
{
    // partial class-files should be in same assemblies
    public partial class PhysSoundObject
    {
        #if PHYS_SOUND_2D
         
        #region 2D Collision Messages

        void OnCollisionEnter2D(Collision2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            PlayImpactSound(c.collider, c.relativeVelocity, c.GetContact(0).normal, c.GetContact(0).point);

            SetPrevVelocity = true;
        }

        void OnCollisionStay2D(Collision2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            if (SetPrevVelocity)
            {
                PrevVelocity = _r2D.velocity;
                SetPrevVelocity = false;
            }
 
            Vector3 deltaVel = _r2D.velocity - (Vector2) PrevVelocity;

            PlayImpactSound(c.collider, deltaVel, c.GetContact(0).normal, c.GetContact(0).point);
            SetSlideTargetVolumes(c.collider.gameObject,
                c.relativeVelocity,
                c.contacts[0].normal,
                c.contacts[0].point,
                false);

            PrevVelocity = _r2D.velocity;
        }

        void OnCollisionExit2D(Collision2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            SetSlideTargetVolumes(c.collider.gameObject, c.relativeVelocity, Vector3.up, transform.position, true);

            SetPrevVelocity = true;
        }

        #endregion

        #region 2D Trigger Messages

        void OnTriggerEnter2D(Collider2D collider)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            PlayImpactSound(collider, TotalKinematicVelocity, Vector3.zero, collider.transform.position);
        }

        void OnTriggerStay2D(Collider2D collider)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            SetSlideTargetVolumes(collider.gameObject, TotalKinematicVelocity, Vector3.zero, collider.transform.position, false);
        }

        void OnTriggerExit2D(Collider2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersMap == null)
                return;

            SetSlideTargetVolumes(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position, true);
        }

        #endregion


#endif
    }
}