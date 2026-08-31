#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PhysSound.Editor
{
    internal static class PhysSoundAudioExporter
    {
        internal static void Export(
            Object owner,
            PhysSoundInteraction interaction,
            bool impact,
            int impactRangeIndex,
            AudioClip source,
            List<PhysSoundAudioRegion> regions)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath) || regions.Count == 0)
            {
                return;
            }

            string sourceDirectory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            string exportDirectory = $"{sourceDirectory}/{sourceName}_PhysSound";
            Directory.CreateDirectory(exportDirectory);

            List<string> exportedPaths = new();
            try
            {
                int regionCount = impact ? regions.Count : Mathf.Min(1, regions.Count);
                for (int i = 0; i < regionCount; i++)
                {
                    PhysSoundAudioRegion region = regions[i];
                    int startSample = Mathf.Clamp(
                        Mathf.FloorToInt(region.StartTime * source.frequency),
                        0,
                        Mathf.Max(0, source.samples - 1));
                    int endSample = Mathf.Clamp(
                        Mathf.CeilToInt(region.EndTime * source.frequency),
                        startSample + 1,
                        source.samples);
                    int sampleCount = endSample - startSample;
                    float[] samples = new float[sampleCount * source.channels];

                    if (!source.GetData(samples, startSample))
                    {
                        throw new InvalidOperationException(
                            $"Could not read {source.name}. Enable decompression/read access in its Audio Import Settings.");
                    }

                    string suffix = impact ? $"Impact_{i + 1:00}" : "Slide_Loop";
                    string outputPath = $"{exportDirectory}/{sourceName}_{suffix}.wav";
                    WriteWave(outputPath, samples, source.channels, source.frequency);
                    exportedPaths.Add(outputPath);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AudioClip[] clips = new AudioClip[exportedPaths.Count];
                for (int i = 0; i < exportedPaths.Count; i++)
                {
                    clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(exportedPaths[i]);
                }

                Undo.RecordObject(owner, "Export Phys Sound Audio Regions");
                if (impact)
                {
                    interaction.SetExportedImpactClips(clips, impactRangeIndex);
                }
                else
                {
                    interaction.SetExportedSlideClips(clips);
                }

                EditorUtility.SetDirty(owner);
                AssetDatabase.SaveAssets();
                Debug.Log($"Phys Sound exported {clips.Length} clip(s) to {exportDirectory}.", owner);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, owner);
            }
        }

        private static void WriteWave(string path, float[] samples, int channels, int frequency)
        {
            const short bitsPerSample = 16;
            int dataSize = samples.Length * sizeof(short);

            using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using BinaryWriter writer = new(stream);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(frequency * channels * bitsPerSample / 8);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            for (int i = 0; i < samples.Length; i++)
            {
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
            }
        }
    }
}
#endif
