using System;
using System.IO;
using System.Text;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 키마다 UTF-8 <c>{키}.json</c> 파일 하나를 한 폴더에 저장합니다. 폴더는 생성자에 넘긴 경로(없으면 만듦)이고,
	/// 기본은 <see cref="Application.persistentDataPath"/> 입니다.
	/// 쓰기는 임시 파일에 먼저 쓴 뒤 기존 파일과 바꾸므로, 쓰는 도중 앱이 죽거나 전원이 꺼져도 반쯤 쓰인 값이 남지 않고
	/// 마지막으로 완전히 쓴 값이 유지됩니다.
	/// </summary>
	public class FileDataStorage : IDataStorage
	{
		private const string FileExtension = ".json";
		private const string TempExtension = ".tmp";

		private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
		private static readonly char[] InvalidKeyChars = Path.GetInvalidFileNameChars();

		private readonly string _directory;

		/// <summary>
		/// <see cref="Application.persistentDataPath"/> 에 저장하는 저장소를 만듭니다
		/// </summary>
		public FileDataStorage() : this(Application.persistentDataPath)
		{
		}

		/// <summary>
		/// <paramref name="directory"/> 에 저장하는 저장소를 만듭니다. 폴더가 없으면 만듭니다
		/// </summary>
		/// <exception cref="ArgumentException"><paramref name="directory"/> 가 비어 있는 경우</exception>
		public FileDataStorage(string directory)
		{
			if (string.IsNullOrEmpty(directory))
			{
				throw new ArgumentException("A storage directory is required.", nameof(directory));
			}

			_directory = directory;
			Directory.CreateDirectory(_directory);
		}

		/// <inheritdoc />
		/// <exception cref="ArgumentException"><paramref name="key"/> 가 파일 이름으로 쓸 수 없는 경우</exception>
		public bool HasKey(string key)
		{
			return File.Exists(GetPath(key));
		}

		/// <inheritdoc />
		/// <exception cref="ArgumentException"><paramref name="key"/> 가 파일 이름으로 쓸 수 없는 경우</exception>
		public string Read(string key)
		{
			var path = GetPath(key);

			return File.Exists(path) ? File.ReadAllText(path, Utf8NoBom) : null;
		}

		/// <inheritdoc />
		/// <exception cref="ArgumentException"><paramref name="key"/> 가 파일 이름으로 쓸 수 없는 경우</exception>
		public void Write(string key, string value)
		{
			var path = GetPath(key);
			var tempPath = path + TempExtension;

			using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				var bytes = Utf8NoBom.GetBytes(value ?? string.Empty);

				stream.Write(bytes, 0, bytes.Length);
				// 디스크까지 내려간 뒤에 바꿔치기해야 바꾼 파일이 비어 있지 않다
				stream.Flush(true);
			}

			if (File.Exists(path))
			{
				File.Replace(tempPath, path, null);
			}
			else
			{
				File.Move(tempPath, path);
			}
		}

		/// <inheritdoc />
		/// <exception cref="ArgumentException"><paramref name="key"/> 가 파일 이름으로 쓸 수 없는 경우</exception>
		public void Delete(string key)
		{
			File.Delete(GetPath(key));
		}

		// 키가 폴더 밖의 파일을 가리키지 못하도록 파일 이름으로 쓸 수 있는 키만 받는다
		private string GetPath(string key)
		{
			if (string.IsNullOrEmpty(key) || key.IndexOfAny(InvalidKeyChars) >= 0)
			{
				throw new ArgumentException($"'{key}' is not a valid file name.", nameof(key));
			}

			return Path.Combine(_directory, key + FileExtension);
		}
	}
}
