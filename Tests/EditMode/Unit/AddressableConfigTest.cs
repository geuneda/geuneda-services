using NUnit.Framework;
using Geuneda.Services.AssetsImporter;
using UnityEngine.SceneManagement;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class AddressableConfigTest
	{
		private AddressableConfig _sceneConfig;
		private AddressableConfig _spriteConfig;

		[SetUp]
		public void Init()
		{
			_sceneConfig = new AddressableConfig(0, "Scenes/MainMenu.unity", "Assets/Scenes/MainMenu.unity",
				typeof(Scene), new[] { "scenes" });
			_spriteConfig = new AddressableConfig(1, "Sprites/hero", "Assets/Sprites/hero.png",
				typeof(UnityEngine.Sprite), new string[0]);
		}

		[Test]
		public void GetSceneName_WithSceneAssetType_ReturnsName()
		{
			Assert.AreEqual("MainMenu", _sceneConfig.GetSceneName());
		}

		[Test]
		public void GetSceneName_WithNonSceneAssetType_Throws()
		{
			Assert.Throws<System.InvalidOperationException>(() => _spriteConfig.GetSceneName());
		}

		[Test]
		public void AddressableConfigComparer_EqualIds_ReturnsTrue()
		{
			var comparer = new AddressableConfigComparer();
			var other = new AddressableConfig(0, "Other", "Assets/Other", typeof(Scene), new string[0]);

			Assert.IsTrue(comparer.Equals(_sceneConfig, other));
		}

		[Test]
		public void AddressableConfigComparer_GetHashCode_ReturnsId()
		{
			var comparer = new AddressableConfigComparer();

			Assert.AreEqual(0, comparer.GetHashCode(_sceneConfig));
			Assert.AreEqual(1, comparer.GetHashCode(_spriteConfig));
		}

		[Test]
		public void GetSceneName_WithAddressWithoutSlash_ReturnsFullAddress()
		{
			var rootSceneConfig = new AddressableConfig(2, "MainMenu.unity", "Assets/MainMenu.unity",
				typeof(Scene), new string[0]);

			Assert.AreEqual("MainMenu", rootSceneConfig.GetSceneName());
		}

		[Test]
		public void GetSceneName_WithAddressWithoutExtension_ReturnsFullAddress()
		{
			var noExtensionSceneConfig = new AddressableConfig(3, "Scenes/MyScene", "Assets/Scenes/MyScene",
				typeof(Scene), new string[0]);

			Assert.AreEqual("MyScene", noExtensionSceneConfig.GetSceneName());
		}
	}
}
