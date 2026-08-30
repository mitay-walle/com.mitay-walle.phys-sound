#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace PhysSound
{
    internal static class PhysSoundRuntime
    {
        private static readonly Dictionary<PhysicsMaterial, string> Surfaces =
            new Dictionary<PhysicsMaterial, string>();

        private static readonly Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> Interactions =
            new Dictionary<PhysSoundInteractionKey, PhysSoundInteraction>();

        private static readonly Dictionary<ulong, int> ComponentReportFrames =
            new Dictionary<ulong, int>();

        private static readonly Dictionary<ulong, int> ComponentContinuousOwners =
            new Dictionary<ulong, int>();

        private static readonly Dictionary<ulong, float> LastImpactTimes =
            new Dictionary<ulong, float>();

        private static PhysSoundSettings _settings;
        private static PhysSoundRuntimeHost _host;
        private static bool _initialized;
        private static bool _contactEventSubscribed;
        private static bool _missingSettingsWarningShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (_contactEventSubscribed)
            {
                Physics.ContactEvent -= OnContactEvent;
            }

            Surfaces.Clear();
            Interactions.Clear();
            ComponentReportFrames.Clear();
            ComponentContinuousOwners.Clear();
            LastImpactTimes.Clear();

            _settings = null;
            _host = null;
            _initialized = false;
            _contactEventSubscribed = false;
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
                _settings.ContactBackend != PhysSoundContactBackend.Components ||
                owner == null ||
                collision == null ||
                !TryReadCollision(collision, out PhysSoundContactData contact))
            {
                return false;
            }

            pairKey = GetPairKey(contact.FirstCollider, contact.SecondCollider);

            if (!TryResolveInteraction(
                    contact.FirstCollider,
                    contact.SecondCollider,
                    out PhysSoundInteraction interaction))
            {
                return false;
            }

            int frame = Time.frameCount;
            bool firstReportThisFrame =
                !ComponentReportFrames.TryGetValue(pairKey, out int previousFrame) ||
                previousFrame != frame;

            ComponentReportFrames[pairKey] = frame;

            if (firstReportThisFrame)
            {
                PlayImpact(pairKey, interaction, contact.Position, contact.Impulse);
            }

            if (!interaction.HasSlide)
            {
                return false;
            }

            int ownerId = owner.GetInstanceID();

            if (!ComponentContinuousOwners.TryGetValue(pairKey, out int existingOwner))
            {
                ComponentContinuousOwners.Add(pairKey, ownerId);
                return true;
            }

            return existingOwner == ownerId;
        }

        internal static void ReportComponentStay(
            int ownerId,
            ulong pairKey,
            Collision collision)
        {
            if (!EnsureInitialized() ||
                _settings.ContactBackend != PhysSoundContactBackend.Components ||
                collision == null ||
                !ComponentContinuousOwners.TryGetValue(pairKey, out int registeredOwner) ||
                registeredOwner != ownerId ||
                !TryReadCollision(collision, out PhysSoundContactData contact) ||
                !TryResolveInteraction(
                    contact.FirstCollider,
                    contact.SecondCollider,
                    out PhysSoundInteraction interaction) ||
                !interaction.HasSlide)
            {
                return;
            }

            float slideSpeed = GetTangentialSpeed(contact.RelativeVelocity, contact.Normal);
            _host.UpdateSlide(pairKey, interaction, contact.Position, slideSpeed);
        }

        internal static void ReportComponentExit(int ownerId, ulong pairKey)
        {
            if (!_initialized)
            {
                return;
            }

            if (ComponentContinuousOwners.TryGetValue(pairKey, out int registeredOwner) &&
                registeredOwner == ownerId)
            {
                ComponentContinuousOwners.Remove(pairKey);
                _host.StopSlide(pairKey);
            }
        }

        internal static void ReportComponentDisabled(int ownerId, IEnumerable<ulong> pairKeys)
        {
            if (!_initialized || pairKeys == null)
            {
                return;
            }

            foreach (ulong pairKey in pairKeys)
            {
                ReportComponentExit(ownerId, pairKey);
            }
        }

        internal static bool TryGetPairKey(Collision collision, out ulong pairKey)
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
            if (_initialized)
            {
                return _settings != null && _host != null;
            }

            _initialized = true;
            _settings = PhysSoundSettings.Load();

            if (_settings == null)
            {
                if (!_missingSettingsWarningShown)
                {
                    Debug.LogWarning(
                        "Phys Sound settings were not found. Open Project Settings > Phys Sound and create them.");
                    _missingSettingsWarningShown = true;
                }

                return false;
            }

            _settings.BuildLookups(Surfaces, Interactions);

            GameObject hostObject = new GameObject("Phys Sound");
            hostObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(hostObject);

            _host = hostObject.AddComponent<PhysSoundRuntimeHost>();
            _host.Initialize(_settings);

            if (_settings.ContactBackend == PhysSoundContactBackend.ProvidesContacts)
            {
                Physics.ContactEvent += OnContactEvent;
                _contactEventSubscribed = true;
            }

            return true;
        }

        private static void OnContactEvent(
            PhysicsScene physicsScene,
            NativeArray<ContactPairHeader>.ReadOnly headers)
        {
            if (!EnsureInitialized() ||
                _settings.ContactBackend != PhysSoundContactBackend.ProvidesContacts)
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
                        _host.StopSlide(pairKey);
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
                            out PhysSoundInteraction interaction))
                    {
                        continue;
                    }

                    if (pair.isCollisionEnter)
                    {
                        PlayImpact(pairKey, interaction, contact.Position, contact.Impulse);
                    }

                    if (pair.isCollisionStay && interaction.HasSlide)
                    {
                        float slideSpeed = GetTangentialSpeed(contact.RelativeVelocity, contact.Normal);
                        _host.UpdateSlide(pairKey, interaction, contact.Position, slideSpeed);
                    }
                }
            }
        }

        private static void PlayImpact(
            ulong pairKey,
            PhysSoundInteraction interaction,
            Vector3 position,
            float impulse)
        {
            float now = Time.unscaledTime;

            if (LastImpactTimes.TryGetValue(pairKey, out float previousTime) &&
                now - previousTime < _settings.MinimumImpactInterval)
            {
                return;
            }

            LastImpactTimes[pairKey] = now;
            _host.PlayImpact(interaction, position, impulse);
        }

        private static bool TryResolveInteraction(
            Collider firstCollider,
            Collider secondCollider,
            out PhysSoundInteraction interaction)
        {
            string firstSurface = GetSurface(firstCollider.sharedMaterial);
            string secondSurface = GetSurface(secondCollider.sharedMaterial);

            if (Interactions.TryGetValue(
                    new PhysSoundInteractionKey(firstSurface, secondSurface),
                    out interaction))
            {
                return true;
            }

            string lower = firstSurface;
            string upper = secondSurface;

            if (string.Compare(lower, upper, StringComparison.OrdinalIgnoreCase) > 0)
            {
                lower = secondSurface;
                upper = firstSurface;
            }

            if (Interactions.TryGetValue(
                    new PhysSoundInteractionKey(lower, PhysSoundSettings.AnySurface),
                    out interaction) ||
                Interactions.TryGetValue(
                    new PhysSoundInteractionKey(upper, PhysSoundSettings.AnySurface),
                    out interaction) ||
                Interactions.TryGetValue(
                    new PhysSoundInteractionKey(
                        PhysSoundSettings.DefaultSurface,
                        PhysSoundSettings.AnySurface),
                    out interaction) ||
                Interactions.TryGetValue(
                    new PhysSoundInteractionKey(
                        PhysSoundSettings.DefaultSurface,
                        PhysSoundSettings.DefaultSurface),
                    out interaction) ||
                Interactions.TryGetValue(
                    new PhysSoundInteractionKey(
                        PhysSoundSettings.AnySurface,
                        PhysSoundSettings.AnySurface),
                    out interaction))
            {
                return true;
            }

            interaction = null;
            return false;
        }

        private static string GetSurface(PhysicsMaterial material)
        {
            return material != null && Surfaces.TryGetValue(material, out string surface)
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

            position /= totalWeight;
            normal = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up;

            contact = new PhysSoundContactData(
                firstCollider,
                secondCollider,
                position,
                normal,
                collision.relativeVelocity,
                collision.impulse.magnitude);

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
            normal = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up;

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

            contact = new PhysSoundContactData(
                firstCollider,
                secondCollider,
                position,
                normal,
                firstVelocity - secondVelocity,
                impulse.magnitude);

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

        private static float GetTangentialSpeed(Vector3 relativeVelocity, Vector3 normal)
        {
            return Vector3.ProjectOnPlane(relativeVelocity, normal).magnitude;
        }

        private static ulong GetPairKey(Collider firstCollider, Collider secondCollider)
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
    }

    internal readonly struct PhysSoundContactData
    {
        internal readonly Collider FirstCollider;
        internal readonly Collider SecondCollider;
        internal readonly Vector3 Position;
        internal readonly Vector3 Normal;
        internal readonly Vector3 RelativeVelocity;
        internal readonly float Impulse;

        internal PhysSoundContactData(
            Collider firstCollider,
            Collider secondCollider,
            Vector3 position,
            Vector3 normal,
            Vector3 relativeVelocity,
            float impulse)
        {
            FirstCollider = firstCollider;
            SecondCollider = secondCollider;
            Position = position;
            Normal = normal;
            RelativeVelocity = relativeVelocity;
            Impulse = impulse;
        }
    }

    internal sealed class PhysSoundRuntimeHost : MonoBehaviour
    {
        private readonly List<PhysSoundEmitter> _emitters = new List<PhysSoundEmitter>();
        private readonly Dictionary<ulong, PhysSoundEmitter> _slides =
            new Dictionary<ulong, PhysSoundEmitter>();

        private PhysSoundSettings _settings;

        internal void Initialize(PhysSoundSettings settings)
        {
            _settings = settings;
        }

        internal void PlayImpact(
            PhysSoundInteraction interaction,
            Vector3 position,
            float impulse)
        {
            AudioClip clip = interaction.GetImpactClip();
            float volume = interaction.EvaluateImpactVolume(impulse);

            if (clip == null || volume <= 0f)
            {
                return;
            }

            PhysSoundEmitter emitter = AcquireEmitter();
            PrepareEmitter(emitter, PhysSoundEmitterMode.Impact, position);

            emitter.Source.loop = false;
            emitter.Source.resource = clip;
            emitter.Source.volume = volume;
            emitter.Source.pitch = interaction.GetImpactPitch();
            emitter.Source.Play();
        }

        internal void UpdateSlide(
            ulong pairKey,
            PhysSoundInteraction interaction,
            Vector3 position,
            float speed)
        {
            float targetVolume = interaction.EvaluateSlideVolume(speed);

            if (!_slides.TryGetValue(pairKey, out PhysSoundEmitter emitter))
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

                emitter = AcquireEmitter();
                PrepareEmitter(emitter, PhysSoundEmitterMode.Slide, position);

                emitter.PairKey = pairKey;
                emitter.Interaction = interaction;
                emitter.Source.loop = true;
                emitter.Source.resource = clip;
                emitter.Source.volume = 0f;
                emitter.Source.pitch = interaction.EvaluateSlidePitch(speed);
                emitter.Source.Play();

                _slides.Add(pairKey, emitter);
            }
            else if (emitter.Interaction != interaction)
            {
                AudioClip clip = interaction.GetSlideClip();

                if (clip == null)
                {
                    StopSlide(pairKey);
                    return;
                }

                emitter.Interaction = interaction;
                emitter.Source.Stop();
                emitter.Source.resource = clip;
                emitter.Source.loop = true;
                emitter.Source.Play();
            }

            emitter.TargetPosition = position;
            emitter.TargetVolume = targetVolume;
            emitter.TargetPitch = interaction.EvaluateSlidePitch(speed);
            emitter.LastSeenAt = Time.unscaledTime;
            emitter.Stopping = false;
        }

        internal void StopSlide(ulong pairKey)
        {
            if (_slides.TryGetValue(pairKey, out PhysSoundEmitter emitter))
            {
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

            for (int i = 0; i < _emitters.Count; i++)
            {
                PhysSoundEmitter emitter = _emitters[i];

                if (emitter.Mode == PhysSoundEmitterMode.Free)
                {
                    continue;
                }

                if (emitter.Mode == PhysSoundEmitterMode.Impact)
                {
                    if (!emitter.Source.isPlaying)
                    {
                        ReleaseEmitter(emitter);
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

                emitter.Transform.position = Vector3.Lerp(
                    emitter.Transform.position,
                    emitter.TargetPosition,
                    positionFactor);

                if (emitter.Stopping &&
                    emitter.Source.volume <= 0.001f)
                {
                    ReleaseEmitter(emitter);
                }
            }
        }

        private PhysSoundEmitter AcquireEmitter()
        {
            for (int i = 0; i < _emitters.Count; i++)
            {
                if (_emitters[i].Mode == PhysSoundEmitterMode.Free)
                {
                    return _emitters[i];
                }
            }

            if (_emitters.Count < _settings.MaximumVoices)
            {
                PhysSoundEmitter created = CreateEmitter();
                _emitters.Add(created);
                return created;
            }

            PhysSoundEmitter victim = _emitters[0];
            float victimScore = GetStealScore(victim);

            for (int i = 1; i < _emitters.Count; i++)
            {
                float score = GetStealScore(_emitters[i]);

                if (score < victimScore)
                {
                    victim = _emitters[i];
                    victimScore = score;
                }
            }

            ReleaseEmitter(victim);
            return victim;
        }

        private PhysSoundEmitter CreateEmitter()
        {
            GameObject emitterObject = new GameObject("Emitter");
            emitterObject.hideFlags = HideFlags.HideAndDontSave;
            emitterObject.transform.SetParent(transform, false);

            AudioSource source = emitterObject.AddComponent<AudioSource>();
            source.playOnAwake = false;

            return new PhysSoundEmitter
            {
                Transform = emitterObject.transform,
                Source = source,
                Mode = PhysSoundEmitterMode.Free
            };
        }

        private void PrepareEmitter(
            PhysSoundEmitter emitter,
            PhysSoundEmitterMode mode,
            Vector3 position)
        {
            if (emitter.Mode != PhysSoundEmitterMode.Free)
            {
                ReleaseEmitter(emitter);
            }

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

            emitter.Mode = mode;
            emitter.PairKey = 0;
            emitter.Interaction = null;
            emitter.Transform.position = position;
            emitter.TargetPosition = position;
            emitter.TargetVolume = 0f;
            emitter.TargetPitch = 1f;
            emitter.LastSeenAt = Time.unscaledTime;
            emitter.Stopping = false;
        }

        private void ReleaseEmitter(PhysSoundEmitter emitter)
        {
            if (emitter.Mode == PhysSoundEmitterMode.Slide &&
                emitter.PairKey != 0 &&
                _slides.TryGetValue(emitter.PairKey, out PhysSoundEmitter registered) &&
                ReferenceEquals(registered, emitter))
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
            emitter.Interaction = null;
            emitter.TargetVolume = 0f;
            emitter.TargetPitch = 1f;
            emitter.Stopping = false;
        }

        private static float GetStealScore(PhysSoundEmitter emitter)
        {
            if (emitter.Mode == PhysSoundEmitterMode.Free)
            {
                return float.NegativeInfinity;
            }

            float modePenalty = emitter.Mode == PhysSoundEmitterMode.Slide ? 1f : 0f;
            return modePenalty + emitter.Source.volume;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _emitters.Count; i++)
            {
                if (_emitters[i].Source != null)
                {
                    _emitters[i].Source.Stop();
                }
            }

            _slides.Clear();
            _emitters.Clear();
        }
    }

    internal enum PhysSoundEmitterMode : byte
    {
        Free,
        Impact,
        Slide
    }

    internal sealed class PhysSoundEmitter
    {
        internal Transform Transform;
        internal AudioSource Source;
        internal PhysSoundEmitterMode Mode;
        internal ulong PairKey;
        internal PhysSoundInteraction Interaction;
        internal Vector3 TargetPosition;
        internal float TargetVolume;
        internal float TargetPitch;
        internal float LastSeenAt;
        internal bool Stopping;
    }
}
#endif
