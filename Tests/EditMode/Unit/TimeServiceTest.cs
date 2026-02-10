using System;
using Geuneda.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class TimeServiceTest
	{
		private const float ErrorValue = 0.01f;
		private TimeService _timeService;

		[SetUp]
		public void Init()
		{
			_timeService = new TimeService();
		}

		[Test]
		public void DateTime_Convertions_Successfully()
		{
			Assert.GreaterOrEqual(ErrorValue, (_timeService.DateTimeUtcFromUnityTime(_timeService.UnityTimeNow) - _timeService.DateTimeUtcNow).TotalMilliseconds);
			Assert.GreaterOrEqual(ErrorValue, (_timeService.DateTimeUtcFromUnixTime(_timeService.UnixTimeNow) - _timeService.DateTimeUtcNow).TotalMilliseconds);
		}

		[Test]
		public void UnityTime_Convertions_Successfully()
		{
			Assert.GreaterOrEqual(ErrorValue, _timeService.UnityTimeFromDateTimeUtc(_timeService.DateTimeUtcNow) - _timeService.UnityTimeNow);
			Assert.GreaterOrEqual(ErrorValue, _timeService.UnityTimeFromUnixTime(_timeService.UnixTimeNow) - _timeService.UnityTimeNow);
		}

		[Test]
		public void UnixTime_Convertions_Successfully()
		{
			Assert.GreaterOrEqual(ErrorValue, _timeService.UnixTimeFromDateTimeUtc(_timeService.DateTimeUtcNow) - _timeService.UnixTimeNow);
			Assert.GreaterOrEqual(ErrorValue, _timeService.UnixTimeFromUnityTime(_timeService.UnityTimeNow) - _timeService.UnixTimeNow);
		}

		[Test]
		public void AddTime_AllTimeTypes_Successfully()
		{
			var extraTime = 50.5f;
			var extraTimeInMilliseconds = TimeSpan.FromSeconds(extraTime).TotalMilliseconds;
			var dateTime = _timeService.DateTimeUtcNow;
			var unityTime = _timeService.UnityTimeNow;
			var unixTime = _timeService.UnixTimeNow;

			_timeService.AddTime(extraTime);

			Assert.LessOrEqual(0, _timeService.DateTimeUtcNow.CompareTo(dateTime.AddSeconds(extraTime)));
			Assert.GreaterOrEqual(_timeService.UnityTimeNow, unityTime + extraTime);
			Assert.GreaterOrEqual(_timeService.UnixTimeNow, unixTime - extraTimeInMilliseconds);
		}

		[Test]
		public void AddTime_NegativeValue_SubtractsTime()
		{
			var initialUnityTime = _timeService.UnityTimeNow;
			var negativeTime = -10f;

			_timeService.AddTime(negativeTime);

			Assert.Less(_timeService.UnityTimeNow, initialUnityTime);
			Assert.That(_timeService.UnityTimeNow, Is.EqualTo(initialUnityTime + negativeTime).Within(ErrorValue));
		}

		[Test]
		public void SetInitialTime_ResetsTimeBase()
		{
			// SetInitialTime은 시간 기준을 동기화하여 "리셋" 역할을 합니다
			var customInitialTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
			
			_timeService.SetInitialTime(customInitialTime);
			
			// 초기 시간 설정 후, DateTimeUtcNow는 사용자 지정 시간에 가까워야 합니다
			// (realtimeSinceStartup이 캡처된 이후 경과한 시간 포함)
			var now = _timeService.DateTimeUtcNow;
			
			// 차이는 매우 작아야 합니다 (SetInitialTime 호출 이후 경과한 시간만큼)
			Assert.That((now - customInitialTime).TotalSeconds, Is.LessThan(1.0));
		}
	}
}
