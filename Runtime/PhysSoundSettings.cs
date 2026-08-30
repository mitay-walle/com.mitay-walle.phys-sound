#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace PhysSound
{
    public enum PhysSoundContactBackend
    {
        ProvidesContacts,
        Components
    }

    [Serializable]
    public sealed class PhysSoundSurface
    {
        [SerializeField] private string _name = "Surface";
        [SerializeField] private PhysicsMaterial[] _materials = Array.Empty<PhysicsMaterial>();

        internal string Name => string.IsNullOrWhiteSpace(_name) ? PhysSoundSettings.DefaultSurface : _name.Trim();
        internal PhysicsMaterial[] Materials => _materials;
    }

    [Serializable]
    public sealed class PhysSoundInteraction
    {
        [SerializeField] private string _surfaceA = PhysSoundSettings.DefaultSurface;
        [SerializeField] private string _surfaceB = PhysSoundSettings.AnySurface;

        [Header("Impact")]
        [SerializeField] private AudioClip[] _impactClips = Array.Empty<AudioClip>();
        [SerializeField] private AnimationCurve _impactVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField, Min(0f)] private float _minimumImpactImpulse = 0.1f;
        [SerializeField, Min(0f)] private float _maximumImpactImpulse = 10f;
        [SerializeField, Min(0f)] private float _impactVolumeMultiplier = 1f;
        [SerializeField] private Vector2 _impactPitchRange = new Vector2(0.95f, 1.05f);

        [Header("Slide")]
        [SerializeField] private AudioClip[] _slideClips = Array.Empty<AudioClip>();
        [SerializeField] private AnimationCurve _slideVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField, Min(0f)] private float _minimumSlideSpeed = 0.05f;
        [SerializeField, Min(0f)] private float _maximumSlideSpeed = 5f;
        [SerializeField, Min(0f)] private float _slideVolumeMultiplier = 1f;
        [SerializeField] private Vector2 _slidePitchRange = new Vector2(0.9f, 1.2f);

        internal string SurfaceA => _surfaceA;
        internal string SurfaceB => _surfaceB;
        internal bool HasSlide => HasValidClip(_slideClips);

        internal AudioClip GetImpactClip()
        {
            return GetRandomClip(_impactClips);
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

    public sealed class PhysSoundSettings : ScriptableObject
    {
        public const string DefaultSurface = "Default";
        public const string AnySurface = "*";
        public const string ResourcePath = "PhysSound/PhysSoundSettings";

        [SerializeField] private PhysSoundContactBackend _contactBackend = PhysSoundContactBackend.ProvidesContacts;
        [SerializeField] private List<PhysSoundSurface> _surfaces = new List<PhysSoundSurface>();
        [SerializeField] private List<PhysSoundInteraction> _interactions = new List<PhysSoundInteraction>();

        [Header("Voice Pool")]
        [SerializeField, Min(1)] private int _maximumVoices = 32;
        [SerializeField, Min(0f)] private float _minimumImpactInterval = 0.04f;
        [SerializeField, Min(0f)] private float _slideContactTimeout = 0.1f;
        [SerializeField, Min(0f)] private float _slideFadeInSpeed = 8f;
        [SerializeField, Min(0f)] private float _slideFadeOutSpeed = 12f;
        [SerializeField, Min(0f)] private float _slidePitchSpeed = 8f;
        [SerializeField, Min(0f)] private float _slidePositionSpeed = 20f;

        [Header("Spatial Audio")]
        [SerializeField] private AudioMixerGroup _output;
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 1f;
        [SerializeField] private AudioRolloffMode _rolloffMode = AudioRolloffMode.Logarithmic;
        [SerializeField, Min(0f)] private float _minimumDistance = 1f;
        [SerializeField, Min(0f)] private float _maximumDistance = 40f;
        [SerializeField, Range(0f, 5f)] private float _dopplerLevel;
        [SerializeField, Range(0f, 360f)] private float _spread;
        [SerializeField, Range(0, 256)] private int _priority = 128;
        [SerializeField, Range(0f, 1.1f)] private float _reverbZoneMix = 1f;

        internal PhysSoundContactBackend ContactBackend => _contactBackend;
        internal int MaximumVoices => Mathf.Max(1, _maximumVoices);
        internal float MinimumImpactInterval => Mathf.Max(0f, _minimumImpactInterval);
        internal float SlideContactTimeout => Mathf.Max(0f, _slideContactTimeout);
        internal float SlideFadeInSpeed => Mathf.Max(0f, _slideFadeInSpeed);
        internal float SlideFadeOutSpeed => Mathf.Max(0f, _slideFadeOutSpeed);
        internal float SlidePitchSpeed => Mathf.Max(0f, _slidePitchSpeed);
        internal float SlidePositionSpeed => Mathf.Max(0f, _slidePositionSpeed);
        internal AudioMixerGroup Output => _output;
        internal float SpatialBlend => _spatialBlend;
        internal AudioRolloffMode RolloffMode => _rolloffMode;
        internal float MinimumDistance => Mathf.Max(0f, _minimumDistance);
        internal float MaximumDistance => Mathf.Max(MinimumDistance, _maximumDistance);
        internal float DopplerLevel => _dopplerLevel;
        internal float Spread => _spread;
        internal int Priority => Mathf.Clamp(_priority, 0, 256);
        internal float ReverbZoneMix => _reverbZoneMix;

        internal static PhysSoundSettings Load()
        {
            return Resources.Load<PhysSoundSettings>(ResourcePath);
        }

        internal void BuildLookups(
            Dictionary<PhysicsMaterial, string> surfaces,
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions)
        {
            surfaces.Clear();
            interactions.Clear();

            if (_surfaces != null)
            {
                for (int i = 0; i < _surfaces.Count; i++)
                {
                    PhysSoundSurface surface = _surfaces[i];

                    if (surface == null || surface.Materials == null)
                    {
                        continue;
                    }

                    string surfaceName = NormalizeSurfaceName(surface.Name);

                    for (int j = 0; j < surface.Materials.Length; j++)
                    {
                        PhysicsMaterial material = surface.Materials[j];

                        if (material != null)
                        {
                            surfaces[material] = surfaceName;
                        }
                    }
                }
            }

            if (_interactions == null)
            {
                return;
            }

            for (int i = 0; i < _interactions.Count; i++)
            {
                PhysSoundInteraction interaction = _interactions[i];

                if (interaction == null)
                {
                    continue;
                }

                interactions[new PhysSoundInteractionKey(interaction.SurfaceA, interaction.SurfaceB)] = interaction;
            }
        }

        internal static string NormalizeSurfaceName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? DefaultSurface : value.Trim();
        }

        private void OnValidate()
        {
            _maximumVoices = Mathf.Max(1, _maximumVoices);
            _minimumDistance = Mathf.Max(0f, _minimumDistance);
            _maximumDistance = Mathf.Max(_minimumDistance, _maximumDistance);
            _priority = Mathf.Clamp(_priority, 0, 256);
        }
    }

    internal readonly struct PhysSoundInteractionKey : IEquatable<PhysSoundInteractionKey>
    {
        private readonly string _first;
        private readonly string _second;

        internal PhysSoundInteractionKey(string first, string second)
        {
            first = PhysSoundSettings.NormalizeSurfaceName(first);
            second = PhysSoundSettings.NormalizeSurfaceName(second);

            if (string.Compare(first, second, StringComparison.OrdinalIgnoreCase) <= 0)
            {
                _first = first;
                _second = second;
            }
            else
            {
                _first = second;
                _second = first;
            }
        }

        public bool Equals(PhysSoundInteractionKey other)
        {
            return string.Equals(_first, other._first, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(_second, other._second, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is PhysSoundInteractionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int firstHash = StringComparer.OrdinalIgnoreCase.GetHashCode(_first ?? string.Empty);
                int secondHash = StringComparer.OrdinalIgnoreCase.GetHashCode(_second ?? string.Empty);
                return (firstHash * 397) ^ secondHash;
            }
        }
    }
}
#endif
