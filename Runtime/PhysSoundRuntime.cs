#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System.Collections.Generic;
using Unity.Collections;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace PhysSound
{
	[NoAutoStaticsCleanup, AddComponentMenu("")]
	internal sealed class PhysSoundRuntime : MonoBehaviour
	{
		private static PhysSoundRuntime _instance;
        private static bool _configurationWarningShown;

		private readonly Dictionary<PhysicsMaterial, string> _surfaces = new();
#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
		private readonly Dictionary<PhysicsMaterial2D, string> _surfaces2D = new();
#endif
		private readonly Dictionary<PhysSoundInteractionKey, int> _interactions = new();
		private readonly List<PhysSoundInteraction> _interactionValues = new();
		private readonly HashSet<PhysSoundPairKey> _componentReportedPairs = new();
		private readonly Dictionary<PhysSoundPairKey, EntityId> _componentContinuousOwners = new();
		private readonly Dictionary<PhysSoundPairKey, float> _lastImpactTimes = new();
		private readonly Dictionary<PhysSoundPairKey, int> _slides = new();

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
            _configurationWarningShown = false;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void InitializeAfterSceneLoad()
		{
			EnsureInitialized();
		}

		internal static bool ReportComponentEnter(PhysSoundObject owner, Collision collision, out PhysSoundPairKey pairKey)
		{
			pairKey = default;

			if (!EnsureInitialized() || _instance._settings.ContactBackend != PhysSoundContactBackend.Components || owner == null ||
			    collision == null)
			{
				return false;
			}

			return _instance.ProcessComponentEnter(owner.GetEntityId(), collision, out pairKey);
		}

		internal static void ReportComponentStay(EntityId ownerId, PhysSoundPairKey pairKey, Collision collision)
		{
			if (!EnsureInitialized() || _instance._settings.ContactBackend != PhysSoundContactBackend.Components)
			{
				return;
			}

			_instance.ProcessComponentStay(ownerId, pairKey, collision);
		}

		internal static void ReportComponentExit(EntityId ownerId, PhysSoundPairKey pairKey)
		{
			if (_instance == null)
			{
				return;
			}

			_instance.ProcessComponentExit(ownerId, pairKey);
		}

		internal static void ReportComponentDisabled(EntityId ownerId, IEnumerable<PhysSoundPairKey> pairKeys)
		{
			if (_instance == null || pairKeys == null)
			{
				return;
			}

			foreach (PhysSoundPairKey pairKey in pairKeys)
			{
				_instance.ProcessComponentExit(ownerId, pairKey);
			}
		}

#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
		internal static bool ReportComponentEnter2D(EntityId ownerId, Collision2D collision, out PhysSoundPairKey pairKey)
		{
			pairKey = default;

			if (!EnsureInitialized() || collision == null)
			{
				return false;
			}

			return _instance.ProcessComponentEnter2D(ownerId, collision, out pairKey);
		}

		internal static void ReportComponentStay2D(EntityId ownerId, PhysSoundPairKey pairKey, Collision2D collision)
		{
			if (!EnsureInitialized())
			{
				return;
			}

			_instance.ProcessComponentStay2D(ownerId, pairKey, collision);
		}

		internal static bool TryGetPairKey2D(Collision2D collision, out PhysSoundPairKey pairKey)
		{
			pairKey = default;

			if (collision == null || collision.contactCount == 0)
			{
				return false;
			}

			ContactPoint2D contact = collision.GetContact(0);

			if (contact.collider == null || contact.otherCollider == null)
			{
				return false;
			}

			pairKey = GetPairKey(contact.collider, contact.otherCollider);
			return true;
		}
#endif

		internal static bool TryGetPairKey(Collision collision, out PhysSoundPairKey pairKey)
		{
			pairKey = default;

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
                if (!_configurationWarningShown)
                {
                    Debug.LogWarning("Phys Sound settings were not found. Open Project Settings > Audio > Phys Sound and create them.");
                    _configurationWarningShown = true;
                }

                return false;
            }

            if (settings.EmitterPrefab == null)
            {
                if (!_configurationWarningShown)
                {
                    Debug.LogError("Phys Sound settings require an AudioSource emitter prefab.");
                    _configurationWarningShown = true;
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
			settings.BuildLookups(_surfaces, _interactions, _interactionValues);
#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
			settings.BuildLookups2D(_surfaces2D);
#endif

			if (settings.ContactBackend == PhysSoundContactBackend.ProvidesContacts)
			{
				Physics.ContactEvent += OnContactEvent;
				_contactEventSubscribed = true;
			}
		}

		private bool ProcessComponentEnter(EntityId ownerId, Collision collision, out PhysSoundPairKey pairKey)
		{
			pairKey = default;

			if (!TryReadCollision(collision, out PhysSoundContactData contact))
			{
				return false;
			}

			pairKey = GetPairKey(contact.FirstCollider, contact.SecondCollider);

			if (!TryResolveInteraction(contact.FirstCollider, contact.SecondCollider, out int interactionIndex))
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

			PhysSoundInteraction interaction = _interactionValues[interactionIndex];

			if (!interaction.HasSlide)
			{
				return false;
			}

			if (!_componentContinuousOwners.TryGetValue(pairKey, out EntityId existingOwner))
			{
				_componentContinuousOwners.Add(pairKey, ownerId);
				return true;
			}

			return existingOwner == ownerId;
		}

		private void ProcessComponentStay(EntityId ownerId, PhysSoundPairKey pairKey, Collision collision)
		{
			if (collision == null || !_componentContinuousOwners.TryGetValue(pairKey, out EntityId registeredOwner) || registeredOwner != ownerId ||
			    !TryReadCollision(collision, out PhysSoundContactData contact) ||
			    !TryResolveInteraction(contact.FirstCollider, contact.SecondCollider, out int interactionIndex))
			{
				return;
			}

			PhysSoundInteraction interaction = _interactionValues[interactionIndex];

			if (!interaction.HasSlide)
			{
				return;
			}

			float slideSpeed = GetTangentialSpeed(contact.RelativeVelocity, contact.Normal);

			UpdateSlide(pairKey, interactionIndex, contact.Position, slideSpeed);
		}

#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
		private bool ProcessComponentEnter2D(EntityId ownerId, Collision2D collision, out PhysSoundPairKey pairKey)
		{
			pairKey = default;

			if (!TryReadCollision2D(collision, out PhysSoundContactData2D contact))
			{
				return false;
			}

			pairKey = GetPairKey(contact.FirstCollider, contact.SecondCollider);

			if (!TryResolveInteraction(contact.FirstCollider, contact.SecondCollider, out int interactionIndex))
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

			PhysSoundInteraction interaction = _interactionValues[interactionIndex];

			if (!interaction.HasSlide)
			{
				return false;
			}

			if (!_componentContinuousOwners.TryGetValue(pairKey, out EntityId existingOwner))
			{
				_componentContinuousOwners.Add(pairKey, ownerId);
				return true;
			}

			return existingOwner == ownerId;
		}

		private void ProcessComponentStay2D(EntityId ownerId, PhysSoundPairKey pairKey, Collision2D collision)
		{
			if (collision == null ||
			    !_componentContinuousOwners.TryGetValue(pairKey, out EntityId registeredOwner) ||
			    registeredOwner != ownerId ||
			    !TryReadCollision2D(collision, out PhysSoundContactData2D contact) ||
			    !TryResolveInteraction(contact.FirstCollider, contact.SecondCollider, out int interactionIndex))
			{
				return;
			}

			PhysSoundInteraction interaction = _interactionValues[interactionIndex];

			if (!interaction.HasSlide)
			{
				return;
			}

			float slideSpeed = GetTangentialSpeed(contact.RelativeVelocity, contact.Normal);
			UpdateSlide(pairKey, interactionIndex, contact.Position, slideSpeed);
		}
#endif

		private void ProcessComponentExit(EntityId ownerId, PhysSoundPairKey pairKey)
		{
			if (_componentContinuousOwners.TryGetValue(pairKey, out EntityId registeredOwner) && registeredOwner == ownerId)
			{
				_componentContinuousOwners.Remove(pairKey);
				StopSlide(pairKey);
			}
		}

		private void OnContactEvent(PhysicsScene physicsScene, NativeArray<ContactPairHeader>.ReadOnly headers)
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

					if (firstCollider == null || secondCollider == null || (!firstCollider.providesContacts && !secondCollider.providesContacts))
					{
						continue;
					}

					PhysSoundPairKey pairKey = GetPairKey(firstCollider, secondCollider);

					if (pair.isCollisionExit)
					{
						StopSlide(pairKey);
						continue;
					}

					if (!TryReadContactPair(header, pair, firstCollider, secondCollider, out PhysSoundContactData contact) ||
					    !TryResolveInteraction(firstCollider, secondCollider, out int interactionIndex))
					{
						continue;
					}

					if (pair.isCollisionEnter)
					{
						PlayImpact(pairKey, interactionIndex, contact.Position, contact.Impulse);
					}

					PhysSoundInteraction interaction = _interactionValues[interactionIndex];

					if (pair.isCollisionStay && interaction.HasSlide)
					{
						float slideSpeed = GetTangentialSpeed(contact.RelativeVelocity, contact.Normal);

						UpdateSlide(pairKey, interactionIndex, contact.Position, slideSpeed);
					}
				}
			}
		}

		private void PlayImpact(PhysSoundPairKey pairKey, int interactionIndex, Vector3 position, float impulse)
		{
			float now = Time.unscaledTime;

			if (_lastImpactTimes.TryGetValue(pairKey, out float previousTime) && now - previousTime < _settings.MinimumImpactInterval)
			{
				return;
			}

			PhysSoundInteraction interaction = _interactionValues[interactionIndex];
			float volume = interaction.EvaluateImpactVolume(impulse);

			if (!interaction.TryGetImpactPlayback(impulse, out PhysSoundImpactPlayback playback) || volume <= 0f)
			{
				return;
			}

			_lastImpactTimes[pairKey] = now;

			int emitterIndex = AcquireEmitter();
			PrepareEmitter(emitterIndex, PhysSoundEmitterMode.Impact, position);

			ref PhysSoundEmitter emitter = ref _emitters[emitterIndex];
			emitter.Source.loop = false;
			emitter.Source.resource = playback.Clip;
			emitter.Source.volume = volume;
			emitter.Source.pitch = interaction.EvaluateImpactPitch(impulse);
			emitter.Source.time = playback.StartTime;
			emitter.ImpactEndDspTime = AudioSettings.dspTime +
			                           (playback.EndTime - playback.StartTime) / Mathf.Max(0.01f, Mathf.Abs(emitter.Source.pitch));
			emitter.Source.Play();
		}

		private void UpdateSlide(PhysSoundPairKey pairKey, int interactionIndex, Vector3 position, float speed)
		{
			PhysSoundInteraction interaction = _interactionValues[interactionIndex];
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

		private void StopSlide(PhysSoundPairKey pairKey)
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
					if (!emitter.Source.isPlaying || AudioSettings.dspTime >= emitter.ImpactEndDspTime)
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

				float volumeSpeed = emitter.TargetVolume > emitter.Source.volume ? _settings.SlideFadeInSpeed : _settings.SlideFadeOutSpeed;

				emitter.Source.volume = Mathf.MoveTowards(emitter.Source.volume, emitter.TargetVolume, volumeSpeed * deltaTime);

				emitter.Source.pitch = Mathf.MoveTowards(emitter.Source.pitch, emitter.TargetPitch, _settings.SlidePitchSpeed * deltaTime);

				float positionFactor = _settings.SlidePositionSpeed <= 0f ? 1f : 1f - Mathf.Exp(-_settings.SlidePositionSpeed * deltaTime);

				emitter.Source.transform.position = Vector3.Lerp(emitter.Source.transform.position, emitter.TargetPosition, positionFactor);

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

            AudioSource source = Instantiate(_settings.EmitterPrefab, transform);
            GameObject emitterObject = source.gameObject;
            emitterObject.name = "Emitter";
            emitterObject.hideFlags = HideFlags.HideAndDontSave;
            emitterObject.transform.localPosition = Vector3.zero;
            emitterObject.transform.localRotation = Quaternion.identity;
            source.playOnAwake = false;
            source.Stop();
            source.resource = null;

			_emitters[index] = new PhysSoundEmitter
			{
				Source = source,
				Mode = PhysSoundEmitterMode.Free,
				InteractionIndex = -1
			};

			return index;
		}

		private void PrepareEmitter(int emitterIndex, PhysSoundEmitterMode mode, Vector3 position)
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
            source.transform.position = position;

			emitter.Mode = mode;
			emitter.PairKey = default;
			emitter.InteractionIndex = -1;
			emitter.TargetPosition = position;
			emitter.TargetVolume = 0f;
			emitter.TargetPitch = 1f;
			emitter.LastSeenAt = Time.unscaledTime;
			emitter.ImpactEndDspTime = 0d;
			emitter.Stopping = false;
		}

		private void ReleaseEmitter(int emitterIndex)
		{
			ref PhysSoundEmitter emitter = ref _emitters[emitterIndex];

			if (emitter.Mode == PhysSoundEmitterMode.Slide && _slides.TryGetValue(emitter.PairKey, out int registeredIndex) &&
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
			emitter.PairKey = default;
			emitter.InteractionIndex = -1;
			emitter.TargetVolume = 0f;
			emitter.TargetPitch = 1f;
			emitter.ImpactEndDspTime = 0d;
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

		private bool TryResolveInteraction(Collider firstCollider, Collider secondCollider, out int interactionIndex)
		{
			string firstSurface = GetSurface(firstCollider.sharedMaterial);
			string secondSurface = GetSurface(secondCollider.sharedMaterial);

			return _interactions.TryGetValue(new PhysSoundInteractionKey(firstSurface, secondSurface), out interactionIndex) ||
			       _interactions.TryGetValue(new PhysSoundInteractionKey(firstSurface, PhysSoundSettings.AnySurface), out interactionIndex) ||
			       _interactions.TryGetValue(new PhysSoundInteractionKey(secondSurface, PhysSoundSettings.AnySurface), out interactionIndex) ||
			       _interactions.TryGetValue(new PhysSoundInteractionKey(PhysSoundSettings.DefaultSurface, PhysSoundSettings.AnySurface),
				       out interactionIndex);
		}

#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
		private bool TryResolveInteraction(Collider2D firstCollider, Collider2D secondCollider, out int interactionIndex)
		{
			string firstSurface = GetSurface(firstCollider.sharedMaterial);
			string secondSurface = GetSurface(secondCollider.sharedMaterial);

			return _interactions.TryGetValue(new PhysSoundInteractionKey(firstSurface, secondSurface), out interactionIndex) ||
			       _interactions.TryGetValue(new PhysSoundInteractionKey(firstSurface, PhysSoundSettings.AnySurface), out interactionIndex) ||
			       _interactions.TryGetValue(new PhysSoundInteractionKey(secondSurface, PhysSoundSettings.AnySurface), out interactionIndex) ||
			       _interactions.TryGetValue(new PhysSoundInteractionKey(PhysSoundSettings.DefaultSurface, PhysSoundSettings.AnySurface),
				       out interactionIndex);
		}
#endif

		private string GetSurface(PhysicsMaterial material)
		{
			return material != null && _surfaces.TryGetValue(material, out string surface) ? surface : PhysSoundSettings.DefaultSurface;
		}

#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
		private string GetSurface(PhysicsMaterial2D material)
		{
			return material != null && _surfaces2D.TryGetValue(material, out string surface)
				? surface
				: PhysSoundSettings.DefaultSurface;
		}

		private static bool TryReadCollision2D(Collision2D collision, out PhysSoundContactData2D contact)
		{
			contact = default;

			int count = collision.contactCount;

			if (count <= 0)
			{
				return false;
			}

			Vector3 position = Vector3.zero;
			Vector3 normal = Vector3.zero;
			float impulse = 0f;
			Collider2D firstCollider = null;
			Collider2D secondCollider = null;

			for (int i = 0; i < count; i++)
			{
				ContactPoint2D point = collision.GetContact(i);

				if (firstCollider == null)
				{
					firstCollider = point.collider;
					secondCollider = point.otherCollider;
				}

				position += (Vector3)point.point;
				normal += (Vector3)point.normal;
				impulse += Mathf.Abs(point.normalImpulse) + Mathf.Abs(point.tangentImpulse);
			}

			if (firstCollider == null || secondCollider == null)
			{
				return false;
			}

			contact = new PhysSoundContactData2D
			{
				FirstCollider = firstCollider,
				SecondCollider = secondCollider,
				Position = position / count,
				Normal = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up,
				RelativeVelocity = collision.relativeVelocity,
				Impulse = impulse
			};

			return true;
		}
#endif

		private static bool TryReadCollision(Collision collision, out PhysSoundContactData contact)
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

		private static bool TryReadContactPair(ContactPairHeader header, ContactPair pair, Collider firstCollider, Collider secondCollider,
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

			Vector3 firstVelocity = GetPointVelocity(header.body, header.bodyLinearVelocity, header.bodyAngularVelocity, position);

			Vector3 secondVelocity = GetPointVelocity(header.otherBody, header.otherBodyLinearVelocity, header.otherBodyAngularVelocity, position);

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

		private static Vector3 GetPointVelocity(Component body, Vector3 linearVelocity, Vector3 angularVelocity, Vector3 point)
		{
			if (body == null)
			{
				return Vector3.zero;
			}

			return linearVelocity + Vector3.Cross(angularVelocity, point - body.transform.position);
		}

		private static float GetTangentialSpeed(Vector3 relativeVelocity, Vector3 normal)
		{
			return Vector3.ProjectOnPlane(relativeVelocity, normal).magnitude;
		}

		private static PhysSoundPairKey GetPairKey(Collider firstCollider, Collider secondCollider)
		{
			return new PhysSoundPairKey(firstCollider.GetEntityId(), secondCollider.GetEntityId());
		}

#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
		private static PhysSoundPairKey GetPairKey(Collider2D firstCollider, Collider2D secondCollider)
		{
			return new PhysSoundPairKey(firstCollider.GetEntityId(), secondCollider.GetEntityId());
		}
#endif

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
