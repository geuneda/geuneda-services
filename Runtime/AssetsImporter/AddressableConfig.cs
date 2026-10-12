using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services.AssetsImporter
{
	/// <summary>
	/// Addressable 하나의 주요 데이터를 모두 담은 설정입니다.
	/// Id 는 AddressableIds 코드 생성기가 만든 AddressableId 의 int 값입니다
	/// </summary>
	[Serializable]
	public class AddressableConfig
	{
		public int Id;
		public string Address;
		public string Path;
		public string AssetFileType;
		public Type AssetType;
		public ReadOnlyCollection<string> Labels;

		public AddressableConfig(int id, string address, string path, Type assetType, string[] labels)
		{
			Id = id;
			Address = address;
			Path = path;
			AssetFileType = path.Substring(path.LastIndexOf('.') + 1);
			AssetType = assetType;
			Labels = new ReadOnlyCollection<string>(labels);
		}

		/// <summary>
		/// 이 <see cref="AddressableConfig"/> 가 씬 설정이면 씬 이름을 반환합니다.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// 이 <see cref="AddressableConfig"/> 가 씬 설정이 아닐 때 발생합니다
		/// </exception>
		public string GetSceneName()
		{
			if (AssetType != typeof(UnityEngine.SceneManagement.Scene))
			{
				throw new InvalidOperationException($"This {nameof(AddressableConfig)} is not of a " +
													$"{typeof(UnityEngine.SceneManagement.Scene)} config type. It's of {AssetType.Name} type.");
			}

			var index = Address.LastIndexOf("/", StringComparison.Ordinal);
			var typeIndex = Address.LastIndexOf(".", StringComparison.Ordinal);

			index = index < 0 ? 0 : index + 1;
			typeIndex = typeIndex < index ? Address.Length : typeIndex;

			return Address.Substring(index, typeIndex - index);
		}
	}

	/// <summary>
	/// Dictionary 에서 박싱을 피하기 위한 비교자
	/// </summary>
	public class AddressableConfigComparer : IEqualityComparer<AddressableConfig>
	{
		/// <inheritdoc />
		public bool Equals(AddressableConfig x, AddressableConfig y)
		{
			return x.Id == y.Id;
		}

		/// <inheritdoc />
		public int GetHashCode(AddressableConfig config)
		{
			return config.Id;
		}
	}
}