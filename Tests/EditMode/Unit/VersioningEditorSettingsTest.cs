using Geuneda.Services.Versioning.Editor;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class VersioningEditorSettingsTest
	{
		[Test]
		public void IsValidResourcesPath_EmptyString_ReturnsFalse()
		{
			Assert.IsFalse(VersioningEditorSettings.IsValidResourcesPath("", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidResourcesPath_NoAssetsPrefix_ReturnsFalse()
		{
			Assert.IsFalse(VersioningEditorSettings.IsValidResourcesPath("Configs/Resources", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidResourcesPath_DotDotSegment_ReturnsFalse()
		{
			Assert.IsFalse(VersioningEditorSettings.IsValidResourcesPath("Assets/../Resources", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidResourcesPath_NoResourcesSegment_ReturnsFalse()
		{
			Assert.IsFalse(VersioningEditorSettings.IsValidResourcesPath("Assets/Configs/Data", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidResourcesPath_ValidDefaultPath_ReturnsTrue()
		{
			Assert.IsTrue(VersioningEditorSettings.IsValidResourcesPath(
				VersioningEditorSettings.DefaultFolderPath, out var error));
			Assert.IsNull(error);
		}
	}
}
