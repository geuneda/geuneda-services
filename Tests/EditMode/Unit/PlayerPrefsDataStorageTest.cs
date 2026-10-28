using Geuneda.Services;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class PlayerPrefsDataStorageTest
	{
		private const string Key = "Geuneda.Services.Tests." + nameof(PlayerPrefsDataStorageTest);

		private PlayerPrefsDataStorage _storage;

		[SetUp]
		public void Init()
		{
			_storage = new PlayerPrefsDataStorage();
			PlayerPrefs.DeleteKey(Key);
		}

		[TearDown]
		public void Cleanup()
		{
			PlayerPrefs.DeleteKey(Key);
		}

		[Test]
		public void Read_MissingKey_ReturnsNull()
		{
			// PlayerPrefs.GetString 의 기본값(빈 문자열)이 아니라 null 이어야 "저장된 값 없음" 과 빈 값을 구별할 수 있다
			Assert.IsFalse(_storage.HasKey(Key));
			Assert.IsNull(_storage.Read(Key));
		}

		[Test]
		public void Write_Read_Delete_RoundTrip()
		{
			_storage.Write(Key, "{\"Value\":1}");

			Assert.IsTrue(_storage.HasKey(Key));
			Assert.AreEqual("{\"Value\":1}", new PlayerPrefsDataStorage().Read(Key));

			_storage.Delete(Key);

			Assert.IsFalse(_storage.HasKey(Key));
			Assert.IsNull(_storage.Read(Key));
		}

		[Test]
		public void Write_EmptyString_ReadsBackEmptyString()
		{
			_storage.Write(Key, string.Empty);

			Assert.IsTrue(_storage.HasKey(Key));
			Assert.AreEqual(string.Empty, _storage.Read(Key));
		}
	}
}
