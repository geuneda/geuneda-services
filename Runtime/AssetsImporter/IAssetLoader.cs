using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

// ReSharper disable CheckNamespace

namespace Geuneda.Services.AssetsImporter
{
	/// <summary>
	/// This interface allows to wrap the asset loading scheme into an object reference
	/// </summary>
	public interface IAssetLoader
	{
		/// <summary>
		/// Loads any asset of the given <typeparamref name="T"/> in the given <paramref name="key"/>.
		/// To help the execution of this method is recommended to request the asset path from an <seealso cref="AddressableConfig"/>.
		/// Invokes <paramref name="onCompleteCallback"/> when the asset is loaded.
		/// This method can be controlled in an async method and returns the asset loaded.
		/// </summary>
		UniTask<T> LoadAssetAsync<T>(object key, Action<T> onCompleteCallback = null);

		/// <summary>
		/// Loads and instantiates the prefab in the given <paramref name="key"/> with the given <paramref name="parent"/>
		/// and the given <paramref name="instantiateInWorldSpace"/> to preserve the instance transform relative to world
		/// space or relative to the parent.
		/// To help the execution of this method is recommended to request the asset path from an <seealso cref="AddressableConfig"/>.
		/// Invokes <paramref name="onCompleteCallback"/> when the asset is instantiated.
		/// This method can be controlled in an async method and returns the prefab instantiated
		/// </summary>
		UniTask<GameObject> InstantiateAsync(object key, Transform parent, bool instantiateInWorldSpace, 
			Action<GameObject> onCompleteCallback = null);

		/// <summary>
		/// Loads and instantiates the prefab in the given <paramref name="key"/> with the given <paramref name="position"/>,
		/// the given <paramref name="rotation"/> & the given <paramref name="parent"/>.
		/// To help the execution of this method is recommended to request the asset path from an <seealso cref="AddressableConfig"/>.
		/// Invokes <paramref name="onCompleteCallback"/> when the asset is instantiated.
		/// This method can be controlled in an async method and returns the prefab instantiated
		/// </summary>
		UniTask<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent, 
			Action<GameObject> onCompleteCallback = null);

		/// <summary>
		/// Releases the Addressables reference for <paramref name="asset"/>, decrementing its handle reference count.
		/// This call is synchronous — memory is not freed immediately. Invoke <c>Resources.UnloadUnusedAssets</c>
		/// at scene transitions, boot, or memory-pressure events to reclaim memory. Unity also runs this sweep
		/// automatically on <c>LoadSceneMode.Single</c> scene loads.
		/// For prefab instances returned by <see cref="InstantiateAsync(object, Transform, bool, Action{GameObject})"/>,
		/// this does not destroy the <seealso cref="GameObject"/>; call <c>Object.Destroy</c> separately if required.
		/// Invokes <paramref name="onCompleteCallback"/> immediately before returning.
		/// </summary>
		void UnloadAsset<T>(T asset, Action onCompleteCallback = null);
	}
}