#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D && PHYS_SOUND_PERFORMANCE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;

namespace PhysSound.Tests
{
	internal sealed class PhysSoundRuntimePerformanceTests
	{
		private const int ContactEvaluationsPerIteration = 100;
		private const int InteractionCount = 256;

		private AudioClip _clip;
		private PhysSoundInteraction _interaction;

		[SetUp]
		public void SetUp()
		{
			_clip = AudioClip.Create("Performance Clip", 64, 1, 44100, false);
			_interaction = new PhysSoundInteraction();

			PhysSoundImpactRange range = new(0f, 10f);
			for (int i = 0; i < 64; i++)
			{
				range.ClipSources.Add(new PhysSoundImpactClipSource(_clip, new[] { _clip }));
			}

			_interaction.ImpactRanges.Add(range);
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_clip);
		}

		[Test, Performance]
		public void ContactCurveEvaluation()
		{
			float result = 0f;

			Measure.Method(() =>
				{
					for (int i = 0; i < ContactEvaluationsPerIteration; i++)
					{
						float value = i * 0.05f;
						result += _interaction.EvaluateImpactVolume(value);
						result += _interaction.EvaluateImpactPitch(value);
						result += _interaction.EvaluateSlideVolume(value);
						result += _interaction.EvaluateSlidePitch(value);
					}
				})
				.SampleGroup("Contact curve evaluation")
				.WarmupCount(5)
				.MeasurementCount(10)
				.IterationsPerMeasurement(100)
				.GC()
				.Run();

			Assert.That(result, Is.GreaterThan(0f));
		}

		[Test, Performance]
		public void ImpactPlaybackSelection()
		{
			PhysSoundImpactPlayback playback = default;

			Measure.Method(() =>
				{
					for (int i = 0; i < ContactEvaluationsPerIteration; i++)
					{
						_interaction.TryGetImpactPlayback(5f, out playback);
					}
				})
				.SampleGroup("Impact playback selection")
				.WarmupCount(5)
				.MeasurementCount(10)
				.IterationsPerMeasurement(100)
				.GC()
				.Run();

			Assert.That(playback.Clip, Is.SameAs(_clip));
		}

		[Test, Performance]
		public void SettingsLookupBuild()
		{
			PhysSoundSettings settings = ScriptableObject.CreateInstance<PhysSoundSettings>();
			try
			{
				Dictionary<PhysicsMaterial, string> surfaces = new(InteractionCount);
				Dictionary<PhysSoundInteractionKey, int> interactions = new(InteractionCount + 1);
				List<PhysSoundInteraction> interactionValues = new(InteractionCount + 1);

				for (int i = 0; i < InteractionCount; i++)
				{
					string surfaceA = $"Surface {i}";
					string surfaceB = $"Surface {i + 1}";
					settings.Surfaces.Add(surfaceA, new PhysSoundSurface());
					settings.Interactions.Add(
						new PhysSoundInteractionKey(surfaceA, surfaceB),
						new PhysSoundInteraction());
				}

				Measure.Method(() => settings.BuildLookups(surfaces, interactions, interactionValues))
					.SampleGroup("Settings lookup build")
					.WarmupCount(5)
					.MeasurementCount(10)
					.IterationsPerMeasurement(10)
					.GC()
					.Run();

				Assert.That(interactions, Has.Count.EqualTo(InteractionCount + 1));
			}
			finally
			{
				Object.DestroyImmediate(settings);
			}
		}

	}
}
#endif
