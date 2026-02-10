using System;
using System.Collections.Generic;
using Geuneda.GameData;

namespace Geuneda.Services
{
	/// <summary>
	/// 클래스에서 데이터 구조를 사용하여 참조로 값을 전달하고
	/// 내부 데이터 컨테이너를 직접 업데이트하려면 이 인터페이스를 구현하세요.
	/// </summary>
	public interface IRngData
	{
		/// <summary>
		/// RNG 초기화에 사용된 시드를 가져옵니다.
		/// </summary>
		int Seed { get; }

		/// <summary>
		/// 지금까지 생성된 난수의 개수를 가져옵니다.
		/// </summary>
		int Count { get; }

		/// <summary>
		/// RNG의 현재 상태를 가져옵니다.
		/// </summary>
		IReadOnlyList<int> State { get; }
	}

	/// <summary>
	/// 난수 생성(RNG) 상태를 저장하기 위한 데이터 구조를 나타냅니다.
	/// </summary>
	public class RngData : IRngData
	{
		/// <summary>
		/// RNG 초기화에 사용된 시드
		/// </summary>
		public int Seed;

		/// <summary>
		/// 지금까지 생성된 난수의 개수
		/// </summary>
		public int Count;

		/// <summary>
		/// RNG의 현재 상태
		/// </summary>
		public int[] State;

		/// <inheritdoc/>
		int IRngData.Seed => Seed;
		/// <inheritdoc/>
		int IRngData.Count => Count;
		/// <inheritdoc/>
		IReadOnlyList<int> IRngData.State => Array.AsReadOnly(State);
	}
	/// <summary>
	/// 항상 결정론적 결과를 갖는 난수 생성 값을 관리하는 데 필요한 동작을 제공하는 서비스입니다.
	/// .Net 라이브러리 Random 클래스를 기반으로 합니다 <see cref="https://referencesource.microsoft.com/#mscorlib/system/random.cs"/>
	/// </summary>
	public interface IRngService
	{
		/// <summary>
		/// 이 서비스가 조작하는 <see cref="IRngData"/>
		/// </summary>
		public IRngData Data { get; }

		/// <summary>
		/// RNG가 카운트된 횟수를 반환합니다
		/// </summary>
		int Counter { get; }

		/// <summary>
		/// 상태를 변경하지 않고 다음 <see cref="int"/> 생성 값을 요청합니다.
		/// 연속으로 여러 번 호출해도 항상 동일한 결과를 반환합니다.
		/// </summary>
		int Peek { get; }

		/// <summary>
		/// 상태를 변경하지 않고 다음 <see cref="float"/> 생성 값을 요청합니다.
		/// 연속으로 여러 번 호출해도 항상 동일한 결과를 반환합니다.
		/// </summary>
		floatP Peekfloat { get; }

		/// <summary>
		/// 다음 <see cref="int"/> 생성 값을 요청합니다
		/// </summary>
		int Next { get; }

		/// <summary>
		/// 다음 <see cref="floatP"/> 생성 값을 요청합니다
		/// </summary>
		floatP Nextfloat { get; }

		/// <inheritdoc cref="Range(int,int,int[],bool)"/>
		/// <remarks>
		/// 동일한 매개변수로 연속 호출해도 항상 동일한 결과를 반환합니다.
		/// </remarks>
		int PeekRange(int min, int max, bool maxInclusive = false);

		/// <inheritdoc cref="Range(floatP,floatP,int[],bool)"/>
		/// <remarks>
		/// 동일한 매개변수로 연속 호출해도 항상 동일한 결과를 반환합니다.
		/// </remarks>
		floatP PeekRange(floatP min, floatP max, bool maxInclusive = true);

		/// <inheritdoc cref="Range(int,int,int[],bool)"/>
		int Range(int min, int max, bool maxInclusive = false);

		/// <inheritdoc cref="Range(floatP,floatP,int[],bool)"/>
		floatP Range(floatP min, floatP max, bool maxInclusive = true);

		/// <summary>
		/// 현재 RNG 상태를 주어진 <paramref name="count"/>로 복원합니다.
		/// 과거 상태 또는 미래 상태의 값을 정의할 수 있습니다.
		/// </summary>
		void Restore(int count);
	}

	/// <inheritdoc />
	public class RngService : IRngService
	{
		private const int _basicSeed = 161803398;
		private const int _stateLength = 56;
		private const int _helperInc = 21;
		private const int _valueIndex = 0;

		private RngData _rngData;

		/// <inheritdoc />
		public int Counter => Data.Count;

		/// <inheritdoc />
		public int Peek => PeekRange(0, int.MaxValue);

		/// <inheritdoc />
		public floatP Peekfloat => PeekRange((floatP) 0, floatP.MaxValue);

		/// <inheritdoc />
		public int Next => Range(0, int.MaxValue);

		/// <inheritdoc />
		public floatP Nextfloat => Range((floatP) 0, floatP.MaxValue);

		/// <inheritdoc />
		public IRngData Data => _rngData;

		public RngService(RngData rngData)
		{
			_rngData = rngData;
		}

		/// <inheritdoc />
		public int PeekRange(int min, int max, bool maxInclusive = false)
		{
			return Range(min, max, CopyRngState(_rngData.State), maxInclusive);
		}

		/// <inheritdoc />
		public floatP PeekRange(floatP min, floatP max, bool maxInclusive = true)
		{
			return Range(min, max, CopyRngState(_rngData.State), maxInclusive);
		}

		/// <inheritdoc />
		public int Range(int min, int max, bool maxInclusive = false)
		{
			_rngData.Count++;

			return Range(min, max, _rngData.State, maxInclusive);
		}

		/// <inheritdoc />
		public floatP Range(floatP min, floatP max, bool maxInclusive = true)
		{
			_rngData.Count++;

			return Range(min, max, _rngData.State, maxInclusive);
		}

		/// <inheritdoc />
		public void Restore(int count)
		{
			_rngData.Count = count;
			_rngData.State = Restore(count, _rngData.Seed);
		}

		/// <summary>
		/// 주어진 <paramref name="seed"/>를 기반으로 현재 RNG 상태를 주어진 <paramref name="count"/>로 복원합니다.
		/// <paramref name="count "/> 값은 과거 상태 또는 미래 상태로 정의할 수 있습니다.
		/// </summary>
		public static int[] Restore(int count, int seed)
		{
			var newState = GenerateRngState(seed);

			for (var i = 0; i < count; i++)
			{
				NextNumber(newState);
			}

			return newState;
		}

		/// <summary>
		/// 주어진 <paramref name="min"/>과 <paramref name="max"/> 사이의 난수 <see cref="int"/> 값을 요청합니다.
		/// <paramref name="maxInclusive"/>에 따라 최대값 포함 여부가 결정되며 상태를 변경하지 않습니다.
		/// </summary>
		public static int Range(int min, int max, int[] rndState, bool maxInclusive)
		{
			floatP floatMin = min;
			floatP floatMax = max;

			return Range(floatMin, floatMax, rndState, maxInclusive);
		}

		/// <summary>
		/// 주어진 <paramref name="min"/>과 <paramref name="max"/> 사이의 난수 값을 요청합니다.
		/// <paramref name="maxInclusive"/>에 따라 최대값 포함 여부가 결정되며 상태를 변경하지 않습니다.
		/// </summary>
		/// <remarks>
		/// 부동소수점 정밀도로 인해 범위 요청의 결정론적 결과가 보장되지 않습니다.
		/// </remarks>
		public static floatP Range(floatP min, floatP max, int[] rndState, bool maxInclusive)
		{
			if (min > max || (!maxInclusive && MathfloatP.Abs(min - max) < floatP.Epsilon))
			{
				throw new IndexOutOfRangeException("The min range value must be less the max range value");
			}

			if (MathfloatP.Abs(min - max) < floatP.Epsilon)
			{
				return min;
			}

			var range = max - min;
			var value = NextNumber(rndState);

			value = maxInclusive && value == int.MaxValue ? value - 1 : value;

			return range * value / int.MaxValue + min;
		}

		/// <summary>
		/// 주어진 <paramref name="state"/>의 정확한 복사본으로 새 상태를 생성합니다.
		/// RNG의 현재 상태를 변경하지 않고 새로운 난수를 생성하려면 이 메서드를 사용하세요.
		/// </summary>
		/// <exception cref="IndexOutOfRangeException">
		/// 주어진 <paramref name="state"/>의 길이가 <seealso cref="_stateLength"/>와 같지 않으면 발생합니다.
		/// </exception>
		public static int[] CopyRngState(int[] state)
		{
			if (state == null || state.Length != _stateLength)
			{
				throw new IndexOutOfRangeException($"The Random data created has the wrong state date." +
												   $"It should have a lenght of {_stateLength.ToString()} but has {state?.Length}");
			}

			var newState = new int[_stateLength];

			Array.Copy(state, newState, _stateLength);

			return newState;
		}

		/// <summary>
		/// 주어진 <paramref name="seed"/>로 <see cref="RngData"/>의 새 인스턴스를 생성합니다.
		/// </summary>
		/// <param name="seed">RNG의 시드 값입니다.</param>
		/// <returns>주어진 <paramref name="seed"/>와 초기 카운트 0을 가진 <see cref="RngData"/>의 새 인스턴스입니다.</returns>
		public static RngData CreateRngData(int seed)
		{
			return new RngData
			{
				Seed = seed,
				Count = 0,
				State = GenerateRngState(seed)
			};
		}

		/// <summary>
		/// 주어진 <paramref name="seed"/>를 기반으로 완전히 새로운 RNG 상태를 생성합니다.
		/// D.E. Knuth의 연구를 기반으로 합니다 <see cref="https://www.informit.com/articles/article.aspx?p=2221790"/>
		/// </summary>
		public static int[] GenerateRngState(int seed)
		{
			var value = _basicSeed - (seed == int.MinValue ? int.MaxValue : System.Math.Abs(seed));
			var state = new int[_stateLength];

			state[_stateLength - 1] = value;
			state[_valueIndex] = 0;

			// [1..55] 범위는 특별합니다 (Knuth)
			for (int i = 1, j = 1; i < _stateLength - 1; i++)
			{
				var index = (_helperInc * i) % (_stateLength - 1);

				state[index] = j;

				j = value - j;

				if (j < 0)
				{
					j += int.MaxValue;
				}

				value = state[index];
			}

			for (var k = 1; k < 5; k++)
			{
				for (var i = 1; i < _stateLength; i++)
				{
					state[i] -= state[1 + (i + 30) % (_stateLength - 1)];

					if (state[i] < 0)
					{
						state[i] += int.MaxValue;
					}
				}
			}

			return state;
		}

		/// <summary>
		/// 주어진 <paramref name="rndState"/>를 기반으로 [0...int.MaxValue] 사이의 다음 난수를 생성합니다
		/// </summary>
		private static int NextNumber(int[] rndState)
		{
			var index1 = rndState[_valueIndex] + 1;
			var index2 = index1 + _helperInc + 1;

			index1 = index1 < _stateLength ? index1 : 1;
			index2 = index2 < _stateLength ? index2 : 1;

			var ret = rndState[index1] - rndState[index2];

			ret = ret < 0 ? ret + int.MaxValue : ret;

			rndState[index1] = ret;
			rndState[_valueIndex] = index1;

			return ret;
		}
	}
}
