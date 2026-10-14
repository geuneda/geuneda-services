using System;
using System.Threading;
using Geuneda.Core;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 게임 시계를 기준으로 시간을 제공하는 서비스입니다.
	/// 제공되는 시간은 <see cref="ITimeManipulator"/>의 조작에 영향을 받습니다.
	/// </summary>
	/// <remarks>
	/// 현재 UTC 는 기준점(UTC 시각과 그때의 단조 시계 값)에 단조 시계(core <see cref="IMonotonicClock"/>)의 경과를 더해 계산합니다.
	/// 그래서 서비스를 만든 뒤에 기기 시계를 바꿔도 현재 시각은 바뀌지 않습니다. 기준점은 처음에는 생성할 때의 기기 시계이고,
	/// <see cref="ITimeManipulator.SetServerTime"/> 으로 서버 시각을 넣으면 서버 기준이 됩니다(<see cref="IsServerTime"/>).
	/// <c>DateTime</c> 을 받는 메서드는 <c>Kind</c> 가 <c>Utc</c> 나 <c>Local</c>(UTC 로 바꿈)이어야 하고, <c>Unspecified</c> 면 <see cref="ArgumentException"/> 입니다.
	/// </remarks>
	public interface ITimeService
	{
		/// <summary>
		/// 게임 기준 현재 그레고리력 UTC 시간(<c>Kind</c> 는 <c>Utc</c>)입니다. 어느 스레드에서 읽어도 됩니다.
		/// </summary>
		DateTime DateTimeUtcNow { get; }
		/// <summary>
		/// 현재 시각이 서버 시각 기준인지입니다. <see cref="ITimeManipulator.SetServerTime"/> 을 부른 뒤로 true 이고,
		/// 그 전에는 생성할 때의 기기 시계 기준입니다. 어느 스레드에서 읽어도 됩니다.
		/// </summary>
		bool IsServerTime { get; }
		/// <summary>
		/// 게임 시작 이후 현재 Unity 시간
		/// </summary>
		float UnityTimeNow { get; }
		/// <inheritdoc cref="Time.time"/>
		float UnityScaleTimeNow { get; }
		/// <summary>
		/// 밀리초 단위의 현재 Unix 시간입니다. 어느 스레드에서 읽어도 됩니다.
		/// </summary>
		long UnixTimeNow { get; }
		/// <summary>
		/// 현재 시각이 흐름과 별개로 바뀌었을 때(<see cref="ITimeManipulator.SetServerTime"/>, <see cref="ITimeManipulator.AddTime"/>) 발생합니다.
		/// 인자는 바뀐 양(새 현재 시각 - 이전 현재 시각)이고 음수일 수 있습니다. 처리기 예외는 로그만 남기고 다음 처리기로 넘어갑니다.
		/// </summary>
		event Action<TimeSpan> TimeAdjusted;
		/// <summary>
		/// 그레고리력 UTC <paramref name="time"/>을 밀리초 단위의 Unix 시간으로 변환합니다
		/// </summary>
		long UnixTimeFromDateTimeUtc(DateTime time);
		/// <summary>
		/// Unity <paramref name="time"/>을 밀리초 단위의 Unix 시간으로 변환합니다
		/// </summary>
		long UnixTimeFromUnityTime(float time);
		/// <summary>
		/// 밀리초 단위의 Unix <paramref name="time"/>을 UTC 그레고리력 시간으로 변환합니다
		/// </summary>
		DateTime DateTimeUtcFromUnixTime(long time);
		/// <summary>
		/// Unity <paramref name="time"/>을 UTC 그레고리력 시간으로 변환합니다
		/// </summary>
		DateTime DateTimeUtcFromUnityTime(float time);
		/// <summary>
		/// 그레고리력 UTC <paramref name="time"/>을 Unity 시간으로 변환합니다
		/// </summary>
		float UnityTimeFromDateTimeUtc(DateTime time);
		/// <summary>
		/// 밀리초 단위의 Unix <paramref name="time"/>을 Unity 시간으로 변환합니다
		/// </summary>
		float UnityTimeFromUnixTime(long time);
	}

	/// <inheritdoc cref="ITimeService"/>
	/// <remarks>
	/// 서비스의 시간을 조작합니다.
	/// 게임 속도를 높이거나 외부 소스와 시간을 동기화하려는 경우에 유용합니다. 메인 스레드에서 부릅니다.
	/// </remarks>
	public interface ITimeManipulator : ITimeService
	{
		/// <summary>
		/// 현재 게임 시계에 <paramref name="timeInSeconds"/>를 추가합니다.
		/// 양수이면 시간을 앞으로 이동하고, 음수이면 주어진 초만큼 시간을 되돌립니다.
		/// 더한 시간은 <see cref="SetServerTime"/> 뒤에도 유지됩니다.
		/// </summary>
		void AddTime(float timeInSeconds);

		/// <summary>
		/// 지금 이 순간의 서버 시각 <paramref name="serverUtcNow"/> 를 기준점으로 삼습니다. 이후 <see cref="ITimeService.IsServerTime"/> 은 true 입니다.
		/// 왕복 지연 보정과 재동기화는 com.geuneda.servertime 이 합니다.
		/// </summary>
		/// <exception cref="ArgumentException"><paramref name="serverUtcNow"/> 의 <c>Kind</c> 가 <c>Unspecified</c> 인 경우</exception>
		void SetServerTime(DateTime serverUtcNow);
	}

	/// <inheritdoc cref="ITimeService"/>
	public class TimeService : ITimeManipulator
	{
		private static readonly DateTime UnixInitialTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Services.Time");

		private readonly IMonotonicClock _clock;
		// 바꿀 때마다 새로 만드는 불변 객체라 다른 스레드가 읽어도 기준점 값이 섞이지 않습니다
		private Anchor _anchor;

		internal float ExtraTime => (float) Volatile.Read(ref _anchor).ExtraSeconds;
		internal DateTime AnchorUtc => Volatile.Read(ref _anchor).Utc;

		/// <inheritdoc />
		public DateTime DateTimeUtcNow => Now(Volatile.Read(ref _anchor), _clock.Elapsed);
		/// <inheritdoc />
		public bool IsServerTime => Volatile.Read(ref _anchor).IsServerTime;
		/// <inheritdoc />
		public float UnityTimeNow => Time.realtimeSinceStartup + ExtraTime;
		/// <inheritdoc />
		public float UnityScaleTimeNow => Time.time + ExtraTime;
		/// <inheritdoc />
		public long UnixTimeNow => (long) (DateTimeUtcNow - UnixInitialTime).TotalMilliseconds;

		/// <inheritdoc />
		public event Action<TimeSpan> TimeAdjusted;

		/// <summary>플랫폼 기본 단조 시계(core <see cref="MonotonicClock"/>)로 만듭니다.</summary>
		public TimeService() : this(new MonotonicClock())
		{
		}

		/// <summary><paramref name="clock"/> 으로 시간을 진행합니다. 서버 시간 동기화와 같은 시계를 써야 왕복 지연 보정이 맞습니다.</summary>
		public TimeService(IMonotonicClock clock)
		{
			_clock = clock ?? throw new ArgumentNullException(nameof(clock));
			_anchor = new Anchor(DateTime.UtcNow, _clock.Elapsed, Time.realtimeSinceStartupAsDouble, false, 0);
		}

		/// <inheritdoc />
		public long UnixTimeFromDateTimeUtc(DateTime time)
		{
			return (long) (ToUtc(time, nameof(time)) - UnixInitialTime).TotalMilliseconds;
		}

		/// <inheritdoc />
		public long UnixTimeFromUnityTime(float time)
		{
			return UnixTimeFromDateTimeUtc(DateTimeUtcFromUnityTime(time));
		}

		/// <inheritdoc />
		public DateTime DateTimeUtcFromUnixTime(long time)
		{
			return UnixInitialTime.AddMilliseconds(time);
		}

		/// <inheritdoc />
		public DateTime DateTimeUtcFromUnityTime(float time)
		{
			var anchor = Volatile.Read(ref _anchor);

			return anchor.Utc + FromSeconds(time - anchor.UnityTime);
		}

		/// <inheritdoc />
		public float UnityTimeFromDateTimeUtc(DateTime time)
		{
			var anchor = Volatile.Read(ref _anchor);

			return (float) ((ToUtc(time, nameof(time)) - anchor.Utc).TotalSeconds + anchor.UnityTime);
		}

		/// <inheritdoc />
		public float UnityTimeFromUnixTime(long time)
		{
			return UnityTimeFromDateTimeUtc(DateTimeUtcFromUnixTime(time));
		}

		/// <inheritdoc />
		public void AddTime(float timeInSeconds)
		{
			var current = _anchor;

			Volatile.Write(ref _anchor, new Anchor(current.Utc, current.Elapsed, current.UnityTime, current.IsServerTime,
				current.ExtraSeconds + timeInSeconds));
			RaiseTimeAdjusted(FromSeconds(timeInSeconds));
		}

		/// <inheritdoc />
		public void SetServerTime(DateTime serverUtcNow)
		{
			var utc = ToUtc(serverUtcNow, nameof(serverUtcNow));
			var elapsed = _clock.Elapsed;
			var previous = _anchor;
			var next = new Anchor(utc, elapsed, Time.realtimeSinceStartupAsDouble, true, previous.ExtraSeconds);

			Volatile.Write(ref _anchor, next);
			RaiseTimeAdjusted(Now(next, elapsed) - Now(previous, elapsed));
		}

		private void RaiseTimeAdjusted(TimeSpan delta)
		{
			foreach (var handler in TimeAdjusted?.GetInvocationList() ?? Array.Empty<Delegate>())
			{
				try
				{
					((Action<TimeSpan>) handler)(delta);
				}
				catch (Exception exception)
				{
					Logger.Error(exception);
				}
			}
		}

		private static DateTime Now(Anchor anchor, TimeSpan elapsed)
		{
			return anchor.Utc + (elapsed - anchor.Elapsed) + FromSeconds(anchor.ExtraSeconds);
		}

		// TimeSpan.FromSeconds 는 런타임에 따라 밀리초로 반올림하므로 틱으로 바꿉니다
		private static TimeSpan FromSeconds(double seconds)
		{
			return TimeSpan.FromTicks((long) (seconds * TimeSpan.TicksPerSecond));
		}

		private static DateTime ToUtc(DateTime time, string parameterName)
		{
			switch (time.Kind)
			{
				case DateTimeKind.Utc:
					return time;
				case DateTimeKind.Local:
					return time.ToUniversalTime();
				default:
					throw new ArgumentException("DateTime.Kind must be Utc or Local; Unspecified is ambiguous", parameterName);
			}
		}

		private sealed class Anchor
		{
			public readonly DateTime Utc;
			public readonly TimeSpan Elapsed;
			public readonly double UnityTime;
			public readonly bool IsServerTime;
			public readonly double ExtraSeconds;

			public Anchor(DateTime utc, TimeSpan elapsed, double unityTime, bool isServerTime, double extraSeconds)
			{
				Utc = utc;
				Elapsed = elapsed;
				UnityTime = unityTime;
				IsServerTime = isServerTime;
				ExtraSeconds = extraSeconds;
			}
		}
	}
}
