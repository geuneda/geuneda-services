using System;
using Geuneda.Core;
using Geuneda.DataExtensions;
using Geuneda.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class RngServiceTest
	{
		// 정수 기대값은 PCG 참조 구현과 같은 알고리즘의 Python 구현으로 만들었습니다.
		// floatP 기대값은 floatP 의 곱셈이 IEEE 반올림이 아니라 잘라내기라서 같은 파일을 .NET 에서 실행해 얻었습니다
		private const ulong Seed = 12345;
		// 시드 12345, 스트림 0 에서 처음으로 출력의 상위 24비트가 모두 1(0xFFFFFF)인 위치
		private const ulong AllOnesPosition = 6618017;

		private RngData _rngData;
		private RngService _rngService;

		[SetUp]
		public void Init()
		{
			_rngData = RngService.CreateRngData(Seed);
			_rngService = new RngService(_rngData);
		}

		[Test]
		public void CreateRngData_InitializesCorrectly()
		{
			var data = RngService.CreateRngData(Seed, 9);

			Assert.AreEqual(Seed, data.Seed);
			Assert.AreEqual(9UL, data.Stream);
			Assert.AreEqual(0UL, data.Count);
			Assert.AreEqual(0UL, _rngData.Stream);
		}

		[Test]
		public void Constructor_NullData_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new RngService(null));
		}

		[Test]
		public void NextUInt_MatchesCorePcg32Sequence()
		{
			var expected = new uint[] { 2660500096, 1751544610, 1857258793, 3416782584, 1391937286 };

			for (var i = 0; i < expected.Length; i++)
			{
				Assert.AreEqual(expected[i], _rngService.NextUInt());
				Assert.AreEqual((ulong)(i + 1), _rngData.Count);
			}
		}

		[Test]
		public void NextUInt_SameSeedAndStream_ReturnsSameSequence()
		{
			var other = new RngService(RngService.CreateRngData(Seed));

			for (var i = 0; i < 10; i++)
			{
				Assert.AreEqual(_rngService.NextUInt(), other.NextUInt());
			}
		}

		[Test]
		public void NextUInt_DifferentStream_ReturnsDifferentSequence()
		{
			var other = new RngService(RngService.CreateRngData(Seed, 1));
			var same = 0;

			for (var i = 0; i < 100; i++)
			{
				if (_rngService.NextUInt() == other.NextUInt())
				{
					same++;
				}
			}

			Assert.Less(same, 3);
		}

		[Test]
		public void NextFloat_MatchesGoldenValuesInUnitInterval()
		{
			var expected = new uint[] { 0x3f1e9402, 0x3ed0cce6, 0x3edd670a, 0x3f4ba7f6, 0x3ea5ee8e };

			foreach (var raw in expected)
			{
				var value = _rngService.NextFloat();

				Assert.AreEqual(raw, value.RawValue);
				Assert.GreaterOrEqual((float)value, 0f);
				Assert.Less((float)value, 1f);
			}

			Assert.AreEqual(5UL, _rngData.Count);
		}

		[Test]
		public void Range_Int_MatchesGoldenValues()
		{
			var expected = new[] { 11, -10, -7, 29, -18, 29, -20, 6, -10, -31 };

			foreach (var value in expected)
			{
				Assert.AreEqual(value, _rngService.Range(-50, 50));
			}
		}

		[Test]
		public void Range_IntWithRejections_CountsEveryConsumedOutput()
		{
			// 범위 크기 3 * 2^30 은 약 1/4 이 거절됩니다. 20 번 뽑는 동안 출력 30 개를 씁니다
			const int max = 1 << 30;

			for (var i = 0; i < 20; i++)
			{
				_rngService.Range(int.MinValue, max);
			}

			Assert.AreEqual(30UL, _rngData.Count);
		}

		[Test]
		public void Range_IntMaxInclusive_ReturnsMax()
		{
			// 이전 Knuth 구현은 maxInclusive 여도 max 를 사실상 돌려주지 않았습니다(R2)
			var sawMax = false;
			var sawMin = false;

			for (var i = 0; i < 1000; i++)
			{
				var value = _rngService.Range(0, 10, true);

				Assert.GreaterOrEqual(value, 0);
				Assert.LessOrEqual(value, 10);
				sawMax |= value == 10;
				sawMin |= value == 0;
			}

			Assert.IsTrue(sawMax);
			Assert.IsTrue(sawMin);
		}

		[Test]
		public void Range_IntNegativeBounds_IsUnbiased()
		{
			// 이전 Knuth 구현은 Range(-50, 50) 에서 0 이 약 2%, -50 이 거의 0% 였습니다(R1)
			var counts = new int[100];

			for (var i = 0; i < 100000; i++)
			{
				counts[_rngService.Range(-50, 50) + 50]++;
			}

			foreach (var count in counts)
			{
				Assert.GreaterOrEqual(count, 850);
				Assert.LessOrEqual(count, 1150);
			}
		}

		[Test]
		public void Range_IntMinEqualsMax_InclusiveReturnsMinAndExclusiveThrows()
		{
			Assert.AreEqual(10, _rngService.Range(10, 10, true));
			Assert.AreEqual(1UL, _rngData.Count);

			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.Range(10, 10));
			Assert.AreEqual(1UL, _rngData.Count);
		}

		[Test]
		public void Range_IntInvertedBounds_ThrowsWithoutAdvancing()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.Range(10, 5));
			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.Range(10, 5, true));
			Assert.AreEqual(0UL, _rngData.Count);
		}

		[Test]
		public void Range_FloatInclusive_MatchesGoldenValues()
		{
			var expected = new uint[] { 0x40c63902, 0x4082800f, 0x408a6066 };

			foreach (var raw in expected)
			{
				Assert.AreEqual(raw, _rngService.Range((floatP)0f, (floatP)10f).RawValue);
			}
		}

		[Test]
		public void Range_FloatExclusive_MatchesGoldenValues()
		{
			var expected = new uint[] { 0x406288c3, 0x3fbcf36f, 0x3fdbab28 };

			foreach (var raw in expected)
			{
				Assert.AreEqual(raw, _rngService.Range((floatP)(-2.5f), (floatP)7.25f, false).RawValue);
			}
		}

		[Test]
		public void Range_FloatInclusive_TopOutputReturnsExactMax()
		{
			floatP min = 0.1f;
			floatP max = 0.3f;

			_rngService.Restore(AllOnesPosition);

			Assert.AreEqual(max.RawValue, _rngService.Range(min, max).RawValue);
		}

		[Test]
		public void Range_FloatExclusive_RoundingToMaxReturnsValueJustBelowMax()
		{
			// t = 1 - 2^-24 에서 -2 * 2^-24 + -1 * t 는 -(1 + 2^-24) 이고 -1(max)로 반올림되므로 max 바로 아래 값을 돌려줍니다
			_rngService.Restore(AllOnesPosition);

			var value = _rngService.Range((floatP)(-2f), (floatP)(-1f), false);

			Assert.AreEqual(0xbf800001u, value.RawValue);
			Assert.AreEqual(AllOnesPosition + 1, _rngData.Count);
		}

		[TestCase(0x80000001u, 0x00000000u, TestName = "Range_FloatExclusive_NarrowRange_NegativeSubnormalToZero")]
		[TestCase(0x00000000u, 0x00000001u, TestName = "Range_FloatExclusive_NarrowRange_ZeroToPositiveSubnormal")]
		[TestCase(0xbf800001u, 0xbf800000u, TestName = "Range_FloatExclusive_NarrowRange_AdjacentNegativeNormals")]
		[TestCase(0x3f800000u, 0x3f800001u, TestName = "Range_FloatExclusive_NarrowRange_AdjacentPositiveNormals")]
		public void Range_FloatExclusive_NarrowRange_ReturnsMinWithOneOutputPerCall(uint minRaw, uint maxRaw)
		{
			// 이전에는 max 로 반올림되면 다시 뽑아서 -Epsilon ~ 0 에서 한 번에 출력 수십만 개 이상을 썼습니다
			var min = floatP.FromRaw(minRaw);
			var max = floatP.FromRaw(maxRaw);

			for (var i = 1; i <= 100; i++)
			{
				Assert.AreEqual(minRaw, _rngService.Range(min, max, false).RawValue);
				Assert.AreEqual((ulong)i, _rngData.Count);
			}
		}

		[Test]
		public void Range_FloatFullFiniteRange_StaysFiniteAndInRange()
		{
			var min = -floatP.MaxValue;
			var max = floatP.MaxValue;

			for (var i = 0; i < 1000; i++)
			{
				var value = _rngService.Range(min, max);

				Assert.IsTrue(value.IsFinite());
				Assert.IsTrue(value >= min && value <= max);
			}
		}

		[Test]
		public void Range_FloatMinEqualsMax_InclusiveReturnsMinAndAdvances()
		{
			floatP value = 0.1f;

			Assert.AreEqual(value.RawValue, _rngService.Range(value, value).RawValue);
			Assert.AreEqual(1UL, _rngData.Count);
		}

		[Test]
		public void Range_FloatInvalidBounds_ThrowWithoutAdvancing()
		{
			floatP zero = 0f;
			floatP one = 1f;
			var negativeZero = floatP.FromRaw(0x80000000);
			var nan = floatP.FromRaw(0x7FC00000);
			var infinity = floatP.FromRaw(0x7F800000);

			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.Range(one, zero));
			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.Range(one, one, false));
			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.Range(negativeZero, zero, false));
			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.Range(zero, nan));
			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.Range(nan, one));
			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.Range(zero, infinity));
			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.Range(-infinity, zero));
			Assert.AreEqual(0UL, _rngData.Count);
		}

		[Test]
		public void PeekRange_DoesNotAdvanceAndMatchesNextRange()
		{
			var peekedInt = _rngService.PeekRange(5, 50);

			Assert.AreEqual(peekedInt, _rngService.PeekRange(5, 50));
			Assert.AreEqual(0UL, _rngData.Count);
			Assert.AreEqual(peekedInt, _rngService.Range(5, 50));
			Assert.AreEqual(1UL, _rngData.Count);

			var peekedFloat = _rngService.PeekRange((floatP)0f, (floatP)1f);

			Assert.AreEqual(peekedFloat, _rngService.PeekRange((floatP)0f, (floatP)1f));
			Assert.AreEqual(1UL, _rngData.Count);
			Assert.AreEqual(peekedFloat, _rngService.Range((floatP)0f, (floatP)1f));
			Assert.AreEqual(2UL, _rngData.Count);
		}

		[Test]
		public void PeekRange_InvalidBounds_Throws()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.PeekRange(10, 5));
			Assert.Throws<ArgumentOutOfRangeException>(() => _rngService.PeekRange((floatP)1f, (floatP)0f));
		}

		[Test]
		public void Restore_ToPastCount_ReproducesSequence()
		{
			_rngService.NextUInt();
			_rngService.NextUInt();

			var count = _rngData.Count;
			var expected = new[] { _rngService.NextUInt(), _rngService.NextUInt() };

			_rngService.Restore(count);

			Assert.AreEqual(count, _rngData.Count);
			Assert.AreEqual(expected[0], _rngService.NextUInt());
			Assert.AreEqual(expected[1], _rngService.NextUInt());
		}

		[Test]
		public void Restore_ToFutureCount_MatchesSteppedService()
		{
			var stepped = new RngService(RngService.CreateRngData(Seed));

			for (var i = 0; i < 1000; i++)
			{
				stepped.NextUInt();
			}

			_rngService.Restore(1000);

			Assert.AreEqual(1000UL, _rngData.Count);
			Assert.AreEqual(stepped.NextUInt(), _rngService.NextUInt());
		}

		[Test]
		public void Restore_HugeCount_IsLogarithmicAndMatchesPcg32Advance()
		{
			const ulong count = 1UL << 62;
			var generator = new Pcg32(Seed, 0);

			generator.Advance(count);
			_rngService.Restore(count);

			Assert.AreEqual(generator.NextUInt(), _rngService.NextUInt());
			Assert.AreEqual(count + 1, _rngData.Count);
		}

		[Test]
		public void DataChangedOutside_NextCallUsesNewState()
		{
			var first = _rngService.NextUInt();

			_rngService.NextUInt();

			// 세이브 불러오기처럼 같은 RngData 객체의 필드를 직접 바꿉니다
			_rngData.Count = 0;
			Assert.AreEqual(first, _rngService.NextUInt());

			_rngData.Stream = 4;
			_rngData.Count = 0;
			Assert.AreEqual(new Pcg32(Seed, 4).NextUInt(), _rngService.NextUInt());

			_rngData.Seed = 99;
			_rngData.Count = 2;

			var expected = new Pcg32(99, 4);

			expected.Advance(2);
			Assert.AreEqual(expected.NextUInt(), _rngService.NextUInt());
			Assert.AreEqual(3UL, _rngData.Count);
		}

		[Test]
		public void DataInterface_ReflectsBackingData()
		{
			_rngService.NextUInt();

			IRngData data = _rngService.Data;

			Assert.AreSame(_rngData, data);
			Assert.AreEqual(Seed, data.Seed);
			Assert.AreEqual(0UL, data.Stream);
			Assert.AreEqual(1UL, data.Count);
		}
	}
}
