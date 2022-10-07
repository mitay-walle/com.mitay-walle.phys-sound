using UnityEngine;
using System.Collections;
using System;

namespace PhysSound
{
    [System.Serializable]
    public struct Range
    {
        /// <summary>
        /// The minimum value of the range.
        /// </summary>
        public float Min;
        /// <summary>
        /// The maximium value of the range.
        /// </summary>
        public float Max;

        public Range(float min, float max)
        {
            this.Min = min;
            this.Max = max;
        }

        /// <summary>
        /// Checks to see if the given value is within the range's min and max values.
        /// </summary>
        public bool isWithinRange(float f)
        {
            return (f >= Min && f <= Max);
        }

        /// <summary>
        /// Clamps the given value to be between the range's min and max values.
        /// </summary>
        public float Clamp(float f)
        {
            return Mathf.Clamp(f, Min, Max);
        }

        /// <summary>
        /// Gets a random float value within the range.
        /// </summary>
        public float RandomInRange()
        {
            return UnityEngine.Random.Range(Min, Max);
        }

        /// <summary>
        /// Gets and random int value within the range.
        /// </summary>
        /// <returns></returns>
        public int RandomInRangeInteger()
        {
            return (int)UnityEngine.Random.Range(Min, Max + 1);
        }

        /// <summary>
        /// Gets a value between min and max, specified by the variable t.
        /// </summary>
        public float Lerp(float t)
        {
            return Min + (Max - Min) * t;
        }

        /// <summary>
        /// Gets a value between 0 and 1 based on the given value, where 0 corresponds to Min and 1 corresponds to Max.
        /// </summary>
        public float Normalize(float val)
        {
            if (val <= Min) return 0;
            else if (val >= Max) return 1;

            return (val - Min) / (Max - Min);
        }
    }
}