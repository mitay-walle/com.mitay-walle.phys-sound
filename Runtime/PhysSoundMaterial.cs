using UnityEngine;
using System.Collections.Generic;
using PhysSound.Utilities;

namespace PhysSound
{
    [CreateAssetMenu(menuName = "Phys Sound/Material")]
    public class PhysSoundMaterial : ScriptableObject
    {
        public PhysSoundKey MaterialTypeKey;
        public PhysSoundMaterial Fallback;
        [SerializeField] protected PhysSoundDatabase _database;

        public bool TimeScalePitch;
        public bool ScaleImpactVolume = true;
        public float SlidePitchMod = 0.05f;

        [SerializeField] protected AnimationCurve VolumeCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] protected LayerMask CollisionMask = -1;
        [SerializeField] protected Range RelativeVelocityThreshold;
        [SerializeField] protected float PitchRandomness = 0.1f;
        [SerializeField] protected float SlideVolMultiplier = 1;
        [SerializeField] protected float ImpactNormalBias = 1;
        [SerializeField] protected float ScaleMod = 0.15f;
        [SerializeField] protected bool UseCollisionVelocity = true;

        public List<PhysSoundAudioSet> AudioSets = new List<PhysSoundAudioSet>();
        private Dictionary<PhysSoundKey, PhysSoundAudioSet> _audioSetDic;

        public void Init()
        {
            if (AudioSets.Count <= 0)
                return;

            _audioSetDic = new Dictionary<PhysSoundKey, PhysSoundAudioSet>();

            foreach (PhysSoundAudioSet audSet in AudioSets)
            {
                if (_audioSetDic.ContainsKey(audSet.Key))
                {
                    Debug.LogError(
                        $"PhysSound Material {name} has duplicate audio set for Material Type '{audSet.Key.name}'. It will not be used during runtime.");

                    continue;
                }

                _audioSetDic.Add(audSet.Key, audSet);
            }
        }

#if PHYS_SOUND_3D
        public AudioClip GetImpactAudio(Collider other, Vector3 relativeVel, Vector3 normal, Vector3 contact)
        {
            if (!CanCollideAudioWith(other.gameObject))
                return null;

            PhysSoundBase otherPhysSoundComponent = other.GetComponent<PhysSoundBase>();

            if (otherPhysSoundComponent)
            {
                PhysSoundMaterial soundMaterial = otherPhysSoundComponent.GetPhysSoundMaterial(contact);
                PhysSoundKey soundKey = soundMaterial.MaterialTypeKey;
                return GetImpactRecursive(soundKey, relativeVel, normal);
            }

            PhysSoundKey key = _database.Physic3D.GetSoundMaterial(other.sharedMaterial);
            return GetImpactRecursive(key, relativeVel, normal);
        }
#endif

#if PHYS_SOUND_2D
        public AudioClip GetImpactAudio(Collider2D other, Vector3 relativeVel, Vector3 normal, Vector3 contact)
        {
            if (!CanCollideAudioWith(other.gameObject))
                return null;

            PhysSoundBase otherPhysSoundComponent = other.GetComponent<PhysSoundBase>();

            if (otherPhysSoundComponent)
            {
                PhysSoundMaterial soundMaterial = otherPhysSoundComponent.GetPhysSoundMaterial(contact);
                PhysSoundKey soundKey = soundMaterial.MaterialTypeKey;
                return GetImpactRecursive(soundKey, relativeVel, normal);
            }

            PhysSoundKey key = _database.Physic2D.GetSoundMaterial(other.sharedMaterial);
            return GetImpactRecursive(key, relativeVel, normal);
        }
#endif

        private AudioClip GetImpactRecursive(PhysSoundKey key, Vector3 relativeVel, Vector3 normal)
        {
            Debug.Log($"GetImpactRecursive {key?.name}");

            bool random = !UseCollisionVelocity;
            if (key && _audioSetDic.ContainsKey(key))
            {
                float velNorm = GetImpactVolume(relativeVel, normal);
                if (velNorm <= 0)
                {
                    Debug.Log($"return velNorm <= 0");

                    return null;
                }

                velNorm = UseCollisionVelocity ? velNorm : 0;

                return _audioSetDic[key].GetImpact(velNorm, random);
            }

            return Fallback != null ? Fallback.GetImpactRecursive(key, relativeVel, normal) : null;
        }

        /// <summary>
        /// Gets the volume of the slide audio based on the velocity and normal of the collision.
        /// </summary>
        public float GetSlideVolume(Vector3 relativeVel, Vector3 norm)
        {
            float slideAmt = norm == Vector3.zero ? 1 : 1 - Mathf.Abs(Vector3.Dot(norm, relativeVel));
            float slideVel = (slideAmt) * relativeVel.magnitude * SlideVolMultiplier;

            return VolumeCurve.Evaluate(RelativeVelocityThreshold.Normalize(slideVel));
        }

        /// <summary>
        /// Gets the volume of the impact audio based on the velocity and normal of the collision.
        /// </summary>
        public float GetImpactVolume(Vector3 relativeVel, Vector3 norm)
        {
            float impactAmt = norm == Vector3.zero
                ? 1
                : Mathf.Abs(Vector3.Dot(norm.normalized, relativeVel.normalized));

            float impactVel = (impactAmt + (1 - impactAmt) * (1 - ImpactNormalBias)) * relativeVel.magnitude;

            if (impactVel < RelativeVelocityThreshold.Min)
                return -1;

            return VolumeCurve.Evaluate(RelativeVelocityThreshold.Normalize(impactVel));
        }

        /// <summary>
        /// Gets a random pitch within this material's pitch randomness range.
        /// </summary>
        public float GetRandomPitch()
        {
            return Random.Range(-PitchRandomness, PitchRandomness);
        }

        /// <summary>
        /// Gets the amount to multiply the pitch by based on the given scale and the ScaleMod property.
        /// </summary>
        public float GetScaleModPitch(Vector3 scale)
        {
            float val = (1 - ScaleMod) + (1.7320508075688772f / scale.magnitude) * ScaleMod;

            if (TimeScalePitch)
                val *= Time.timeScale;

            return val;
        }

        /// <summary>
        /// Gets the amount to multiply the volume by based on the given scale and the ScaleMod property.
        /// </summary>
        public float GetScaleModVolume(Vector3 scale)
        {
            return (1 - ScaleMod) + (scale.magnitude / 1.7320508075688772f) * ScaleMod;
        }

        /// <summary>
        /// Checks if this material has an audio set corresponding to the given key index.
        /// </summary>
        public bool HasAudioSet(PhysSoundKey key)
        {
            foreach (PhysSoundAudioSet aud in AudioSets)
            {
                if (aud.CompareKey(key))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the audio set corresponding to the given key index, if it exists.
        /// </summary>
        public PhysSoundAudioSet GetAudioSet(PhysSoundKey key)
        {
            foreach (PhysSoundAudioSet aud in AudioSets)
            {
                if (aud.CompareKey(key))
                    return aud;
            }

            return null;
        }

        /// <summary>
        /// Compares the layer of the given GameObject to this material's collision mask.
        /// </summary>
        public bool CanCollideAudioWith(GameObject g)
        {
            return (1 << g.layer & CollisionMask.value) != 0;
        }
    }
}