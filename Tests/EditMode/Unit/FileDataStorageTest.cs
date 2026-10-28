using System;
using System.IO;
using Geuneda.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class FileDataStorageTest
	{
		private const string Key = "Geuneda.Services.Tests.PersistentData";

		private string _directory;
		private FileDataStorage _storage;

		[SetUp]
		public void Init()
		{
			_directory = Path.Combine(Path.GetTempPath(), nameof(FileDataStorageTest), Guid.NewGuid().ToString("N"));
			_storage = new FileDataStorage(_directory);
		}

		[TearDown]
		public void Cleanup()
		{
			if (Directory.Exists(_directory))
			{
				Directory.Delete(_directory, true);
			}
		}

		[Test]
		public void Constructor_EmptyDirectory_ThrowsArgumentException()
		{
			Assert.Throws<ArgumentException>(() => new FileDataStorage(""));
		}

		[Test]
		public void Write_Read_Delete_RoundTripThroughNewInstance()
		{
			Assert.IsNull(_storage.Read(Key));
			Assert.IsFalse(_storage.HasKey(Key));

			_storage.Write(Key, "{\"Name\":\"Hérø\"}");

			var reopened = new FileDataStorage(_directory);

			Assert.IsTrue(reopened.HasKey(Key));
			Assert.AreEqual("{\"Name\":\"Hérø\"}", reopened.Read(Key));

			reopened.Delete(Key);

			Assert.IsFalse(_storage.HasKey(Key));
			Assert.IsNull(_storage.Read(Key));
		}

		[Test]
		public void Delete_MissingKey_DoesNotThrow()
		{
			Assert.DoesNotThrow(() => _storage.Delete(Key));
		}

		[Test]
		public void Write_ExistingKey_ReplacesValueAndLeavesNoTempFile()
		{
			_storage.Write(Key, "first");
			_storage.Write(Key, "second");

			Assert.AreEqual("second", _storage.Read(Key));
			CollectionAssert.AreEquivalent(new[] { Key + ".json" },
				Array.ConvertAll(Directory.GetFileSystemEntries(_directory), Path.GetFileName));
		}

		[Test]
		public void Write_TempFileFails_KeepsPreviousValue()
		{
			_storage.Write(Key, "first");
			// 임시 파일 자리에 폴더를 두어 임시 파일 쓰기를 실패시킨다
			Directory.CreateDirectory(Path.Combine(_directory, Key + ".json.tmp"));

			Assert.Catch<Exception>(() => _storage.Write(Key, "second"));
			Assert.AreEqual("first", _storage.Read(Key));
		}

		[Test]
		public void Write_NullValue_StoresEmptyString()
		{
			_storage.Write(Key, null);

			Assert.IsTrue(_storage.HasKey(Key));
			Assert.AreEqual(string.Empty, _storage.Read(Key));
		}

		[TestCase("")]
		[TestCase("../escape")]
		[TestCase("nested/key")]
		public void AnyMethod_KeyIsNotAFileName_ThrowsArgumentException(string key)
		{
			Assert.Throws<ArgumentException>(() => _storage.HasKey(key));
			Assert.Throws<ArgumentException>(() => _storage.Read(key));
			Assert.Throws<ArgumentException>(() => _storage.Write(key, "value"));
			Assert.Throws<ArgumentException>(() => _storage.Delete(key));
		}
	}
}
