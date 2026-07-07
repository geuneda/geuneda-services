using Geuneda.Services.AddressableIds.Editor;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class AddressableIdsEditorSettingsTest
	{
		// AddressableIdsEditorSettings is a ScriptableSingleton persisted to ProjectSettings/;
		// snapshot + restore so test mutations don't leak into the user's project.
		private string _originalScriptFilename;
		private string _originalNamespace;
		private string _originalAddressableLabel;

		[SetUp]
		public void SaveOriginalSettings()
		{
			_originalScriptFilename = AddressableIdsEditorSettings.instance.ScriptFilename;
			_originalNamespace = AddressableIdsEditorSettings.instance.Namespace;
			_originalAddressableLabel = AddressableIdsEditorSettings.instance.AddressableLabel;
		}

		[TearDown]
		public void RestoreOriginalSettings()
		{
			AddressableIdsEditorSettings.instance.ScriptFilename = _originalScriptFilename;
			AddressableIdsEditorSettings.instance.Namespace = _originalNamespace;
			AddressableIdsEditorSettings.instance.AddressableLabel = _originalAddressableLabel;
		}

		// ---- IsValidIdentifier ----

		[Test]
		public void IsValidIdentifier_EmptyString_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidIdentifier("", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidIdentifier_WhitespaceOnly_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidIdentifier("   ", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidIdentifier_StartsWithDigit_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidIdentifier("1AddressableId", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidIdentifier_ContainsDot_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidIdentifier("Addressable.Id", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidIdentifier_ContainsHyphen_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidIdentifier("Addressable-Id", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidIdentifier_ValidDefault_ReturnsTrue()
		{
			Assert.IsTrue(AddressableIdsEditorSettings.IsValidIdentifier("AddressableId", out var error));
			Assert.IsNull(error);
		}

		[Test]
		public void IsValidIdentifier_UnderscorePrefix_ReturnsTrue()
		{
			Assert.IsTrue(AddressableIdsEditorSettings.IsValidIdentifier("_AddressableId", out var error));
			Assert.IsNull(error);
		}

		// ---- IsValidNamespace ----

		[Test]
		public void IsValidNamespace_EmptyString_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidNamespace("", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidNamespace_WhitespaceOnly_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidNamespace("   ", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidNamespace_TrailingDot_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidNamespace("Game.Ids.", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidNamespace_ConsecutiveDots_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidNamespace("Game..Ids", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidNamespace_SegmentStartsWithDigit_ReturnsFalse()
		{
			Assert.IsFalse(AddressableIdsEditorSettings.IsValidNamespace("Game.1Ids", out var error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void IsValidNamespace_ValidDefault_ReturnsTrue()
		{
			Assert.IsTrue(AddressableIdsEditorSettings.IsValidNamespace("Game.Ids", out var error));
			Assert.IsNull(error);
		}

		[Test]
		public void IsValidNamespace_SingleSegment_ReturnsTrue()
		{
			Assert.IsTrue(AddressableIdsEditorSettings.IsValidNamespace("Game", out var error));
			Assert.IsNull(error);
		}

		[Test]
		public void IsValidNamespace_DeepHierarchy_ReturnsTrue()
		{
			Assert.IsTrue(AddressableIdsEditorSettings.IsValidNamespace("Com.Geuneda.Game.Ids", out var error));
			Assert.IsNull(error);
		}

		// ---- Setter normalization ----

		[Test]
		public void ScriptFilename_SetterNormalizesAndPersists()
		{
			AddressableIdsEditorSettings.instance.ScriptFilename = "  CustomFilename  ";

			Assert.AreEqual("CustomFilename", AddressableIdsEditorSettings.instance.ScriptFilename);

			AddressableIdsEditorSettings.instance.ScriptFilename = null;

			Assert.AreEqual("AddressableId", AddressableIdsEditorSettings.instance.ScriptFilename);
		}

		[Test]
		public void Namespace_SetterNormalizesAndPersists()
		{
			AddressableIdsEditorSettings.instance.Namespace = "  Custom.Namespace  ";

			Assert.AreEqual("Custom.Namespace", AddressableIdsEditorSettings.instance.Namespace);

			AddressableIdsEditorSettings.instance.Namespace = null;

			Assert.AreEqual("Game.Ids", AddressableIdsEditorSettings.instance.Namespace);
		}

		[Test]
		public void AddressableLabel_SetterNormalizesAndPersists()
		{
			AddressableIdsEditorSettings.instance.AddressableLabel = "  custom-label  ";

			Assert.AreEqual("custom-label", AddressableIdsEditorSettings.instance.AddressableLabel);

			AddressableIdsEditorSettings.instance.AddressableLabel = null;

			Assert.AreEqual(string.Empty, AddressableIdsEditorSettings.instance.AddressableLabel);
		}
	}
}
