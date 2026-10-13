using System.Text.RegularExpressions;
using Geuneda.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class VersionServicesTest
	{
		[Test]
		public void IsOutdatedVersion_NewerMajor_ReturnsTrue()
		{
			Assert.IsTrue(VersionServices.IsOutdatedVersion("1.0.0", "2.0.0"));
		}

		[Test]
		public void IsOutdatedVersion_NewerMinor_ReturnsTrue()
		{
			Assert.IsTrue(VersionServices.IsOutdatedVersion("1.1.0", "1.2.0"));
		}

		[Test]
		public void IsOutdatedVersion_NewerPatch_ReturnsTrue()
		{
			Assert.IsTrue(VersionServices.IsOutdatedVersion("1.1.1", "1.1.2"));
		}

		[Test]
		public void IsOutdatedVersion_SameVersion_ReturnsFalse()
		{
			Assert.IsFalse(VersionServices.IsOutdatedVersion("1.1.1", "1.1.1"));
		}

		[Test]
		public void IsOutdatedVersion_OlderVersion_ReturnsFalse()
		{
			Assert.IsFalse(VersionServices.IsOutdatedVersion("2.0.0", "1.0.0"));
			Assert.IsFalse(VersionServices.IsOutdatedVersion("1.2.0", "1.1.0"));
			Assert.IsFalse(VersionServices.IsOutdatedVersion("1.1.2", "1.1.1"));
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

		[Test]
		public void IsOutdatedVersion_DirectInvocation_AgreesWithLocalVersion()
		{
			Assert.IsFalse(VersionServices.IsOutdatedVersion(Application.version));
		}

		[TestCase("1.2", "1.2.1", true)]
		[TestCase("1.2.1", "1.2", false)]
		[TestCase("1", "1.0.0", false)]
		[TestCase("1", "1.1", true)]
		public void IsOutdatedVersion_MissingParts_TreatedAsZero(string current, string other, bool expected)
		{
			Assert.AreEqual(expected, VersionServices.IsOutdatedVersion(current, other));
		}

		[TestCase("1.2.3", "1.2.3-rc", false)]
		[TestCase("1.2.3-rc", "1.2.3", false)]
		[TestCase("1.2.3", "1.3.0-beta.2", true)]
		[TestCase("1.2.3", "1.2.4+42", true)]
		[TestCase("1.2-rc.9", "1.2.1", true)]
		[TestCase(" 1.2.3 ", "1.2.3", false)]
		public void IsOutdatedVersion_Suffixes_ComparesLeadingNumbersOnly(string current, string other, bool expected)
		{
			Assert.AreEqual(expected, VersionServices.IsOutdatedVersion(current, other));
		}

		[TestCase("1.2.2147483646", "1.2.2147483647", true)]
		[TestCase("1.2.2147483647", "1.2.2147483646", false)]
		[TestCase("1.2147483647.0", "1.2147483647.0", false)]
		public void IsOutdatedVersion_IntMaxValueParts_Compared(string current, string other, bool expected)
		{
			Assert.AreEqual(expected, VersionServices.IsOutdatedVersion(current, other));
		}

		[TestCase("1.2.3", "abc")]
		[TestCase("1.2.3", "")]
		[TestCase("1.2.3", null)]
		[TestCase("v1.2.3", "1.2.4")]
		[TestCase("1.2.3", "99999999999.0.0")]
		[TestCase("1.2.2147483648", "1.2.1")]
		[TestCase("1.2.1", "1.2.2147483648")]
		[TestCase("1.2147483648.0", "1.0.0")]
		[TestCase("1.0.0", "1.2147483648.0")]
		public void IsOutdatedVersion_Unparsable_LogsWarningAndReturnsFalse(string current, string other)
		{
			LogAssert.Expect(LogType.Warning, new Regex("Cannot compare versions"));

			Assert.IsFalse(VersionServices.IsOutdatedVersion(current, other));
		}
	}
}
