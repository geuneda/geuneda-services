using System.Collections.Generic;
using System.IO;
using Geuneda.Services;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class DataJournalTest
	{
		private sealed class Section : IJournalSection
		{
			public long Number;
			public double Real;
			public float Single;
			public bool Flag;
			public string Text = "";

			public void Write(JournalWriter writer)
			{
				writer.Write(Number);
				writer.Write(Real);
				writer.Write(Single);
				writer.Write(Flag);
				writer.Write(Text);
			}

			public void Read(JournalReader reader)
			{
				Number = reader.ReadInt64();
				Real = reader.ReadDouble();
				Single = reader.ReadSingle();
				Flag = reader.ReadBool();
				Text = reader.ReadString();
			}
		}

		private string _directory;

		[SetUp]
		public void Init()
		{
			_directory = Path.Combine(Path.GetTempPath(), "journal_" + Path.GetRandomFileName());
		}

		[TearDown]
		public void Cleanup()
		{
			if (Directory.Exists(_directory))
			{
				Directory.Delete(_directory, true);
			}
		}

		private DataJournal Open(Section section, int format = 1)
		{
			return new DataJournal(_directory, "t", format, new List<IJournalSection> { section });
		}

		[Test]
		public void WriteNow_ThenRestore_ReturnsAllValues()
		{
			var written = new Section { Number = -5_000_000_000L, Real = 1.5e300, Single = 0.25f, Flag = true, Text = "한글 key" };
			using (var journal = Open(written))
			{
				journal.WriteNow();
			}

			var restored = new Section();
			using (var journal = Open(restored))
			{
				Assert.That(journal.TryRestore(), Is.True);
			}

			Assert.That(restored.Number, Is.EqualTo(written.Number));
			Assert.That(restored.Real, Is.EqualTo(written.Real));
			Assert.That(restored.Single, Is.EqualTo(written.Single));
			Assert.That(restored.Flag, Is.True);
			Assert.That(restored.Text, Is.EqualTo(written.Text));
		}

		[Test]
		public void Write_WorkerFlushesOnDispose_RestoresNewest()
		{
			var section = new Section { Number = 1 };
			using (var journal = Open(section))
			{
				journal.Write();
				section.Number = 2;
				journal.Write();
			}

			var restored = new Section();
			using (var journal = Open(restored))
			{
				Assert.That(journal.TryRestore(), Is.True);
			}

			Assert.That(restored.Number, Is.EqualTo(2));
		}

		[Test]
		public void TryRestore_NewestSlotCorrupt_FallsBackToOtherSlot()
		{
			var section = new Section { Number = 1 };
			using (var journal = Open(section))
			{
				journal.WriteNow();
				section.Number = 2;
				journal.WriteNow();
			}

			// 번호 1 은 b 슬롯, 번호 2 는 a 슬롯이다(시작 번호 -1 다음 0, 1 은 슬롯 0, 1)
			var newest = Path.Combine(_directory, "t.b.jnl");
			var bytes = File.ReadAllBytes(newest);
			bytes[bytes.Length / 2] ^= 0xFF;
			File.WriteAllBytes(newest, bytes);

			var restored = new Section();
			using (var journal = Open(restored))
			{
				Assert.That(journal.TryRestore(), Is.True);
			}

			Assert.That(restored.Number, Is.EqualTo(1));
		}

		[Test]
		public void TryRestore_NothingWritten_ReturnsFalse()
		{
			using (var journal = Open(new Section()))
			{
				Assert.That(journal.TryRestore(), Is.False);
			}
		}

		[Test]
		public void TryRestore_OtherFormatVersion_ReturnsFalse()
		{
			using (var journal = Open(new Section { Number = 1 }))
			{
				journal.WriteNow();
			}

			using (var journal = Open(new Section(), 2))
			{
				Assert.That(journal.TryRestore(), Is.False);
			}
		}

		[Test]
		public void Reset_DropsFramesAndPendingWrite()
		{
			var section = new Section { Number = 1 };
			using (var journal = Open(section))
			{
				journal.WriteNow();
				journal.Write();
				journal.Reset();
			}

			using (var journal = Open(new Section()))
			{
				Assert.That(journal.TryRestore(), Is.False);
			}
		}

		[Test]
		public void Sequence_ContinuesAcrossRuns_SoNewFramesWin()
		{
			var section = new Section { Number = 1 };
			using (var journal = Open(section))
			{
				journal.WriteNow();
				journal.WriteNow();
				journal.WriteNow();
			}

			section.Number = 9;
			using (var journal = Open(section))
			{
				journal.WriteNow();
			}

			var restored = new Section();
			using (var journal = Open(restored))
			{
				Assert.That(journal.TryRestore(), Is.True);
			}

			Assert.That(restored.Number, Is.EqualTo(9));
		}

		[Test]
		public void WriteNow_AfterBuffersGrown_DoesNotAllocate()
		{
			var section = new Section { Number = 1, Text = "keep" };
			using (var journal = Open(section))
			{
				// 첫 호출의 JIT·버퍼 할당을 지나 보낸다
				for (var i = 0; i < 4; i++)
				{
					journal.WriteNow();
				}

				Assert.That(() => journal.WriteNow(), Is.Not.AllocatingGCMemory());
			}
		}
	}
}
