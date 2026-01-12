using System.Collections.Generic;
using Geuneda.Services;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class DataServiceTest
	{
		private DataService _dataService;

		// ReSharper disable once MemberCanBePrivate.Global
		public interface IDataMockup {}
		
		public class PersistentData
		{
			public string Name;
			public int Value;
		}

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
	}
}
