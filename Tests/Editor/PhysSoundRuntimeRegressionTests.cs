#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PhysSound.Tests
{
	internal sealed class PhysSoundRuntimeRegressionTests
	{
		private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
		private static readonly PhysSoundPairKey PairKey = new(default, default);

		private AudioClip _slideClip;
		private GameObject _emitterTemplateObject;
		private GameObject _runtimeObject;
		private PhysSoundRuntime _runtime;
		private PhysSoundSettings _settings;

		[SetUp]
		public void SetUp()
		{
			_slideClip = AudioClip.Create("Slide", 64, 1, 44100, false);

			_emitterTemplateObject = new GameObject("Emitter Template");
			AudioSource emitterTemplate = _emitterTemplateObject.AddComponent<AudioSource>();

			_settings = ScriptableObject.CreateInstance<PhysSoundSettings>();
			SetField(_settings, "_contactBackend", PhysSoundContactBackend.Components);
			SetField(_settings, "_emitterPrefab", emitterTemplate);
			SetField(_settings.DefaultInteraction, "_slideClips", new[] { _slideClip });

			_runtimeObject = new GameObject("Phys Sound Runtime Test");
			_runtime = _runtimeObject.AddComponent<PhysSoundRuntime>();
			Invoke(_runtime, "Initialize", _settings);
			typeof(PhysSoundRuntime).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _runtime);
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_runtimeObject);
			Object.DestroyImmediate(_emitterTemplateObject);
			Object.DestroyImmediate(_settings);
			Object.DestroyImmediate(_slideClip);
		}

		[Test]
		public void FirstSlideContactAfterImpact_DoesNotStartSlide()
		{
			ReportSlideContact(1f);

			Assert.That(
				GetContinuousEmitters(),
				Is.Empty,
				"The first stay sample after an impact must only arm sliding, not start its loop emitter.");
		}

		[Test]
		public void SustainedSlideContact_StartsAfterThreeSamples()
		{
			ReportSlideContact(1f);
			ReportSlideContact(1f);

			Assert.That(GetContinuousEmitters(), Is.Empty);

			ReportSlideContact(1f);

			Assert.That(GetContinuousEmitters(), Has.Count.EqualTo(1));
		}

		[Test]
		public void InterruptedSlideContact_RequiresFreshSamples()
		{
			ReportSlideContact(1f);
			ReportSlideContact(1f);
			ReportSlideContact(0f);
			ReportSlideContact(1f);
			ReportSlideContact(1f);

			Assert.That(
				GetContinuousEmitters(),
				Is.Empty,
				"A contact that stops sliding must satisfy the full start window again.");
		}

		[Test]
		public void RollingContact_StartsDedicatedLoop()
		{
			SetField(_settings.DefaultInteraction, "_rollClips", new[] { _slideClip });

			Invoke(_runtime, "UpdateRoll", PairKey, 0, Vector3.zero, 1f);
			Invoke(_runtime, "UpdateRoll", PairKey, 0, Vector3.zero, 1f);
			Invoke(_runtime, "UpdateRoll", PairKey, 0, Vector3.zero, 1f);

			Dictionary<PhysSoundContinuousKey, int> continuousEmitters = GetContinuousEmitters();
			Assert.That(continuousEmitters, Has.Count.EqualTo(1));

			foreach (int emitterIndex in continuousEmitters.Values)
			{
				PhysSoundEmitter[] emitters = GetField<PhysSoundEmitter[]>(_runtime, "_emitters");
				Assert.That(emitters[emitterIndex].Mode, Is.EqualTo(PhysSoundEmitterMode.Roll));
			}
		}

		[Test]
		public void PureRollingPointVelocity_DoesNotProduceSlideSpeed()
		{
			GameObject rollingObject = new GameObject("Rolling Body");

			try
			{
				SphereCollider collider = rollingObject.AddComponent<SphereCollider>();
				Rigidbody body = rollingObject.AddComponent<Rigidbody>();
				body.useGravity = false;
				body.linearVelocity = Vector3.right * 2f;
				body.angularVelocity = Vector3.back * 2f;
				Vector3 contactPoint = Vector3.down;

				Vector3 pointVelocity = (Vector3)InvokeStatic(
					"GetPointVelocity",
					new[] { typeof(Rigidbody), typeof(Vector3) },
					body,
					contactPoint);
				float slideSpeed = (float)InvokeStatic(
					"GetTangentialSpeed",
					new[] { typeof(Vector3), typeof(Vector3) },
					pointVelocity,
					Vector3.up);
				float rollSpeed = (float)InvokeStatic(
					"GetRollSpeed",
					new[] { typeof(Collider), typeof(Vector3) },
					collider,
					contactPoint);

				Assert.That(slideSpeed, Is.EqualTo(0f).Within(0.001f));
				Assert.That(rollSpeed, Is.EqualTo(2f).Within(0.001f));
			}
			finally
			{
				Object.DestroyImmediate(rollingObject);
			}
		}

		[Test]
		public void ProvidesContactsPureRolling_UsesWorldCenterOfMass()
		{
			GameObject rollingObject = new GameObject("Offset COM Body");

			try
			{
				Rigidbody body = rollingObject.AddComponent<Rigidbody>();
				body.useGravity = false;
				body.centerOfMass = new Vector3(0.5f, 0f, 0f);
				Vector3 contactPoint = body.worldCenterOfMass + Vector3.down;
				Vector3 linearVelocity = Vector3.right * 2f;
				Vector3 angularVelocity = Vector3.back * 2f;

				Vector3 pointVelocity = (Vector3)InvokeStatic(
					"GetPointVelocity",
					new[] { typeof(Component), typeof(Vector3), typeof(Vector3), typeof(Vector3) },
					body,
					linearVelocity,
					angularVelocity,
					contactPoint);
				float rollSpeed = (float)InvokeStatic(
					"GetAngularPointSpeed",
					new[] { typeof(Component), typeof(Vector3), typeof(Vector3) },
					body,
					angularVelocity,
					contactPoint);

				Assert.That(pointVelocity.magnitude, Is.EqualTo(0f).Within(0.001f));
				Assert.That(rollSpeed, Is.EqualTo(2f).Within(0.001f));
			}
			finally
			{
				Object.DestroyImmediate(rollingObject);
			}
		}

		[Test]
		public void TriggerEnter_OrdinaryColliderPlaysImpactAndTracksContinuousContact()
		{
			SetField(_settings.DefaultInteraction, "_impactClips", new[] { _slideClip });
			GameObject ownObject = new GameObject("Trigger Owner");
			GameObject otherObject = new GameObject("Trigger Other");

			try
			{
				ownObject.AddComponent<BoxCollider>();
				BoxCollider otherCollider = otherObject.AddComponent<BoxCollider>();
				otherCollider.isTrigger = true;
				Rigidbody body = ownObject.AddComponent<Rigidbody>();
				body.useGravity = false;
				body.linearVelocity = Vector3.right * 2f;
				PhysSoundObject soundObject = ownObject.AddComponent<PhysSoundObject>();
				SetField(soundObject, "_includeTriggers", true);

				Invoke(soundObject, "OnTriggerEnter", otherCollider);

				Assert.That(GetField<HashSet<PhysSoundPairKey>>(soundObject, "_triggerPairs"), Has.Count.EqualTo(1));
				Assert.That(GetActiveEmitterCount(PhysSoundEmitterMode.Impact), Is.EqualTo(1));
			}
			finally
			{
				Object.DestroyImmediate(ownObject);
				Object.DestroyImmediate(otherObject);
			}
		}

		[Test]
		public void TriggerEnter_DisabledObjectDoesNotPlayImpact()
		{
			SetField(_settings.DefaultInteraction, "_impactClips", new[] { _slideClip });
			GameObject ownObject = new GameObject("Disabled Trigger Owner");
			GameObject otherObject = new GameObject("Trigger Other");

			try
			{
				ownObject.AddComponent<BoxCollider>();
				BoxCollider otherCollider = otherObject.AddComponent<BoxCollider>();
				otherCollider.isTrigger = true;
				Rigidbody body = ownObject.AddComponent<Rigidbody>();
				body.useGravity = false;
				body.linearVelocity = Vector3.right * 2f;
				PhysSoundObject soundObject = ownObject.AddComponent<PhysSoundObject>();
				SetField(soundObject, "_includeTriggers", true);
				soundObject.enabled = false;

				Invoke(soundObject, "OnTriggerEnter", otherCollider);

				Assert.That(GetField<HashSet<PhysSoundPairKey>>(soundObject, "_triggerPairs"), Is.Empty);
				Assert.That(GetActiveEmitterCount(PhysSoundEmitterMode.Impact), Is.EqualTo(0));
			}
			finally
			{
				Object.DestroyImmediate(ownObject);
				Object.DestroyImmediate(otherObject);
			}
		}

#if PHYS_SOUND_TERRAIN
		[Test]
		public void TerrainSplat_ComposesMappedSurfaceInteractions()
		{
			TerrainLayer grass = new TerrainLayer();
			TerrainLayer rock = new TerrainLayer();
			TerrainData terrainData = new TerrainData();
			PhysicsMaterial ballMaterial = new PhysicsMaterial("Ball");
			GameObject terrainObject = new GameObject("Terrain Collider");
			GameObject ballObject = new GameObject("Ball Collider");

			try
			{
				terrainData.heightmapResolution = 33;
				terrainData.alphamapResolution = 16;
				terrainData.size = new Vector3(10f, 1f, 10f);
				terrainData.terrainLayers = new[] { grass, rock };

				float[,,] weights = new float[16, 16, 2];
				for (int z = 0; z < 16; z++)
				{
					for (int x = 0; x < 16; x++)
					{
						weights[z, x, 0] = 0.25f;
						weights[z, x, 1] = 0.75f;
					}
				}

				terrainData.SetAlphamaps(0, 0, weights);
				TerrainCollider terrainCollider = terrainObject.AddComponent<TerrainCollider>();
				terrainCollider.terrainData = terrainData;
				BoxCollider ballCollider = ballObject.AddComponent<BoxCollider>();
				ballCollider.sharedMaterial = ballMaterial;

				Dictionary<TerrainLayer, string> terrainSurfaces =
					GetField<Dictionary<TerrainLayer, string>>(_runtime, "_terrainSurfaces");
				terrainSurfaces[grass] = "Grass";
				terrainSurfaces[rock] = "Rock";

				Dictionary<PhysicsMaterial, string> surfaces =
					GetField<Dictionary<PhysicsMaterial, string>>(_runtime, "_surfaces");
				surfaces[ballMaterial] = "Ball";

				List<PhysSoundInteraction> interactionValues =
					GetField<List<PhysSoundInteraction>>(_runtime, "_interactionValues");
				interactionValues.Add(new PhysSoundInteraction());
				interactionValues.Add(new PhysSoundInteraction());

				Dictionary<PhysSoundInteractionKey, int> interactions =
					GetField<Dictionary<PhysSoundInteractionKey, int>>(_runtime, "_interactions");
				interactions[new PhysSoundInteractionKey("Ball", "Grass")] = 1;
				interactions[new PhysSoundInteractionKey("Ball", "Rock")] = 2;

				bool composed = (bool)InvokeWithResult(
					_runtime,
					"TryBuildTerrainInteractions",
					terrainCollider,
					ballCollider,
					new Vector3(5f, 0f, 5f));

				Assert.That(composed, Is.True);
				List<PhysSoundWeightedInteraction> composition =
					GetField<List<PhysSoundWeightedInteraction>>(_runtime, "_weightedInteractions");
				Assert.That(composition, Has.Count.EqualTo(2));
				Assert.That(GetWeight(composition, 1), Is.EqualTo(0.25f).Within(0.02f));
				Assert.That(GetWeight(composition, 2), Is.EqualTo(0.75f).Within(0.02f));
			}
			finally
			{
				Object.DestroyImmediate(terrainObject);
				Object.DestroyImmediate(ballObject);
				Object.DestroyImmediate(terrainData);
				Object.DestroyImmediate(grass);
				Object.DestroyImmediate(rock);
				Object.DestroyImmediate(ballMaterial);
			}
		}
#endif

		private Dictionary<PhysSoundContinuousKey, int> GetContinuousEmitters()
		{
			return GetField<Dictionary<PhysSoundContinuousKey, int>>(_runtime, "_continuousEmitters");
		}

		private void ReportSlideContact(float speed)
		{
			Invoke(_runtime, "UpdateSlide", PairKey, 0, Vector3.zero, speed);
		}

		private int GetActiveEmitterCount(PhysSoundEmitterMode mode)
		{
			PhysSoundEmitter[] emitters = GetField<PhysSoundEmitter[]>(_runtime, "_emitters");
			int count = 0;

			for (int i = 0; i < emitters.Length; i++)
			{
				if (emitters[i].Mode == mode)
				{
					count++;
				}
			}

			return count;
		}

#if PHYS_SOUND_TERRAIN
		private static float GetWeight(List<PhysSoundWeightedInteraction> composition, int interactionIndex)
		{
			for (int i = 0; i < composition.Count; i++)
			{
				if (composition[i].InteractionIndex == interactionIndex)
				{
					return composition[i].Weight;
				}
			}

			return 0f;
		}
#endif

		private static T GetField<T>(object target, string fieldName)
		{
			return (T)target.GetType().GetField(fieldName, InstancePrivate).GetValue(target);
		}

		private static void Invoke(object target, string methodName, params object[] arguments)
		{
			target.GetType().GetMethod(methodName, InstancePrivate).Invoke(target, arguments);
		}

		private static object InvokeWithResult(object target, string methodName, params object[] arguments)
		{
			return target.GetType().GetMethod(methodName, InstancePrivate).Invoke(target, arguments);
		}

		private static object InvokeStatic(string methodName, System.Type[] parameterTypes, params object[] arguments)
		{
			MethodInfo method = typeof(PhysSoundRuntime).GetMethod(
				methodName,
				BindingFlags.Static | BindingFlags.NonPublic,
				null,
				parameterTypes,
				null);
			return method.Invoke(null, arguments);
		}

		private static void SetField(object target, string fieldName, object value)
		{
			target.GetType().GetField(fieldName, InstancePrivate).SetValue(target, value);
		}
	}
}
#endif
