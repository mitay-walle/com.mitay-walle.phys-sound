using UnityEngine;

namespace PhysSound
{
    public partial class PhysSoundObjectLite
    {
#if PHYS_SOUND_2D
        
        #region 2D Collision Messages

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            PlayImpactSound(collision.collider, collision.relativeVelocity, collision.GetContact(0).normal, collision.GetContact(0).point);

            _setPrevVelocity = true;
        }

        #endregion

        #region 2D Trigger Messages

        void OnTriggerEnter2D(Collider2D collider)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            PlayImpactSound(collider, TotalKinematicVelocity, Vector3.zero, collider.transform.position);
        }

        #endregion
#endif
    }
}