using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

// ReSharper disable CheckNamespace

namespace Geuneda.Services.AssetsImporter
{
	/// <summary>
	/// 에셋 로딩 방식을 객체 참조로 감쌀 수 있게 하는 인터페이스입니다
	/// </summary>
	public interface IAssetLoader
	{
		/// <summary>
		/// 주어진 <paramref name="key"/> 에 있는 <typeparamref name="T"/> 타입의 에셋을 로드합니다.
		/// 에셋 경로는 <seealso cref="AddressableConfig"/> 에서 가져오는 것을 권장합니다.
		/// 에셋이 로드되면 <paramref name="onCompleteCallback"/> 을 호출합니다.
		/// async 메서드에서 대기할 수 있으며 로드된 에셋을 반환합니다.
		/// </summary>
		UniTask<T> LoadAssetAsync<T>(object key, Action<T> onCompleteCallback = null);

		/// <summary>
		/// 주어진 <paramref name="key"/> 의 프리팹을 로드해 주어진 <paramref name="parent"/> 아래에 인스턴스화합니다.
		/// <paramref name="instantiateInWorldSpace"/> 로 인스턴스 트랜스폼을 월드 기준으로 유지할지, 부모 기준으로 둘지 정합니다.
		/// 에셋 경로는 <seealso cref="AddressableConfig"/> 에서 가져오는 것을 권장합니다.
		/// 에셋이 인스턴스화되면 <paramref name="onCompleteCallback"/> 을 호출합니다.
		/// async 메서드에서 대기할 수 있으며 인스턴스화된 프리팹을 반환합니다
		/// </summary>
		UniTask<GameObject> InstantiateAsync(object key, Transform parent, bool instantiateInWorldSpace,
			Action<GameObject> onCompleteCallback = null);

		/// <summary>
		/// 주어진 <paramref name="key"/> 의 프리팹을 로드해 주어진 <paramref name="position"/>,
		/// <paramref name="rotation"/>, <paramref name="parent"/> 로 인스턴스화합니다.
		/// 에셋 경로는 <seealso cref="AddressableConfig"/> 에서 가져오는 것을 권장합니다.
		/// 에셋이 인스턴스화되면 <paramref name="onCompleteCallback"/> 을 호출합니다.
		/// async 메서드에서 대기할 수 있으며 인스턴스화된 프리팹을 반환합니다
		/// </summary>
		UniTask<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent,
			Action<GameObject> onCompleteCallback = null);

		/// <summary>
		/// <paramref name="asset"/> 으로 추적되는 Addressables 작업 핸들을 해제해 참조 카운트를 줄입니다(<c>Addressables.Release</c>).
		/// 동기 호출이며, 참조 카운트가 0 이 된 뒤 메모리가 언제 회수되는지는 Addressables 의 해제 처리에 따릅니다.
		/// 이 메서드는 <c>Resources.UnloadUnusedAssets</c> 를 호출하지 않으므로, 필요하면 씬 전환, 부팅, 메모리 경고 시점에
		/// 직접 호출하세요. Unity 는 <c>LoadSceneMode.Single</c> 씬 로드 때 이 정리를 자동으로 수행합니다.
		/// <see cref="InstantiateAsync(object, Transform, bool, Action{GameObject})"/> 로 만든 인스턴스를 넘기면 그 인스턴스의
		/// 작업 핸들이 해제되며, Addressables 소스(2.9.1, <c>InstanceProvider.ReleaseInstance</c>) 기준 이때
		/// <seealso cref="GameObject"/> 도 파괴됩니다.
		/// 반환 직전에 <paramref name="onCompleteCallback"/> 을 바로 호출합니다.
		/// </summary>
		void UnloadAsset<T>(T asset, Action onCompleteCallback = null);
	}
}
