using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 자주 바뀌는 값을 GC 할당 없이 자주 저장하는 저널입니다. <see cref="DataService"/> 의 JSON 저장을 대신하지 않고 그 사이를 메웁니다.
	/// 파일 두 개(슬롯)에 번갈아 쓰므로 쓰는 도중 앱이 죽거나 전원이 꺼져도 다른 슬롯의 값이 남습니다.
	/// 값 만들기는 부른 스레드에서, 디스크 쓰기는 워커 스레드에서 합니다. <see cref="Write"/> 는 디스크를 기다리지 않습니다.
	/// JSON 저장 직전에 <see cref="WriteNow"/> 를 불러 저널이 항상 JSON 보다 같거나 새롭게 유지하세요.
	/// 세이브를 통째로 바꾸거나 지울 때는 <see cref="Reset"/> 을 불러야 옛 값이 되살아나지 않습니다.
	/// </summary>
	public sealed class DataJournal : IDisposable
	{
		private const int Magic = 0x4C4E524A;
		private const int HeaderSize = 20;
		private const int CrcSize = 4;
		private const int InitialCapacity = 4096;

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Services.Data");
		private static readonly uint[] CrcTable = BuildCrcTable();

		private readonly IReadOnlyList<IJournalSection> _sections;
		private readonly int _formatVersion;
		private readonly string[] _paths;
		private readonly FileStream[] _slots = new FileStream[2];
		private readonly JournalWriter _writer = new JournalWriter(InitialCapacity, HeaderSize);
		private readonly object _pendingGate = new object();
		private readonly object _ioGate = new object();
		private readonly AutoResetEvent _signal = new AutoResetEvent(false);
		private readonly Thread _worker;

		private byte[] _pending = new byte[InitialCapacity];
		private byte[] _ioBuffer = new byte[InitialCapacity];
		private int _pendingLength;
		private long _pendingSequence;
		private long _lastWrittenSequence;
		private long _sequence;
		private volatile bool _stopping;
		private bool _disposed;

		/// <param name="directory">슬롯 파일을 둘 폴더. 없으면 만듭니다</param>
		/// <param name="name">파일 이름 앞부분</param>
		/// <param name="formatVersion">섹션 구성이나 순서를 바꾸면 올립니다. 다른 버전의 저널은 복원하지 않습니다</param>
		/// <param name="sections">쓰고 읽을 순서대로의 섹션</param>
		public DataJournal(string directory, string name, int formatVersion, IReadOnlyList<IJournalSection> sections)
		{
			_sections = sections ?? throw new ArgumentNullException(nameof(sections));
			_formatVersion = formatVersion;
			Directory.CreateDirectory(directory);
			_paths = new[] { Path.Combine(directory, name + ".a.jnl"), Path.Combine(directory, name + ".b.jnl") };
			_sequence = Math.Max(ReadSequence(_paths[0]), ReadSequence(_paths[1]));
			_lastWrittenSequence = _sequence;

			for (var i = 0; i < _slots.Length; i++)
			{
				// 버퍼 크기 1 은 FileStream 내부 버퍼를 끈다. 프레임을 한 번에 쓰므로 필요 없다
				_slots[i] = new FileStream(_paths[i], FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 1);
			}

			_worker = new Thread(WorkerLoop) { IsBackground = true, Name = "DataJournal" };
			_worker.Start();
		}

		/// <summary>
		/// 마지막으로 만든 프레임의 번호입니다
		/// </summary>
		public long Sequence => _sequence;

		/// <summary>
		/// 지금 값으로 프레임을 만들어 워커에 넘깁니다. 디스크를 기다리지 않고, 버퍼가 충분하면 할당하지 않습니다.
		/// 호출 사이에 쓰이지 못한 프레임은 새 프레임이 대신합니다
		/// </summary>
		public void Write()
		{
			var length = BuildFrame();
			lock (_pendingGate)
			{
				SwapIntoPending(length);
			}

			_signal.Set();
		}

		/// <summary>
		/// 지금 값으로 프레임을 만들어 디스크에 쓸 때까지 기다립니다. 할당하지 않습니다
		/// </summary>
		public void WriteNow()
		{
			var length = BuildFrame();
			var sequence = _sequence;
			lock (_ioGate)
			{
				WriteSlot(_writer.Buffer, length, sequence);
			}
		}

		/// <summary>
		/// 슬롯에서 가장 새 프레임을 읽어 섹션에 넘깁니다. 유효한 프레임이 없거나 읽다 실패하면 <c>false</c> 입니다
		/// </summary>
		public bool TryRestore()
		{
			var first = TryLoadFrame(_paths[0], out var firstSequence);
			var second = TryLoadFrame(_paths[1], out var secondSequence);
			var (frame, sequence) = firstSequence >= secondSequence ? (first, firstSequence) : (second, secondSequence);
			if (frame == null)
			{
				return false;
			}

			try
			{
				var reader = new JournalReader(frame, HeaderSize, frame.Length - CrcSize);
				for (var i = 0; i < _sections.Count; i++)
				{
					_sections[i].Read(reader);
				}

				_sequence = Math.Max(_sequence, sequence);
				return true;
			}
			catch (Exception exception)
			{
				Logger.Error(exception, "Journal frame could not be restored");
				return false;
			}
		}

		/// <summary>
		/// 저장된 프레임을 무효로 만듭니다. 다음 <see cref="TryRestore"/> 는 새 프레임이 쓰이기 전까지 <c>false</c> 입니다.
		/// 세이브를 통째로 바꾸거나 지운 뒤에 부릅니다
		/// </summary>
		public void Reset()
		{
			lock (_pendingGate)
			{
				_pendingLength = 0;
			}

			lock (_ioGate)
			{
				for (var i = 0; i < _slots.Length; i++)
				{
					_slots[i].SetLength(0);
					_slots[i].Flush(true);
				}

				// 워커가 이미 꺼내 간 옛 프레임이 되살리지 못하게 한다
				_lastWrittenSequence = _sequence;
			}
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_stopping = true;
			_signal.Set();
			_worker.Join();
			_signal.Dispose();
			for (var i = 0; i < _slots.Length; i++)
			{
				_slots[i].Dispose();
			}
		}

		private int BuildFrame()
		{
			_writer.Restart(_writer.Buffer, HeaderSize);
			for (var i = 0; i < _sections.Count; i++)
			{
				_sections[i].Write(_writer);
			}

			_sequence++;
			var payloadEnd = _writer.Position;
			// CRC 자리까지 쓸 수 있게 버퍼를 늘린다
			_writer.Write(0);
			var buffer = _writer.Buffer;
			JournalWriter.PutInt32(buffer, 0, Magic);
			JournalWriter.PutInt32(buffer, 4, _formatVersion);
			JournalWriter.PutInt64(buffer, 8, _sequence);
			JournalWriter.PutInt32(buffer, 16, payloadEnd - HeaderSize);
			JournalWriter.PutInt32(buffer, payloadEnd, (int)ComputeCrc(buffer, 0, payloadEnd));
			return payloadEnd + CrcSize;
		}

		// 호출자가 _pendingGate 를 쥔다
		private void SwapIntoPending(int length)
		{
			var built = _writer.Buffer;
			_writer.Restart(_pending, HeaderSize);
			_pending = built;
			_pendingLength = length;
			_pendingSequence = _sequence;
		}

		private void WorkerLoop()
		{
			while (true)
			{
				_signal.WaitOne();
				if (_stopping)
				{
					FlushPending();
					return;
				}

				FlushPending();
			}
		}

		private void FlushPending()
		{
			int length;
			long sequence;
			byte[] buffer;
			lock (_pendingGate)
			{
				if (_pendingLength == 0)
				{
					return;
				}

				buffer = _pending;
				_pending = _ioBuffer;
				_ioBuffer = buffer;
				length = _pendingLength;
				sequence = _pendingSequence;
				_pendingLength = 0;
			}

			try
			{
				lock (_ioGate)
				{
					WriteSlot(buffer, length, sequence);
				}
			}
			catch (Exception exception)
			{
				// 다음 주기에 다시 쓴다. 워커가 죽으면 이후 저널이 모두 멈춘다
				Logger.Error(exception, "Journal frame could not be written");
			}
		}

		// 호출자가 _ioGate 를 쥔다. 동기 쓰기가 앞질렀으면 옛 프레임은 버린다
		private void WriteSlot(byte[] buffer, int length, long sequence)
		{
			if (sequence <= _lastWrittenSequence)
			{
				return;
			}

			var slot = _slots[sequence & 1];
			slot.Seek(0, SeekOrigin.Begin);
			slot.Write(buffer, 0, length);
			slot.SetLength(length);
			slot.Flush(true);
			_lastWrittenSequence = sequence;
		}

		private long ReadSequence(string path)
		{
			TryLoadFrame(path, out var sequence, false);
			return sequence;
		}

		private byte[] TryLoadFrame(string path, out long sequence, bool checkFormat = true)
		{
			sequence = -1;
			try
			{
				if (!File.Exists(path))
				{
					return null;
				}

				byte[] bytes;
				using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					bytes = new byte[stream.Length];
					var read = 0;
					while (read < bytes.Length)
					{
						var count = stream.Read(bytes, read, bytes.Length - read);
						if (count <= 0)
						{
							break;
						}

						read += count;
					}

					if (read != bytes.Length)
					{
						return null;
					}
				}

				if (bytes.Length < HeaderSize + CrcSize || JournalReader.GetInt32(bytes, 0) != Magic)
				{
					return null;
				}

				var payload = JournalReader.GetInt32(bytes, 16);
				if (payload < 0 || HeaderSize + payload + CrcSize != bytes.Length)
				{
					return null;
				}

				var end = HeaderSize + payload;
				if ((uint)JournalReader.GetInt32(bytes, end) != ComputeCrc(bytes, 0, end))
				{
					return null;
				}

				var frameSequence = new JournalReader(bytes, 8, 16).ReadInt64();
				if (checkFormat && JournalReader.GetInt32(bytes, 4) != _formatVersion)
				{
					return null;
				}

				sequence = frameSequence;
				return bytes;
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Logger.Warning($"Journal slot could not be read: {exception.Message}");
				return null;
			}
		}

		private static uint ComputeCrc(byte[] buffer, int start, int end)
		{
			var crc = 0xFFFFFFFFu;
			for (var i = start; i < end; i++)
			{
				crc = CrcTable[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
			}

			return ~crc;
		}

		private static uint[] BuildCrcTable()
		{
			var table = new uint[256];
			for (var i = 0u; i < table.Length; i++)
			{
				var crc = i;
				for (var bit = 0; bit < 8; bit++)
				{
					crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
				}

				table[i] = crc;
			}

			return table;
		}
	}
}
