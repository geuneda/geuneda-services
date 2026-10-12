using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Geuneda.Services.AddressableIds.Editor
{
	/// <summary>
	/// Addressable Ids 생성기의 에디터 전용 프로젝트 설정입니다.
	/// <see cref="ScriptableSingleton{T}"/> 로 <c>ProjectSettings/AddressableIdsEditorSettings.asset</c> 에 저장됩니다.
	/// </summary>
	[FilePath("ProjectSettings/AddressableIdsEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
	internal sealed class AddressableIdsEditorSettings : ScriptableSingleton<AddressableIdsEditorSettings>
	{
		[SerializeField] private string _scriptFilename = "AddressableId";
		[SerializeField] private string _namespace = "Game.Ids";
		[SerializeField] private string _addressableLabel = "GenerateIds";

		// ---- 마지막 생성 스냅샷 (저장됨) ----
		[SerializeField] private long _lastGenerationUtcTicks;
		[SerializeField] private int _lastGenerationIdCount;
		[SerializeField] private int _lastGenerationLabelCount;
		[SerializeField] private string _lastGenerationFilenameUsed;
		[SerializeField] private string _lastGenerationLabelFilterUsed;
		[SerializeField] private string[] _lastGenerationAddresses = Array.Empty<string>();
		[SerializeField] private string[] _lastGenerationLabels = Array.Empty<string>();

		/// <summary>생성되는 C# 파일 이름(확장자 제외)이자 그 안의 enum/클래스 이름.</summary>
		public string ScriptFilename
		{
			get => string.IsNullOrWhiteSpace(_scriptFilename) ? "AddressableId" : _scriptFilename;
			set
			{
				var trimmed = (value ?? "AddressableId").Trim();
				if (_scriptFilename == trimmed)
				{
					return;
				}

				_scriptFilename = trimmed;
				Save(true);
			}
		}

		/// <summary>생성되는 파일의 C# 네임스페이스.</summary>
		public string Namespace
		{
			get => string.IsNullOrWhiteSpace(_namespace) ? "Game.Ids" : _namespace;
			set
			{
				var trimmed = (value ?? "Game.Ids").Trim();
				if (_namespace == trimmed)
				{
					return;
				}

				_namespace = trimmed;
				Save(true);
			}
		}

		/// <summary>Id 를 생성할 에셋을 거르는 Addressables 라벨. 비어 있으면 전부 생성합니다.</summary>
		public string AddressableLabel
		{
			get => _addressableLabel ?? "";
			set
			{
				var trimmed = (value ?? "").Trim();
				if (_addressableLabel == trimmed)
				{
					return;
				}

				_addressableLabel = trimmed;
				Save(true);
			}
		}

		// ---- 마지막 생성 스냅샷 접근자 ----

		/// <summary><see cref="RecordGeneration"/> 으로 생성 스냅샷이 기록되었으면 true.</summary>
		public bool HasSnapshot => _lastGenerationUtcTicks != 0L;

		/// <summary>마지막으로 성공한 생성의 UTC 시각. 없으면 <c>default(DateTime)</c>.</summary>
		public DateTime LastGenerationUtc => _lastGenerationUtcTicks == 0L
			? default
			: new DateTime(_lastGenerationUtcTicks, DateTimeKind.Utc);

		public int LastGenerationIdCount => _lastGenerationIdCount;
		public int LastGenerationLabelCount => _lastGenerationLabelCount;
		public string LastGenerationFilenameUsed => _lastGenerationFilenameUsed ?? string.Empty;
		public string LastGenerationLabelFilterUsed => _lastGenerationLabelFilterUsed ?? string.Empty;

		/// <summary>마지막 생성에서 출력한 Addressable 주소의 정렬된 목록. 스냅샷이 없으면 빈 배열.</summary>
		public IReadOnlyList<string> LastGenerationAddresses => _lastGenerationAddresses ?? Array.Empty<string>();

		/// <summary>마지막 생성에서 출력한 Addressable 라벨의 정렬된 목록. 스냅샷이 없으면 빈 배열.</summary>
		public IReadOnlyList<string> LastGenerationLabels => _lastGenerationLabels ?? Array.Empty<string>();

		/// <summary>
		/// 마지막으로 성공한 생성의 스냅샷을 기록합니다: 주소, 라벨, 그리고 그 시점의
		/// 생성기 설정(파일 이름, 라벨 필터). 두 목록은 정렬해서 저장하므로 이후 집합 비교를
		/// 읽을 때 다시 정렬하지 않고 O(n+m) 에 할 수 있습니다.
		/// <c>Save(true)</c> 로 즉시 저장합니다.
		/// </summary>
		internal void RecordGeneration(IReadOnlyList<string> addresses, IReadOnlyList<string> labels)
		{
			_lastGenerationUtcTicks = DateTime.UtcNow.Ticks;
			_lastGenerationIdCount = addresses?.Count ?? 0;
			_lastGenerationLabelCount = labels?.Count ?? 0;
			_lastGenerationFilenameUsed = ScriptFilename;
			_lastGenerationLabelFilterUsed = AddressableLabel;

			_lastGenerationAddresses = SortedCopy(addresses);
			_lastGenerationLabels = SortedCopy(labels);

			Save(true);
		}

		private static string[] SortedCopy(IReadOnlyList<string> source)
		{
			if (source == null || source.Count == 0)
			{
				return Array.Empty<string>();
			}

			var copy = new string[source.Count];

			for (var i = 0; i < source.Count; i++)
			{
				copy[i] = source[i];
			}

			Array.Sort(copy, StringComparer.Ordinal);
			return copy;
		}

		/// <summary>
		/// <paramref name="identifier"/> 가 C# 스크립트 파일 이름 / enum 이름으로 쓸 수 있는지 검증합니다.
		/// 유효하면 <c>true</c> 를 반환하고, 실패하면 <paramref name="error"/> 를 채웁니다.
		/// </summary>
		public static bool IsValidIdentifier(string identifier, out string error)
		{
			error = null;

			if (string.IsNullOrWhiteSpace(identifier))
			{
				error = "Identifier cannot be empty.";
				return false;
			}

			var trimmed = identifier.Trim();

			if (char.IsDigit(trimmed[0]))
			{
				error = "Identifier must not start with a digit.";
				return false;
			}

			foreach (var c in trimmed)
			{
				if (!char.IsLetterOrDigit(c) && c != '_')
				{
					error = $"Identifier contains invalid character '{c}'. Only letters, digits, and underscores are allowed.";
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// <paramref name="ns"/> 가 C# 네임스페이스 문자열(점으로 구분한 식별자)인지 검증합니다.
		/// 유효하면 <c>true</c> 를 반환하고, 실패하면 <paramref name="error"/> 를 채웁니다.
		/// </summary>
		public static bool IsValidNamespace(string ns, out string error)
		{
			error = null;

			if (string.IsNullOrWhiteSpace(ns))
			{
				error = "Namespace cannot be empty.";
				return false;
			}

			var segments = ns.Trim().Split('.');

			foreach (var segment in segments)
			{
				if (string.IsNullOrEmpty(segment))
				{
					error = "Namespace must not contain consecutive dots or trailing dots.";
					return false;
				}

				if (!IsValidIdentifier(segment, out var segmentError))
				{
					error = $"Namespace segment \"{segment}\" is invalid: {segmentError}";
					return false;
				}
			}

			return true;
		}
	}
}
