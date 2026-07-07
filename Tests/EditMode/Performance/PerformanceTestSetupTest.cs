using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	/// <summary>
	/// Regression sentinel for <see cref="PerformanceTestSetup"/>.
	///
	/// The Unity Performance Testing Package requires two PlayerPrefs entries in EditMode:
	///   - PT_Run      (full run metadata; consumed when results are emitted)
	///   - PT_Settings (RunSettings; consumed by MethodMeasurement.SettingsOverride() before warmup)
	///
	/// If PT_Settings is missing, RunSettings.Instance lazy-loads from an empty JSON, the loader
	/// silently returns null, and SettingsOverride() throws a NullReferenceException at the very
	/// first .Run() call — masking real perf-test logic behind an unhelpful infrastructure failure.
	///
	/// This fixture exists to fail fast (with a clear class name) if a future change to
	/// PerformanceTestSetup ever stops priming PT_Settings.
	/// </summary>
	[TestFixture]
	[Category("Performance")]
	[PrebuildSetup(typeof(PerformanceTestSetup))]
	public class PerformanceTestSetupTest
	{
		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			PerformanceTestSetup.InitializePerformanceTestMetadata();
		}

		[Test, Performance]
		public void MeasureMethod_AfterInitialize_DoesNotThrow()
		{
			Assert.DoesNotThrow(() =>
			{
				Measure.Method(() => { })
					.WarmupCount(1)
					.MeasurementCount(1)
					.Run();
			});
		}
	}
}
