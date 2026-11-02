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

			public long ReadSequence;
			public bool Fail;
			private long _number;
			private double _real;
			private float _single;
			private bool _flag;
			private string _text;

			public void Read(JournalReader reader, long frameSequence)
			{
				if (Fail)
				{
					throw new IOException("test");
				}

				ReadSequence = frameSequence;
				_number = reader.ReadInt64();
				_real = reader.ReadDouble();
				_single = reader.ReadSingle();
				_flag = reader.ReadBool();
				_text = reader.ReadString();
			}

			public void Apply()
			{
				Number = _number;
				Real = _real;
				Single = _single;
				Flag = _flag;
				Text = _text;
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

			// 파일이 없으면 시작 슬롯은 b 이다. 첫 프레임은 b, 둘째 프레임은 a 에 간다
			var newest = Path.Combine(_directory, "t.a.jnl");
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
		public void Write_AfterRestart_DoesNotOverwriteNewestSlot()
		{
			var section = new Section { Number = 1 };
			using (var journal = Open(section))
			{
				journal.WriteNow();
				section.Number = 2;
				journal.WriteNow();
			}

			section.Number = 3;
			using (var journal = Open(section))
			{
				journal.WriteNow();
			}

			// 3 을 쓴 슬롯이 깨져도 2 가 남아야 한다
			var a = new FileInfo(Path.Combine(_directory, "t.a.jnl"));
			var b = new FileInfo(Path.Combine(_directory, "t.b.jnl"));
			var newest = Path.Combine(_directory, "t.b.jnl");
			var bytes = File.ReadAllBytes(newest);
			bytes[bytes.Length / 2] ^= 0xFF;
			File.WriteAllBytes(newest, bytes);

			var restored = new Section();
			using (var journal = Open(restored))
			{
				Assert.That(journal.TryRestore(), Is.True);
			}

			Assert.That(restored.Number, Is.EqualTo(2));
			Assert.That(a.Exists && b.Exists, Is.True);
		}

		[Test]
		public void TryRestore_SectionReadFails_AppliesNothing()
		{
			using (var journal = new DataJournal(_directory, "t", 1, new List<IJournalSection> { new Section { Number = 7 }, new Section { Number = 8 } }))
			{
				journal.WriteNow();
			}

			var first = new Section();
			var second = new Section { Fail = true };
			UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("Journal frame could not be restored"));
			UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Exception, new System.Text.RegularExpressions.Regex("IOException"));
			using (var journal = new DataJournal(_directory, "t", 1, new List<IJournalSection> { first, second }))
			{
				Assert.That(journal.TryRestore(), Is.False);
			}

			Assert.That(first.Number, Is.EqualTo(0));
		}

		[Test]
		public void AdvanceSequence_NextFrameIsGreater()
		{
			using (var journal = Open(new Section()))
			{
				journal.AdvanceSequence(100);
				journal.WriteNow();

				Assert.That(journal.Sequence, Is.EqualTo(101));
			}
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
				// 첫 호출의 JIT·버퍼 할당과 워커 시작을 지나 보낸다
				for (var i = 0; i < 4; i++)
				{
					journal.WriteNow();
					journal.Write();
				}

				System.Threading.Thread.Sleep(100);

				Assert.That(() => journal.Write(), Is.Not.AllocatingGCMemory());
				Assert.That(() => journal.WriteNow(), Is.Not.AllocatingGCMemory());
			}
		}
	}
}
