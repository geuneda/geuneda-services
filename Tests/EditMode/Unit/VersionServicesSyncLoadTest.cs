using System.Reflection;
using Geuneda.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	/// <summary>
	/// EditMode unit coverage for <see cref="VersionServices.LoadVersionData"/> (synchronous).
	/// The async sibling is covered in PlayMode/Integration/VersionServicesIntegrationTest.cs;
	/// the sync path needs no Unity runtime, so it lives here. Requires
	/// Assets/Configs/Resources/version-data.txt to exist in the host project (written
	/// automatically by VersionEditorUtils on every domain reload).
	/// </summary>
	[TestFixture]
	public class VersionServicesSyncLoadTest
	{
		private static readonly FieldInfo LoadedField =
			typeof(VersionServices).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static);

		[SetUp]
		public void ResetStaticState()
		{
			LoadedField.SetValue(null, false);
		}

		[Test]
		public void AccessBeforeLoad_AutoLoads()
		{
			Assert.IsFalse((bool)LoadedField.GetValue(null), "Precondition: SetUp resets _loaded to false");

			Assert.DoesNotThrow(() => { var _ = VersionServices.VersionInternal; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.Branch; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.Commit; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.BuildNumber; });

			Assert.IsTrue((bool)LoadedField.GetValue(null), "Accessor should auto-trigger LoadVersionData via EnsureLoaded");
		}

		[Test]
		public void LoadVersionData_Successfully_FlipsLoadedFlag()
		{
			VersionServices.LoadVersionData();

			Assert.IsTrue((bool)LoadedField.GetValue(null), "Version data should be loaded after sync call");
		}

		[Test]
		public void LoadVersionData_DoesNotThrow()
		{
			Assert.DoesNotThrow(() => VersionServices.LoadVersionData());
		}

		[Test]
		public void AfterLoad_VersionInternal_ContainsExpectedParts()
		{
			VersionServices.LoadVersionData();

			var version = VersionServices.VersionInternal;

			Assert.IsNotNull(version);
			Assert.IsNotEmpty(version);
			Assert.IsTrue(version.Contains("."), "VersionInternal should contain version separators");
		}

		[Test]
		public void AfterLoad_Branch_ReturnsNonEmptyString()
		{
			VersionServices.LoadVersionData();

			var branch = VersionServices.Branch;

			Assert.IsNotNull(branch);
			Assert.IsNotEmpty(branch);
		}

		[Test]
		public void AfterLoad_Commit_ReturnsNonEmptyString()
		{
			VersionServices.LoadVersionData();

			var commit = VersionServices.Commit;

			Assert.IsNotNull(commit);
			Assert.IsNotEmpty(commit);
		}

		[Test]
		public void AfterLoad_BuildNumber_ReturnsNonEmptyString()
		{
			VersionServices.LoadVersionData();

			var buildNumber = VersionServices.BuildNumber;

			Assert.IsNotNull(buildNumber);
			Assert.IsNotEmpty(buildNumber);
		}

		[Test]
		public void VersionExternal_AlwaysAccessible_WithoutLoad()
		{
			var external = VersionServices.VersionExternal;

			Assert.IsNotNull(external);
			Assert.IsNotEmpty(external);
		}
	}
}
