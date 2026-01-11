using GameLovers.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class VersionServicesTest
	{
		/// <summary>
		/// Testable version comparison logic extracted from VersionServices.IsOutdatedVersion.
		/// Since IsOutdatedVersion uses Application.version (read-only in EditMode),
		/// we extract the comparison logic here to enable unit testing.
		/// </summary>
		private static bool IsOutdatedVersionTestable(string appVersion, string otherVersion)
		{
			var appVersionParts = appVersion.Split('.');
			var otherVersionParts = otherVersion.Split('.');

			var majorApp = int.Parse(appVersionParts[0]);
			var majorOther = int.Parse(otherVersionParts[0]);

			var minorApp = int.Parse(appVersionParts[1]);
			var minorOther = int.Parse(otherVersionParts[1]);

			var patchApp = int.Parse(appVersionParts[2]);
			var patchOther = int.Parse(otherVersionParts[2]);

			if (majorApp != majorOther)
			{
				return majorOther > majorApp;
			}

			if (minorApp != minorOther)
			{
				return minorOther > minorApp;
			}

			return patchOther > patchApp;
		}

		[Test]
		public void IsOutdatedVersion_NewerMajor_ReturnsTrue()
		{
			Assert.That(IsOutdatedVersionTestable("1.0.0", "2.0.0"), Is.True);
		}

		[Test]
		public void IsOutdatedVersion_NewerMinor_ReturnsTrue()
		{
			Assert.That(IsOutdatedVersionTestable("1.1.0", "1.2.0"), Is.True);
		}

		[Test]
		public void IsOutdatedVersion_NewerPatch_ReturnsTrue()
		{
			Assert.That(IsOutdatedVersionTestable("1.1.1", "1.1.2"), Is.True);
		}

		[Test]
		public void IsOutdatedVersion_SameVersion_ReturnsFalse()
		{
			Assert.That(IsOutdatedVersionTestable("1.1.1", "1.1.1"), Is.False);
		}

		[Test]
		public void IsOutdatedVersion_OlderVersion_ReturnsFalse()
		{
			Assert.That(IsOutdatedVersionTestable("2.0.0", "1.0.0"), Is.False);
			Assert.That(IsOutdatedVersionTestable("1.2.0", "1.1.0"), Is.False);
			Assert.That(IsOutdatedVersionTestable("1.1.2", "1.1.1"), Is.False);
		}

		[Test]
		public void FormatInternalVersion_WithBuildType_IncludesBuildType()
		{
			var data = new VersionServices.VersionData
			{
				CommitHash = "abc",
				BranchName = "main",
				BuildType = "debug",
				BuildNumber = "1"
			};
			var result = VersionServices.FormatInternalVersion(data);
			
			Assert.That(result.Contains("debug"), Is.True);
			Assert.That(result.Contains("abc"), Is.True);
			Assert.That(result.Contains("main"), Is.True);
			Assert.That(result.Contains("1"), Is.True);
		}

		[Test]
		public void FormatInternalVersion_WithoutBuildType_OmitsBuildType()
		{
			var data = new VersionServices.VersionData
			{
				CommitHash = "abc",
				BranchName = "main",
				BuildType = "",
				BuildNumber = "1"
			};
			var result = VersionServices.FormatInternalVersion(data);
			
			Assert.That(result.EndsWith("."), Is.False);
			Assert.That(result.Contains("abc"), Is.True);
		}
	}
}
