namespace PhysSound
{
    // partial class-files should be in same assemblies
    public partial class PhysSoundObject
    {
        #if PHYS_SOUND_2D
        
        protected Rigidbody2D _r2D;

        #region 2D Collision Messages

        void OnCollisionEnter2D(Collision2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            playImpactSound(c.collider.gameObject, c.relativeVelocity, c.contacts[0].normal, c.contacts[0].point);

            _setPrevVelocity = true;
        }

        void OnCollisionStay2D(Collision2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersDic == null)
                return;

            if (_setPrevVelocity)
            {
                _prevVelocity = _r2D.velocity;
                _setPrevVelocity = false;
            }

            Vector3 deltaVel = _r2D.velocity - (Vector2) _prevVelocity;

            playImpactSound(c.collider.gameObject, deltaVel, c.contacts[0].normal, c.contacts[0].point);
            setSlideTargetVolumes(c.collider.gameObject,
                c.relativeVelocity,
                c.contacts[0].normal,
                c.contacts[0].point,
                false);

            _prevVelocity = _r2D.velocity;
        }

        void OnCollisionExit2D(Collision2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersDic == null)
                return;

            setSlideTargetVolumes(c.collider.gameObject, c.relativeVelocity, Vector3.up, transform.position, true);

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

        void OnTriggerStay2D(Collider2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersDic == null)
                return;

            setSlideTargetVolumes(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position, false);
        }

        void OnTriggerExit2D(Collider2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 ||
                _audioContainersDic == null)
                return;

            setSlideTargetVolumes(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position, true);
        }

        #endregion


#endif
    }
}