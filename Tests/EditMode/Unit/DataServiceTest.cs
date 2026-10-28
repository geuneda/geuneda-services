using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Geuneda.Services;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class DataServiceTest
	{
		// ReSharper disable once MemberCanBePrivate.Global
		public interface IDataMockup {}

		public class PersistentData
		{
			public string Name;
			public int Value;
		}

		// 이름은 같고 감싸는 타입이 다른 두 데이터(이전의 Type.Name 키에서는 충돌)
		public static class FirstScope
		{
			public class SameNameData
			{
				public int Value;
			}
		}

		public static class SecondScope
		{
			public class SameNameData
			{
				public int Value;
			}
		}

		// v0: { "Name": "..." } → v1: { "DisplayName": "..." } → v2: { "DisplayName": "...", "Level": 1 }
		[DataVersion(2)]
		public class VersionedData
		{
			public string DisplayName;
			public int Level;
		}

		public class PreciseData
		{
			public decimal Amount;
			public double Large;
			public string Text;
		}

		[DataVersion(1)]
		public class PreciseVersionedData
		{
			public decimal Amount;
			public double Large;
			public string Text;
		}

		// 저장소 교체 테스트용 메모리 저장소
		private class MemoryDataStorage : IDataStorage
		{
			public readonly Dictionary<string, string> Values = new Dictionary<string, string>();

			public bool HasKey(string key) => Values.ContainsKey(key);
			public string Read(string key) => Values.TryGetValue(key, out var value) ? value : null;
			public void Write(string key, string value) => Values[key] = value;
			public void Delete(string key) => Values.Remove(key);
		}

		private class ThrowingRecoveryDataService : DataService
		{
			public bool ThrowOnBackup;
			public bool ThrowOnHook;
			public int HookCount;

			protected override void SaveCorruptBackup(string key, string rawData)
			{
				if (ThrowOnBackup)
				{
					throw new InvalidOperationException("backup failed");
				}

				base.SaveCorruptBackup(key, rawData);
			}

			protected override void OnDataLoadFailed(string key, string rawData, Exception exception)
			{
				HookCount++;

				if (ThrowOnHook)
				{
					throw new InvalidOperationException("hook failed");
				}
			}
		}

		private class TestableDataService : DataService
		{
			public TestableDataService()
			{
			}

			public TestableDataService(IDataStorage storage, string keyPrefix) : base(storage, keyPrefix)
			{
			}

			public int SaveCount;
			public string LastKey;
			public object LastInstance;
			public System.Type LastType;
			public int LoadFailedCount;
			public string LoadFailedKey;
			public string LoadFailedRaw;

			protected override void OnDataSaved(string key, object data, System.Type type)
			{
				SaveCount++;
				LastKey = key;
				LastInstance = data;
				LastType = type;
			}

			protected override void OnDataLoadFailed(string key, string rawData, Exception exception)
			{
				LoadFailedCount++;
				LoadFailedKey = key;
				LoadFailedRaw = rawData;
			}
		}

		private DataService _dataService;

		[SetUp]
		public void Init()
		{
			_dataService = new DataService();
			PlayerPrefs.DeleteAll();
		}

		[Test]
		public void AddData_Successfully()
		{
			var data = Substitute.For<IDataMockup>();
			
			_dataService.AddOrReplaceData(data);

			Assert.AreSame(data, _dataService.GetData<IDataMockup>());
		}

		[Test]
		public void ReplaceData_Successfully()
		{
			var data = Substitute.For<IDataMockup>();
			var data1 = new object();

			_dataService.AddOrReplaceData(data1);
			_dataService.AddOrReplaceData(data);

			Assert.AreNotSame(data1, _dataService.GetData<IDataMockup>());
			Assert.AreSame(data, _dataService.GetData<IDataMockup>());
		}

		[Test]
		public void GetData_NotFound_ThrowsException()
		{
			Assert.Throws<KeyNotFoundException>(() => _dataService.GetData<IDataMockup>());
		}

		[Test]
		public void SaveData_LoadData_RoundTrip_Successfully()
		{
			var data = new PersistentData { Name = "Test", Value = 123 };
			_dataService.AddOrReplaceData(data);
			_dataService.SaveData<PersistentData>();
			
			var dataService2 = new DataService();
			var loadedData = dataService2.LoadData<PersistentData>();
			
			Assert.AreEqual(data.Name, loadedData.Name);
			Assert.AreEqual(data.Value, loadedData.Value);
		}

		[Test]
		public void LoadData_NoExistingData_CreatesNew()
		{
			var loadedData = _dataService.LoadData<PersistentData>();
			
			Assert.IsNotNull(loadedData);
			Assert.IsNull(loadedData.Name);
			Assert.AreEqual(0, loadedData.Value);
		}

		[Test]
		public void HasData_Successfully()
		{
			var data = new PersistentData();
			_dataService.AddOrReplaceData(data);
			
			Assert.IsTrue(_dataService.HasData<PersistentData>());
			Assert.AreSame(data, _dataService.GetData<PersistentData>());
		}

		[Test]
		public void HasData_NotFound_ReturnsFalse()
		{
			Assert.IsFalse(_dataService.HasData<PersistentData>());
		}

		[Test]
		public void SaveAllData_Successfully()
		{
			var data1 = new PersistentData { Name = "Hero", Value = 10 };
			var data2 = new PersistentData { Name = "Alt", Value = 20 };

			_dataService.AddOrReplaceData(data1);
			_dataService.SaveData<PersistentData>();
			_dataService.AddOrReplaceData(data2);

			_dataService.SaveAllData();

			var dataService2 = new DataService();
			var loaded = dataService2.LoadData<PersistentData>();

			Assert.AreEqual(data2.Name, loaded.Name);
			Assert.AreEqual(data2.Value, loaded.Value);
		}

		[Test]
		public void OnDataSaved_SubclassHook_FiresAfterSave()
		{
			var subclass = new TestableDataService();
			var data = new PersistentData { Name = "Hook", Value = 42 };
			subclass.AddOrReplaceData(data);

			subclass.SaveData<PersistentData>();

			Assert.AreEqual(1, subclass.SaveCount);
			Assert.AreEqual(typeof(PersistentData).FullName, subclass.LastKey);
			Assert.AreSame(data, subclass.LastInstance);
			Assert.AreEqual(typeof(PersistentData), subclass.LastType);
		}

		[Test]
		public void SaveData_WritesVersionedEnvelopeUnderFullNameKey()
		{
			_dataService.AddOrReplaceData(new PersistentData { Name = "Env", Value = 7 });
			_dataService.SaveData<PersistentData>();

			Assert.IsFalse(PlayerPrefs.HasKey(typeof(PersistentData).Name));

			var envelope = JObject.Parse(PlayerPrefs.GetString(typeof(PersistentData).FullName));

			Assert.AreEqual(0, envelope["version"].Value<int>());
			Assert.AreEqual("Env", envelope["data"]["Name"].Value<string>());
			Assert.AreEqual(7, envelope["data"]["Value"].Value<int>());
		}

		[Test]
		public void SaveData_VersionedType_WritesAttributeVersion()
		{
			_dataService.AddOrReplaceData(new VersionedData { DisplayName = "Hero", Level = 3 });
			_dataService.SaveData<VersionedData>();

			var envelope = JObject.Parse(PlayerPrefs.GetString(DataService.GetSaveKey(typeof(VersionedData))));

			Assert.AreEqual(2, envelope["version"].Value<int>());
		}

		[Test]
		public void SaveData_SameTypeNameInDifferentScopes_DoesNotCollide()
		{
			_dataService.AddOrReplaceData(new FirstScope.SameNameData { Value = 1 });
			_dataService.AddOrReplaceData(new SecondScope.SameNameData { Value = 2 });
			_dataService.SaveAllData();

			var dataService2 = new DataService();

			Assert.AreEqual(1, dataService2.LoadData<FirstScope.SameNameData>().Value);
			Assert.AreEqual(2, dataService2.LoadData<SecondScope.SameNameData>().Value);
		}

		[TestCase("{ not json")]
		[TestCase("[1, 2, 3]")]
		[TestCase("{\"Name\":\"Legacy\",\"Value\":5}")]
		[TestCase("{\"version\":\"0\",\"data\":{}}")]
		[TestCase("{\"version\":0,\"data\":null}")]
		[TestCase("{\"version\":0,\"data\":{\"Value\":\"not a number\"}}")]
		[TestCase("{\"version\":0,\"data\":{\"Value\":1}} trailing")]
		[TestCase("{\"version\":0,\"data\":{\"Value\":1}}{\"version\":0,\"data\":{\"Value\":2}}")]
		public void LoadData_UnreadableSave_KeepsRawDataAndReturnsNewInstance(string raw)
		{
			var key = DataService.GetSaveKey(typeof(PersistentData));
			var service = new TestableDataService();
			PlayerPrefs.SetString(key, raw);

			LogAssert.Expect(LogType.Error, new Regex("Failed to load save data"));
			var loaded = service.LoadData<PersistentData>();

			Assert.IsNotNull(loaded);
			Assert.AreEqual(0, loaded.Value);
			Assert.AreSame(loaded, service.GetData<PersistentData>());
			Assert.AreEqual(raw, PlayerPrefs.GetString(key + DataService.CorruptKeySuffix));
			Assert.AreEqual(raw, PlayerPrefs.GetString(key), "새 인스턴스를 저장하기 전에는 원래 세이브를 덮어쓰지 않는다");
			Assert.AreEqual(1, service.LoadFailedCount);
			Assert.AreEqual(key, service.LoadFailedKey);
			Assert.AreEqual(raw, service.LoadFailedRaw);
		}

		[Test]
		public void LoadData_OlderVersion_AppliesMigrationsInOrder()
		{
			PlayerPrefs.SetString(DataService.GetSaveKey(typeof(VersionedData)), "{\"version\":0,\"data\":{\"Name\":\"Old\"}}");
			_dataService.AddMigration<VersionedData>(1, data =>
			{
				data["Level"] = 1;
				return data;
			});
			_dataService.AddMigration<VersionedData>(0, data => new JObject { ["DisplayName"] = data["Name"] });

			var loaded = _dataService.LoadData<VersionedData>();

			Assert.AreEqual("Old", loaded.DisplayName);
			Assert.AreEqual(1, loaded.Level);
		}

		[Test]
		public void LoadData_MissingMigrationStep_KeepsRawDataAndReturnsNewInstance()
		{
			var key = DataService.GetSaveKey(typeof(VersionedData));
			var raw = "{\"version\":0,\"data\":{\"Name\":\"Old\"}}";
			PlayerPrefs.SetString(key, raw);
			_dataService.AddMigration<VersionedData>(0, data => new JObject { ["DisplayName"] = data["Name"] });

			LogAssert.Expect(LogType.Error, new Regex("No migration registered"));
			var loaded = _dataService.LoadData<VersionedData>();

			Assert.IsNull(loaded.DisplayName);
			Assert.AreEqual(raw, PlayerPrefs.GetString(key + DataService.CorruptKeySuffix));
		}

		[Test]
		public void LoadData_NewerVersion_LogsWarningAndReadsKnownFields()
		{
			PlayerPrefs.SetString(DataService.GetSaveKey(typeof(PersistentData)),
				"{\"version\":5,\"data\":{\"Name\":\"Future\",\"Value\":9,\"Extra\":true}}");

			LogAssert.Expect(LogType.Warning, new Regex("newer than 0"));
			var loaded = _dataService.LoadData<PersistentData>();

			Assert.AreEqual("Future", loaded.Name);
			Assert.AreEqual(9, loaded.Value);
		}

		[Test]
		public void SaveData_LoadData_PreservesDecimalPrecisionDateLikeStringAndLargeDouble()
		{
			var data = new PreciseData
			{
				Amount = 0.1234567890123456789012345678m,
				Large = 1e300,
				Text = "2026-10-13T00:00:00.000Z"
			};
			_dataService.AddOrReplaceData(data);
			_dataService.SaveData<PreciseData>();

			var loaded = new DataService().LoadData<PreciseData>();

			Assert.AreEqual(data.Amount, loaded.Amount);
			Assert.AreEqual(data.Large, loaded.Large);
			Assert.AreEqual(data.Text, loaded.Text);
		}

		[Test]
		public void LoadData_Migration_PreservesDecimalPrecisionAndDateLikeString()
		{
			PlayerPrefs.SetString(DataService.GetSaveKey(typeof(PreciseVersionedData)),
				"{\"version\":0,\"data\":{\"Amount\":0.1234567890123456789012345678,\"Text\":\"2026-10-13T00:00:00.000Z\"}}");
			_dataService.AddMigration<PreciseVersionedData>(0, data => data);

			var loaded = _dataService.LoadData<PreciseVersionedData>();

			Assert.AreEqual(0.1234567890123456789012345678m, loaded.Amount);
			Assert.AreEqual("2026-10-13T00:00:00.000Z", loaded.Text);
		}

		[Test]
		public void LoadData_Migration_LargeDoubleBeyondDecimalRange_IsRead()
		{
			PlayerPrefs.SetString(DataService.GetSaveKey(typeof(PreciseVersionedData)),
				"{\"version\":0,\"data\":{\"Large\":1E+300}}");
			_dataService.AddMigration<PreciseVersionedData>(0, data => data);

			var loaded = _dataService.LoadData<PreciseVersionedData>();

			Assert.AreEqual(1e300, loaded.Large);
		}

		[Test]
		public void LoadData_Migration_MixedPreciseDecimalAndLargeDouble_BothPreservedThroughResave()
		{
			var key = DataService.GetSaveKey(typeof(PreciseVersionedData));
			PlayerPrefs.SetString(key,
				"{\"version\":0,\"data\":{\"Amount\":0.1234567890123456789012345678,\"Large\":1E+300,\"Text\":\"2026-10-13T00:00:00.000Z\"}}");
			_dataService.AddMigration<PreciseVersionedData>(0, data => data);

			var loaded = _dataService.LoadData<PreciseVersionedData>();

			Assert.AreEqual(0.1234567890123456789012345678m, loaded.Amount);
			Assert.AreEqual(1e300, loaded.Large);
			Assert.AreEqual("2026-10-13T00:00:00.000Z", loaded.Text);

			_dataService.SaveData<PreciseVersionedData>();

			var reloaded = new DataService().LoadData<PreciseVersionedData>();

			Assert.AreEqual(0.1234567890123456789012345678m, reloaded.Amount);
			Assert.AreEqual(1e300, reloaded.Large);
			Assert.AreEqual("2026-10-13T00:00:00.000Z", reloaded.Text);
		}

		[TestCase(0.30000000000000004)]
		[TestCase(1.7976931348623157E+308)]
		[TestCase(4.9406564584124654E-324)]
		[TestCase(1.234567e-27)]
		[TestCase(1.000000000000001E-20)]
		[TestCase(-1.000000000000001E-20)]
		[TestCase(-0.30000000000000004)]
		public void LoadData_Migration_DoubleField_RoundTripsExactly(double value)
		{
			var key = DataService.GetSaveKey(typeof(PreciseVersionedData));
			var literal = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
			PlayerPrefs.SetString(key, "{\"version\":0,\"data\":{\"Large\":" + literal + "}}");
			_dataService.AddMigration<PreciseVersionedData>(0, data => data);

			var loaded = _dataService.LoadData<PreciseVersionedData>();

			Assert.AreEqual(value, loaded.Large);

			// 재저장 후 다시 읽어도 같은 값이어야 한다
			_dataService.SaveData<PreciseVersionedData>();

			Assert.AreEqual(value, new DataService().LoadData<PreciseVersionedData>().Large);
		}

		[Test]
		public void LoadData_Migration_TrailingContent_IsTreatedAsUnreadable()
		{
			var key = DataService.GetSaveKey(typeof(PreciseVersionedData));
			var raw = "{\"version\":0,\"data\":{\"Amount\":1}} trailing-garbage";
			PlayerPrefs.SetString(key, raw);
			_dataService.AddMigration<PreciseVersionedData>(0, data => data);

			LogAssert.Expect(LogType.Error, new Regex("Failed to load save data"));
			var loaded = _dataService.LoadData<PreciseVersionedData>();

			Assert.AreEqual(0m, loaded.Amount);
			Assert.AreEqual(raw, PlayerPrefs.GetString(key + DataService.CorruptKeySuffix));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void LoadData_NewerVersionBodyTooDeep_SaveKeepsOriginal(bool backupFails)
		{
			// 본문이 Newtonsoft 의 기본 최대 깊이(64)를 넘어 헤더 검증 중 본문을 건너뛰다 실패하는 경우
			var key = DataService.GetSaveKey(typeof(PersistentData));
			var nested = new string('[', 80) + new string(']', 80);
			var raw = "{\"version\":5,\"data\":{\"Value\":9,\"Extra\":" + nested + "}}";
			var service = new ThrowingRecoveryDataService { ThrowOnBackup = backupFails };
			PlayerPrefs.SetString(key, raw);

			if (backupFails)
			{
				LogAssert.Expect(LogType.Error, new Regex(@"\[Geuneda\.Services\.Data\] Could not back up the unreadable save data"));
				LogAssert.Expect(LogType.Exception, new Regex("backup failed"));
				LogAssert.Expect(LogType.Error, new Regex("could not be backed up"));
			}
			else
			{
				LogAssert.Expect(LogType.Error, new Regex("raw data was kept"));
			}

			service.LoadData<PersistentData>();

			LogAssert.Expect(LogType.Warning, new Regex("not saved"));
			service.SaveData<PersistentData>();
			LogAssert.Expect(LogType.Warning, new Regex("not saved"));
			service.SaveAllData();

			Assert.AreEqual(raw, PlayerPrefs.GetString(key));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void LoadData_NewerVersionUnreadablePayload_SaveKeepsOriginal(bool backupFails)
		{
			var key = DataService.GetSaveKey(typeof(PersistentData));
			var raw = "{\"version\":5,\"data\":{\"Name\":\"Future\",\"Value\":{\"amount\":9},\"Extra\":true}}";
			var service = new ThrowingRecoveryDataService { ThrowOnBackup = backupFails };
			PlayerPrefs.SetString(key, raw);

			LogAssert.Expect(LogType.Warning, new Regex("newer than 0"));

			if (backupFails)
			{
				LogAssert.Expect(LogType.Error, new Regex(@"\[Geuneda\.Services\.Data\] Could not back up the unreadable save data"));
				LogAssert.Expect(LogType.Exception, new Regex("backup failed"));
				LogAssert.Expect(LogType.Error, new Regex("could not be backed up"));
			}
			else
			{
				LogAssert.Expect(LogType.Error, new Regex("raw data was kept"));
			}

			service.LoadData<PersistentData>();

			LogAssert.Expect(LogType.Warning, new Regex("not saved"));
			service.SaveData<PersistentData>();
			LogAssert.Expect(LogType.Warning, new Regex("not saved"));
			service.SaveAllData();

			Assert.AreEqual(raw, PlayerPrefs.GetString(key));
		}

		[Test]
		public void LoadData_RecoveryHookThrows_StillReturnsNewInstance()
		{
			var service = new ThrowingRecoveryDataService { ThrowOnHook = true };
			PlayerPrefs.SetString(DataService.GetSaveKey(typeof(PersistentData)), "{ not json");

			LogAssert.Expect(LogType.Error, new Regex("raw data was kept"));
			LogAssert.Expect(LogType.Error, new Regex(@"\[Geuneda\.Services\.Data\] OnDataLoadFailed threw"));
			LogAssert.Expect(LogType.Exception, new Regex("hook failed"));
			var loaded = service.LoadData<PersistentData>();

			Assert.IsNotNull(loaded);
			Assert.AreSame(loaded, service.GetData<PersistentData>());
			Assert.AreEqual(1, service.HookCount);
		}

		[Test]
		public void LoadData_BackupThrows_StillCallsHookAndReturnsNewInstance()
		{
			var service = new ThrowingRecoveryDataService { ThrowOnBackup = true };
			var key = DataService.GetSaveKey(typeof(PersistentData));
			PlayerPrefs.SetString(key, "{ not json");

			LogAssert.Expect(LogType.Error, new Regex(@"\[Geuneda\.Services\.Data\] Could not back up the unreadable save data"));
			LogAssert.Expect(LogType.Exception, new Regex("backup failed"));
			LogAssert.Expect(LogType.Error, new Regex("could not be backed up"));
			var loaded = service.LoadData<PersistentData>();

			Assert.IsNotNull(loaded);
			Assert.AreEqual(1, service.HookCount);
			Assert.IsFalse(PlayerPrefs.HasKey(key + DataService.CorruptKeySuffix));
		}

		[Test]
		public void LoadData_NewerVersion_SaveKeepsOriginalUntilReplaced()
		{
			var key = DataService.GetSaveKey(typeof(PersistentData));
			var raw = "{\"version\":5,\"data\":{\"Name\":\"Future\",\"Value\":9,\"Extra\":true}}";
			PlayerPrefs.SetString(key, raw);

			LogAssert.Expect(LogType.Warning, new Regex("newer than 0"));
			var loaded = _dataService.LoadData<PersistentData>();
			loaded.Value = 1;

			LogAssert.Expect(LogType.Warning, new Regex("not saved"));
			_dataService.SaveData<PersistentData>();
			LogAssert.Expect(LogType.Warning, new Regex("not saved"));
			_dataService.SaveAllData();

			Assert.AreEqual(raw, PlayerPrefs.GetString(key));

			_dataService.AddOrReplaceData(new PersistentData { Name = "Replaced", Value = 2 });
			_dataService.SaveData<PersistentData>();

			var envelope = JObject.Parse(PlayerPrefs.GetString(key));

			Assert.AreEqual(0, envelope["version"].Value<int>());
			Assert.AreEqual("Replaced", envelope["data"]["Name"].Value<string>());
		}

		[Test]
		public void AddMigration_InvalidArguments_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => _dataService.AddMigration<VersionedData>(0, null));
			Assert.Throws<ArgumentOutOfRangeException>(() => _dataService.AddMigration<VersionedData>(-1, data => data));
			Assert.Throws<ArgumentOutOfRangeException>(() => _dataService.AddMigration<VersionedData>(2, data => data));
			Assert.Throws<ArgumentOutOfRangeException>(() => _dataService.AddMigration<PersistentData>(0, data => data));

			_dataService.AddMigration<VersionedData>(0, data => data);

			Assert.Throws<ArgumentException>(() => _dataService.AddMigration<VersionedData>(0, data => data));
		}

		[Test]
		public void Constructor_NullStorage_ThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() => new DataService(null));
		}

		[Test]
		public void SaveData_CustomStorage_WritesThroughInjectedStorage()
		{
			var storage = new MemoryDataStorage();
			var service = new DataService(storage);
			var key = DataService.GetSaveKey(typeof(PersistentData));
			service.AddOrReplaceData(new PersistentData { Name = "Stored", Value = 7 });

			service.SaveData<PersistentData>();

			CollectionAssert.AreEquivalent(new[] { key }, storage.Values.Keys);
			Assert.IsFalse(PlayerPrefs.HasKey(key), "주입한 저장소만 쓰고 PlayerPrefs 에는 쓰지 않는다");
			Assert.AreEqual("Stored", new DataService(storage).LoadData<PersistentData>().Name);
		}

		[Test]
		public void SaveAllData_CustomStorage_WritesEveryTypeThroughInjectedStorage()
		{
			var storage = new MemoryDataStorage();
			var service = new DataService(storage);
			service.AddOrReplaceData(new PersistentData { Name = "A" });
			service.AddOrReplaceData(new VersionedData { DisplayName = "B" });

			service.SaveAllData();

			CollectionAssert.AreEquivalent(
				new[] { DataService.GetSaveKey(typeof(PersistentData)), DataService.GetSaveKey(typeof(VersionedData)) },
				storage.Values.Keys);
		}

		[Test]
		public void KeyPrefix_SharedStorage_IsolatesSaves()
		{
			var storage = new MemoryDataStorage();
			var key = DataService.GetSaveKey(typeof(PersistentData));
			var profileA = new DataService(storage, "ProfileA");
			profileA.AddOrReplaceData(new PersistentData { Name = "A", Value = 1 });

			profileA.SaveAllData();

			CollectionAssert.AreEquivalent(new[] { "ProfileA." + key }, storage.Values.Keys);
			Assert.AreEqual("ProfileA." + key, profileA.GetStorageKey(typeof(PersistentData)));
			Assert.IsNull(new DataService(storage, "ProfileB").LoadData<PersistentData>().Name);
			Assert.IsNull(new DataService(storage).LoadData<PersistentData>().Name);
			Assert.AreEqual("A", new DataService(storage, "ProfileA").LoadData<PersistentData>().Name);
		}

		[TestCase(null)]
		[TestCase("")]
		public void KeyPrefix_NullOrEmpty_UsesSaveKey(string prefix)
		{
			var service = new DataService(new MemoryDataStorage(), prefix);

			Assert.AreEqual(DataService.GetSaveKey(typeof(PersistentData)), service.GetStorageKey(typeof(PersistentData)));
		}

		[Test]
		public void OnDataSaved_WithKeyPrefix_ReceivesStorageKey()
		{
			var subclass = new TestableDataService(new MemoryDataStorage(), "Slot1");
			subclass.AddOrReplaceData(new PersistentData());

			subclass.SaveData<PersistentData>();

			Assert.AreEqual("Slot1." + DataService.GetSaveKey(typeof(PersistentData)), subclass.LastKey);
		}

		[Test]
		public void LoadData_UnreadableSave_CustomStorage_KeepsRawDataInSameStorage()
		{
			var storage = new MemoryDataStorage();
			var service = new DataService(storage, "Slot1");
			var key = service.GetStorageKey(typeof(PersistentData));
			storage.Values[key] = "{ not json";

			LogAssert.Expect(LogType.Error, new Regex("Failed to load save data"));
			var loaded = service.LoadData<PersistentData>();

			Assert.AreEqual(0, loaded.Value);
			Assert.AreEqual("{ not json", storage.Values[key + DataService.CorruptKeySuffix]);
			Assert.AreEqual("{ not json", storage.Values[key]);
			Assert.IsFalse(PlayerPrefs.HasKey(key + DataService.CorruptKeySuffix));
		}

	}
}
