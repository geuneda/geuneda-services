using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// <see cref="DataService"/> 로 저장하는 데이터 타입의 세이브 스키마 버전을 지정합니다. 지정하지 않으면 0 입니다.
	/// </summary>
	/// <remarks>
	/// 필드 구조를 바꿀 때 버전을 올리고, 이전 버전 세이브를 바꾸는 단계를 <see cref="DataService.AddMigration{T}"/> 로 등록합니다.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class DataVersionAttribute : Attribute
	{
		/// <summary>
		/// 이 타입의 현재 세이브 스키마 버전
		/// </summary>
		public int Version { get; }

		/// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> 이 음수인 경우</exception>
		public DataVersionAttribute(int version)
		{
			if (version < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(version), version, "Data version must not be negative");
			}

			Version = version;
		}
	}
}
