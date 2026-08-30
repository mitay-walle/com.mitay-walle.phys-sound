#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace PhysSound
{
    internal enum PhysSoundEmitterMode : byte
    {
        Free,
        Impact,
        Slide
    }

    internal struct PhysSoundContactData
    {
        internal Collider FirstCollider;
        internal Collider SecondCollider;
        internal Vector3 Position;
        internal Vector3 Normal;
        internal Vector3 RelativeVelocity;
        internal float Impulse;
    }

    internal struct PhysSoundEmitter
    {
        internal AudioSource Source;
        internal PhysSoundEmitterMode Mode;
        internal ulong PairKey;
        internal int InteractionIndex;
        internal Vector3 TargetPosition;
        internal float TargetVolume;
        internal float TargetPitch;
        internal float LastSeenAt;
        internal bool Stopping;
    }

    [AddComponentMenu("")]
    internal sealed class PhysSoundRuntime : MonoBehaviour
    {
        private static PhysSoundRuntime _instance;
        private static bool _missingSettingsWarningShown;

        private readonly Dictionary<PhysicsMaterial, string> _surfaces =
            new Dictionary<PhysicsMaterial, string>();

        private readonly Dictionary<PhysSoundInteractionKey, int> _interactions =
            new Dictionary<PhysSoundInteractionKey, int>();

        private readonly HashSet<ulong> _componentReportedPairs =
            new HashSet<ulong>();

        private readonly Dictionary<ulong, int> _componentContinuousOwners =
            new Dictionary<ulong, int>();

        private readonly Dictionary<ulong, float> _lastImpactTimes =
            new Dictionary<ulong, float>();

        private readonly Dictionary<ulong, int> _slides =
            new Dictionary<ulong, int>();

        private PhysSoundSettings _settings;
        private PhysSoundEmitter[] _emitters;
        private int _emitterCount;
        private int _componentReportFrame = -1;
        private bool _contactEventSubscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (_instance != null && _instance._contactEventSubscribed)
            {
                Physics.ContactEvent -= _instance.OnContactEvent;
            }

            _instance = null;
            _missingSettingsWarningShown = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            EnsureInitialized();
        }

        internal static bool ReportComponentEnter(
            PhysSoundObject owner,
            Collision collision,
            out ulong pairKey)
        {
            pairKey = 0;

            if (!EnsureInitialized() ||
                _instance._settings.ContactBackend != PhysSoundContactBackend.Components ||
                owner == null ||
                collision == null)
            {
                return false;
            }

            return _instance.ProcessComponentEnter(
                owner.GetInstanceID(),
                collision,
                out pairKey);
        }

        internal static void ReportComponentStay(
            int ownerId,
            ulong pairKey,
            Collision collision)
        {
            if (!EnsureInitialized() ||
                _instance._settings.ContactBackend != PhysSoundContactBackend.Components)
            {
                return;
            }

            _instance.ProcessComponentStay(ownerId, pairKey, collision);
        }

        internal static void ReportComponentExit(int ownerId, ulong pairKey)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.ProcessComponentExit(ownerId, pairKey);
        }

        internal static void ReportComponentDisabled(
            int ownerId,
            IEnumerable<ulong> pairKeys)
        {
            if (_instance == null || pairKeys == null)
            {
                return;
            }

            foreach (ulong pairKey in pairKeys)
            {
                _instance.ProcessComponentExit(ownerId, pairKey);
            }
        }

        internal static bool TryGetPairKey(
            Collision collision,
            out ulong pairKey)
        {
            pairKey = 0;

            if (collision == null || collision.contactCount == 0)
            {
                return false;
            }

            ContactPoint contact = collision.GetContact(0);

            if (contact.thisCollider == null || contact.otherCollider == null)
            {
                return false;
            }

            pairKey = GetPairKey(contact.thisCollider, contact.otherCollider);
            return true;
        }

        private static bool EnsureInitialized()
        {
            if (_instance != null)
            {
                return true;
            }

            PhysSoundSettings settings = PhysSoundSettings.Load();

            if (settings == null)
            {
                if (!_missingSettingsWarningShown)
                {
                    Debug.LogWarning(
                        "Phys Sound settings were not found. Open Project Settings > Phys Sound and create them.");
                    _missingSettingsWarningShown = true;
                }

                return false;
            }

            GameObject runtimeObject = new GameObject("Phys Sound");
            runtimeObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(runtimeObject);

            _instance = runtimeObject.AddComponent<PhysSoundRuntime>();
            _instance.Initialize(settings);
            return true;
        }

        private void Initialize(PhysSoundSettings settings)
        {
            _settings = settings;
            _emitters = new PhysSoundEmitter[settings.MaximumVoices];
            settings.BuildLookups(_surfaces, _interactions);

            if (settings.ContactBackend == PhysSoundContactBackend.ProvidesContacts)
            {
                Physics.ContactEvent += OnContactEvent;
                _contactEventSubscribed = true;
            }
        }

        private bool ProcessComponentEnter(
            int ownerId,
            Collision collision,
            out ulong pairKey)
        {
            pairKey = 0;

            if (!TryReadCollision(collision, out PhysSoundContactData contact))
            {
                return false;
            }

            pairKey = GetPairKey(contact.FirstCollider, contact.SecondCollider);

            if (!TryResolveInteraction(
                    contact.FirstCollider,
                    contact.SecondCollider,
                    out int interactionIndex))
            {
                return false;
            }

            if (_componentReportFrame != Time.frameCount)
            {
                _componentReportFrame = Time.frameCount;
                _componentReportedPairs.Clear();
            }

            if (_componentReportedPairs.Add(pairKey))
            {
                PlayImpact(pairKey, interactionIndex, contact.Position, contact.Impulse);
            }

            PhysSoundInteraction interaction = _settings.GetInteraction(interactionIndex);

            if (!interaction.HasSlide)
            {
                return false;
            }

            if (!_componentContinuousOwners.TryGetValue(pairKey, out int existingOwner))
            {
                _componentContinuousOwners.Add(pairKey, ownerId);
                return true;
            }

            return existingOwner == ownerId;
        }

        private void ProcessComponentStay(
            int ownerId,
            ulong pairKey,
            Collision collision)
        {
            if (collision == null ||
                !_componentContinuousOwners.TryGetValue(pairKey, out int registeredOwner) ||
                registeredOwner != ownerId ||
                !TryReadCollision(collision, out PhysSoundContactData contact) ||
                !TryResolveInteraction(
                    contact.FirstCollider,
                    contact.SecondCollider,
                    out int interactionIndex))
            {
                return;
            }

            PhysSoundInteraction interaction = _settings.GetInteraction(interactionIndex);

            if (!interaction.HasSlide)
            {
                return;
            }

            float slideSpeed = GetTangentialSpeed(
                contact.RelativeVelocity,
                contact.Normal);

            UpdateSlide(pairKey, interactionIndex, contact.Position, slideSpeed);
        }

        private void ProcessComponentExit(int ownerId, ulong pairKey)
        {
            if (_componentContinuousOwners.TryGetValue(pairKey, out int registeredOwner) &&
                registeredOwner == ownerId)
            {
                _componentContinuousOwners.Remove(pairKey);
                StopSlide(pairKey);
            }
        }

        private void OnContactEvent(
            PhysicsScene physicsScene,
            NativeArray<ContactPairHeader>.ReadOnly headers)
        {
            if (_settings.ContactBackend != PhysSoundContactBackend.ProvidesContacts)
            {
                return;
            }

            for (int headerIndex = 0; headerIndex < headers.Length; headerIndex++)
            {
                ContactPairHeader header = headers[headerIndex];

                for (int pairIndex = 0; pairIndex < header.pairCount; pairIndex++)
                {
                    ContactPair pair = header.GetContactPair(pairIndex);
                    Collider firstCollider = pair.collider;
                    Collider secondCollider = pair.otherCollider;

                    if (firstCollider == null ||
                        secondCollider == null ||
                        (!firstCollider.providesContacts && !secondCollider.providesContacts))
                    {
                        continue;
                    }

                    ulong pairKey = GetPairKey(firstCollider, secondCollider);

                    if (pair.isCollisionExit)
                    {
                        StopSlide(pairKey);
                        continue;
                    }

                    if (!TryReadContactPair(
                            header,
                            pair,
                            firstCollider,
                            secondCollider,
                            out PhysSoundContactData contact) ||
                        !TryResolveInteraction(
                            firstCollider,
                            secondCollider,
                            out int interactionIndex))
                    {
                        continue;
                    }

                    if (pair.isCollisionEnter)
                    {
                        PlayImpact(
                            pairKey,
                            interactionIndex,
                            contact.Position,
                            contact.Impulse);
                    }

                    PhysSoundInteraction interaction = _settings.GetInteraction(interactionIndex);

                    if (pair.isCollisionStay && interaction.HasSlide)
                    {
                        float slideSpeed = GetTangentialSpeed(
                            contact.RelativeVelocity,
                            contact.Normal);

                        UpdateSlide(
                            pairKey,
                            interactionIndex,
                            contact.Position,
                            slideSpeed);
                    }
                }
            }
        }

        private void PlayImpact(
            ulong pairKey,
            int interactionIndex,
            Vector3 position,
            float impulse)
        {
            float now = Time.unscaledTime;

            if (_lastImpactTimes.TryGetValue(pairKey, out float previousTime) &&
                now - previousTime < _settings.MinimumImpactInterval)
            {
                return;
            }

            PhysSoundInteraction interaction = _settings.GetInteraction(interactionIndex);
            AudioClip clip = interaction.GetImpactClip();
            float volume = interaction.EvaluateImpactVolume(impulse);

            if (clip == null || volume <= 0f)
            {
                return;
            }

            _lastImpactTimes[pairKey] = now;

            int emitterIndex = AcquireEmitter();
            PrepareEmitter(emitterIndex, PhysSoundEmitterMode.Impact, position);

            ref PhysSoundEmitter emitter = ref _emitters[emitterIndex];
            emitter.Source.loop = false;
            emitter.Source.resource = clip;
            emitter.Source.volume = volume;
            emitter.Source.pitch = interaction.GetImpactPitch();
            emitter.Source.Play();
        }

        private void UpdateSlide(
            ulong pairKey,
            int interactionIndex,
            Vector3 position,
            float speed)
        {
            PhysSoundInteraction interaction = _settings.GetInteraction(interactionIndex);
            float targetVolume = interaction.EvaluateSlideVolume(speed);

            if (!_slides.TryGetValue(pairKey, out int emitterIndex))
            {
                if (targetVolume <= 0f)
                {
                    return;
                }

                AudioClip clip = interaction.GetSlideClip();

                if (clip == null)
                {
                    return;
                }

                emitterIndex = AcquireEmitter();
                PrepareEmitter(emitterIndex, PhysSoundEmitterMode.Slide, position);

                ref PhysSoundEmitter created = ref _emitters[emitterIndex];
                created.PairKey = pairKey;
                created.InteractionIndex = interactionIndex;
                created.Source.loop = true;
                created.Source.resource = clip;
                created.Source.volume = 0f;
                created.Source.pitch = interaction.EvaluateSlidePitch(speed);
                created.Source.Play();

                _slides.Add(pairKey, emitterIndex);
            }
            else
            {
                ref PhysSoundEmitter existing = ref _emitters[emitterIndex];

                if (existing.InteractionIndex != interactionIndex)
                {
                    AudioClip clip = interaction.GetSlideClip();

                    if (clip == null)
                    {
                        StopSlide(pairKey);
                        return;
                    }

                    existing.InteractionIndex = interactionIndex;
                    existing.Source.Stop();
                    existing.Source.resource = clip;
                    existing.Source.loop = true;
                    existing.Source.Play();
                }
            }

            ref PhysSoundEmitter emitter = ref _emitters[emitterIndex];
            emitter.TargetPosition = position;
            emitter.TargetVolume = targetVolume;
            emitter.TargetPitch = interaction.EvaluateSlidePitch(speed);
            emitter.LastSeenAt = Time.unscaledTime;
            emitter.Stopping = false;
        }

        private void StopSlide(ulong pairKey)
        {
            if (_slides.TryGetValue(pairKey, out int emitterIndex))
            {
                ref PhysSoundEmitter emitter = ref _emitters[emitterIndex];
                emitter.TargetVolume = 0f;
                emitter.Stopping = true;
            }
        }

        private void Update()
        {
            if (_settings == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            float deltaTime = Time.unscaledDeltaTime;

            for (int i = 0; i < _emitterCount; i++)
            {
                ref PhysSoundEmitter emitter = ref _emitters[i];

                if (emitter.Mode == PhysSoundEmitterMode.Free)
                {
                    continue;
                }

                if (emitter.Mode == PhysSoundEmitterMode.Impact)
                {
                    if (!emitter.Source.isPlaying)
                    {
                        ReleaseEmitter(i);
                    }

                    continue;
                }

                if (now - emitter.LastSeenAt > _settings.SlideContactTimeout)
                {
                    emitter.TargetVolume = 0f;
                    emitter.Stopping = true;
                }

                float volumeSpeed = emitter.TargetVolume > emitter.Source.volume
                    ? _settings.SlideFadeInSpeed
                    : _settings.SlideFadeOutSpeed;

                emitter.Source.volume = Mathf.MoveTowards(
                    emitter.Source.volume,
                    emitter.TargetVolume,
                    volumeSpeed * deltaTime);

                emitter.Source.pitch = Mathf.MoveTowards(
                    emitter.Source.pitch,
                    emitter.TargetPitch,
                    _settings.SlidePitchSpeed * deltaTime);

                float positionFactor = _settings.SlidePositionSpeed <= 0f
                    ? 1f
                    : 1f - Mathf.Exp(-_settings.SlidePositionSpeed * deltaTime);

                emitter.Source.transform.position = Vector3.Lerp(
                    emitter.Source.transform.position,
                    emitter.TargetPosition,
                    positionFactor);

                if (emitter.Stopping && emitter.Source.volume <= 0.001f)
                {
                    ReleaseEmitter(i);
                }
            }
        }

        private int AcquireEmitter()
        {
            for (int i = 0; i < _emitterCount; i++)
            {
                if (_emitters[i].Mode == PhysSoundEmitterMode.Free)
                {
                    return i;
                }
            }

            if (_emitterCount < _emitters.Length)
            {
                return CreateEmitter();
            }

            int victimIndex = 0;
            float victimScore = GetStealScore(in _emitters[0]);

            for (int i = 1; i < _emitterCount; i++)
            {
                float score = GetStealScore(in _emitters[i]);

                if (score < victimScore)
                {
                    victimIndex = i;
                    victimScore = score;
                }
            }

            ReleaseEmitter(victimIndex);
            return victimIndex;
        }

        private int CreateEmitter()
        {
            int index = _emitterCount++;

            GameObject emitterObject = new GameObject("Emitter");
            emitterObject.hideFlags = HideFlags.HideAndDontSave;
            emitterObject.transform.SetParent(transform, false);

            AudioSource source = emitterObject.AddComponent<AudioSource>();
            source.playOnAwake = false;

            _emitters[index] = new PhysSoundEmitter
            {
                Source = source,
                Mode = PhysSoundEmitterMode.Free,
                InteractionIndex = -1
            };

            return index;
        }

        private void PrepareEmitter(
            int emitterIndex,
            PhysSoundEmitterMode mode,
            Vector3 position)
        {
            if (_emitters[emitterIndex].Mode != PhysSoundEmitterMode.Free)
            {
                ReleaseEmitter(emitterIndex);
            }

            ref PhysSoundEmitter emitter = ref _emitters[emitterIndex];
            AudioSource source = emitter.Source;

            source.Stop();
            source.resource = null;
            source.loop = false;
            source.outputAudioMixerGroup = _settings.Output;
            source.spatialBlend = _settings.SpatialBlend;
            source.rolloffMode = _settings.RolloffMode;
            source.minDistance = _settings.MinimumDistance;
            source.maxDistance = _settings.MaximumDistance;
            source.dopplerLevel = _settings.DopplerLevel;
            source.spread = _settings.Spread;
            source.priority = _settings.Priority;
            source.reverbZoneMix = _settings.ReverbZoneMix;
            source.transform.position = position;

            emitter.Mode = mode;
            emitter.PairKey = 0;
            emitter.InteractionIndex = -1;
            emitter.TargetPosition = position;
            emitter.TargetVolume = 0f;
            emitter.TargetPitch = 1f;
            emitter.LastSeenAt = Time.unscaledTime;
            emitter.Stopping = false;
        }

        private void ReleaseEmitter(int emitterIndex)
        {
            ref PhysSoundEmitter emitter = ref _emitters[emitterIndex];

            if (emitter.Mode == PhysSoundEmitterMode.Slide &&
                emitter.PairKey != 0 &&
                _slides.TryGetValue(emitter.PairKey, out int registeredIndex) &&
                registeredIndex == emitterIndex)
            {
                _slides.Remove(emitter.PairKey);
            }

            emitter.Source.Stop();
            emitter.Source.resource = null;
            emitter.Source.loop = false;
            emitter.Source.volume = 0f;
            emitter.Source.pitch = 1f;

            emitter.Mode = PhysSoundEmitterMode.Free;
            emitter.PairKey = 0;
            emitter.InteractionIndex = -1;
            emitter.TargetVolume = 0f;
            emitter.TargetPitch = 1f;
            emitter.Stopping = false;
        }

        private static float GetStealScore(in PhysSoundEmitter emitter)
        {
            if (emitter.Mode == PhysSoundEmitterMode.Free)
            {
                return float.NegativeInfinity;
            }

            float modePenalty = emitter.Mode == PhysSoundEmitterMode.Slide ? 1f : 0f;
            return modePenalty + emitter.Source.volume;
        }

        private bool TryResolveInteraction(
            Collider firstCollider,
            Collider secondCollider,
            out int interactionIndex)
        {
            string firstSurface = GetSurface(firstCollider.sharedMaterial);
            string secondSurface = GetSurface(secondCollider.sharedMaterial);

            return _interactions.TryGetValue(
                       new PhysSoundInteractionKey(firstSurface, secondSurface),
                       out interactionIndex) ||
                   _interactions.TryGetValue(
                       new PhysSoundInteractionKey(firstSurface, PhysSoundSettings.AnySurface),
                       out interactionIndex) ||
                   _interactions.TryGetValue(
                       new PhysSoundInteractionKey(secondSurface, PhysSoundSettings.AnySurface),
                       out interactionIndex) ||
                   _interactions.TryGetValue(
                       new PhysSoundInteractionKey(
                           PhysSoundSettings.DefaultSurface,
                           PhysSoundSettings.AnySurface),
                       out interactionIndex) ||
                   _interactions.TryGetValue(
                       new PhysSoundInteractionKey(
                           PhysSoundSettings.DefaultSurface,
                           PhysSoundSettings.DefaultSurface),
                       out interactionIndex) ||
                   _interactions.TryGetValue(
                       new PhysSoundInteractionKey(
                           PhysSoundSettings.AnySurface,
                           PhysSoundSettings.AnySurface),
                       out interactionIndex);
        }

        private string GetSurface(PhysicsMaterial material)
        {
            return material != null && _surfaces.TryGetValue(material, out string surface)
                ? surface
                : PhysSoundSettings.DefaultSurface;
        }

        private static bool TryReadCollision(
            Collision collision,
            out PhysSoundContactData contact)
        {
            contact = default;

            int count = collision.contactCount;

            if (count <= 0)
            {
                return false;
            }

            Vector3 position = Vector3.zero;
            Vector3 normal = Vector3.zero;
            float totalWeight = 0f;
            Collider firstCollider = null;
            Collider secondCollider = null;

            for (int i = 0; i < count; i++)
            {
                ContactPoint point = collision.GetContact(i);

                if (firstCollider == null)
                {
                    firstCollider = point.thisCollider;
                    secondCollider = point.otherCollider;
                }

                float weight = Mathf.Max(0.001f, point.impulse.magnitude);
                position += point.point * weight;
                normal += point.normal;
                totalWeight += weight;
            }

            if (firstCollider == null || secondCollider == null)
            {
                return false;
            }

            contact = new PhysSoundContactData
            {
                FirstCollider = firstCollider,
                SecondCollider = secondCollider,
                Position = position / totalWeight,
                Normal = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up,
                RelativeVelocity = collision.relativeVelocity,
                Impulse = collision.impulse.magnitude
            };

            return true;
        }

        private static bool TryReadContactPair(
            ContactPairHeader header,
            ContactPair pair,
            Collider firstCollider,
            Collider secondCollider,
            out PhysSoundContactData contact)
        {
            contact = default;

            int count = pair.contactCount;

            if (count <= 0)
            {
                return false;
            }

            Vector3 position = Vector3.zero;
            Vector3 normal = Vector3.zero;
            Vector3 impulse = Vector3.zero;
            float totalWeight = 0f;

            for (int i = 0; i < count; i++)
            {
                ContactPairPoint point = pair.GetContactPoint(i);
                float weight = Mathf.Max(0.001f, point.impulse.magnitude);

                position += point.position * weight;
                normal += point.normal;
                impulse += point.impulse;
                totalWeight += weight;
            }

            position /= totalWeight;

            Vector3 firstVelocity = GetPointVelocity(
                header.body,
                header.bodyLinearVelocity,
                header.bodyAngularVelocity,
                position);

            Vector3 secondVelocity = GetPointVelocity(
                header.otherBody,
                header.otherBodyLinearVelocity,
                header.otherBodyAngularVelocity,
                position);

            contact = new PhysSoundContactData
            {
                FirstCollider = firstCollider,
                SecondCollider = secondCollider,
                Position = position,
                Normal = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up,
                RelativeVelocity = firstVelocity - secondVelocity,
                Impulse = impulse.magnitude
            };

            return true;
        }

        private static Vector3 GetPointVelocity(
            Component body,
            Vector3 linearVelocity,
            Vector3 angularVelocity,
            Vector3 point)
        {
            if (body == null)
            {
                return Vector3.zero;
            }

            return linearVelocity +
                   Vector3.Cross(angularVelocity, point - body.transform.position);
        }

        private static float GetTangentialSpeed(
            Vector3 relativeVelocity,
            Vector3 normal)
        {
            return Vector3.ProjectOnPlane(relativeVelocity, normal).magnitude;
        }

        private static ulong GetPairKey(
            Collider firstCollider,
            Collider secondCollider)
        {
            uint first = unchecked((uint)firstCollider.GetInstanceID());
            uint second = unchecked((uint)secondCollider.GetInstanceID());

            if (first > second)
            {
                uint temporary = first;
                first = second;
                second = temporary;
            }

            return ((ulong)first << 32) | second;
        }

        private void OnDestroy()
        {
            if (_contactEventSubscribed)
            {
                Physics.ContactEvent -= OnContactEvent;
                _contactEventSubscribed = false;
            }

            for (int i = 0; i < _emitterCount; i++)
            {
                if (_emitters[i].Source != null)
                {
                    _emitters[i].Source.Stop();
                }
            }

            _slides.Clear();

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
#endif
