#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhysSound
{
    internal enum PhysSoundContactBackend
    {
        ProvidesContacts,
        Components
    }

    [Serializable]
    internal sealed class PhysSoundSurface
    {
        [SerializeField] private PhysicsMaterial[] _materials = Array.Empty<PhysicsMaterial>();
#if PHYS_SOUND_2D
#if PHYS_SOUND_DISABLE_2D
        [HideInInspector]
#endif
        [SerializeField] private PhysicsMaterial2D[] _materials2D = Array.Empty<PhysicsMaterial2D>();
#endif
        internal PhysicsMaterial[] Materials => _materials;
#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
        internal PhysicsMaterial2D[] Materials2D => _materials2D;
#endif
    }

    [Serializable]
    internal sealed class PhysSoundImpactClipSource
    {
        [SerializeField] private AudioClip _sourceClip;
        [SerializeField] private List<PhysSoundAudioRegion> _regions = new();
        [SerializeField] private AudioClip[] _runtimeClips = Array.Empty<AudioClip>();

        internal PhysSoundImpactClipSource(AudioClip sourceClip, AudioClip[] runtimeClips = null)
        {
            _sourceClip = sourceClip;
            _runtimeClips = runtimeClips ?? Array.Empty<AudioClip>();
        }

        internal AudioClip SourceClip { get => _sourceClip; set => _sourceClip = value; }
        internal List<PhysSoundAudioRegion> Regions => _regions;
        internal AudioClip[] RuntimeClips { get => _runtimeClips; set => _runtimeClips = value ?? Array.Empty<AudioClip>(); }
    }

    internal readonly struct PhysSoundImpactPlayback
    {
        internal PhysSoundImpactPlayback(AudioClip clip, float startTime, float endTime)
        {
            Clip = clip;
            StartTime = startTime;
            EndTime = endTime;
        }

        internal AudioClip Clip { get; }
        internal float StartTime { get; }
        internal float EndTime { get; }
    }

    [Serializable]
    internal sealed class PhysSoundImpactRange
    {
        [SerializeField, Min(0f)] private float _minimumImpulse = 0.1f;
        [SerializeField, Min(0f)] private float _maximumImpulse = 10f;
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
        [SerializeField, HideInInspector] private List<PhysSoundImpactClipSource> _clipSources = new();

        internal PhysSoundImpactRange(float minimumImpulse, float maximumImpulse, AudioClip[] clips = null)
        {
            _minimumImpulse = minimumImpulse;
            _maximumImpulse = maximumImpulse;
            _clips = clips ?? Array.Empty<AudioClip>();
        }

        internal float MinimumImpulse { get => _minimumImpulse; set => _minimumImpulse = Mathf.Max(0f, value); }
        internal float MaximumImpulse { get => _maximumImpulse; set => _maximumImpulse = Mathf.Max(_minimumImpulse, value); }
        internal AudioClip[] Clips { get => _clips; set => _clips = value ?? Array.Empty<AudioClip>(); }
        internal List<PhysSoundImpactClipSource> ClipSources => _clipSources;
        internal bool Contains(float impulse) => impulse >= _minimumImpulse && impulse <= _maximumImpulse;

        internal void MigrateLegacyClips()
        {
            _clipSources ??= new List<PhysSoundImpactClipSource>();
            if (_clipSources.Count == 0 && _clips != null)
            {
                for (int i = 0; i < _clips.Length; i++)
                {
                    AudioClip clip = _clips[i];
                    if (clip != null)
                    {
                        _clipSources.Add(new PhysSoundImpactClipSource(clip, new[] { clip }));
                    }
                }
            }

            _clips = Array.Empty<AudioClip>();
        }

        internal bool TryGetRandomPlayback(out PhysSoundImpactPlayback playback)
        {
            int validCount = 0;
            for (int i = 0; i < _clipSources.Count; i++)
            {
                PhysSoundImpactClipSource source = _clipSources[i];
                if (source?.SourceClip != null && source.Regions.Count > 0)
                {
                    for (int j = 0; j < source.Regions.Count; j++)
                    {
                        if (IsValidRegion(source.SourceClip, source.Regions[j]))
                        {
                            validCount++;
                        }
                    }

                    continue;
                }

                AudioClip[] clips = source?.RuntimeClips;
                if (clips == null)
                {
                    continue;
                }

                for (int j = 0; j < clips.Length; j++)
                {
                    if (clips[j] != null)
                    {
                        validCount++;
                    }
                }
            }

            if (validCount == 0)
            {
                playback = default;
                return false;
            }

            int selected = UnityEngine.Random.Range(0, validCount);
            for (int i = 0; i < _clipSources.Count; i++)
            {
                PhysSoundImpactClipSource source = _clipSources[i];
                if (source?.SourceClip != null && source.Regions.Count > 0)
                {
                    for (int j = 0; j < source.Regions.Count; j++)
                    {
                        PhysSoundAudioRegion region = source.Regions[j];
                        if (!IsValidRegion(source.SourceClip, region))
                        {
                            continue;
                        }

                        if (selected-- == 0)
                        {
                            float start = Mathf.Clamp(region.StartTime, 0f, source.SourceClip.length);
                            float end = Mathf.Clamp(region.EndTime, start, source.SourceClip.length);
                            playback = new PhysSoundImpactPlayback(source.SourceClip, start, end);
                            return true;
                        }
                    }

                    continue;
                }

                AudioClip[] clips = source?.RuntimeClips;
                if (clips == null)
                {
                    continue;
                }

                for (int j = 0; j < clips.Length; j++)
                {
                    if (clips[j] == null)
                    {
                        continue;
                    }

                    if (selected-- == 0)
                    {
                        playback = new PhysSoundImpactPlayback(clips[j], 0f, clips[j].length);
                        return true;
                    }
                }
            }

            playback = default;
            return false;
        }

        private static bool IsValidRegion(AudioClip clip, PhysSoundAudioRegion region)
        {
            return region != null && region.StartTime < clip.length && region.EndTime > region.StartTime;
        }
    }

    [Serializable]
    internal sealed class PhysSoundInteraction : ISerializationCallbackReceiver
    {
        private const int CurrentDataVersion = 3;

        [Header("Impact")]
        [SerializeField, PhysSoundLabel("Clips")] private AudioClip[] _impactClips = Array.Empty<AudioClip>();
        [SerializeField, PhysSoundLabel("Volume Curve")] private AnimationCurve _impactVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField, PhysSoundLabel("Volume"), Min(0f)] private float _impactVolumeMultiplier = 1f;
        [SerializeField, PhysSoundMinMax(nameof(_maximumImpactImpulse), 0f, 20f, "Impulse")]
        private float _minimumImpactImpulse = 0.1f;
        [SerializeField, HideInInspector] private float _maximumImpactImpulse = 10f;
        [SerializeField, PhysSoundMinMax(0.1f, 3f, "Pitch")] private Vector2 _impactPitchRange = new Vector2(0.95f, 1.05f);

        [Header("Slide")]
        [SerializeField, PhysSoundLabel("Clips")] private AudioClip[] _slideClips = Array.Empty<AudioClip>();
        [SerializeField, PhysSoundLabel("Volume Curve")] private AnimationCurve _slideVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField, PhysSoundLabel("Volume"), Min(0f)] private float _slideVolumeMultiplier = 1f;
        [SerializeField, PhysSoundMinMax(nameof(_maximumSlideSpeed), 0f, 300f, "Speed")]
        private float _minimumSlideSpeed = 0.05f;
        [SerializeField, HideInInspector] private float _maximumSlideSpeed = 5f;
        [SerializeField, PhysSoundMinMax(0.1f, 3f, "Pitch")] private Vector2 _slidePitchRange = new Vector2(0.9f, 1.2f);

        [SerializeField, HideInInspector] private AudioClip _impactSourceClip;
        [SerializeField, HideInInspector] private List<PhysSoundAudioRegion> _impactRegions = new();
        [SerializeField, HideInInspector] private List<PhysSoundImpactRange> _impactRanges = new();
        [SerializeField, HideInInspector] private int _dataVersion;
        [SerializeField, HideInInspector] private AudioClip _slideSourceClip;
        [SerializeField, HideInInspector] private List<PhysSoundAudioRegion> _slideRegions = new();

        internal bool HasSlide => HasValidClip(_slideClips);
        internal List<PhysSoundImpactRange> ImpactRanges => _impactRanges;
        internal float MinimumImpactImpulse => _minimumImpactImpulse;
        internal float MaximumImpactImpulse => _maximumImpactImpulse;
        internal AudioClip SlideSourceClip { get => _slideSourceClip; set => _slideSourceClip = value; }
        internal List<PhysSoundAudioRegion> SlideRegions => _slideRegions;

        internal void SetExportedImpactClips(AudioClip[] clips, int rangeIndex, int sourceIndex)
        {
            if (rangeIndex >= 0 && rangeIndex < _impactRanges.Count &&
                sourceIndex >= 0 && sourceIndex < _impactRanges[rangeIndex].ClipSources.Count)
            {
                _impactRanges[rangeIndex].ClipSources[sourceIndex].RuntimeClips = clips;
            }
            else
            {
                _impactClips = clips ?? Array.Empty<AudioClip>();
            }
        }

        internal void SetExportedSlideClips(AudioClip[] clips)
        {
            _slideClips = clips ?? Array.Empty<AudioClip>();
        }

        internal bool TryGetImpactPlayback(float impulse, out PhysSoundImpactPlayback playback)
        {
            if (_impactRanges.Count == 0)
            {
                AudioClip clip = GetRandomClip(_impactClips);
                playback = clip == null ? default : new PhysSoundImpactPlayback(clip, 0f, clip.length);
                return clip != null;
            }

            for (int i = 0; i < _impactRanges.Count; i++)
            {
                PhysSoundImpactRange range = _impactRanges[i];
                if (range != null && range.Contains(impulse))
                {
                    return range.TryGetRandomPlayback(out playback);
                }
            }

            playback = default;
            return false;
        }

        internal PhysSoundImpactRange CreateInitialImpactRange()
        {
            PhysSoundImpactRange range = new PhysSoundImpactRange(
                _minimumImpactImpulse,
                _maximumImpactImpulse,
                _impactClips);
            _impactRanges.Add(range);
            range.MigrateLegacyClips();
            return range;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (_dataVersion >= CurrentDataVersion)
            {
                return;
            }

            _impactRanges ??= new List<PhysSoundImpactRange>();
            if (_impactRanges.Count == 0 && HasValidClip(_impactClips))
            {
                CreateInitialImpactRange();
            }

            for (int i = 0; i < _impactRanges.Count; i++)
            {
                _impactRanges[i]?.MigrateLegacyClips();
            }

            MigrateLegacyImpactSource();

            _dataVersion = CurrentDataVersion;
        }

        private void MigrateLegacyImpactSource()
        {
            if (_impactSourceClip == null)
            {
                return;
            }

            if (_impactRanges.Count == 0)
            {
                CreateInitialImpactRange();
            }

            PhysSoundImpactRange range = _impactRanges[0];
            PhysSoundImpactClipSource source = null;
            for (int i = 0; i < range.ClipSources.Count; i++)
            {
                if (range.ClipSources[i]?.SourceClip == _impactSourceClip)
                {
                    source = range.ClipSources[i];
                    break;
                }
            }

            source ??= new PhysSoundImpactClipSource(_impactSourceClip);
            if (!range.ClipSources.Contains(source))
            {
                range.ClipSources.Add(source);
            }

            if (source.Regions.Count == 0 && _impactRegions != null)
            {
                source.Regions.AddRange(_impactRegions);
            }
        }

        internal AudioClip GetSlideClip()
        {
            return GetRandomClip(_slideClips);
        }

        internal float EvaluateImpactVolume(float impulse)
        {
            if (impulse < _minimumImpactImpulse)
            {
                return 0f;
            }

            float normalized = _maximumImpactImpulse <= _minimumImpactImpulse
                ? 1f
                : Mathf.InverseLerp(_minimumImpactImpulse, _maximumImpactImpulse, impulse);

            float value = _impactVolume == null ? normalized : _impactVolume.Evaluate(normalized);
            return Mathf.Max(0f, value) * _impactVolumeMultiplier;
        }

        internal float GetImpactPitch()
        {
            float min = Mathf.Min(_impactPitchRange.x, _impactPitchRange.y);
            float max = Mathf.Max(_impactPitchRange.x, _impactPitchRange.y);
            return UnityEngine.Random.Range(min, max);
        }

        internal float EvaluateSlideVolume(float speed)
        {
            if (speed < _minimumSlideSpeed)
            {
                return 0f;
            }

            float normalized = _maximumSlideSpeed <= _minimumSlideSpeed
                ? 1f
                : Mathf.InverseLerp(_minimumSlideSpeed, _maximumSlideSpeed, speed);

            float value = _slideVolume == null ? normalized : _slideVolume.Evaluate(normalized);
            return Mathf.Max(0f, value) * _slideVolumeMultiplier;
        }

        internal float EvaluateSlidePitch(float speed)
        {
            float normalized = _maximumSlideSpeed <= _minimumSlideSpeed
                ? 1f
                : Mathf.InverseLerp(_minimumSlideSpeed, _maximumSlideSpeed, speed);

            return Mathf.Lerp(_slidePitchRange.x, _slidePitchRange.y, normalized);
        }

        private static bool HasValidClip(AudioClip[] clips)
        {
            if (clips == null)
            {
                return false;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static AudioClip GetRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            int validCount = 0;

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                return null;
            }

            int selected = UnityEngine.Random.Range(0, validCount);

            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[i];

                if (clip == null)
                {
                    continue;
                }

                if (selected == 0)
                {
                    return clip;
                }

                selected--;
            }

            return null;
        }
    }

    [Serializable]
    internal struct PhysSoundInteractionKey : IEquatable<PhysSoundInteractionKey>
    {
        [SerializeField] private string _surfaceA;
        [SerializeField] private string _surfaceB;

        internal PhysSoundInteractionKey(string first, string second)
        {
            _surfaceA = first?.Trim() ?? string.Empty;
            _surfaceB = second?.Trim() ?? string.Empty;
        }

        internal string SurfaceA => _surfaceA?.Trim() ?? string.Empty;
        internal string SurfaceB => _surfaceB?.Trim() ?? string.Empty;
        internal bool HasConfiguredSurface =>
            !string.IsNullOrEmpty(SurfaceA) ||
            !string.IsNullOrEmpty(SurfaceB);
        internal bool IsDefaultFallback =>
            (string.Equals(SurfaceA, PhysSoundSettings.DefaultSurface, StringComparison.OrdinalIgnoreCase) &&
             string.IsNullOrEmpty(SurfaceB)) ||
            (string.Equals(SurfaceB, PhysSoundSettings.DefaultSurface, StringComparison.OrdinalIgnoreCase) &&
             string.IsNullOrEmpty(SurfaceA));

        internal string PreviewName => $"{SurfaceA} / {SurfaceB}";

        public bool Equals(PhysSoundInteractionKey other)
        {
            return (string.Equals(SurfaceA, other.SurfaceA, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(SurfaceB, other.SurfaceB, StringComparison.OrdinalIgnoreCase)) ||
                   (string.Equals(SurfaceA, other.SurfaceB, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(SurfaceB, other.SurfaceA, StringComparison.OrdinalIgnoreCase));
        }

        public override bool Equals(object obj)
        {
            return obj is PhysSoundInteractionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                string first = SurfaceA;
                string second = SurfaceB;

                if (string.Compare(first, second, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    (first, second) = (second, first);
                }

                return (StringComparer.OrdinalIgnoreCase.GetHashCode(first) * 397) ^
                       StringComparer.OrdinalIgnoreCase.GetHashCode(second);
            }
        }
    }

    [Serializable]
    internal sealed class PhysSoundAudioRegion
    {
        [SerializeField, Min(0f)] private float _startTime;
        [SerializeField, Min(0f)] private float _endTime;

        internal PhysSoundAudioRegion()
        {
        }

        internal PhysSoundAudioRegion(float startTime, float endTime)
        {
            _startTime = startTime;
            _endTime = endTime;
        }

        internal float StartTime { get => _startTime; set => _startTime = Mathf.Max(0f, value); }
        internal float EndTime { get => _endTime; set => _endTime = Mathf.Max(_startTime, value); }
    }

    public sealed class PhysSoundSettings : ScriptableObject
    {
        public const string DefaultSurface = "Default";
        public const string AnySurface = "";
        public const string ResourcePath = "PhysSound/PhysSoundSettings";

        [SerializeField] private PhysSoundContactBackend _contactBackend = PhysSoundContactBackend.ProvidesContacts;
        [SerializeField] private PhysSoundSubprofile[] _externalSubprofiles = Array.Empty<PhysSoundSubprofile>();
        [SerializeField] private Dictionary<string, PhysSoundSurface> _surfaces = new();
        [SerializeField] private PhysSoundInteraction _defaultInteraction = new();
        [SerializeField] private Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> _interactions = new();

        [Header("Emitter")]
        [SerializeField] private AudioSource _emitterPrefab;

        [Header("Voice Pool")]
        [SerializeField, Min(1)] private int _maximumVoices = 32;
        [SerializeField, Min(0f)] private float _minimumImpactInterval = 0.04f;
        [SerializeField, Min(0f)] private float _slideContactTimeout = 0.1f;
        [SerializeField, Min(0f)] private float _slideFadeInSpeed = 8f;
        [SerializeField, Min(0f)] private float _slideFadeOutSpeed = 12f;
        [SerializeField, Min(0f)] private float _slidePitchSpeed = 8f;
        [SerializeField, Min(0f)] private float _slidePositionSpeed = 20f;

        internal PhysSoundContactBackend ContactBackend => _contactBackend;
        internal AudioSource EmitterPrefab => _emitterPrefab;
        internal int MaximumVoices => Mathf.Max(1, _maximumVoices);
        internal float MinimumImpactInterval => Mathf.Max(0f, _minimumImpactInterval);
        internal float SlideContactTimeout => Mathf.Max(0f, _slideContactTimeout);
        internal float SlideFadeInSpeed => Mathf.Max(0f, _slideFadeInSpeed);
        internal float SlideFadeOutSpeed => Mathf.Max(0f, _slideFadeOutSpeed);
        internal float SlidePitchSpeed => Mathf.Max(0f, _slidePitchSpeed);
        internal float SlidePositionSpeed => Mathf.Max(0f, _slidePositionSpeed);
        internal PhysSoundInteraction DefaultInteraction => _defaultInteraction;
        internal Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> Interactions => _interactions;
        internal static PhysSoundSettings Load()
        {
            return Resources.Load<PhysSoundSettings>(ResourcePath);
        }

        internal void BuildLookups(
            Dictionary<PhysicsMaterial, string> surfaces,
            Dictionary<PhysSoundInteractionKey, int> interactions,
            List<PhysSoundInteraction> interactionValues)
        {
            surfaces.Clear();
            interactions.Clear();
            interactionValues.Clear();

            if (_defaultInteraction == null)
            {
                throw new InvalidOperationException("Phys Sound requires a Default Interaction.");
            }

            interactionValues.Add(_defaultInteraction);
            interactions[new PhysSoundInteractionKey(DefaultSurface, AnySurface)] = 0;

            if (_externalSubprofiles != null)
            {
                for (int i = 0; i < _externalSubprofiles.Length; i++)
                {
                    PhysSoundSubprofile subprofile = _externalSubprofiles[i];

                    if (subprofile != null)
                    {
                        AppendProfile(
                            subprofile.Surfaces,
                            subprofile.Interactions,
                            surfaces,
                            interactions,
                            interactionValues);
                    }
                }
            }

            AppendProfile(_surfaces, _interactions, surfaces, interactions, interactionValues);
        }

#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
        internal void BuildLookups2D(Dictionary<PhysicsMaterial2D, string> surfaces)
        {
            surfaces.Clear();

            if (_externalSubprofiles != null)
            {
                for (int i = 0; i < _externalSubprofiles.Length; i++)
                {
                    PhysSoundSubprofile subprofile = _externalSubprofiles[i];

                    if (subprofile != null)
                    {
                        AppendProfile2D(subprofile.Surfaces, surfaces);
                    }
                }
            }

            AppendProfile2D(_surfaces, surfaces);
        }
#endif

        internal static string NormalizeSurfaceName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? DefaultSurface : value.Trim();
        }

        private static void AppendProfile(
            Dictionary<string, PhysSoundSurface> profileSurfaces,
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> profileInteractions,
            Dictionary<PhysicsMaterial, string> surfaces,
            Dictionary<PhysSoundInteractionKey, int> interactions,
            List<PhysSoundInteraction> interactionValues)
        {
            if (profileSurfaces != null)
            {
                foreach ((string surfaceName, PhysSoundSurface surface) in profileSurfaces)
                {
                    if (string.IsNullOrWhiteSpace(surfaceName) || surface == null)
                    {
                        continue;
                    }

                    PhysicsMaterial[] materials = surface.Materials;

                    if (materials == null)
                    {
                        continue;
                    }

                    string normalizedSurfaceName = surfaceName.Trim();

                    for (int j = 0; j < materials.Length; j++)
                    {
                        PhysicsMaterial material = materials[j];

                        if (material != null)
                        {
                            surfaces[material] = normalizedSurfaceName;
                        }
                    }
                }
            }

            if (profileInteractions == null)
            {
                return;
            }

            foreach ((PhysSoundInteractionKey key, PhysSoundInteraction interaction) in profileInteractions)
            {
                if (!key.HasConfiguredSurface || key.IsDefaultFallback || interaction == null)
                {
                    continue;
                }

                if (interactions.TryGetValue(key, out int interactionIndex))
                {
                    interactionValues[interactionIndex] = interaction;
                }
                else
                {
                    interactionIndex = interactionValues.Count;
                    interactionValues.Add(interaction);
                    interactions.Add(key, interactionIndex);
                }
            }
        }

#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
        private static void AppendProfile2D(
            Dictionary<string, PhysSoundSurface> profileSurfaces,
            Dictionary<PhysicsMaterial2D, string> surfaces)
        {
            if (profileSurfaces == null)
            {
                return;
            }

            foreach ((string surfaceName, PhysSoundSurface surface) in profileSurfaces)
            {
                if (string.IsNullOrWhiteSpace(surfaceName) || surface == null)
                {
                    continue;
                }

                PhysicsMaterial2D[] materials = surface.Materials2D;

                if (materials == null)
                {
                    continue;
                }

                string normalizedSurfaceName = surfaceName.Trim();

                for (int i = 0; i < materials.Length; i++)
                {
                    PhysicsMaterial2D material = materials[i];

                    if (material != null)
                    {
                        surfaces[material] = normalizedSurfaceName;
                    }
                }
            }
        }
#endif
    }
}
#endif
