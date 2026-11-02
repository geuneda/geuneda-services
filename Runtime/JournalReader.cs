using System;
using System.IO;
using System.Text;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// <see cref="JournalWriter"/> 가 쓴 값을 같은 순서로 읽습니다. 범위를 넘으면 <see cref="EndOfStreamException"/> 을 던집니다.
	/// </summary>
	public sealed class JournalReader
	{
		private readonly byte[] _buffer;
		private readonly int _end;
		private int _position;

		internal JournalReader(byte[] buffer, int start, int end)
		{
			_buffer = buffer;
			_position = start;
			_end = end;
		}

		public bool ReadBool()
		{
			Require(1);
			return _buffer[_position++] != 0;
		}

		public int ReadInt32()
		{
			Require(4);
			var value = GetInt32(_buffer, _position);
			_position += 4;
			return value;
		}

		public long ReadInt64()
		{
			Require(8);
			var low = (uint)GetInt32(_buffer, _position);
			var high = (long)GetInt32(_buffer, _position + 4);
			_position += 8;
			return (high << 32) | low;
		}

		public float ReadSingle()
		{
			return new JournalWriter.FloatBits { Int = ReadInt32() }.Float;
		}

		public double ReadDouble()
		{
			return BitConverter.Int64BitsToDouble(ReadInt64());
		}

		public string ReadString()
		{
			var length = ReadInt32();
			if (length < 0)
			{
				return null;
			}

			Require(length);
			var value = Encoding.UTF8.GetString(_buffer, _position, length);
			_position += length;
			return value;
		}

		internal static int GetInt32(byte[] buffer, int position)
		{
			return buffer[position]
			       | (buffer[position + 1] << 8)
			       | (buffer[position + 2] << 16)
			       | (buffer[position + 3] << 24);
		}

		private void Require(int count)
		{
			if (count < 0 || _position + count > _end)
			{
				throw new EndOfStreamException("Journal section read past the end of the frame.");
			}
		}
	}
}
