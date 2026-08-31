#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System.Collections.Generic;
using UnityEngine;

namespace PhysSound.Editor
{
    internal static class PhysSoundAudioRegionDetector
    {
        internal static List<Vector2> Detect(
            float[] minMaxData,
            float clipDuration,
            bool multipleRegions,
            float soundVolumeMinimum,
            float soundVolumeMaximum,
            float pauseVolumeMinimum,
            float pauseVolumeMaximum,
            float soundDurationMinimum,
            float soundDurationMaximum,
            float pauseDurationMinimum,
            float pauseDurationMaximum)
        {
            List<Vector2> regions = new List<Vector2>();
            if (minMaxData == null || minMaxData.Length < 2 || clipDuration <= 0f)
            {
                return regions;
            }

            int sampleCount = minMaxData.Length / 2;
            float secondsPerSample = clipDuration / sampleCount;
            int minimumPauseSamples = Mathf.Max(1, Mathf.CeilToInt(pauseDurationMinimum / secondsPerSample));
            int regionStart = -1;
            int lastSoundSample = -1;
            int pauseStart = -1;

            for (int i = 0; i < sampleCount; i++)
            {
                float amplitude = Mathf.Max(
                    Mathf.Abs(minMaxData[i * 2]),
                    Mathf.Abs(minMaxData[i * 2 + 1]));
                bool isSound = amplitude >= soundVolumeMinimum && amplitude <= soundVolumeMaximum;
                bool isPause = amplitude >= pauseVolumeMinimum && amplitude <= pauseVolumeMaximum;

                if (isSound)
                {
                    if (regionStart < 0)
                    {
                        regionStart = i;
                    }

                    lastSoundSample = i;
                    pauseStart = -1;
                    continue;
                }

                if (regionStart < 0)
                {
                    continue;
                }

                if (!isPause)
                {
                    pauseStart = -1;
                    continue;
                }

                if (pauseStart < 0)
                {
                    pauseStart = i;
                }

                if (i - pauseStart + 1 >= minimumPauseSamples)
                {
                    AddRegion(
                        regions,
                        regionStart,
                        lastSoundSample,
                        secondsPerSample,
                        clipDuration,
                        soundDurationMinimum,
                        soundDurationMaximum,
                        pauseDurationMaximum);
                    regionStart = -1;
                    lastSoundSample = -1;
                    pauseStart = -1;
                }
            }

            if (regionStart >= 0 && lastSoundSample >= regionStart)
            {
                AddRegion(
                    regions,
                    regionStart,
                    lastSoundSample,
                    secondsPerSample,
                    clipDuration,
                    soundDurationMinimum,
                    soundDurationMaximum,
                    pauseDurationMaximum);
            }

            if (multipleRegions || regions.Count <= 1)
            {
                return regions;
            }

            Vector2 longest = regions[0];
            for (int i = 1; i < regions.Count; i++)
            {
                if (regions[i].y - regions[i].x > longest.y - longest.x)
                {
                    longest = regions[i];
                }
            }

            regions.Clear();
            regions.Add(longest);
            return regions;
        }

        private static void AddRegion(
            List<Vector2> regions,
            int startSample,
            int endSample,
            float secondsPerSample,
            float clipDuration,
            float minimumDuration,
            float maximumDuration,
            float maximumPauseDuration)
        {
            float rawStart = startSample * secondsPerSample;
            float rawEnd = (endSample + 1) * secondsPerSample;
            float duration = rawEnd - rawStart;
            if (duration < minimumDuration || duration > maximumDuration)
            {
                return;
            }

            float padding = Mathf.Min(0.015f, maximumPauseDuration * 0.5f);
            float start = Mathf.Max(0f, rawStart - padding);
            float end = Mathf.Min(clipDuration, rawEnd + padding);
            regions.Add(new Vector2(start, end));
        }
    }
}
#endif
