using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 플레이어의 저장된 영구 데이터에 접근하기 위한 인터페이스
	/// </summary>
	public interface IDataProvider
	{
		/// <summary>
		/// <typeparamref name="T"/> 타입의 플레이어 데이터를 요청합니다
		/// </summary>
		T GetData<T>() where T : class;

		/// <summary>
		/// 서비스가 <typeparamref name="T"/> 타입의 플레이어 데이터를 보유하고 있는지 확인합니다
		/// </summary>
		bool HasData<T>() where T : class;
	}

	/// <summary>
	/// 게임의 모든 영구 데이터를 관리하는 서비스입니다.
	/// 데이터 변경 시 박싱/언박싱 및 참조 손실이 발생하지 않도록 참조 타입만 사용합니다.
	/// </summary>
	public interface IDataService : IDataProvider
	{
		/// <summary>
		/// 게임의 <typeparamref name="T"/> 데이터를 디스크에 저장합니다
		/// </summary>
		void SaveData<T>() where T : class;

		/// <summary>
		/// 모든 게임 데이터를 디스크에 저장합니다
		/// </summary>
		void SaveAllData();

		/// <summary>
		/// 디스크에서 게임의 <typeparamref name="T"/> 데이터를 로드하여 반환합니다
		/// </summary>
		T LoadData<T>() where T : class;

		/// <summary>
		/// 주어진 <paramref name="data"/>를 메모리에 추가하거나 교체합니다.
		/// </summary>
		void AddOrReplaceData<T>(T data) where T : class;
	}

	/// <inheritdoc />
	public class DataService : IDataService
	{
		private readonly IDictionary<Type, object> _data = new Dictionary<Type, object>();

		/// <inheritdoc />
		public bool HasData<T>() where T : class
		{
			return _data.ContainsKey(typeof(T));
		}

		/// <inheritdoc />
		public T GetData<T>() where T : class
		{
			return _data[typeof(T)] as T;
		}

		/// <inheritdoc />
		public void SaveData<T>() where T : class
		{
			var type = typeof(T);

			PlayerPrefs.SetString(type.Name, JsonConvert.SerializeObject(_data[type]));
			PlayerPrefs.Save();
			OnDataSaved(type.Name, _data[type], type);
		}

		/// <inheritdoc />
		public void SaveAllData()
		{
			foreach (var data in _data)
			{
				PlayerPrefs.SetString(data.Key.Name, JsonConvert.SerializeObject(data.Value));
				OnDataSaved(data.Key.Name, data.Value, data.Key);
			}
			
			PlayerPrefs.Save();
		}

		/// <inheritdoc />
		public T LoadData<T>() where T : class
		{
			var json = PlayerPrefs.GetString(typeof(T).Name, "");
			var instance = string.IsNullOrEmpty(json) ? Activator.CreateInstance<T>() : JsonConvert.DeserializeObject<T>(json);

			AddOrReplaceData(instance);

			return instance;
		}

		/// <inheritdoc />
		public void AddOrReplaceData<T>(T data) where T : class
		{
			if(HasData<T>())
			{
				_data[typeof(T)] = data;
			}
			else
			{
				_data.Add(typeof(T), data);
			}
		}

		protected virtual void OnDataSaved(string key, object data, Type type)
		{
		}
	}
}