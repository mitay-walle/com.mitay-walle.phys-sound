using System.Collections.Generic;
using UnityEngine;

namespace PhysSound
{
    [System.Serializable]
    public class PhysSoundAudioSet
    {
        public PhysSoundKey Key;
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
        public bool CompareKey(PhysSoundKey k)
        {
            return Key == k;
        }
    }
}