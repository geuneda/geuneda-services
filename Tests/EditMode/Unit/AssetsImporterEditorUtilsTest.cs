using Geuneda.Services.AssetsImporter.Editor;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class AssetsImporterEditorUtilsTest
	{
		[Test]
		public void TryGetScriptableObject_NullType_ReturnsFalse()
		{
			// 설정 타입이 아직 생성되지 않은 생성기 임포터는 ScriptableObjectType 이 null 이다
			Assert.IsFalse(AssetsImporterEditorUtils.TryGetScriptableObject(null, out var scriptableObject));
			Assert.IsNull(scriptableObject);
		}

		[Test]
		public void TryGetScriptableObject_NonAssetConfigsType_ReturnsFalse()
		{
			Assert.IsFalse(AssetsImporterEditorUtils.TryGetScriptableObject(typeof(ScriptableObject), out var scriptableObject));
			Assert.IsNull(scriptableObject);
		}
	}
}
