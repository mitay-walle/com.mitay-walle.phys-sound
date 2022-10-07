using UnityEngine;

namespace PhysSound
{
    public class PhysSoundObjectBase : PhysSoundBase
    {
        public PhysSoundMaterial SoundMaterial;

        public bool AutoCreateSources;
        public bool PlayClipAtPoint;
        public bool HitsTriggers;

        public AudioSource ImpactAudio;

        protected float baseImpactVol, baseImpactPitch;

        protected Vector3 _prevVelocity;
        protected bool _setPrevVelocity = true;

        protected Vector3 _prevPosition;
        protected Vector3 _kinematicVelocity;
        protected Quaternion _prevRotation;
        protected float _kinematicAngularVelocity;

        protected int _lastFrame;

        protected Rigidbody _r;
        protected Rigidbody2D _r2D;

        protected Vector3 TotalKinematicVelocity
        {
            get { return _kinematicVelocity + (Vector3.one * _kinematicAngularVelocity); }
        }


        void Start()
        {
            if (SoundMaterial == null)
                return;

            Initialize();
        }

        /// <summary>
        /// Gets the PhysSound Material of this object.
        /// </summary>
        public override PhysSoundMaterial GetPhysSoundMaterial(Vector3 contactPoint)
        {
            return SoundMaterial;
        }

        public virtual void Initialize() { }

        public virtual void SetEnabled(bool enabled) { }

        protected void playImpactSound(GameObject otherObject, Vector3 relativeVelocity, Vector3 normal, Vector3 contactPoint)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || Time.frameCount == _lastFrame)
            {
                return;
            }

            if (ImpactAudio)
            {
                AudioClip a = SoundMaterial.GetImpactAudio(otherObject, relativeVelocity, normal, contactPoint);

                if (a)
                {
                    float pitch = baseImpactPitch * SoundMaterial.GetScaleModPitch(transform.localScale) + SoundMaterial.GetRandomPitch();
                    float vol = baseImpactVol * SoundMaterial.GetScaleModVolume(transform.localScale) * SoundMaterial.GetImpactVolume(relativeVelocity, normal);

                    if (PlayClipAtPoint)
                    {
                        PhysSoundTempAudioPool.Instance.PlayClip(a, transform.position, ImpactAudio, SoundMaterial.ScaleImpactVolume ? vol : ImpactAudio.volume, pitch);
                    }
                    else
                    {
                        ImpactAudio.pitch = pitch;
                        if (SoundMaterial.ScaleImpactVolume)
                            ImpactAudio.volume = vol;

                        ImpactAudio.clip = a;
                        ImpactAudio.Play();
                    }

                    _lastFrame = Time.frameCount;
                }
            }
        }
    }
}