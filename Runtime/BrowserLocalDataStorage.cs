using System;
using System.IO;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// WebGL 플레이어에서 브라우저의 <c>localStorage</c> 에 저장합니다. <c>localStorage</c> 는 빌드가 아니라 페이지 출처(origin) 단위라,
	/// 업로드할 때마다 빌드 경로가 바뀌는 호스트에서도 세이브가 유지됩니다. 쓰기는 동기입니다.
	/// WebGL 플레이어 밖(에디터 포함)에서 만들면 <see cref="PlatformNotSupportedException"/>,
	/// 브라우저가 <c>localStorage</c> 를 막았으면 <see cref="InvalidOperationException"/> 을 던집니다.
	/// </summary>
	public class BrowserLocalDataStorage : IDataStorage
	{
		/// <exception cref="PlatformNotSupportedException">WebGL 플레이어가 아닌 경우(에디터 포함)</exception>
		/// <exception cref="InvalidOperationException">브라우저가 이 페이지의 <c>localStorage</c> 를 막은 경우</exception>
		public BrowserLocalDataStorage()
		{
			if (!LocalStorageIsAvailable())
			{
				throw new InvalidOperationException("The browser blocks localStorage for this page.");
			}
		}

		/// <inheritdoc />
		public bool HasKey(string key)
		{
			return LocalStorageHasKey(key);
		}

		/// <inheritdoc />
		public string Read(string key)
		{
			return LocalStorageRead(key);
		}

		/// <inheritdoc />
		/// <exception cref="IOException">브라우저가 쓰기를 거부한 경우(예: 용량 초과)</exception>
		public void Write(string key, string value)
		{
			if (!LocalStorageWrite(key, value ?? string.Empty))
			{
				throw new IOException($"localStorage rejected the write for '{key}', for example because it is full.");
			}
		}

		/// <inheritdoc />
		public void Delete(string key)
		{
			LocalStorageDelete(key);
		}

#if UNITY_WEBGL && !UNITY_EDITOR
		[DllImport("__Internal", EntryPoint = "GeunedaLocalStorageIsAvailable")]
		private static extern bool LocalStorageIsAvailable();

		[DllImport("__Internal", EntryPoint = "GeunedaLocalStorageHasKey")]
		private static extern bool LocalStorageHasKey(string key);

		[DllImport("__Internal", EntryPoint = "GeunedaLocalStorageRead")]
		private static extern string LocalStorageRead(string key);

		[DllImport("__Internal", EntryPoint = "GeunedaLocalStorageWrite")]
		private static extern bool LocalStorageWrite(string key, string value);

		[DllImport("__Internal", EntryPoint = "GeunedaLocalStorageDelete")]
		private static extern void LocalStorageDelete(string key);
#else
		// WebGL 플레이어 밖에서는 생성자가 여기서 실패하므로 나머지 스텁에는 닿지 않는다
		private static bool LocalStorageIsAvailable() => throw new PlatformNotSupportedException(
			$"{nameof(BrowserLocalDataStorage)} only runs in WebGL players. Use another {nameof(IDataStorage)} " +
			"in the Editor and on other platforms.");

		private static bool LocalStorageHasKey(string key) => throw new PlatformNotSupportedException();
		private static string LocalStorageRead(string key) => throw new PlatformNotSupportedException();
		private static bool LocalStorageWrite(string key, string value) => throw new PlatformNotSupportedException();
		private static void LocalStorageDelete(string key) => throw new PlatformNotSupportedException();
#endif
	}
}
