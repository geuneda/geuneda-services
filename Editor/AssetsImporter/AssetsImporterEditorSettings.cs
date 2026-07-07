using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services.AssetsImporter.Editor
{
	/// <summary>
	/// Editor-only project-level settings for the Assets Importer pipeline.
	/// Persisted to <c>ProjectSettings/AssetsImporterEditorSettings.asset</c> via <see cref="ScriptableSingleton{T}"/>.
	/// </summary>
	[FilePath("ProjectSettings/AssetsImporterEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
	internal sealed class AssetsImporterEditorSettings : ScriptableSingleton<AssetsImporterEditorSettings>
	{
		[SerializeField] private bool _autoUpdateOnRefresh;

		/// <summary>
		/// When <c>true</c>, all importers are refreshed automatically after every script compilation.
		/// Change this via the Services Explorer Assets Importer tab.
		/// </summary>
		public bool AutoUpdateOnRefresh
		{
			get => _autoUpdateOnRefresh;
			set
			{
				if (_autoUpdateOnRefresh == value)
				{
					return;
				}

				_autoUpdateOnRefresh = value;
				Save(true);
			}
		}
	}
}
