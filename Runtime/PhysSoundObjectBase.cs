using PhysSound.Utilities;
using UnityEngine;

namespace PhysSound
{
    public abstract class PhysSoundObjectBase : PhysSoundBase
    {
        public PhysSoundMaterial SoundMaterial;
        [SerializeField] protected AudioSource _impactAudio;
        [SerializeField] protected CollisionEventsToggles _collisionEvents = new CollisionEventsToggles();
        [SerializeField] protected bool _autoCreateSources;
        public bool PlayClipAtPoint;

        protected float _kinematicAngularVelocity;
        protected float _baseImpactVol;
        protected float _baseImpactPitch;
        protected int _lastFrame;
        protected bool _setPrevVelocity = true;
        protected Vector3 _prevVelocity;
        protected Vector3 _prevPosition;
        protected Vector3 _kinematicVelocity;
        protected Quaternion _prevRotation;
        private TimeTrigger _delay;

        protected Vector3 TotalKinematicVelocity => _kinematicVelocity + (Vector3.one * _kinematicAngularVelocity);

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
            if (!_delay.IsReady() || SoundMaterial == null || !enabled || SoundMaterial.AudioSets.Count == 0 ||
                Time.frameCount == _lastFrame)
            {
                return;
            }

            if (!_impactAudio) return;

            _delay.Restart(SoundMaterial.Delay);

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
            if (!_delay.IsReady() || SoundMaterial == null || !enabled || SoundMaterial.AudioSets.Count == 0 ||
                Time.frameCount == _lastFrame)
            {
                return;
            }

            if (!_impactAudio) return;

            _delay.Restart(SoundMaterial.Delay);

            AudioClip clip = SoundMaterial.GetImpactAudio(other, relativeVelocity, normal, contactPoint);
            if (clip)
            {
                PlayImpactSound(clip, relativeVelocity, normal, contactPoint);
            }
        }
#endif

        protected void PlayImpactSound(AudioClip clip, Vector3 relativeVelocity, Vector3 normal, Vector3 contactPoint)
        {
            float pitch = _baseImpactPitch * SoundMaterial.GetScaleModPitch(transform.localScale) +
                          SoundMaterial.GetRandomPitch();

            float vol = _baseImpactVol * SoundMaterial.GetScaleModVolume(transform.localScale) *
                        SoundMaterial.GetImpactVolume(relativeVelocity, normal);

            if (PlayClipAtPoint)
            {
                PhysSoundTempAudioPool.Instance.PlayClip(clip,
                    transform.position,
                    _impactAudio,
                    SoundMaterial.ScaleImpactVolume ? vol : _impactAudio.volume,
                    pitch);
            }
            else
            {
                _impactAudio.pitch = pitch;
                if (SoundMaterial.ScaleImpactVolume)
                    _impactAudio.volume = vol;

                _impactAudio.clip = clip;
                _impactAudio.Play();
            }

            _lastFrame = Time.frameCount;
        }
    }
}