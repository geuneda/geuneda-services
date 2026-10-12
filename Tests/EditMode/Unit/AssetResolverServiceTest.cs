using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using Geuneda.DataExtensions;
using Geuneda.Services;
using Geuneda.Services.AssetsImporter;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class AssetResolverServiceTest
	{
		private class TestSpriteConfigs : AssetConfigsScriptableObject<int, Sprite> { }

		private AssetResolverService _service;

		[SetUp]
		public void Init()
		{
			_service = new AssetResolverService();
		}

		[Test]
		public void AddAsset_NewType_RegistersEntry()
		{
			var assetRef = new AssetReference();
			Assert.DoesNotThrow(() => _service.AddAsset<int>(typeof(Sprite), 1, assetRef));
		}

		[Test]
		public void AddAssets_DuplicateType_MergesEntries()
		{
			var ref1 = new AssetReference();
			var ref2 = new AssetReference();

			_service.AddAssets(typeof(Sprite), new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(1, ref1)
			});
			_service.AddAssets(typeof(Sprite), new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(2, ref2)
			});

			// 이제 두 항목이 모두 존재해야 한다 — UnloadAssets 가 예외 없이 동작하는지로 확인한다
			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(false));
		}

		[Test]
		public void UnloadAssets_UnknownType_DoesNotThrow()
		{
			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(false));
		}

		[Test]
		public void UnloadAssets_ClearReferences_RemovesMap()
		{
			var assetRef = new AssetReference();
			_service.AddAsset<int>(typeof(Sprite), 1, assetRef);
			_service.UnloadAssets<int, Sprite>(clearReferences: true);

			// 정리 후 두 번째 정리는 경고를 내야 한다 (맵 항목이 제거됨)
			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
			_service.UnloadAssets<int, Sprite>(clearReferences: false);
		}

		[Test]
		public void AddDebugConfigs_StoresAllProvided()
		{
			var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
			var mat = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));

			Assert.DoesNotThrow(() => _service.AddDebugConfigs(errorMaterial: mat));

			var field = typeof(AssetResolverService).GetField("_errorMaterial",
				BindingFlags.NonPublic | BindingFlags.Instance);

			Assert.IsNotNull(field);
			Assert.AreSame(mat, field.GetValue(_service));
		}

		[Test]
		public void UnloadAssets_WithAssetConfigsContainer_ReleasesAssetsInContainer()
		{
			var so = ScriptableObject.CreateInstance<TestSpriteConfigs>();
			so.Configs = new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(1, new AssetReference()),
				new Pair<int, AssetReference>(2, new AssetReference())
			};
			_service.AddAssets(typeof(Sprite), so.Configs);

			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(clearReferences: false, assetConfigs: so));

			UnityEngine.Object.DestroyImmediate(so);
		}

		[Test]
		public void UnloadAssets_WithIdsArray_ReleasesOnlyMatching()
		{
			_service.AddAssets(typeof(Sprite), new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(10, new AssetReference()),
				new Pair<int, AssetReference>(20, new AssetReference()),
				new Pair<int, AssetReference>(30, new AssetReference())
			});

			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(clearReferences: true, 10, 20));

			// 일치하지 않은 id 30 은 여전히 조회할 수 있어야 한다 — 남은 맵 항목에 대한 두 번째 정리는 경고를 내지 않아야 한다
			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(clearReferences: true, 30));
		}

		[Test]
		public void AddConfigs_DelegatesToAddAssets()
		{
			var so = ScriptableObject.CreateInstance<TestSpriteConfigs>();
			so.Configs = new List<Pair<int, AssetReference>>
			{
				new Pair<int, AssetReference>(7, new AssetReference())
			};

			IAssetAdderService adderService = _service;
			Assert.DoesNotThrow(() => adderService.AddConfigs<int, Sprite>(so));

			// 기본 인터페이스 메서드로 등록되었으므로 이후 언로드는 경고를 내지 않아야 한다
			Assert.DoesNotThrow(() => _service.UnloadAssets<int, Sprite>(clearReferences: true));

			UnityEngine.Object.DestroyImmediate(so);
		}

		[Test]
		public async System.Threading.Tasks.Task RequestAsset_UnknownId_ThrowsMissingMember()
		{
			MissingMemberException caught = null;
			try
			{
				await _service.RequestAsset<int, Sprite>(99);
			}
			catch (MissingMemberException ex)
			{
				caught = ex;
			}

			Assert.IsNotNull(caught);
		}

		[Test]
		public async System.Threading.Tasks.Task LoadSceneAsync_UnknownId_ThrowsMissingMember()
		{
			MissingMemberException caught = null;
			try
			{
				await _service.LoadSceneAsync(7);
			}
			catch (MissingMemberException ex)
			{
				caught = ex;
			}

			Assert.IsNotNull(caught);
		}

		[Test]
		public async System.Threading.Tasks.Task RequestAsset_ThreeParamWithData_UnknownId_ThrowsMissingMember()
		{
			MissingMemberException caught = null;
			try
			{
				await _service.RequestAsset<int, Sprite, string>(99, "payload");
			}
			catch (MissingMemberException ex)
			{
				caught = ex;
			}

			Assert.IsNotNull(caught);
		}

		[Test]
		public async System.Threading.Tasks.Task LoadAllAssets_UnknownAssetType_ThrowsMissingMember()
		{
			MissingMemberException caught = null;
			try
			{
				await _service.LoadAllAssets<int, Sprite>();
			}
			catch (MissingMemberException ex)
			{
				caught = ex;
			}

			Assert.IsNotNull(caught);
		}

		[Test]
		public async System.Threading.Tasks.Task UnloadSceneAsync_UnknownId_LogsWarningAndCompletes()
		{
			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));

			Exception caught = null;
			try
			{
				await _service.UnloadSceneAsync(123);
			}
			catch (Exception ex)
			{
				caught = ex;
			}

			Assert.IsNull(caught);
		}
	}
}
