using System;
using System.Collections;
using System.Collections.Generic;
using Geuneda.DataExtensions;
using Geuneda.Services.AssetsImporter;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace Geuneda.Services
{
	/// <summary>
	/// 프로젝트의 필요에 맞게 에셋 로드 동작을 확장하는 서비스입니다
	/// </summary>
	public interface IAssetResolverService : IAssetLoader, ISceneLoader
	{
		/// <summary>
		/// 주어진 <paramref name="id"/> 의 <typeparamref name="TAsset"/> 을 요청합니다.
		/// <paramref name="loadAsynchronously"/> 가 true 이면 비동기로 로드합니다.
		/// 로드가 끝나면 결과를 <paramref name="onLoadCallback"/> 으로 돌려주고,
		/// <paramref name="instantiate"/> 가 true 이면 에셋을 인스턴스화합니다
		/// </summary>
		UniTask<TAsset> RequestAsset<TId, TAsset>(TId id, bool loadAsynchronously = true, bool instantiate = true,
												Action<TId, TAsset, bool> onLoadCallback = null)
			where TAsset : Object;

		/// <inheritdoc cref="RequestAsset{TId,TAsset}"/>
		/// <remarks>
		/// 주어진 <paramref name="data"/> 를 <paramref name="onLoadCallback"/> 에 함께 넘깁니다
		/// </remarks>
		UniTask<TAsset> RequestAsset<TId, TAsset, TData>(TId id, TData data, bool loadAsynchronously = true,
														bool instantiate = true,
														Action<TId, TAsset, TData, bool> onLoadCallback = null)
			where TAsset : Object;

		/// <summary>
		/// 주어진 <paramref name="id"/> 에 매핑된 <see cref="Scene"/> 을 주어진 정보로 비동기 로드합니다.
		/// 로드가 끝나면 결과를 <paramref name="onLoadCallback"/> 으로 돌려줍니다
		/// </summary>
		UniTask<SceneInstance> LoadSceneAsync<TId>(TId id, LoadSceneMode loadMode = LoadSceneMode.Single,
											bool activateOnLoad = true,
											bool setActive = true, Action<TId, SceneInstance> onLoadCallback = null);


		/// <summary>
		/// <seealso cref="IAssetAdderService.AddConfigs{TId,TAsset}(AssetConfigsScriptableObject{TId,TAsset})"/> 로 추가한 에셋을 모두 로드합니다.
		/// <paramref name="loadAsynchronously"/> 로 비동기 로드를 고를 수 있고, 에셋을 하나 로드할 때마다
		/// <paramref name="onLoadCallback"/> 을 호출합니다.
		/// 메모리에 로드한 에셋 전체 목록을 반환합니다.
		/// </summary>
		/// <remarks>
		/// 메모리에서 다시 정리하려면 <see cref="UnloadAssets{TId,TAsset}(bool,AssetConfigsScriptableObject{TId,TAsset})"/> 를
		/// 호출해야 합니다
		/// </remarks>
		UniTask<List<Pair<TId, TAsset>>> LoadAllAssets<TId, TAsset>(bool loadAsynchronously = true,
																	Action<TId, TAsset> onLoadCallback = null);

		/// <summary>
		/// 주어진 <paramref name="id"/> 에 매핑된 <see cref="Scene"/> 을 비동기로 언로드합니다.
		/// 언로드가 끝나면 결과를 <paramref name="onUnloadCallback"/> 으로 돌려줍니다
		/// </summary>
		UniTask UnloadSceneAsync<TId>(TId id, Action<TId> onUnloadCallback = null);

		/// <summary>
		/// <typeparamref name="TId"/> 타입의 에셋 참조를 모두 언로드합니다.
		/// <paramref name="clearReferences"/> 가 true 이면 에셋 참조도 함께 제거합니다
		/// </summary>
		void UnloadAssets<TId, TAsset>(bool clearReferences);

		/// <summary>
		/// 주어진 <paramref name="assetConfigs"/> 에 있는 <typeparamref name="TId"/> 에셋 참조를 언로드합니다.
		/// <paramref name="clearReferences"/> 가 true 이면 에셋 참조도 함께 제거합니다
		/// </summary>
		void UnloadAssets<TId, TAsset>(bool clearReferences, AssetConfigsScriptableObject<TId, TAsset> assetConfigs);

		/// <summary>
		/// 주어진 <paramref name="ids"/> 에 해당하는 <typeparamref name="TId"/> 타입 에셋 참조를 언로드합니다.
		/// <paramref name="clearReferences"/> 가 true 이면 에셋 참조도 함께 제거합니다
		/// </summary>
		void UnloadAssets<TId, TAsset>(bool clearReferences, params TId[] ids);
	}

	/// <inheritdoc />
	/// <remarks>
	/// 서비스의 조회 동작과 추가 동작을 나누기 위해, 새 에셋 참조를 서비스에 추가하는 기능을 따로 제공합니다
	/// </remarks>
	public interface IAssetAdderService : IAssetResolverService
	{
		/// <summary>
		/// 주어진 <paramref name="configs"/> 를 <typeparamref name="TId"/> 식별자 타입,
		/// <typeparamref name="TAsset"/> 에셋 타입으로 에셋 참조 목록에 추가합니다
		/// </summary>
		void AddConfigs<TId, TAsset>(AssetConfigsScriptableObject<TId, TAsset> configs) => AddAssets(configs.AssetType, configs.Configs);

		/// <summary>
		/// 주어진 <paramref name="assets"/> 를 <typeparamref name="TId"/> 식별자 타입,
		/// <paramref name="assetType"/> 에셋 타입으로 에셋 참조 목록에 추가합니다
		/// </summary>
		/// <typeparam name="TId">식별자 타입</typeparam>
		/// <param name="assetType">에셋 타입</param>
		/// <param name="assets">추가할 에셋</param>
		void AddAssets<TId>(Type assetType, List<Pair<TId, AssetReference>> assets);

		/// <summary>
		/// 에셋 참조 하나를 <typeparamref name="TId"/> 식별자 타입,
		/// <paramref name="assetType"/> 에셋 타입으로 에셋 참조 목록에 추가합니다
		/// </summary>
		/// <typeparam name="TId">식별자 타입</typeparam>
		/// <param name="assetType">에셋 타입</param>
		/// <param name="id">에셋 식별자</param>
		/// <param name="assetReference">추가할 에셋 참조</param>
		void AddAsset<TId>(Type assetType, TId id, AssetReference assetReference);

		/// <summary>
		/// 오류가 났을 때 대신 쓸 디버그 에셋을 추가합니다
		/// </summary>
		void AddDebugConfigs(Sprite errorSprite = null, GameObject errorCube = null, Material errorMaterial = null, 
			AudioClip errorClip = null);
	}

	/// <inheritdoc cref="IAssetResolverService" />
	public class AssetResolverService : AddressablesAssetLoader, IAssetAdderService
	{
		private readonly IDictionary<Type, IDictionary<Type, IDictionary>> _assetMap =
			new Dictionary<Type, IDictionary<Type, IDictionary>>();

		internal IReadOnlyDictionary<Type, IDictionary<Type, IDictionary>> AssetMap =>
			(IReadOnlyDictionary<Type, IDictionary<Type, IDictionary>>)_assetMap;

		private Sprite _errorSprite;
		private GameObject _errorCube;
		private Material _errorMaterial;
		private AudioClip _errorClip;

		/// <inheritdoc />
		public async UniTask<SceneInstance> LoadSceneAsync<TId>(TId id, LoadSceneMode loadMode = LoadSceneMode.Single,
														bool activateOnLoad = true, bool setActive = true,
														Action<TId, SceneInstance> onLoadCallback = null)
		{
			if (!TryGetAssetReference<TId, Scene>(id, out var assetReference))
			{
				throw new MissingMemberException($"The {nameof(AssetResolverService)} does not have the scene config " +
												 $"to load with the given {id} id of {typeof(TId)} type. " +
												 $"Use the {nameof(AddAssets)} method to add it.");
			}

			if (!assetReference.OperationHandle.IsValid())
			{
				_ = assetReference.LoadSceneAsync(loadMode, activateOnLoad);
			}

			if (!assetReference.IsDone)
			{
				await assetReference.OperationHandle.ToUniTask();
			}

			var sceneOperation = assetReference.OperationHandle.Convert<SceneInstance>();

			if (setActive)
			{
				SceneManager.SetActiveScene(sceneOperation.Result.Scene);
			}

			onLoadCallback?.Invoke(id, sceneOperation.Result);

			return sceneOperation.Result;
		}

		/// <inheritdoc />
		public async UniTask<List<Pair<TId, TAsset>>> LoadAllAssets<TId, TAsset>(bool loadAsynchronously = true,
																			  Action<TId, TAsset> onLoadCallback = null)
		{
			var list = new List<Pair<TId, TAsset>>();
			var tasks = new List<UniTask>();

			if (!TryGetDictionary<TId, TAsset>(out var dictionary))
			{
				throw new MissingMemberException($"The {nameof(AssetResolverService)} does not have the '{typeof(TAsset)}' " +
												 $"asset list for the '{typeof(TId)}' type to load. " +
												 $"Use the {nameof(AddAssets)} method to add it.");
			}

			foreach (var pair in dictionary)
			{
				var operation = pair.Value.LoadAssetAsync<TAsset>();
				var id = pair.Key;
				var task = operation.ToUniTask().ContinueWith(result =>
				{
					list.Add(new Pair<TId, TAsset>(id, result));
					onLoadCallback?.Invoke(id, result);
				});

				tasks.Add(task);

				if (!loadAsynchronously)
				{
					operation.WaitForCompletion();
				}
			}

			await UniTask.WhenAll(tasks);

			/* Keep the code in case some asset types don't work with the code above
			foreach (var pair in dictionary)
			{
				list.Add(new Pair<TId, TAsset>(pair.Key, pair.Value.OperationHandle.Convert<TAsset>().Result));
			}*/

			return list;
		}

		/// <inheritdoc />
		public async UniTask<TAsset> RequestAsset<TId, TAsset>(TId id, bool loadAsynchronously = true,
															bool instantiate = true,
															Action<TId, TAsset, bool> onLoadCallback = null)
			where TAsset : Object
		{
			return await RequestAsset<TId, TAsset, int>(id, 0, loadAsynchronously, instantiate,
														(arg1, arg2, _, arg4) => onLoadCallback?.Invoke(arg1, arg2, arg4));
		}

		/// <inheritdoc />
		public async UniTask<TAsset> RequestAsset<TId, TAsset, TData>(TId id, TData data, bool loadAsynchronously = true,
																   bool instantiate = true,
																   Action<TId, TAsset, TData, bool> onLoadCallback = null)
			where TAsset : Object
		{
			if (!TryGetAssetReference<TId, TAsset>(id, out var assetReference))
			{
				throw new MissingMemberException($"The {nameof(AssetResolverService)} does not have the " +
												 $"{nameof(AssetReference)} config to load the necessary asset for the " +
												 $"given {typeof(TAsset)} type with the given {id} id of {typeof(TId)} type");
			}

			if (!assetReference.OperationHandle.IsValid())
			{
				_ = assetReference.LoadAssetAsync<TAsset>();
			}

			if (!loadAsynchronously)
			{
				assetReference.OperationHandle.WaitForCompletion();
			}

			if (!assetReference.IsDone)
			{
				await assetReference.OperationHandle.ToUniTask();
			}

			var asset = Convert<TAsset>(assetReference, instantiate);

			if (asset == null || assetReference.Asset == null)
			{
				Debug.LogWarning($"The given id '{id.ToString()}' is loading an empty asset '{typeof(TAsset)}' type");
			}

			onLoadCallback?.Invoke(id, asset, data, instantiate);

			return asset;
		}

		/// <inheritdoc />
		public async UniTask UnloadSceneAsync<TId>(TId id, Action<TId> onUnloadCallback = null)
		{
			if (!TryGetAssetReference<TId, Scene>(id, out var assetReference))
			{
				Debug.LogWarning($"The {nameof(AssetResolverService)} does not have the config to unload the scene with the given {id} " +
					$"id of {typeof(TId)} type. Use the {nameof(AddAssets)} method to add it.");
				return;
			}

			await assetReference.UnLoadScene().ToUniTask();

			onUnloadCallback?.Invoke(id);
		}

		/// <inheritdoc />
		public void UnloadAssets<TId, TAsset>(bool clearReferences)
		{
			if (!TryGetDictionary<TId, TAsset>(out var dictionary))
			{
				Debug.LogWarning($"The {nameof(AssetResolverService)} does not have the '{typeof(TAsset)}' " +
					$"asset list for the '{typeof(TId)}' type. Use the {nameof(AddAssets)} method to add it.");
				return;
			}

			foreach (var asset in dictionary)
			{
				if (asset.Value.IsValid())
				{
					asset.Value.ReleaseAsset();
				}
			}

			if (clearReferences)
			{
				_assetMap[typeof(TAsset)].Remove(typeof(TId));
			}
		}

		/// <inheritdoc />
		public void UnloadAssets<TId, TAsset>(bool clearReferences,
											  AssetConfigsScriptableObject<TId, TAsset> assetConfigs)
		{
			if (!TryGetDictionary<TId, TAsset>(out var dictionary))
			{
				Debug.LogWarning($"The {nameof(AssetResolverService)} does not have the '{typeof(TAsset)}' " +
					$"asset list for the '{typeof(TId)}' type. Use the {nameof(AddAssets)} method to add it.");
				return;
			}

			foreach (var pair in assetConfigs.Configs)
			{
				if (!dictionary.TryGetValue(pair.Key, out var asset))
				{
					continue;
				}

				if (asset.IsValid())
				{
					asset.ReleaseAsset();
				}

				if (clearReferences)
				{
					dictionary.Remove(pair.Key);
				}
			}
		}

		/// <inheritdoc />
		public void UnloadAssets<TId, TAsset>(bool clearReferences, params TId[] ids)
		{
			if(!TryGetDictionary<TId, TAsset>(out var dictionary))
			{
				Debug.LogWarning($"The {nameof(AssetResolverService)} does not have the '{typeof(TAsset)}' " +
					$"asset list for the '{typeof(TId)}' type. Use the {nameof(AddAssets)} method to add it.");
				return;
			}

			foreach (var id in ids)
			{
				if (!dictionary.TryGetValue(id, out var asset))
				{
					continue;
				}

				if (asset.IsValid())
				{
					asset.ReleaseAsset();
				}

				if (clearReferences)
				{
					dictionary.Remove(id);
				}
			}
		}

		/// <inheritdoc />
		public void AddAssets<TId>(Type assetType, List<Pair<TId, AssetReference>> assets)
		{
			var idType = typeof(TId);
			var assetReferences = new Dictionary<TId, AssetReference>(assets.Count);

			for (var i = 0; i < assets.Count; i++)
			{
				assetReferences.Add(assets[i].Key, assets[i].Value);
			}

			if (!_assetMap.TryGetValue(assetType, out var map))
			{
				map = new Dictionary<Type, IDictionary>();

				_assetMap.Add(assetType, map);
			}

			if (map.TryGetValue(idType, out var assetMap))
			{
				var dictionary = assetMap as Dictionary<TId, AssetReference>;

				// ReSharper disable once PossibleNullReferenceException
				foreach (var asset in dictionary)
				{
					assetReferences.Add(asset.Key, asset.Value);
				}

				map[idType] = assetReferences;
			}
			else
			{
				map.Add(idType, assetReferences);
			}
		}

		/// <inheritdoc />
		public void AddAsset<TId>(Type assetType, TId id, AssetReference assetReference)
		{
			AddAssets(assetType, new List<Pair<TId, AssetReference>> { new Pair<TId, AssetReference>(id, assetReference) });
		}

		/// <inheritdoc />
		public void AddDebugConfigs(Sprite errorSprite = null, GameObject errorCube = null, Material errorMaterial = null, 
									AudioClip errorClip = null)
		{
			_errorSprite = errorSprite;
			_errorCube = errorCube;
			_errorMaterial = errorMaterial;
			_errorClip = errorClip;
		}

		private TAsset Convert<TAsset>(AssetReference assetReference, bool instantiate)
			where TAsset : Object
		{
			var type = typeof(TAsset);

			/* AssetReference types
			
				GameObject
				ScriptableObject
				Texture
				Texture3D
				Texture2D
				RenderTexture
				CustomRenderTexture
				CubeMap
				Material
				PhysicMaterial
				PhysicMaterial2D
				Sprite
				SpriteAtlas
				VideoClip
				AudioClip
				AudioMixer
				Avatar
				AnimatorController
				AnimatorOverrideController
				TextAsset
				Mesh
				Shader
				ComputeShader
				Flare
				NavMeshData
				TerrainData
				TerrainLayer
				Font
				Scene
				GUISkin
			 * */

			if (assetReference.Asset == null)
			{
				return null;
			}

			if (type == typeof(GameObject))
			{
				var asset = !assetReference.IsDone ? _errorCube : assetReference.Asset as GameObject;

				return instantiate ? Object.Instantiate(asset) as TAsset : asset as TAsset;
			}

			if (type == typeof(Sprite))
			{
				return !assetReference.IsDone ? _errorSprite as TAsset : assetReference.Asset as TAsset;
			}

			if (type == typeof(Material))
			{
				var asset = !assetReference.IsDone ? _errorMaterial : assetReference.Asset as Material;

				return instantiate ? new Material(asset) as TAsset : asset as TAsset;
			}

			if (type == typeof(AudioClip))
			{
				return !assetReference.IsDone ? _errorClip as TAsset : assetReference.Asset as TAsset;
			}

			return assetReference.Asset as TAsset;
		}

		private bool TryGetDictionary<TId, TAsset>(out Dictionary<TId, AssetReference> dictionary)
		{
			dictionary = null;

			if (!_assetMap.TryGetValue(typeof(TAsset), out var idMap))
			{
				return false;
			}

			if (!idMap.TryGetValue(typeof(TId), out var assetMap))
			{
				return false;
			}

			dictionary = assetMap as Dictionary<TId, AssetReference>;

			return true;
		}

		private bool TryGetAssetReference<TId, TAsset>(TId id, out AssetReference assetReference)
		{
			if (!TryGetDictionary<TId, TAsset>(out var dictionary))
			{
				assetReference = default;

				return false;
			}

			return dictionary.TryGetValue(id, out assetReference);
		}
	}
}