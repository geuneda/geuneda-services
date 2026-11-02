using System;
using System.Text;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 바이트 버퍼에 값을 이어 쓰는 쓰개입니다. 버퍼가 충분하면 어떤 호출도 할당하지 않습니다.
	/// 버퍼는 필요한 만큼만 커지고 줄지 않으므로 처음 몇 번 뒤에는 할당이 없습니다.
	/// </summary>
	public sealed class JournalWriter
	{
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

		private byte[] _buffer;
		private int _position;

		internal JournalWriter(int capacity, int startPosition)
		{
			_buffer = new byte[capacity];
			_position = startPosition;
		}

		internal byte[] Buffer => _buffer;
		internal int Position => _position;

		internal void Restart(byte[] buffer, int startPosition)
		{
			_buffer = buffer;
			_position = startPosition;
		}

		public void Write(bool value)
		{
			Ensure(1);
			_buffer[_position++] = value ? (byte)1 : (byte)0;
		}

		public void Write(int value)
		{
			Ensure(4);
			PutInt32(_buffer, _position, value);
			_position += 4;
		}

		public void Write(long value)
		{
			Ensure(8);
			PutInt64(_buffer, _position, value);
			_position += 8;
		}

		public void Write(float value)
		{
			Write(new FloatBits { Float = value }.Int);
		}

		public void Write(double value)
		{
			Write(BitConverter.DoubleToInt64Bits(value));
		}

		/// <summary>
		/// 문자열을 UTF-8 로 씁니다. <c>null</c> 과 빈 문자열을 구분합니다
		/// </summary>
		public void Write(string value)
		{
			if (value == null)
			{
				Write(-1);
				return;
			}

			var length = Utf8.GetByteCount(value);
			Write(length);
			Ensure(length);
			Utf8.GetBytes(value, 0, value.Length, _buffer, _position);
			_position += length;
		}

		internal static void PutInt32(byte[] buffer, int position, int value)
		{
			buffer[position] = (byte)value;
			buffer[position + 1] = (byte)(value >> 8);
			buffer[position + 2] = (byte)(value >> 16);
			buffer[position + 3] = (byte)(value >> 24);
		}

		internal static void PutInt64(byte[] buffer, int position, long value)
		{
			PutInt32(buffer, position, (int)value);
			PutInt32(buffer, position + 4, (int)(value >> 32));
		}

		private void Ensure(int count)
		{
			var needed = _position + count;
			if (needed > _buffer.Length)
			{
				Array.Resize(ref _buffer, Math.Max(needed, _buffer.Length * 2));
			}
		}

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
		internal struct FloatBits
		{
			[System.Runtime.InteropServices.FieldOffset(0)] public float Float;
			[System.Runtime.InteropServices.FieldOffset(0)] public int Int;
		}
	}
}
