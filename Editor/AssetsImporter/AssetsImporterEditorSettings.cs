using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services.AssetsImporter.Editor
{
	/// <summary>
	/// Assets Importer 파이프라인의 에디터 전용 프로젝트 설정입니다.
	/// <see cref="ScriptableSingleton{T}"/> 로 <c>ProjectSettings/AssetsImporterEditorSettings.asset</c> 에 저장됩니다.
	/// </summary>
	[FilePath("ProjectSettings/AssetsImporterEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
	internal sealed class AssetsImporterEditorSettings : ScriptableSingleton<AssetsImporterEditorSettings>
	{
		[SerializeField] private bool _autoUpdateOnRefresh;

		/// <summary>
		/// <c>true</c> 이면 스크립트 컴파일이 끝날 때마다 모든 임포터를 자동으로 임포트합니다
		/// (<see cref="AssetsImporterEditorUtils.TryScheduleAutoImport"/>).
		/// Services Explorer 의 Assets Importer 탭에서 바꿉니다.
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
