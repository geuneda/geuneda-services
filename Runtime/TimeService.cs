using System;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 게임 시계를 기준으로 시간을 제공하는 서비스입니다.
	/// 제공되는 시간은 <see cref="ITimeManipulator"/>의 조작에 영향을 받습니다.
	/// </summary>
	public interface ITimeService
	{
		/// <summary>
		/// 게임 기준 현재 그레고리력 UTC 시간
		/// </summary>
		DateTime DateTimeUtcNow { get; }
		/// <summary>
		/// 게임 시작 이후 현재 Unity 시간
		/// </summary>
		float UnityTimeNow { get; }
		/// <inheritdoc cref="Time.time"/>
		float UnityScaleTimeNow { get; }
		/// <summary>
		/// 밀리초 단위의 현재 Unix 시간
		/// </summary>
		long UnixTimeNow { get; }
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
	/// 게임 속도를 높이거나 외부 소스와 시간을 동기화하려는 경우에 유용합니다.
	/// </remarks>
	public interface ITimeManipulator : ITimeService
	{
		/// <summary>
		/// 현재 게임 시계에 <paramref name="timeInSeconds"/>를 추가합니다.
		/// 양수이면 시간을 앞으로 이동하고, 음수이면 주어진 초만큼 시간을 되돌립니다.
		/// </summary>
		void AddTime(float timeInSeconds);
		
		/// <summary>
		/// 게임 시작 시간을 주어진 <paramref name="initialTime"/>으로 동기화합니다
		/// </summary>
		void SetInitialTime(DateTime initialTime);
	}

	/// <inheritdoc cref="ITimeService"/>
	public class TimeService : ITimeManipulator
	{
		private static readonly DateTime UnixInitialTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		private float _initialUnityTime;
		private float _extraTime;
		private DateTime _initialTime = DateTime.MinValue;

		/// <inheritdoc />
		public DateTime DateTimeUtcNow => _initialTime.AddSeconds(Time.realtimeSinceStartup - _initialUnityTime).AddSeconds(_extraTime).ToUniversalTime();
		/// <inheritdoc />
		public float UnityTimeNow => Time.realtimeSinceStartup + _extraTime;
		/// <inheritdoc />
		public float UnityScaleTimeNow => Time.time + _extraTime;
		/// <inheritdoc />
		public long UnixTimeNow => (long)( DateTimeUtcNow - UnixInitialTime ).TotalMilliseconds;

		public TimeService()
		{
			_initialUnityTime = Time.realtimeSinceStartup;

			if (_initialTime == DateTime.MinValue)
			{
				_initialTime = DateTime.Now;
			}
		}

		/// <inheritdoc />
		public long UnixTimeFromDateTimeUtc(DateTime time)
		{
			return (long)( time.ToUniversalTime() - UnixInitialTime ).TotalMilliseconds;
		}

		/// <inheritdoc />
		public long UnixTimeFromUnityTime(float time)
		{
			return UnixTimeFromDateTimeUtc(DateTimeUtcFromUnityTime(time));
		}

		/// <inheritdoc />
		public DateTime DateTimeUtcFromUnixTime(long time)
		{
			return UnixInitialTime.AddMilliseconds(time).ToUniversalTime();
		}

		/// <inheritdoc />
		public DateTime DateTimeUtcFromUnityTime(float time)
		{
			return _initialTime.AddSeconds(time - _initialUnityTime).ToUniversalTime();
		}

		/// <inheritdoc />
		public float UnityTimeFromDateTimeUtc(DateTime time)
		{
			return (float) (time.ToUniversalTime() - _initialTime.ToUniversalTime()).TotalSeconds + _initialUnityTime;
		}

		/// <inheritdoc />
		public float UnityTimeFromUnixTime(long time)
		{
			return UnityTimeFromDateTimeUtc(DateTimeUtcFromUnixTime(time));
		}

		/// <inheritdoc />
		public void AddTime(float timeInSeconds)
		{
			_extraTime += timeInSeconds;
		}

		/// <inheritdoc />
		public void SetInitialTime(DateTime initialTime)
		{
			_initialTime = initialTime;
			_initialUnityTime = Time.realtimeSinceStartup;
		}
	}
}