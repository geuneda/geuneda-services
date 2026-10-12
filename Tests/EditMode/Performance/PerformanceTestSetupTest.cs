using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	/// <summary>
	/// <see cref="PerformanceTestSetup"/> 의 회귀를 감지하는 테스트입니다.
	///
	/// Unity Performance Testing Package 는 EditMode 에서 두 개의 PlayerPrefs 항목을 요구합니다:
	///   - PT_Run      (전체 Run 메타데이터이며, 결과가 방출될 때 사용됩니다)
	///   - PT_Settings (RunSettings 이며, 워밍업 전에 MethodMeasurement.SettingsOverride() 에서 사용됩니다)
	///
	/// PT_Settings 가 없으면 RunSettings.Instance 가 빈 JSON 에서 지연 로드되고, 로더가
	/// 조용히 null 을 반환하며, SettingsOverride() 가 첫 .Run() 호출에서 NullReferenceException 을
	/// 던집니다 — 실제 성능 테스트 로직의 문제가 도움이 안 되는 인프라 실패에 가려집니다.
	///
	/// 이후 PerformanceTestSetup 이 변경되어 PT_Settings 를 미리 채우지 않게 되면
	/// (명확한 클래스 이름과 함께) 바로 실패하도록 하기 위해 이 픽스처가 존재합니다.
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
