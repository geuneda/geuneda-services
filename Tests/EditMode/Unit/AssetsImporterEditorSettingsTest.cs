using Geuneda.Services.AssetsImporter.Editor;
using NUnit.Framework;
using UnityEditor;

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

		[Test]
		public void TryScheduleAutoImport_WhenEnabled_SchedulesImportAll()
		{
			AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh = true;

			try
			{
				Assert.IsTrue(AssetsImporterEditorUtils.TryScheduleAutoImport());
			}
			finally
			{
				// 테스트 프로젝트에서 실제 임포트가 실행되지 않도록 예약을 취소합니다
				EditorApplication.delayCall -= AssetsImporterEditorUtils.ImportAll;
			}
		}

		[Test]
		public void TryScheduleAutoImport_WhenDisabled_DoesNotSchedule()
		{
			AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh = false;

			Assert.IsFalse(AssetsImporterEditorUtils.TryScheduleAutoImport());
		}
	}
}
