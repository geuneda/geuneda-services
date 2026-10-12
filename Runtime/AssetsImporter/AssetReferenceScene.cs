using UnityEngine;
using UnityEngine.AddressableAssets;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services.AssetsImporter
{
	/// <summary>
	/// 씬용 <see cref="AssetReference"/> 구현
	/// </summary>
	[System.Serializable]
	public class AssetReferenceScene : AssetReference
	{
		/// <summary>
		/// 새 AssetReference 객체를 생성합니다.
		/// </summary>
		/// <param name="guid">에셋의 guid.</param>
		public AssetReferenceScene(string guid) : base(guid)
		{
		}
 
		/// <inheritdoc/>
		public override bool ValidateAsset(Object obj)
		{
#if UNITY_EDITOR
			var type = obj.GetType();
			return typeof(UnityEditor.SceneAsset).IsAssignableFrom(type);
#else
			return false;
#endif
		}
 
		/// <inheritdoc/>
		public override bool ValidateAsset(string path)
		{
#if UNITY_EDITOR
			var type = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path);
			return typeof(UnityEditor.SceneAsset).IsAssignableFrom(type);
#else
			return false;
#endif
		}
 
#if UNITY_EDITOR
		/// <summary>
		/// 부모 editorAsset 의 타입 특화 재정의입니다. 에디터에서 참조된 에셋을 표시할 때 사용합니다.
		/// </summary>
		public new UnityEditor.SceneAsset editorAsset => (UnityEditor.SceneAsset)base.editorAsset;
#endif
	}
}