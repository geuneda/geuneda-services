using System;
using Geuneda.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class RngServiceTest
	{
		private RngService _rngService;
		private RngData _rngData;
		private const int Seed = 12345;

		[SetUp]
		public void Init()
		{
			_rngData = RngService.CreateRngData(Seed);
			_rngService = new RngService(_rngData);
		}

		[Test]
		public void Next_SameSeed_ReturnsDeterministicSequence()
		{
			var sequence1 = new int[10];
			for (var i = 0; i < 10; i++) sequence1[i] = _rngService.Next;

			var data2 = RngService.CreateRngData(Seed);
			var rng2 = new RngService(data2);
			var sequence2 = new int[10];
			for (var i = 0; i < 10; i++) sequence2[i] = rng2.Next;

			Assert.AreEqual(sequence1, sequence2);
		}

		[Test]
		public void Peek_DoesNotAdvanceState()
		{
			var peeked = _rngService.Peek;
			var peeked2 = _rngService.Peek;
			var next = _rngService.Next;

			Assert.AreEqual(peeked, peeked2);
			Assert.AreEqual(peeked, next);
			Assert.AreEqual(1, _rngService.Counter);
		}

		[Test]
		public void Range_MinEqualsMax_ReturnsMin()
		{
			const int minMax = 10;
			Assert.AreEqual(minMax, _rngService.Range(minMax, minMax, true));
		}

		[Test]
		public void Range_MinGreaterThanMax_ThrowsException()
		{
			Assert.Throws<IndexOutOfRangeException>(() => _rngService.Range(10, 5));
		}

		[Test]
		public void Restore_ToPastCount_ReproducesSequence()
		{
			_ = _rngService.Next;
			_ = _rngService.Next;
			var count = _rngService.Counter;
			var nextValue = _rngService.Peek;
			
			_ = _rngService.Next;
			_ = _rngService.Next;
			
			_rngService.Restore(count);
			
			Assert.AreEqual(count, _rngService.Counter);
			Assert.AreEqual(nextValue, _rngService.Next);
		}

		[Test]
		public void Restore_ToFutureCount_AdvancesCorrectly()
		{
			var count = 5;
			_rngService.Restore(count);
			
			Assert.AreEqual(count, _rngService.Counter);
		}

		[Test]
		public void CopyRngState_CreatesIndependentCopy()
		{
			var stateCopy = RngService.CopyRngState(_rngData.State);
			_ = _rngService.Next;
			
			// 복사본을 수동으로 진행
			// 참고: NextNumber는 private이므로, 복사본이 동일한지만 확인합니다
			Assert.AreNotEqual(_rngData.State, stateCopy); 
		}

		[Test]
		public void CreateRngData_InitializesCorrectly()
		{
			var data = RngService.CreateRngData(Seed);
			Assert.AreEqual(Seed, data.Seed);
			Assert.AreEqual(0, data.Count);
			Assert.IsNotNull(data.State);
			Assert.AreEqual(56, data.State.Length);
		}
	}
}
