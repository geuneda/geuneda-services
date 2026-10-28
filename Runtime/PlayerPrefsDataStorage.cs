using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// <see cref="PlayerPrefs"/> 에 저장합니다. <see cref="DataService"/> 의 기본 저장소입니다.
	/// 쓰고 지울 때마다 <see cref="PlayerPrefs.Save"/> 를 호출합니다.
	/// </summary>
	public class PlayerPrefsDataStorage : IDataStorage
	{
		/// <inheritdoc />
		public bool HasKey(string key)
		{
			return PlayerPrefs.HasKey(key);
		}

		/// <inheritdoc />
		public string Read(string key)
		{
			return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
		}

		/// <inheritdoc />
		public void Write(string key, string value)
		{
			PlayerPrefs.SetString(key, value);
			PlayerPrefs.Save();
		}

		/// <inheritdoc />
		public void Delete(string key)
		{
			PlayerPrefs.DeleteKey(key);
			PlayerPrefs.Save();
		}
	}
}
