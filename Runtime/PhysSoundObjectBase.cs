using UnityEngine;

namespace PhysSound
{
    public abstract class PhysSoundObjectBase : PhysSoundBase
    {
        public PhysSoundMaterial SoundMaterial;
        [SerializeField] protected AudioSource ImpactAudio;
        [SerializeField] protected bool AutoCreateSources;
        [SerializeField] protected bool HitsTriggers;
        public bool PlayClipAtPoint;

        protected float KinematicAngularVelocity;
        protected float BaseImpactVol;
        protected float BaseImpactPitch;
        protected int LastFrame;
        protected bool SetPrevVelocity = true;
        protected Vector3 PrevVelocity;
        protected Vector3 PrevPosition;
        protected Vector3 KinematicVelocity;
        protected Quaternion PrevRotation;

        protected Vector3 TotalKinematicVelocity => KinematicVelocity + (Vector3.one * KinematicAngularVelocity);

        private void Start()
        {
            if (SoundMaterial == null)
                return;

            Initialize();
        }

        public override PhysSoundMaterial GetPhysSoundMaterial(Vector3 contactPoint) => SoundMaterial;

        public abstract void SetEnabled(bool enabled);

        protected abstract void Initialize();
        
#if PHYS_SOUND_3D
        protected Rigidbody _r;
        protected void PlayImpactSound(Collider other, Vector3 relativeVelocity, Vector3 normal, Vector3 contactPoint)
        {
            if (SoundMaterial == null || !enabled || SoundMaterial.AudioSets.Count == 0 || Time.frameCount == LastFrame)
            {
                return;
            }

            if (!ImpactAudio) return;

            AudioClip clip = SoundMaterial.GetImpactAudio(other, relativeVelocity, normal, contactPoint);
            if (clip)
            {
                PlayImpactSound(clip, relativeVelocity, normal, contactPoint);
            }
        }
#endif

#if PHYS_SOUND_2D
        protected Rigidbody2D _r2D;
        protected void PlayImpactSound(Collider2D other, Vector3 relativeVelocity, Vector3 normal, Vector3 contactPoint)
        {
            if (SoundMaterial == null || !enabled || SoundMaterial.AudioSets.Count == 0 || Time.frameCount == LastFrame)
            {
                return;
            }

            if (!ImpactAudio) return;

            AudioClip clip = SoundMaterial.GetImpactAudio(other, relativeVelocity, normal, contactPoint);
            if (clip)
            {
                PlayImpactSound(clip, relativeVelocity, normal, contactPoint);
            }
        }
#endif

        protected void PlayImpactSound(AudioClip clip, Vector3 relativeVelocity, Vector3 normal, Vector3 contactPoint)
        {
            float pitch = BaseImpactPitch * SoundMaterial.GetScaleModPitch(transform.localScale) +
                          SoundMaterial.GetRandomPitch();

            float vol = BaseImpactVol * SoundMaterial.GetScaleModVolume(transform.localScale) *
                        SoundMaterial.GetImpactVolume(relativeVelocity, normal);

            if (PlayClipAtPoint)
            {
                PhysSoundTempAudioPool.Instance.PlayClip(clip,
                    transform.position,
                    ImpactAudio,
                    SoundMaterial.ScaleImpactVolume ? vol : ImpactAudio.volume,
                    pitch);
            }
            else
            {
                ImpactAudio.pitch = pitch;
                if (SoundMaterial.ScaleImpactVolume)
                    ImpactAudio.volume = vol;

                ImpactAudio.clip = clip;
                ImpactAudio.Play();
            }

            LastFrame = Time.frameCount;
        }
    }
}