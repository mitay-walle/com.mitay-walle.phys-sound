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

    public sealed class PhysSoundSettings : ScriptableObject
    {
        public const string ResourcePath = "PhysSound/PhysSoundSettings";
        public const string AssetPath = "Assets/Resources/PhysSound/PhysSoundSettings.asset";

        [Serializable]
        public sealed class Surface
        {
            [SerializeField] private string _name = "Surface";
            [SerializeField] private PhysicsMaterial[] _physicsMaterials = Array.Empty<PhysicsMaterial>();

            public string Name => _name;
            internal PhysicsMaterial[] PhysicsMaterials => _physicsMaterials;

            internal void EnsureDefaultName()
            {
                if (string.IsNullOrWhiteSpace(_name))
                {
                    _name = "Default";
                }
            }
        }

        [Serializable]
        public sealed class Interaction
        {
            [SerializeField, Min(0)] private int _surfaceA;
            [SerializeField, Min(0)] private int _surfaceB;

            [Header("Impact")]
            [SerializeField] private AudioClip[] _impactClips = Array.Empty<AudioClip>();
            [SerializeField, Min(0f)] private float _minimumImpactImpulse = 0.25f;
            [SerializeField, Min(0f)] private float _maximumImpactImpulse = 12f;
            [SerializeField] private AnimationCurve _impactVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            [SerializeField] private Vector2 _impactPitch = new Vector2(0.94f, 1.06f);

            [Header("Slide")]
            [SerializeField] private AudioClip _slideClip;
            [SerializeField, Min(0f)] private float _minimumSlideSpeed = 0.05f;
            [SerializeField, Min(0f)] private float _maximumSlideSpeed = 5f;
            [SerializeField] private AnimationCurve _slideVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            [SerializeField] private Vector2 _slidePitch = new Vector2(0.85f, 1.35f);

            internal int SurfaceA => _surfaceA;
            internal int SurfaceB => _surfaceB;
            internal bool HasImpact
            {
                get
                {
                    if (_impactClips == null)
                    {
                        return false;
                    }

                    for (int i = 0; i < _impactClips.Length; i++)
                    {
                        if (_impactClips[i] != null)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            internal bool HasSlide => _slideClip != null;
            internal AudioClip SlideClip => _slideClip;

            internal AudioClip GetImpactClip()
            {
                if (_impactClips == null || _impactClips.Length == 0)
                {
                    return null;
                }

                int start = UnityEngine.Random.Range(0, _impactClips.Length);
                for (int offset = 0; offset < _impactClips.Length; offset++)
                {
                    AudioClip clip = _impactClips[(start + offset) % _impactClips.Length];
                    if (clip != null)
                    {
                        return clip;
                    }
                }

                return null;
            }

            internal float EvaluateImpactVolume(float impulse)
            {
                float normalized = Mathf.InverseLerp(_minimumImpactImpulse, Mathf.Max(_minimumImpactImpulse, _maximumImpactImpulse), impulse);
                return Mathf.Max(0f, _impactVolume == null ? normalized : _impactVolume.Evaluate(normalized));
            }

            internal float GetImpactPitch()
            {
                float minimum = Mathf.Min(_impactPitch.x, _impactPitch.y);
                float maximum = Mathf.Max(_impactPitch.x, _impactPitch.y);
                return UnityEngine.Random.Range(minimum, maximum);
            }

            internal float EvaluateSlideVolume(float speed)
            {
                float normalized = Mathf.InverseLerp(_minimumSlideSpeed, Mathf.Max(_minimumSlideSpeed, _maximumSlideSpeed), speed);
                return Mathf.Max(0f, _slideVolume == null ? normalized : _slideVolume.Evaluate(normalized));
            }

            internal float EvaluateSlidePitch(float speed)
            {
                float normalized = Mathf.InverseLerp(_minimumSlideSpeed, Mathf.Max(_minimumSlideSpeed, _maximumSlideSpeed), speed);
                return Mathf.Lerp(_slidePitch.x, _slidePitch.y, normalized);
            }

            internal void ClampSurfaceIndexes(int surfaceCount)
            {
                int maximum = Mathf.Max(0, surfaceCount - 1);
                _surfaceA = Mathf.Clamp(_surfaceA, 0, maximum);
                _surfaceB = Mathf.Clamp(_surfaceB, 0, maximum);
                _maximumImpactImpulse = Mathf.Max(_minimumImpactImpulse, _maximumImpactImpulse);
                _maximumSlideSpeed = Mathf.Max(_minimumSlideSpeed, _maximumSlideSpeed);
            }
        }

        [Header("Contact Collection")]
        [SerializeField] private PhysSoundContactBackend _contactBackend = PhysSoundContactBackend.ProvidesContacts;

        [Header("Authoring")]
        [SerializeField] private List<Surface> _surfaces = new List<Surface> { new Surface() };
        [SerializeField] private List<Interaction> _interactions = new List<Interaction>();

        [Header("Voices")]
        [SerializeField, Range(1, 256)] private int _maximumVoices = 32;
        [SerializeField, Range(0, 64)] private int _maximumSlideVoices = 8;
        [SerializeField, Min(0f)] private float _impactCooldown = 0.035f;
        [SerializeField, Min(0f)] private float _masterVolume = 1f;
        [SerializeField, Min(0f)] private float _slideAttack = 8f;
        [SerializeField, Min(0f)] private float _slideRelease = 5f;
        [SerializeField, Min(0f)] private float _slidePositionSmoothing = 20f;

        [Header("Spatial Audio")]
        [SerializeField] private AudioMixerGroup _output;
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 1f;
        [SerializeField] private bool _spatialize;
        [SerializeField, Range(0f, 5f)] private float _dopplerLevel;
        [SerializeField, Range(0f, 360f)] private float _spread;
        [SerializeField] private AudioRolloffMode _rolloffMode = AudioRolloffMode.Logarithmic;
        [SerializeField, Min(0f)] private float _minimumDistance = 1f;
        [SerializeField, Min(0f)] private float _maximumDistance = 50f;
        [SerializeField, Range(0, 256)] private int _priority = 128;

        [NonSerialized] private Dictionary<int, int> _surfaceByMaterial;
        [NonSerialized] private Dictionary<ulong, Interaction> _interactionByPair;

        public PhysSoundContactBackend ContactBackend => _contactBackend;

        internal int MaximumVoices => Mathf.Max(1, _maximumVoices);
        internal int MaximumSlideVoices => Mathf.Clamp(_maximumSlideVoices, 0, MaximumVoices);
        internal float ImpactCooldown => Mathf.Max(0f, _impactCooldown);
        internal float MasterVolume => Mathf.Max(0f, _masterVolume);
        internal float SlideAttack => Mathf.Max(0f, _slideAttack);
        internal float SlideRelease => Mathf.Max(0f, _slideRelease);
        internal float SlidePositionSmoothing => Mathf.Max(0f, _slidePositionSmoothing);

        private void OnEnable()
        {
            ValidateAndRebuild();
        }

        private void OnValidate()
        {
            ValidateAndRebuild();
        }

        internal bool TryGetInteraction(PhysicsMaterial first, PhysicsMaterial second, out Interaction interaction)
        {
            EnsureLookup();

            int firstSurface = ResolveSurface(first);
            int secondSurface = ResolveSurface(second);

            if (_interactionByPair.TryGetValue(GetPairKey(firstSurface, secondSurface), out interaction))
            {
                return true;
            }

            if ((firstSurface != 0 || secondSurface != 0) &&
                _interactionByPair.TryGetValue(GetPairKey(0, 0), out interaction))
            {
                return true;
            }

            interaction = null;
            return false;
        }

        internal void ApplyTo(AudioSource source)
        {
            source.playOnAwake = false;
            source.outputAudioMixerGroup = _output;
            source.spatialBlend = _spatialBlend;
            source.spatialize = _spatialize;
            source.dopplerLevel = _dopplerLevel;
            source.spread = _spread;
            source.rolloffMode = _rolloffMode;
            source.minDistance = Mathf.Max(0f, _minimumDistance);
            source.maxDistance = Mathf.Max(source.minDistance, _maximumDistance);
            source.priority = Mathf.Clamp(_priority, 0, 256);
            source.velocityUpdateMode = AudioVelocityUpdateMode.Fixed;
        }

        private void ValidateAndRebuild()
        {
            if (_surfaces == null)
            {
                _surfaces = new List<Surface>();
            }

            if (_surfaces.Count == 0)
            {
                _surfaces.Add(new Surface());
            }

            if (_surfaces[0] == null)
            {
                _surfaces[0] = new Surface();
            }

            _surfaces[0].EnsureDefaultName();

            if (_interactions == null)
            {
                _interactions = new List<Interaction>();
            }

            for (int i = 0; i < _interactions.Count; i++)
            {
                _interactions[i]?.ClampSurfaceIndexes(_surfaces.Count);
            }

            _maximumVoices = Mathf.Max(1, _maximumVoices);
            _maximumSlideVoices = Mathf.Clamp(_maximumSlideVoices, 0, _maximumVoices);
            _maximumDistance = Mathf.Max(_minimumDistance, _maximumDistance);

            RebuildLookup();
        }

        private void EnsureLookup()
        {
            if (_surfaceByMaterial == null || _interactionByPair == null)
            {
                RebuildLookup();
            }
        }

        private void RebuildLookup()
        {
            _surfaceByMaterial = new Dictionary<int, int>();
            _interactionByPair = new Dictionary<ulong, Interaction>();

            if (_surfaces != null)
            {
                for (int surfaceIndex = 0; surfaceIndex < _surfaces.Count; surfaceIndex++)
                {
                    Surface surface = _surfaces[surfaceIndex];
                    if (surface?.PhysicsMaterials == null)
                    {
                        continue;
                    }

                    PhysicsMaterial[] materials = surface.PhysicsMaterials;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        PhysicsMaterial material = materials[materialIndex];
                        if (material == null)
                        {
                            continue;
                        }

                        int id = material.GetInstanceID();
                        if (!_surfaceByMaterial.ContainsKey(id))
                        {
                            _surfaceByMaterial.Add(id, surfaceIndex);
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
                Interaction interaction = _interactions[i];
                if (interaction == null)
                {
                    continue;
                }

                ulong key = GetPairKey(interaction.SurfaceA, interaction.SurfaceB);
                if (!_interactionByPair.ContainsKey(key))
                {
                    _interactionByPair.Add(key, interaction);
                }
            }
        }

        private int ResolveSurface(PhysicsMaterial material)
        {
            if (material == null)
            {
                return 0;
            }

            return _surfaceByMaterial.TryGetValue(material.GetInstanceID(), out int surface) ? surface : 0;
        }

        private static ulong GetPairKey(int first, int second)
        {
            int minimum = Mathf.Min(first, second);
            int maximum = Mathf.Max(first, second);
            return ((ulong)(uint)minimum << 32) | (uint)maximum;
        }
    }
}
#endif
