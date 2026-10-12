using Geuneda.DataExtensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.AddressableAssets;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services.AssetsImporter
{
	/// <summary>
	/// 에셋 설정 ScriptableObject 의 추상 기반 클래스입니다.
	/// 에셋을 weak-link 로 참조하는 에셋 설정의 기본 계약을 제공합니다.
	/// </summary>
	public abstract class AssetConfigsScriptableObject : ScriptableObject
	{
		[SerializeField] private string _assetsFolderPath;

		/// <summary>
		/// 이 ScriptableObject 가 설정하는 에셋의 타입을 가져옵니다.
		/// </summary>
		public abstract Type AssetType { get; }

		/// <summary>
		/// 이 컨테이너가 참조할 에셋들의 폴더 경로를 반환합니다.
		/// </summary>
		public string AssetsFolderPath
		{
			get => _assetsFolderPath;
			set => _assetsFolderPath = value;
		}
	}
	/// <summary>
	/// 에셋 설정 ScriptableObject 의 추상 기반 클래스입니다.
	/// 에셋을 weak-link 로 참조하는 에셋 설정의 기본 계약을 제공합니다.
	/// </summary>
	/// <typeparam name="TId">식별자 타입(struct 여야 함).</typeparam>
	/// <typeparam name="TAsset">에셋 타입.</typeparam>
	public abstract class AssetConfigsScriptableObjectBase<TId, TAsset> :
		AssetConfigsScriptableObject, IPairConfigsContainer<TId, TAsset>, ISerializationCallbackReceiver
	{
		[SerializeField] private List<Pair<TId, TAsset>> _configs = new();

		/// <inheritdoc />
		public List<Pair<TId, TAsset>> Configs
		{
			get => _configs;
			set => _configs = value;
		}

		/// <summary>
		/// 에셋 설정을 읽기 전용 딕셔너리로 가져옵니다
		/// </summary>
		public IReadOnlyDictionary<TId, TAsset> ConfigsDictionary { get; private set; }

		/// <inheritdoc />
		public void OnBeforeSerialize()
		{
			// 아무 작업도 하지 않음
		}

		/// <inheritdoc />
		public virtual void OnAfterDeserialize()
		{
			var dictionary = new Dictionary<TId, TAsset>();

			foreach (var config in Configs)
			{
				dictionary.Add(config.Key, config.Value);
			}

			ConfigsDictionary = new ReadOnlyDictionary<TId, TAsset>(dictionary);
		}
	}

	/// <inheritdoc />
	public abstract class AssetConfigsScriptableObject<TId, TAsset> :
		AssetConfigsScriptableObjectBase<TId, AssetReference>
	{
		/// <inheritdoc />
		public override Type AssetType => typeof(TAsset);
	}
}