using UnityEngine;
using System.Collections.Generic;

namespace PhysSound
{
    public class PhysSoundMaterial : ScriptableObject
    {
        public int MaterialTypeKey;
        public int FallbackTypeIndex;
        public int FallbackTypeKey;
        public AnimationCurve VolumeCurve = AnimationCurve.Linear(0, 0, 1, 1);

        public Range RelativeVelocityThreshold;
        public float PitchRandomness = 0.1f;
        public bool TimeScalePitch;
        public float SlidePitchMod = 0.05f;
        public float SlideVolMultiplier = 1;
        public float ImpactNormalBias = 1;
        public float ScaleMod = 0.15f;

        public LayerMask CollisionMask = -1;

        public bool UseCollisionVelocity = true;
        public bool ScaleImpactVolume = true;

        public List<PhysSoundAudioSet> AudioSets = new List<PhysSoundAudioSet>();
        private Dictionary<int, PhysSoundAudioSet> audioSetDic;

        void OnEnable()
        {
            if (AudioSets.Count <= 0)
                return;

            audioSetDic = new Dictionary<int, PhysSoundAudioSet>();

            foreach (PhysSoundAudioSet audSet in AudioSets)
            {
                if (audioSetDic.ContainsKey(audSet.Key))
                {
                    Debug.LogError("PhysSound Material " + name + " has duplicate audio set for Material Type \"" + PhysSoundTypeList.GetKey(audSet.Key) + "\". It will not be used during runtime.");
                    continue;
                }

                audioSetDic.Add(audSet.Key, audSet);
            }

            if (FallbackTypeIndex == 0)
                FallbackTypeKey = -1;
            else
                FallbackTypeKey = AudioSets[FallbackTypeIndex - 1].Key;
        }

        /// <summary>
        /// Gets the impact audio clip based on the given object that was hit, the velocity of the collision, the normal, and the contact point.
        /// </summary>
        public AudioClip GetImpactAudio(GameObject otherObject, Vector3 relativeVel, Vector3 norm, Vector3 contact, int layer = -1)
        {
            if (audioSetDic == null)
                return null;

            if (!CollideWith(otherObject))
                return null;

            PhysSoundMaterial m = null;
            PhysSoundBase b = otherObject.GetComponent<PhysSoundBase>();

            if (b)
                m = b.GetPhysSoundMaterial(contact);

            float velNorm = GetImpactVolume(relativeVel, norm);
            if (velNorm <= 0)
                return null;

            //Get sounds using collision velocity
            if (UseCollisionVelocity)
            {
                if (m)
                {
                    PhysSoundAudioSet audSet;

                    if (audioSetDic.TryGetValue(m.MaterialTypeKey, out audSet))
                        return audSet.GetImpact(velNorm, false);
                    else if (FallbackTypeKey != -1)
                        return audioSetDic[FallbackTypeKey].GetImpact(velNorm, false);
                }
                else if (FallbackTypeKey != -1)
                    return audioSetDic[FallbackTypeKey].GetImpact(velNorm, false);
            }
            //Get sound randomly
            else
            {
                if (m)
                {
                    PhysSoundAudioSet audSet;

                    if (audioSetDic.TryGetValue(m.MaterialTypeKey, out audSet))
                        return audSet.GetImpact(0, true);
                    else if (FallbackTypeKey != -1)
                        return audioSetDic[FallbackTypeKey].GetImpact(0, true);
                }
                else if (FallbackTypeKey != -1)
                    return audioSetDic[FallbackTypeKey].GetImpact(0, true);
            }

            return null;
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
            float impactAmt = norm == Vector3.zero ? 1 : Mathf.Abs(Vector3.Dot(norm.normalized, relativeVel.normalized));
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
        public bool HasAudioSet(int keyIndex)
        {
            foreach (PhysSoundAudioSet aud in AudioSets)
            {
                if (aud.CompareKeyIndex(keyIndex))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the audio set corresponding to the given key index, if it exists.
        /// </summary>
        public PhysSoundAudioSet GetAudioSet(int keyIndex)
        {
            foreach (PhysSoundAudioSet aud in AudioSets)
            {
                if (aud.CompareKeyIndex(keyIndex))
                    return aud;
            }

            return null;
        }

        /// <summary>
        /// Gets the list of audio set names. (Used by the editor to display the list of potential fallback audio sets).
        /// </summary>
        public string[] GetFallbackAudioSets()
        {
            string[] names = new string[AudioSets.Count + 1];
            names[0] = "None";

            for (int i = 0; i < AudioSets.Count; i++)
            {
                names[i + 1] = PhysSoundTypeList.GetKey(AudioSets[i].Key);
            }

            return names;
        }

        /// <summary>
        /// Compares the layer of the given GameObject to this material's collision mask.
        /// </summary>
        public bool CollideWith(GameObject g)
        {
            return (1 << g.layer & CollisionMask.value) != 0;
        }
    }

    [System.Serializable]
    public class PhysSoundAudioSet
    {
        public int Key;
        public List<AudioClip> Impacts = new List<AudioClip>();
        public AudioClip Slide;

        /// <summary>
        /// Gets the appropriate audio clip. Either based on the given velocity or picked at random.
        /// </summary>
        public AudioClip GetImpact(float vel, bool random)
        {
            if (Impacts.Count == 0)
                return null;

            if (random)
            {
                return Impacts[Random.Range(0, Impacts.Count)];
            }
            else
            {
                int i = (int)(vel * (Impacts.Count - 1));
                return Impacts[i];
            }
        }

        /// <summary>
        /// Returns true if this Audio Set's key index is the same as the given key index.
        /// </summary>
        public bool CompareKeyIndex(int k)
        {
            return Key == k;
        }
    }
}