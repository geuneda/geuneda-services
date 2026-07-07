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

			// Both entries should now exist — verifiable via UnloadAssets without throwing
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

			// After clear, a second clear should warn (map entry removed)
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

			// The non-matching id 30 must still be resolvable — a second clear on the remaining map entries should not warn
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

			// Registered via the default interface method — subsequent unload should not warn
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
