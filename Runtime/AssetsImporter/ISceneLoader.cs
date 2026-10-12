using Cysharp.Threading.Tasks;
using System;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

// ReSharper disable CheckNamespace

namespace Geuneda.Services.AssetsImporter
{
	/// <summary>
	/// 씬 로딩 방식을 객체 참조로 감쌀 수 있게 하는 인터페이스입니다
	/// </summary>
	public interface ISceneLoader
	{
		/// <summary>
		/// 주어진 <paramref name="path"/> 의 씬을 주어진 설정으로 로드합니다.
		/// 씬 경로는 <seealso cref="AddressableConfig"/> 에서 가져오는 것을 권장합니다.
		/// 씬이 로드되면 <paramref name="onCompleteCallback"/> 을 호출합니다.
		/// async 메서드에서 대기할 수 있으며 로드된 씬을 반환합니다
		/// </summary>
		UniTask<Scene> LoadSceneAsync(string path, LoadSceneMode loadMode = LoadSceneMode.Single,
			bool activateOnLoad = true, Action<Scene> onCompleteCallback = null);

		/// <summary>
		/// 주어진 <paramref name="scene"/> 을 게임 메모리에서 언로드합니다.
		/// 씬이 언로드되면 <paramref name="onCompleteCallback"/> 을 호출합니다.
		/// async 메서드에서 대기할 수 있습니다
		/// </summary>
		UniTask UnloadSceneAsync(Scene scene, Action onCompleteCallback = null);
	}
}
