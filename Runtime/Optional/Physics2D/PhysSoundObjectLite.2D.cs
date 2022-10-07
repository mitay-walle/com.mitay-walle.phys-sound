namespace PhysSound
{
    public partial class PhysSoundObjectLite
    {
#if PHYS_SOUND_2D
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
#endif
    }
}