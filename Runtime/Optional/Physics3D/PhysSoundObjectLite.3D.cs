namespace PhysSound
{
    public partial class PhysSoundObjectLite
    {
 #if PHYS_SOUND_3D
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
#endif
    }
}