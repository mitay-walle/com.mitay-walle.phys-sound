using UnityEngine;
using System.Reflection;
using System;

namespace PhysSound
{
    public class PhysSoundTempAudioPool : MonoBehaviour
    {
		public static int TempAudioPoolSize=100;

        public static PhysSoundTempAudioPool Instance;

        public static void Create()
        {
            if (Instance != null)
                return;

            GameObject g = new GameObject("PhysSound Temp Audio Pool");
            PhysSoundTempAudioPool p = g.AddComponent<PhysSoundTempAudioPool>();
            p.Initialize();
        }

        /// <summary>
        /// Creates a new AudioSource component on the given GameObject, using the given template's properties.
        /// </summary>
        public static AudioSource GetAudioSourceCopy(AudioSource template, GameObject g)
        {
            AudioSource a = g.AddComponent<AudioSource>();

            if (!template)
                return a;

            CopyAudioSource(template, a);

            return a;
        }

        /// <summary>
        /// Applies the properties of the template AudioSource to the target AudioSource.
        /// </summary>
        public static void CopyAudioSource(AudioSource template, AudioSource target)
        {
            target.bypassEffects = template.bypassEffects;
            target.bypassListenerEffects = template.bypassListenerEffects;
            target.bypassReverbZones = template.bypassReverbZones;
            target.dopplerLevel = template.dopplerLevel;
            target.ignoreListenerPause = template.ignoreListenerPause;
            target.ignoreListenerVolume = template.ignoreListenerVolume;
            target.loop = template.loop;
            target.maxDistance = template.maxDistance;
            target.minDistance = template.minDistance;
            target.mute = template.mute;
            target.outputAudioMixerGroup = template.outputAudioMixerGroup;
            target.panStereo = template.panStereo;
            target.pitch = template.pitch;
            target.playOnAwake = template.playOnAwake;
            target.priority = template.priority;
            target.reverbZoneMix = template.reverbZoneMix;
            target.rolloffMode = template.rolloffMode;
            target.spatialBlend = template.spatialBlend;
            target.spread = template.spread;
            target.time = template.time;
            target.timeSamples = template.timeSamples;
            target.velocityUpdateMode = template.velocityUpdateMode;
            target.volume = template.volume;
        }

        private PhysSoundTempAudio[] audioSources;
        private int lastAvailable;

        public void Initialize()
        {
            Instance = this;

            audioSources = new PhysSoundTempAudio[TempAudioPoolSize];

            for (int i = 0; i < TempAudioPoolSize; i++)
            {
                GameObject g = new GameObject("Temp Audio Source");
                PhysSoundTempAudio a = g.AddComponent<PhysSoundTempAudio>();
                a.Initialize(this);

                audioSources[i] = a;
            }
        }

        public void PlayClip(AudioClip clip, Vector3 point, AudioSource template, float volume, float pitch)
        {
            int checkedIndices = 0;
            int i = lastAvailable;

            while (checkedIndices < TempAudioPoolSize)
            {
                PhysSoundTempAudio a = audioSources[i];

                if (!a.gameObject.activeInHierarchy)
                {
                    a.PlayClip(clip, point, template, volume, pitch);

                    lastAvailable = i;
                    return;
                }

                i++;
                checkedIndices++;

                if (i >= TempAudioPoolSize)
                    i = 0;
            }
        }

        public AudioSource GetSource(AudioSource template)
        {
            int checkedIndices = 0;
            int i = lastAvailable;

            while (checkedIndices < TempAudioPoolSize)
            {
                PhysSoundTempAudio a = audioSources[i];

                if (!a.gameObject.activeInHierarchy)
                {
                    CopyAudioSource(template, a.Audio);
                    a.gameObject.SetActive(true);
                    lastAvailable = i;
                    return a.Audio;
                }

                i++;
                checkedIndices++;

                if (i >= TempAudioPoolSize)
                    i = 0;
            }

            return null;
        }

        public void ReleaseSource(AudioSource a)
        {
            a.Stop();
        }
    }
}
