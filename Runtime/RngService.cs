using System;
using Geuneda.Core;
using Geuneda.DataExtensions;

namespace Geuneda.Services
{
	/// <summary>
	/// RNG 상태를 읽기 전용으로 봅니다. <see cref="Seed"/>, <see cref="Stream"/>, <see cref="Count"/> 세 값이 상태 전부입니다.
	/// </summary>
	public interface IRngData
	{
		/// <summary>
		/// RNG 를 만든 시드입니다.
		/// </summary>
		ulong Seed { get; }

		/// <summary>
		/// 수열을 고르는 스트림 번호입니다. 시드가 같아도 스트림이 다르면 다른 수열입니다(스트림 공간은 2^63, <see cref="Pcg32"/> 참고).
		/// </summary>
		ulong Stream { get; }

		/// <summary>
		/// 지금까지 쓴 32비트 출력의 수입니다. 범위 함수는 드물게 한 번에 여러 개를 씁니다.
		/// </summary>
		ulong Count { get; }
	}

	/// <summary>
	/// RNG 상태를 저장하는 데이터입니다.
	/// </summary>
	/// <remarks>
	/// <see cref="RngService"/> 가 이 객체의 <see cref="Count"/> 를 직접 바꾸므로 이 객체를 저장하면 현재 상태가 저장됩니다.
	/// 필드를 바꾸면(예: 세이브 불러오기) 서비스는 다음 호출부터 바뀐 상태를 씁니다.
	/// </remarks>
	public class RngData : IRngData
	{
		/// <summary>
		/// RNG 를 만든 시드
		/// </summary>
		public ulong Seed;

		/// <summary>
		/// 수열을 고르는 스트림 번호
		/// </summary>
		public ulong Stream;

		/// <summary>
		/// 지금까지 쓴 32비트 출력의 수
		/// </summary>
		public ulong Count;

		/// <inheritdoc/>
		ulong IRngData.Seed => Seed;
		/// <inheritdoc/>
		ulong IRngData.Stream => Stream;
		/// <inheritdoc/>
		ulong IRngData.Count => Count;
	}

	/// <summary>
	/// 시드, 스트림, 쓴 출력 수만으로 상태를 재현하는 결정론적 난수 서비스입니다.
	/// core 의 <see cref="Pcg32"/>(PCG32 XSH-RR)를 씁니다.
	/// </summary>
	public interface IRngService
	{
		/// <summary>
		/// 이 서비스가 조작하는 <see cref="IRngData"/>
		/// </summary>
		IRngData Data { get; }

		/// <summary>
		/// 다음 32비트 값을 돌려줍니다.
		/// </summary>
		uint NextUInt();

		/// <summary>
		/// [0, 1) 의 값을 돌려줍니다. 2^-24 간격의 값 중 하나입니다.
		/// </summary>
		floatP NextFloat();

		/// <summary>
		/// <paramref name="min"/> 과 <paramref name="max"/> 사이의 균등한 정수를 돌려줍니다.
		/// <paramref name="maxInclusive"/> 가 false 면 [min, max), true 면 [min, max] 입니다.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// 범위가 비었으면 발생합니다(<c>min &gt; max</c>, 또는 <paramref name="maxInclusive"/> 가 false 인데 <c>min == max</c>).
		/// </exception>
		int Range(int min, int max, bool maxInclusive = false);

		/// <summary>
		/// <paramref name="min"/> 과 <paramref name="max"/> 사이의 값을 돌려줍니다.
		/// <paramref name="maxInclusive"/> 가 true 면 [min, max], false 면 [min, max) 입니다. 출력을 정확히 하나 씁니다.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// 경계가 유한하지 않거나 범위가 비었으면 발생합니다(<c>min &gt; max</c>, 또는 <paramref name="maxInclusive"/> 가 false 인데 <c>min == max</c>).
		/// </exception>
		floatP Range(floatP min, floatP max, bool maxInclusive = true);

		/// <inheritdoc cref="Range(int,int,bool)"/>
		/// <remarks>
		/// 상태를 바꾸지 않습니다. 같은 인자로 다시 부르면 같은 값이고, 이어서 부른 <see cref="Range(int,int,bool)"/> 도 같은 값입니다.
		/// </remarks>
		int PeekRange(int min, int max, bool maxInclusive = false);

		/// <inheritdoc cref="Range(floatP,floatP,bool)"/>
		/// <remarks>
		/// 상태를 바꾸지 않습니다. 같은 인자로 다시 부르면 같은 값이고, 이어서 부른 <see cref="Range(floatP,floatP,bool)"/> 도 같은 값입니다.
		/// </remarks>
		floatP PeekRange(floatP min, floatP max, bool maxInclusive = true);

		/// <summary>
		/// 상태를 출력 <paramref name="count"/> 개를 쓴 시점으로 옮깁니다. 과거와 미래 모두 O(log n) 입니다.
		/// </summary>
		void Restore(ulong count);
	}

	/// <inheritdoc />
	/// <remarks>
	/// 스레드 안전하지 않습니다. 스트림을 여러 개 쓰려면 스트림마다 <see cref="RngData"/> 와 <see cref="RngService"/> 를 만듭니다.
	/// </remarks>
	public class RngService : IRngService
	{
		// 2^-24: 24비트 정수를 [0, 1) 로 정확히 옮깁니다
		private static readonly floatP _unitScale = floatP.FromRaw(0x33800000);
		// 2^24 - 1: 24비트 정수를 [0, 1] 로 옮깁니다
		private static readonly floatP _inclusiveDivisor = 0xFFFFFF;

		private readonly RngData _data;
		private Pcg32 _generator;
		private ulong _generatorSeed;
		private ulong _generatorStream;
		private bool _hasGenerator;

		/// <inheritdoc />
		public IRngData Data => _data;

		/// <exception cref="ArgumentNullException"><paramref name="rngData"/> 가 null 이면 발생합니다.</exception>
		public RngService(RngData rngData)
		{
			_data = rngData ?? throw new ArgumentNullException(nameof(rngData));
		}

		/// <inheritdoc />
		public uint NextUInt()
		{
			SyncGenerator();

			var value = _generator.NextUInt();

			_data.Count = _generator.Position;

			return value;
		}

		/// <inheritdoc />
		public floatP NextFloat()
		{
			SyncGenerator();

			var value = NextFloat(ref _generator);

			_data.Count = _generator.Position;

			return value;
		}

		/// <inheritdoc />
		public int Range(int min, int max, bool maxInclusive = false)
		{
			SyncGenerator();

			var value = Range(ref _generator, min, max, maxInclusive);

			_data.Count = _generator.Position;

			return value;
		}

		/// <inheritdoc />
		public floatP Range(floatP min, floatP max, bool maxInclusive = true)
		{
			SyncGenerator();

			var value = Range(ref _generator, min, max, maxInclusive);

			_data.Count = _generator.Position;

			return value;
		}

		/// <inheritdoc />
		public int PeekRange(int min, int max, bool maxInclusive = false)
		{
			SyncGenerator();

			var copy = _generator;

			return Range(ref copy, min, max, maxInclusive);
		}

		/// <inheritdoc />
		public floatP PeekRange(floatP min, floatP max, bool maxInclusive = true)
		{
			SyncGenerator();

			var copy = _generator;

			return Range(ref copy, min, max, maxInclusive);
		}

		/// <inheritdoc />
		public void Restore(ulong count)
		{
			_data.Count = count;
		}

		/// <summary>
		/// 주어진 <paramref name="seed"/> 와 <paramref name="stream"/> 으로 <see cref="RngData"/> 를 만듭니다(<c>Count = 0</c>).
		/// </summary>
		public static RngData CreateRngData(ulong seed, ulong stream = 0)
		{
			return new RngData
			{
				Seed = seed,
				Stream = stream,
				Count = 0
			};
		}

		/// <summary>
		/// <see cref="_data"/> 가 바깥에서 바뀌었으면 생성기를 그 상태로 맞춥니다. 시드·스트림이 같으면 위치만 O(log n) 으로 옮깁니다.
		/// </summary>
		private void SyncGenerator()
		{
			if (!_hasGenerator || _generatorSeed != _data.Seed || _generatorStream != _data.Stream)
			{
				_generator = new Pcg32(_data.Seed, _data.Stream);
				_generatorSeed = _data.Seed;
				_generatorStream = _data.Stream;
				_hasGenerator = true;
			}

			if (_generator.Position != _data.Count)
			{
				_generator.Advance(unchecked(_data.Count - _generator.Position));
			}
		}

		private static int Range(ref Pcg32 generator, int min, int max, bool maxInclusive)
		{
			return maxInclusive ? generator.NextIntInclusive(min, max) : generator.NextInt(min, max);
		}

		private static floatP NextFloat(ref Pcg32 generator)
		{
			return (floatP)(int)(generator.NextUInt() >> 8) * _unitScale;
		}

		private static floatP Range(ref Pcg32 generator, floatP min, floatP max, bool maxInclusive)
		{
			// 비교 연산자는 NaN 이면 false 이고 -0 과 +0 을 같게 봅니다
			var isEmpty = maxInclusive ? !(min <= max) : !(min < max);

			if (!floatP.IsFinite(min) || !floatP.IsFinite(max) || isEmpty)
			{
				throw new ArgumentOutOfRangeException(nameof(max), max,
					$"범위가 비었거나 유한하지 않습니다: min={min}, max={max}, maxInclusive={maxInclusive}");
			}

			if (maxInclusive)
			{
				var t = (floatP)(int)(generator.NextUInt() >> 8) / _inclusiveDivisor;

				return Interpolate(min, max, t);
			}

			var value = Interpolate(min, max, NextFloat(ref generator));

			// 반올림으로 max 가 나오면 max 바로 아래 값을 돌려줍니다. 다시 뽑으면 좁은 범위(예: -floatP.Epsilon ~ 0)에서
			// 거의 모든 값이 max 로 반올림되어 한 번에 출력 수백만 개를 쓸 수 있습니다. min < max 라 바로 아래 값은 min 이상입니다
			return value < max ? value : NextDown(max);
		}

		private static floatP Interpolate(floatP min, floatP max, floatP t)
		{
			// min + (max - min) * t 는 max - min 이 floatP.MaxValue 를 넘으면 무한대가 되므로 끝점을 따로 곱합니다
			return MathfloatP.Clamp(min * (floatP.One - t) + max * t, min, max);
		}

		/// <summary>
		/// 유한한 <paramref name="value"/> 보다 작은 가장 큰 floatP 입니다. ±0 이면 가장 작은 음수 서브노멀입니다.
		/// </summary>
		private static floatP NextDown(floatP value)
		{
			var raw = value.RawValue;

			if ((raw & 0x7FFFFFFF) == 0)
			{
				return floatP.FromRaw(0x80000001);
			}

			// 양수는 크기를 하나 줄이고 음수는 크기를 하나 늘립니다
			return floatP.FromRaw((raw & 0x80000000) == 0 ? raw - 1 : raw + 1);
		}
	}
}
