using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Geuneda.Core;
using Geuneda.Services;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class TimeServiceTest
	{
		private static readonly DateTime ServerTime = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

		private FakeClock _clock;
		private TimeService _timeService;
		private ILogSink _originalSink;
		private List<LogEntry> _logs;

		private sealed class FakeClock : IMonotonicClock
		{
			public TimeSpan Elapsed { get; set; } = TimeSpan.FromHours(5);
		}

		private sealed class ListSink : ILogSink
		{
			private readonly List<LogEntry> _entries;

			public ListSink(List<LogEntry> entries)
			{
				_entries = entries;
			}

			public void Write(in LogEntry entry)
			{
				_entries.Add(entry);
			}
		}

		[SetUp]
		public void Init()
		{
			_logs = new List<LogEntry>();
			_originalSink = Log.Sink;
			Log.Sink = new ListSink(_logs);
			_clock = new FakeClock();
			_timeService = new TimeService(_clock);
		}

		[TearDown]
		public void TearDown()
		{
			Log.Sink = _originalSink;
		}

		[Test]
		public void Constructor_StartsFromDeviceUtc_NotServerTime()
		{
			var before = DateTime.UtcNow;
			var timeService = new TimeService(_clock);
			var after = DateTime.UtcNow;

			var now = timeService.DateTimeUtcNow;

			Assert.AreEqual(DateTimeKind.Utc, now.Kind);
			Assert.GreaterOrEqual(now, before);
			Assert.LessOrEqual(now, after);
			Assert.IsFalse(timeService.IsServerTime);
		}

		[Test]
		public void DefaultConstructor_UsesPlatformClock()
		{
			var before = DateTime.UtcNow;
			var now = new TimeService().DateTimeUtcNow;

			Assert.AreEqual(0, (now - before).TotalSeconds, 0.5);
		}

		[Test]
		public void DateTimeUtcNow_FollowsMonotonicClockOnly()
		{
			var start = _timeService.DateTimeUtcNow;

			_clock.Elapsed += TimeSpan.FromMinutes(90);

			// 기기 시계가 아니라 단조 시계의 경과만큼 정확히 흐릅니다
			Assert.AreEqual(start + TimeSpan.FromMinutes(90), _timeService.DateTimeUtcNow);
		}

		[Test]
		public void SetServerTime_AnchorsToServerTime()
		{
			_timeService.SetServerTime(ServerTime);

			Assert.IsTrue(_timeService.IsServerTime);
			Assert.AreEqual(ServerTime, _timeService.DateTimeUtcNow);
			Assert.AreEqual(DateTimeKind.Utc, _timeService.DateTimeUtcNow.Kind);

			_clock.Elapsed += TimeSpan.FromSeconds(10.25);

			Assert.AreEqual(ServerTime.AddSeconds(10.25), _timeService.DateTimeUtcNow);
			Assert.AreEqual((long) (ServerTime.AddSeconds(10.25) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds, _timeService.UnixTimeNow);
		}

		[Test]
		public void SetServerTime_UnspecifiedKind_Throws()
		{
			var unspecified = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);

			Assert.Throws<ArgumentException>(() => _timeService.SetServerTime(unspecified));
			Assert.IsFalse(_timeService.IsServerTime);
		}

		[Test]
		public void SetServerTime_LocalKind_IsConvertedToUtc()
		{
			var local = ServerTime.ToLocalTime();

			_timeService.SetServerTime(local);

			Assert.AreEqual(ServerTime, _timeService.DateTimeUtcNow);
		}

		[Test]
		public void TimeAdjusted_ReportsDelta()
		{
			var deltas = new List<TimeSpan>();
			_timeService.TimeAdjusted += deltas.Add;
			var before = _timeService.DateTimeUtcNow;

			_timeService.SetServerTime(before + TimeSpan.FromHours(1));
			_timeService.AddTime(-30f);
			_timeService.SetServerTime(before);

			CollectionAssert.AreEqual(new[]
			{
				TimeSpan.FromHours(1),
				TimeSpan.FromSeconds(-30),
				TimeSpan.FromHours(-1)
			}, deltas);
		}

		[Test]
		public void TimeAdjusted_HandlerException_IsLoggedAndOthersStillRun()
		{
			var called = false;
			_timeService.TimeAdjusted += _ => throw new InvalidOperationException("handler failure");
			_timeService.TimeAdjusted += _ => called = true;

			_timeService.SetServerTime(ServerTime);

			Assert.IsTrue(called);
			Assert.AreEqual(ServerTime, _timeService.DateTimeUtcNow);
			Assert.IsTrue(_logs.Exists(entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException));
		}

		[Test]
		public void AddTime_PersistsAcrossSetServerTime()
		{
			_timeService.AddTime(60f);
			_timeService.SetServerTime(ServerTime);

			Assert.AreEqual(ServerTime.AddSeconds(60), _timeService.DateTimeUtcNow);
		}

		[Test]
		public void AddTime_AllTimeTypes_Successfully()
		{
			var extraTime = 50.5f;
			var dateTime = _timeService.DateTimeUtcNow;
			var unixTime = _timeService.UnixTimeNow;
			var unityTime = _timeService.UnityTimeNow;
			var scaleTime = _timeService.UnityScaleTimeNow;

			_timeService.AddTime(extraTime);

			Assert.AreEqual(dateTime.AddSeconds(extraTime), _timeService.DateTimeUtcNow);
			Assert.AreEqual(unixTime + 50500, _timeService.UnixTimeNow);
			Assert.AreEqual(unityTime + extraTime, _timeService.UnityTimeNow, 0.1f);
			Assert.AreEqual(scaleTime + extraTime, _timeService.UnityScaleTimeNow, 0.1f);
		}

		[Test]
		public void AddTime_NegativeValue_SubtractsTime()
		{
			var initialUnityTime = _timeService.UnityTimeNow;

			_timeService.AddTime(-10f);

			Assert.AreEqual(initialUnityTime - 10f, _timeService.UnityTimeNow, 0.1f);
		}

		[Test]
		public void UnixConversions_UseUtc()
		{
			var oneSecond = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc);

			Assert.AreEqual(1000, _timeService.UnixTimeFromDateTimeUtc(oneSecond));
			Assert.AreEqual(1000, _timeService.UnixTimeFromDateTimeUtc(oneSecond.ToLocalTime()));
			Assert.AreEqual(oneSecond, _timeService.DateTimeUtcFromUnixTime(1000));
			Assert.AreEqual(DateTimeKind.Utc, _timeService.DateTimeUtcFromUnixTime(1000).Kind);
			Assert.Throws<ArgumentException>(() => _timeService.UnixTimeFromDateTimeUtc(new DateTime(1970, 1, 1, 0, 0, 1)));
		}

		[Test]
		public void UnityTimeConversions_RoundTripThroughServerAnchor()
		{
			_timeService.SetServerTime(ServerTime);
			var unityNow = Time.realtimeSinceStartup;

			var converted = _timeService.DateTimeUtcFromUnityTime(unityNow);

			// 기준점을 잡은 뒤 이 줄까지 흐른 실제 시간만큼만 차이 납니다(Unity 시간이 float 라 조금 앞설 수 있음)
			Assert.GreaterOrEqual((converted - ServerTime).TotalSeconds, -0.05);
			Assert.LessOrEqual((converted - ServerTime).TotalSeconds, 0.5);
			Assert.AreEqual(unityNow, _timeService.UnityTimeFromDateTimeUtc(converted), 0.01f);
			Assert.AreEqual(unityNow, _timeService.UnityTimeFromUnixTime(_timeService.UnixTimeFromUnityTime(unityNow)), 0.01f);
			Assert.Throws<ArgumentException>(() => _timeService.UnityTimeFromDateTimeUtc(new DateTime(2030, 1, 1)));
		}

		[Test]
		public void DateTimeUtcNow_ReadableFromOtherThreads()
		{
			_timeService.SetServerTime(ServerTime);

			var fromThreadPool = Task.Run(() => (_timeService.DateTimeUtcNow, _timeService.IsServerTime, _timeService.UnixTimeNow)).Result;

			Assert.AreEqual(ServerTime, fromThreadPool.DateTimeUtcNow);
			Assert.IsTrue(fromThreadPool.IsServerTime);
		}
	}
}
