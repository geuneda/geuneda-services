using Geuneda.Services.AssetsImporter.Editor;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class AssetsImporterEditorSettingsTest
	{
		private bool _originalValue;

		[SetUp]
		public void Init()
		{
			_originalValue = AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh;
		}

		[TearDown]
		public void Cleanup()
		{
			AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh = _originalValue;
		}

		[Test]
		public void AutoUpdateOnRefresh_SetterRoundTrips_PreservesValue()
		{
			AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh = true;
			Assert.IsTrue(AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh);

			AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh = false;
			Assert.IsFalse(AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh);
		}
	}
}
