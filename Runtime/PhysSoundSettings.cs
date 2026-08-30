#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace PhysSound
{
    internal enum PhysSoundContactBackend
    {
        ProvidesContacts,
        Components
    }

    [Serializable]
    internal struct PhysSoundSurface
    {
        [SerializeField] private string _name;
        [SerializeField] private PhysicsMaterial[] _materials;

        internal string Name => PhysSoundSettings.NormalizeSurfaceName(_name);
        internal PhysicsMaterial[] Materials => _materials;
        internal bool IsUninitialized => string.IsNullOrEmpty(_name) && _materials == null;

        internal static PhysSoundSurface CreateDefault()
        {
            return new PhysSoundSurface
            {
                _name = PhysSoundSettings.DefaultSurface,
                _materials = Array.Empty<PhysicsMaterial>()
            };
        }

        internal void EnsureDefaults()
        {
            if (_materials == null)
            {
                _materials = Array.Empty<PhysicsMaterial>();
            }
        }
    }

    [Serializable]
    internal struct PhysSoundInteraction
    {
        [SerializeField] private string _surfaceA;
        [SerializeField] private string _surfaceB;

        [Header("Impact")]
        [SerializeField] private AudioClip[] _impactClips;
        [SerializeField] private AnimationCurve _impactVolume;
        [SerializeField, Min(0f)] private float _minimumImpactImpulse;
        [SerializeField, Min(0f)] private float _maximumImpactImpulse;
        [SerializeField, Min(0f)] private float _impactVolumeMultiplier;
        [SerializeField] private Vector2 _impactPitchRange;

        [Header("Slide")]
        [SerializeField] private AudioClip[] _slideClips;
        [SerializeField] private AnimationCurve _slideVolume;
        [SerializeField, Min(0f)] private float _minimumSlideSpeed;
        [SerializeField, Min(0f)] private float _maximumSlideSpeed;
        [SerializeField, Min(0f)] private float _slideVolumeMultiplier;
        [SerializeField] private Vector2 _slidePitchRange;

        internal string SurfaceA => PhysSoundSettings.NormalizeSurfaceName(_surfaceA);
        internal string SurfaceB => string.IsNullOrWhiteSpace(_surfaceB)
            ? PhysSoundSettings.AnySurface
            : _surfaceB.Trim();

        internal bool HasSlide => HasValidClip(_slideClips);

        internal bool IsUninitialized =>
            string.IsNullOrEmpty(_surfaceA) &&
            string.IsNullOrEmpty(_surfaceB) &&
            _impactClips == null &&
            _impactVolume == null &&
            _minimumImpactImpulse == 0f &&
            _maximumImpactImpulse == 0f &&
            _impactVolumeMultiplier == 0f &&
            _impactPitchRange == Vector2.zero &&
            _slideClips == null &&
            _slideVolume == null &&
            _minimumSlideSpeed == 0f &&
            _maximumSlideSpeed == 0f &&
            _slideVolumeMultiplier == 0f &&
            _slidePitchRange == Vector2.zero;

        internal static PhysSoundInteraction CreateDefault()
        {
            return new PhysSoundInteraction
            {
                _surfaceA = PhysSoundSettings.DefaultSurface,
                _surfaceB = PhysSoundSettings.AnySurface,
                _impactClips = Array.Empty<AudioClip>(),
                _impactVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f),
                _minimumImpactImpulse = 0.1f,
                _maximumImpactImpulse = 10f,
                _impactVolumeMultiplier = 1f,
                _impactPitchRange = new Vector2(0.95f, 1.05f),
                _slideClips = Array.Empty<AudioClip>(),
                _slideVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f),
                _minimumSlideSpeed = 0.05f,
                _maximumSlideSpeed = 5f,
                _slideVolumeMultiplier = 1f,
                _slidePitchRange = new Vector2(0.9f, 1.2f)
            };
        }

        internal void EnsureDefaults()
        {
            if (_impactClips == null)
            {
                _impactClips = Array.Empty<AudioClip>();
            }

            if (_slideClips == null)
            {
                _slideClips = Array.Empty<AudioClip>();
            }

            if (_impactVolume == null)
            {
                _impactVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            }

            if (_slideVolume == null)
            {
                _slideVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            }
        }

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

    public sealed class PhysSoundSettings : ScriptableObject
    {
        public const string DefaultSurface = "Default";
        public const string AnySurface = "*";
        public const string ResourcePath = "PhysSound/PhysSoundSettings";

        [SerializeField] private PhysSoundContactBackend _contactBackend = PhysSoundContactBackend.ProvidesContacts;
        [SerializeField] private List<PhysSoundSurface> _surfaces =
            new List<PhysSoundSurface> { PhysSoundSurface.CreateDefault() };
        [SerializeField] private List<PhysSoundInteraction> _interactions =
            new List<PhysSoundInteraction> { PhysSoundInteraction.CreateDefault() };

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

        internal PhysSoundInteraction GetInteraction(int index)
        {
            return _interactions[index];
        }

        internal void BuildLookups(
            Dictionary<PhysicsMaterial, string> surfaces,
            Dictionary<PhysSoundInteractionKey, int> interactions)
        {
            surfaces.Clear();
            interactions.Clear();

            if (_surfaces != null)
            {
                for (int i = 0; i < _surfaces.Count; i++)
                {
                    PhysSoundSurface surface = _surfaces[i];
                    PhysicsMaterial[] materials = surface.Materials;

                    if (materials == null)
                    {
                        continue;
                    }

                    string surfaceName = surface.Name;

                    for (int j = 0; j < materials.Length; j++)
                    {
                        PhysicsMaterial material = materials[j];

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
                interactions[new PhysSoundInteractionKey(interaction.SurfaceA, interaction.SurfaceB)] = i;
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

            if (_surfaces == null)
            {
                _surfaces = new List<PhysSoundSurface>();
            }

            for (int i = 0; i < _surfaces.Count; i++)
            {
                PhysSoundSurface surface = _surfaces[i];
                surface = surface.IsUninitialized ? PhysSoundSurface.CreateDefault() : surface;
                surface.EnsureDefaults();
                _surfaces[i] = surface;
            }

            if (_interactions == null)
            {
                _interactions = new List<PhysSoundInteraction>();
            }

            for (int i = 0; i < _interactions.Count; i++)
            {
                PhysSoundInteraction interaction = _interactions[i];
                interaction = interaction.IsUninitialized
                    ? PhysSoundInteraction.CreateDefault()
                    : interaction;
                interaction.EnsureDefaults();
                _interactions[i] = interaction;
            }
        }
    }
}
#endif
